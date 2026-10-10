#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Sinh model ONNX cho "bộ não" cua MonsterAI (Silent-Sprint).

Pipeline:
    1. Sinh dataset cac trang thai gameplay hop ly (observation 16 chieu).
    2. Sinh nhan mem (soft target) tu mot "teacher policy" duoc thiet ke thu cong.
    3. Train MLP nho (16 -> 32 ReLU -> 32 Tanh -> 2 head) bang behaviour cloning.
    4. Tu tay dung graph ONNX toi gian (chi Gemm / Relu / Tanh / Softmax / Sigmoid)
       de chac chan Inference Engine ho tro.
    5. Verify: onnx.checker + onnxruntime, doi chieu voi forward pass numpy.
    6. Ghi ra Assets/Resources/MonsterBrain/monster_brain.onnx

Model nay la LOP QUYET DINH. Vong lap FSM trong MonsterAI.cs van la lop phan xa
(an toan), nen model chi can chon "y dinh" cap cao chu khong dieu khien di chuyen.

Layout phai khop tuyet doi voi Assets/Scripts/Gameplay/AI/MonsterObservation.cs.
"""

import argparse
import os
import sys

import numpy as np

# --------------------------------------------------------------------------------------
# Layout observation (16 chieu) - KHOP voi MonsterObservation.cs
# --------------------------------------------------------------------------------------
OBS_SIZE = 16
OBS_NAMES = [
    "distNorm",         # 0  clamp01(dist / sightDistance)
    "angleNorm",        # 1  |goc lech| / 180
    "losClear",         # 2  1 neu raycast khong bi chan
    "inSightCone",      # 3  1 neu goc < sightAngle
    "relDirX",          # 4  huong toi player trong khong gian local (truc X)
    "relDirZ",          # 5  huong toi player trong khong gian local (truc Z)
    "playerSpeedNorm",  # 6  clamp01(toc do player / runSpeed)
    "playerNoise",      # 7  0 di dung yen / 0.4 di bo / 1.0 chay
    "staminaNorm",      # 8  player stamina / maxStamina
    "flashlightOn",     # 9  0/1
    "proximity",        # 10 clamp01(dist / hearingDistance)
    "timeSinceSeen",    # 11 clamp01(timeSinceLost / 8)
    "hasLastKnown",     # 12 0/1
    "lastKnownDist",    # 13 clamp01(dist(lastKnown) / stalkRadius)
    "prevState",        # 14 chi so state truoc do / 2 (0, 0.5, 1)
    "bias",             # 15 luon 1.0
]

# Layout output
STATE_NAMES = ["patrol", "investigate", "chase"]
INTENT_NAMES = ["aggression", "searchRadius", "flank", "alertness"]

# Cac tham so "the gioi" dung luc sinh dataset - lay dung mac dinh trong MonsterAI.cs
SIGHT_DISTANCE = 20.0
HEARING_DISTANCE = 25.0
STALK_RADIUS = 25.0
RUN_SPEED = 8.0
LOST_TIMEOUT = 8.0


# --------------------------------------------------------------------------------------
# 1. Sinh dataset
# --------------------------------------------------------------------------------------
def sample_observations(n, rng):
    """Sinh n ngau nhien cac trang thai gameplay 'co the xay ra that'."""
    obs = np.zeros((n, OBS_SIZE), dtype=np.float32)

    # distNorm: tron deu voi viec tap trung quanh cac nguong cam giac (1.0 = sight, 1.25 = hearing)
    mix = rng.random(n)
    dist_norm = np.where(
        mix < 0.55,
        rng.random(n) * 1.6,
        rng.normal(loc=np.where(rng.random(n) < 0.5, 1.0, 1.25), scale=0.12, size=n),
    )
    dist_norm = np.clip(dist_norm, 0.0, 2.0)
    obs[:, 0] = dist_norm

    angle_norm = rng.random(n) * 1.0                      # 0..1  (goc 0..180 do)
    obs[:, 1] = angle_norm
    in_cone = (angle_norm * 180.0 < 45.0).astype(np.float32)   # sightAngle = 45
    obs[:, 3] = in_cone

    # Tia nhin: trong tam nhin thi de bi chan hon la ngoai tam nhin
    blocked_p = np.where(in_cone > 0.5, 0.35, 0.97)
    los = (rng.random(n) > blocked_p).astype(np.float32)
    obs[:, 2] = los

    # Huong tuong doi: neu trong tam nhin thi huong truc Z phai duong
    theta = np.radians(angle_norm * 180.0) * np.where(rng.random(n) < 0.5, -1.0, 1.0)
    signed = np.where(los * in_cone > 0.5, theta, theta)
    obs[:, 4] = np.sin(signed).astype(np.float32)
    obs[:, 5] = np.cos(signed).astype(np.float32)

    # Trang thai di chuyen cua player: 35% dung yen, 40% di, 25% chay
    roll = rng.random(n)
    speed = np.where(roll < 0.35, 0.0, np.where(roll < 0.75, RUN_SPEED / 2.5, RUN_SPEED))
    noise = np.where(roll < 0.35, 0.0, np.where(roll < 0.75, 0.4, 1.0))
    obs[:, 6] = (speed / RUN_SPEED).astype(np.float32)
    obs[:, 7] = noise.astype(np.float32)

    obs[:, 8] = rng.random(n).astype(np.float32)                       # staminaNorm
    obs[:, 9] = (rng.random(n) < 0.45).astype(np.float32)              # flashlightOn

    # proximity = dist / hearingDistance; hearingDistance gap ~1.25 lan sightDistance
    obs[:, 10] = np.clip(dist_norm / (HEARING_DISTANCE / SIGHT_DISTANCE), 0.0, 1.0).astype(np.float32)

    seen = los * in_cone * (dist_norm <= 1.0)
    # Thoi gian tu lan cuoi thay player: gan 0 neu dang thay
    time_since_seen = np.where(seen > 0.5, rng.random(n) * 0.15, rng.random(n)).astype(np.float32)
    obs[:, 11] = time_since_seen

    has_last_known = np.maximum(seen, (rng.random(n) < 0.55).astype(np.float32)).astype(np.float32)
    obs[:, 12] = has_last_known
    obs[:, 13] = np.clip(rng.random(n) * 1.2, 0.0, 1.5).astype(np.float32)

    obs[:, 14] = rng.integers(0, 3, size=n).astype(np.float32) / 2.0
    obs[:, 15] = 1.0
    return obs


# --------------------------------------------------------------------------------------
# 2. Teacher policy (nhan mem)
# --------------------------------------------------------------------------------------
def teacher(obs):
    """Tra ve (state_probs, intents) - chinh sach thiet ke tay lam nhan cho behaviour cloning."""
    dist_norm = obs[:, 0]
    los = obs[:, 2]
    in_cone = obs[:, 3]
    player_speed = obs[:, 6]
    noise = obs[:, 7]
    proximity = obs[:, 10]
    time_since_seen = obs[:, 11]
    has_last_known = obs[:, 12]

    seen = los * in_cone * (dist_norm <= 1.0)
    heard = (noise > 0.4).astype(np.float32) * (proximity <= 1.0).astype(np.float32)

    # Utility -> logits. "seen" chi phoi manh nhat, sau do la "heard"/"last known".
    u_patrol = 1.0 - 3.0 * seen - 1.8 * heard
    u_investigate = 2.2 * heard + 2.6 * has_last_known * (1.0 - time_since_seen) - 1.2 * seen
    u_chase = 5.0 * seen + 2.0 * seen * (1.0 - dist_norm) - 2.5 * (1.0 - seen)

    logits = np.stack([u_patrol, u_investigate, u_chase], axis=1) / 0.6
    logits -= logits.max(axis=1, keepdims=True)
    exp = np.exp(logits)
    probs = (exp / exp.sum(axis=1, keepdims=True)).astype(np.float32)

    # Intents: cac tham so "tinh cach" ma FSM khong co.
    aggression = np.clip(
        0.20 + 0.80 * seen * (1.0 - 0.55 * dist_norm) + 0.30 * heard, 0.0, 1.0)
    loss_age = time_since_seen * (1.0 - seen)
    search_radius = np.where(
        has_last_known > 0.5, np.clip(0.25 + 0.75 * loss_age, 0.0, 1.0), 0.20)
    flank = np.clip(0.15 + 0.85 * seen * player_speed, 0.0, 1.0)
    alertness = np.clip(
        0.25 + 0.45 * heard + 0.40 * (1.0 - time_since_seen) * has_last_known + 0.35 * seen,
        0.0, 1.0)

    intents = np.stack([aggression, search_radius, flank, alertness], axis=1).astype(np.float32)
    return probs, intents


# --------------------------------------------------------------------------------------
# 3. Train MLP (behaviour cloning)
# --------------------------------------------------------------------------------------
def train_mlp(obs, target_probs, target_intents, epochs, lr, seed):
    import torch
    import torch.nn as nn

    torch.manual_seed(seed)
    torch.set_num_threads(os.cpu_count() or 1)

    model = nn.Sequential(
        nn.Linear(OBS_SIZE, 32), nn.ReLU(),
        nn.Linear(32, 32), nn.Tanh(),
    )

    class Heads(nn.Module):
        def __init__(self, trunk):
            super().__init__()
            self.trunk = trunk
            self.state_head = nn.Linear(32, 3)
            self.intent_head = nn.Linear(32, 4)

        def forward(self, x):
            h = self.trunk(x)
            return self.state_head(h), self.intent_head(h)

    net = Heads(model)
    opt = torch.optim.Adam(net.parameters(), lr=lr)

    x = torch.from_numpy(obs)
    y_probs = torch.from_numpy(target_probs)
    y_int = torch.from_numpy(target_intents)

    n = x.shape[0]
    batch = 256
    mse = nn.MSELoss()
    for epoch in range(epochs):
        perm = torch.randperm(n)
        total = 0.0
        for i in range(0, n, batch):
            idx = perm[i:i + batch]
            opt.zero_grad()
            logits, intents = net(x[idx])
            loss = nn.functional.mse_loss(
                torch.softmax(logits, dim=1), y_probs[idx]) + mse(intents, y_int[idx])
            loss.backward()
            opt.step()
            total += float(loss.detach()) * idx.shape[0]
        if (epoch + 1) % max(1, epochs // 10) == 0:
            print(f"  epoch {epoch + 1:>5}/{epochs}  loss={total / n:.6f}")

    with torch.no_grad():
        logits, intents = net(x)
        pred_probs = torch.softmax(logits, dim=1).numpy()
        pred_int = intents.numpy()

    weights = {
        "W1": model[0].weight.detach().numpy().T, "b1": model[0].bias.detach().numpy(),
        "W2": model[2].weight.detach().numpy().T, "b2": model[2].bias.detach().numpy(),
        "W3": net.state_head.weight.detach().numpy().T, "b3": net.state_head.bias.detach().numpy(),
        "W4": net.intent_head.weight.detach().numpy().T, "b4": net.intent_head.bias.detach().numpy(),
    }
    fit = {
        "probs_mae": float(np.abs(pred_probs - target_probs).mean()),
        "intents_mae": float(np.abs(pred_int - target_intents).mean()),
        "state_acc": float((pred_probs.argmax(1) == target_probs.argmax(1)).mean()),
    }
    return weights, fit


def numpy_forward(weights, obs):
    """Forward pass thuan numpy - dung lam chuan doi chieu voi onnxruntime."""
    h = np.maximum(obs @ weights["W1"] + weights["b1"], 0.0)
    h = np.tanh(h @ weights["W2"] + weights["b2"])
    logits = h @ weights["W3"] + weights["b3"]
    logits = logits - logits.max(axis=1, keepdims=True)
    exp = np.exp(logits)
    probs = exp / exp.sum(axis=1, keepdims=True)
    intents = 1.0 / (1.0 + np.exp(-(h @ weights["W4"] + weights["b4"])))
    return probs.astype(np.float32), intents.astype(np.float32)


# --------------------------------------------------------------------------------------
# 4. Dung graph ONNX toi gian
# --------------------------------------------------------------------------------------
def build_onnx(weights, path):
    import onnx
    from onnx import TensorProto, helper, numpy_helper

    def init(name, array):
        return numpy_helper.from_array(np.ascontiguousarray(array, dtype=np.float32), name=name)

    initializers = [
        init("W1", weights["W1"]), init("b1", weights["b1"]),
        init("W2", weights["W2"]), init("b2", weights["b2"]),
        init("W3", weights["W3"]), init("b3", weights["b3"]),
        init("W4", weights["W4"]), init("b4", weights["b4"]),
    ]

    # Gemm: Y = A @ B + C  (B da la ma tran (in, out), khong dung transB)
    nodes = [
        helper.make_node("Gemm", ["observation", "W1", "b1"], ["h1"], name="fc1"),
        helper.make_node("Relu", ["h1"], ["a1"], name="relu1"),
        helper.make_node("Gemm", ["a1", "W2", "b2"], ["h2"], name="fc2"),
        helper.make_node("Tanh", ["h2"], ["a2"], name="tanh1"),
        helper.make_node("Gemm", ["a2", "W3", "b3"], ["state_logits"], name="head_state"),
        helper.make_node("Softmax", ["state_logits"], ["state_probs"], axis=1, name="softmax_state"),
        helper.make_node("Gemm", ["a2", "W4", "b4"], ["intent_logits"], name="head_intents"),
        helper.make_node("Sigmoid", ["intent_logits"], ["intents"], name="sigmoid_intents"),
    ]

    graph = helper.make_graph(
        nodes,
        "MonsterBrain",
        [helper.make_tensor_value_info("observation", TensorProto.FLOAT, [1, OBS_SIZE])],
        [
            helper.make_tensor_value_info("state_probs", TensorProto.FLOAT, [1, len(STATE_NAMES)]),
            helper.make_tensor_value_info("intents", TensorProto.FLOAT, [1, len(INTENT_NAMES)]),
        ],
        initializer=initializers,
    )

    model = helper.make_model(
        graph,
        producer_name="SilentSprint-MonsterBrain",
        opset_imports=[helper.make_opsetid("", 13)],
    )
    # Batch co dinh = 1: Inference Engine xu ly tot nhat voi shape tinh.
    model.ir_version = 8
    onnx.checker.check_model(model)

    os.makedirs(os.path.dirname(path), exist_ok=True)
    onnx.save(model, path)
    return model


# --------------------------------------------------------------------------------------
# 5. Verify
# --------------------------------------------------------------------------------------
def verify_onnx(path, weights, seed):
    import onnxruntime as ort

    rng = np.random.default_rng(seed)
    cases = np.concatenate([
        sample_observations(2048, rng),
        # vai trang thai bien: nhin thang mat, dung sat, mat dau hoan toan
        np.array([
            [0.05, 0.00, 1, 1, 0.0, 1.0, 1.00, 1.0, 0.8, 1, 0.04, 0.00, 1, 0.0, 0.0, 1],   # thay, rat gan
            [0.90, 0.10, 1, 1, 0.1, 0.9, 1.00, 1.0, 0.3, 0, 0.72, 0.00, 1, 0.2, 1.0, 1],   # thay, xa
            [0.60, 0.75, 0, 0, 0.6, 0.5, 0.95, 1.0, 0.5, 0, 0.48, 0.60, 1, 0.8, 1.0, 1],   # mat dau, con nho
            [1.60, 0.40, 0, 0, 0.4, 0.9, 0.40, 0.4, 0.9, 0, 1.00, 1.00, 0, 1.4, 0.0, 1],   # khong biet gi
        ], dtype=np.float32),
    ])

    sess = ort.InferenceSession(path, providers=["CPUExecutionProvider"])

    # Model co batch co dinh = 1 (Inference Engine xu ly shape tinh tot nhat), nen chay tung mau mot.
    got_probs = np.zeros((cases.shape[0], len(STATE_NAMES)), dtype=np.float32)
    got_intents = np.zeros((cases.shape[0], len(INTENT_NAMES)), dtype=np.float32)
    for i in range(cases.shape[0]):
        p, it = sess.run(["state_probs", "intents"], {"observation": cases[i:i + 1]})
        got_probs[i] = p[0]
        got_intents[i] = it[0]

    ref_probs, ref_intents = numpy_forward(weights, cases)

    prob_err = float(np.abs(got_probs - ref_probs).max())
    intent_err = float(np.abs(got_intents - ref_intents).max())

    print("\n  onnxruntime vs numpy forward:")
    print(f"    max |d state_probs| = {prob_err:.3e}")
    print(f"    max |d intents|     = {intent_err:.3e}")

    print("\n  Vi du quyet dinh (4 dong cuoi la trang thai bien):")
    header = "    " + " ".join(f"{n:>11}" for n in STATE_NAMES + INTENT_NAMES)
    print(header)
    for i in range(max(0, cases.shape[0] - 4), cases.shape[0]):
        vals = list(got_probs[i]) + list(got_intents[i])
        print("    " + " ".join(f"{v:>11.4f}" for v in vals))
    print(f"    (trung binh tren {cases.shape[0]} mau: "
          f"p_chase={got_probs[:, 2].mean():.3f}, p_investigate={got_probs[:, 1].mean():.3f}, "
          f"p_patrol={got_probs[:, 0].mean():.3f})")

    tol = 1e-4
    if prob_err > tol or intent_err > tol:
        raise SystemExit(f"FAIL: onnxruntime lech qua nguong {tol}")
    if np.abs(got_probs.sum(axis=1) - 1.0).max() > 1e-4:
        raise SystemExit("FAIL: state_probs khong cong lai bang 1")
    if got_intents.min() < 0.0 or got_intents.max() > 1.0:
        raise SystemExit("FAIL: intents khong nam trong [0,1]")
    print("\n  OK: model hop le va khop voi tham chieu numpy.")


# --------------------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(description="Sinh monster_brain.onnx cho Silent-Sprint")
    parser.add_argument("--samples", type=int, default=40000)
    parser.add_argument("--epochs", type=int, default=1200)
    parser.add_argument("--lr", type=float, default=3e-3)
    parser.add_argument("--seed", type=int, default=1234)
    parser.add_argument("--out", type=str, default=None,
                        help="mac dinh: <repo>/Assets/Resources/MonsterBrain/monster_brain.onnx")
    args = parser.parse_args()

    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out = args.out or os.path.join(root, "Assets", "Resources", "MonsterBrain", "monster_brain.onnx")

    print(f"[1/4] Sinh {args.samples} mau observation...")
    rng = np.random.default_rng(args.seed)
    obs = sample_observations(args.samples, rng)
    assert obs.shape == (args.samples, OBS_SIZE)
    assert np.isfinite(obs).all(), "observation co gia tri khong hop le"

    print("[2/4] Sinh nhan tu teacher policy...")
    target_probs, target_intents = teacher(obs)

    print(f"[3/4] Train MLP ({args.epochs} epoch)...")
    weights, fit = train_mlp(obs, target_probs, target_intents, args.epochs, args.lr, args.seed)
    print(f"  do khop voi teacher: state_acc={fit['state_acc']:.4f} "
          f"probs_mae={fit['probs_mae']:.5f} intents_mae={fit['intents_mae']:.5f}")

    print(f"[4/4] Dung graph ONNX -> {out}")
    build_onnx(weights, out)
    print(f"  kich thuoc file: {os.path.getsize(out) / 1024:.1f} KB")

    verify_onnx(out, weights, args.seed)


if __name__ == "__main__":
    sys.exit(main())
