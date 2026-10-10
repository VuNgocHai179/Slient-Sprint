using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    /// <summary>Dáng của MỘT nhịp trong cảnh phim: tụ ở hông, hoặc đẩy hai tay ra trước lúc bắn.</summary>
    [Serializable]
    public class KamehamehaPoseBeat
    {
        [Header("Thân trên")]
        [Tooltip("Cúi người về trước (độ).")]
        public float lean = 10f;

        [Tooltip("Vặn người (độ, dương = vặn sang phải, vai trái đưa ra trước). Thấy vặn sai chiều thì đổi dấu.")]
        public float twist = 20f;

        [Tooltip("Nghiêng người về phía hông phải (độ).")]
        public float sideBend = 14f;

        [Tooltip("Cúi đầu thêm (độ, dương = nhìn xuống hai bàn tay).")]
        public float headPitch = 8f;

        [Tooltip("Hạ hông xuống (m). Hông hạ thì hai chân tự khuỵu, bàn chân vẫn đứng nguyên chỗ.")]
        public float hipDrop = 0.07f;

        [Header("Hai bàn tay (lệch so với hông, tính bằng mét)")]
        [Tooltip("x = sang phải, y = lên, z = về phía cổng. Đích bám theo hông nên hông hạ là tay đi theo. " +
                 "Với nhân vật ~1.75m, tầm với của tay tính từ vai chỉ ~0.53m: đích xa hơn sẽ bị kẹp lại " +
                 "(tay duỗi thẳng, không với tới).")]
        public Vector3 rightHand = new Vector3(0.22f, 0.12f, 0.24f);

        public Vector3 leftHand = new Vector3(0.08f, 0.14f, 0.18f);

        [Header("Khuỷu tay chĩa về đâu")]
        [Tooltip("x = phải, y = lên, z = về phía cổng. Khuỷu phải chĩa ra ngoài và hơi xuống.")]
        public Vector3 rightElbow = new Vector3(1f, -0.3f, -0.5f);

        [Tooltip("Khuỷu trái: tay trái vòng qua người nên khuỷu chĩa ra ngoài về phía trái.")]
        public Vector3 leftElbow = new Vector3(-0.8f, 0.15f, -0.5f);
    }

    /// <summary>
    /// Tư thế Kamehameha dựng bằng code, không cần clip animation.
    ///
    /// Cách làm: giữ dáng gốc của bộ xương (dáng Idle lúc Animator bị đóng băng) rồi chồng lên đó hai loại
    /// điều chỉnh:
    ///  - <b>Thân trên</b>: xoay thêm quanh TRỤC THẾ GIỚI (phải / lên / trước của nhân vật), nên không phụ
    ///    thuộc trục cục bộ của từng xương — đoán số là ra đúng chiều, không phải mò trục.
    ///  - <b>Tay và chân</b>: giải hai xương (2-bone IK) tới một ĐIỂM ĐÍCH tính bằng mét so với hông, nên
    ///    bàn tay thật sự với tới chỗ muốn chứ không phải đoán góc từng khớp.
    ///
    /// Mọi con số nằm trong <see cref="KamehamehaPoseBeat"/> đặt trên <see cref="KamehamehaEnding"/>
    /// (chỉnh được ngay trong Inspector). Đổi nhịp thì các xương được làm mượt dần về dáng mới, không giật.
    ///
    /// Chỉ dùng khi người chơi KHÔNG gắn clip Charge Pose: lúc đó Animator bị tắt để nó khỏi ghi đè tư thế
    /// này mỗi frame. Có clip thì <see cref="KamehamehaEnding"/> đi đường clip, lớp này không được tạo ra.
    /// </summary>
    public class KamehamehaPose : MonoBehaviour
    {
        // Chia độ cúi / vặn / nghiêng cho từng đốt để thân không gãy khúc.
        // Tổng các phần "thuận" đúng bằng 1, nên thân trên cùng cúi/vặn đúng bằng số độ trong Beat.
        private const float HipsLeanShare = 0.15f;
        private const float HipsTwistShare = 0.30f;
        private const float HipsSideShare = 0.25f;
        private const float SpineLeanShare = 0.25f;
        private const float SpineTwistShare = 0.20f;
        private const float SpineSideShare = 0.25f;
        private const float Spine01LeanShare = 0.30f;
        private const float Spine01TwistShare = 0.20f;
        private const float Spine01SideShare = 0.25f;
        private const float Spine02LeanShare = 0.30f;
        private const float Spine02TwistShare = 0.30f;
        private const float Spine02SideShare = 0.25f;

        // Cổ và đầu ngửa lại bớt một phần độ cúi của thân để mặt vẫn nhìn về phía cổng.
        private const float NeckCounterShare = -0.35f;
        private const float HeadCounterShare = -0.35f;

        /// <summary>Một chuỗi hai xương để giải IK (vai→khuỷu→bàn tay, hoặc hông→gối→bàn chân).</summary>
        private struct TwoBoneChain
        {
            public Transform upper;
            public Transform lower;
            public Transform tip;
            public Vector3 upperLocalAxis;
            public Vector3 lowerLocalAxis;
            public float upperLength;
            public float lowerLength;
            public Vector3 tipWorldPosition;
            public Quaternion tipWorldRotation;

            public bool IsValid => upper != null && lower != null && tip != null;
        }

        private Transform playerRoot;
        private Animator animator;
        private bool animatorWasEnabled;

        private Transform hips;
        private Transform spine;
        private Transform spine01;
        private Transform spine02;
        private Transform neck;
        private Transform head;

        private TwoBoneChain rightArm;
        private TwoBoneChain leftArm;
        private TwoBoneChain rightLeg;
        private TwoBoneChain leftLeg;

        private readonly Dictionary<Transform, Quaternion> baseLocal = new Dictionary<Transform, Quaternion>();
        private Vector3 baseHipsLocalPosition;

        private Vector3 poseRight;
        private Vector3 poseForward;

        private KamehamehaPoseBeat beat;
        private float speed = 6f;
        private bool ready;

        /// <summary>Bộ xương đã sẵn sàng để tạo dáng chưa (thiếu xương thì lớp này tự tắt).</summary>
        public bool IsReady => ready;

        /// <summary>
        /// Dò bộ xương, ghi lại dáng gốc rồi (tuỳ chọn) đóng băng Animator.
        /// </summary>
        /// <param name="playerTransform">Gốc của nhân vật (đang quay mặt về phía cổng).</param>
        /// <param name="freezeAnimator">Tắt Animator để nó khỏi ghi đè tư thế dựng bằng code.</param>
        /// <returns>false nếu bộ xương thiếu xương cần thiết — lúc đó lớp này không làm gì cả.</returns>
        public bool Initialize(Transform playerTransform, bool freezeAnimator)
        {
            playerRoot = playerTransform;

            poseRight = Flat(playerTransform.right);
            poseForward = Flat(playerTransform.forward);

            if (poseRight.sqrMagnitude < 0.0001f) poseRight = Vector3.right;
            if (poseForward.sqrMagnitude < 0.0001f) poseForward = Vector3.forward;

            hips = FindBone("Hips");
            spine = FindBone("Spine");
            spine01 = FindBone("Spine01", "Spine1");
            spine02 = FindBone("Spine02", "Spine2");
            neck = FindBone("neck", "Neck");
            head = FindBone("Head");

            CaptureChain(ref rightArm, "RightArm", "RightForeArm", "RightHand");
            CaptureChain(ref leftArm, "LeftArm", "LeftForeArm", "LeftHand");
            CaptureChain(ref rightLeg, "RightUpLeg", "RightLeg", "RightFoot");
            CaptureChain(ref leftLeg, "LeftUpLeg", "LeftLeg", "LeftFoot");

            if (hips == null)
            {
                Debug.LogWarning("[KamehamehaPose] Không tìm thấy xương \"Hips\" nên không dựng được tư thế.", this);
                return false;
            }

            CaptureBase(hips);
            CaptureBase(spine);
            CaptureBase(spine01);
            CaptureBase(spine02);
            CaptureBase(neck);
            CaptureBase(head);
            CaptureBase(rightArm.upper);
            CaptureBase(rightArm.lower);
            CaptureBase(rightArm.tip);
            CaptureBase(leftArm.upper);
            CaptureBase(leftArm.lower);
            CaptureBase(leftArm.tip);
            CaptureBase(rightLeg.upper);
            CaptureBase(rightLeg.lower);
            CaptureBase(rightLeg.tip);
            CaptureBase(leftLeg.upper);
            CaptureBase(leftLeg.lower);
            CaptureBase(leftLeg.tip);

            baseHipsLocalPosition = hips.localPosition;

            animator = playerRoot.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animatorWasEnabled = animator.enabled;
                if (freezeAnimator) animator.enabled = false;
            }

            ready = true;
            return true;
        }

        /// <summary>Đổi dáng đích. <paramref name="blendSpeed"/> càng lớn thì về dáng mới càng nhanh (1/giây).</summary>
        public void SetBeat(KamehamehaPoseBeat target, float blendSpeed)
        {
            beat = target;
            speed = Mathf.Max(0.1f, blendSpeed);
        }

        /// <summary>Trả bộ xương về dáng gốc, mở lại Animator rồi tự xoá lớp này.</summary>
        public void Release()
        {
            ready = false;
            beat = null;

            foreach (KeyValuePair<Transform, Quaternion> pair in baseLocal)
                if (pair.Key != null)
                    pair.Key.localRotation = pair.Value;

            if (hips != null) hips.localPosition = baseHipsLocalPosition;

            if (animator != null) animator.enabled = animatorWasEnabled;

            Destroy(this);
        }

        // ------------------------------------------------------------------ mỗi frame

        private void LateUpdate()
        {
            if (!ready || beat == null) return;

            // Làm mượt kiểu hàm mũ: khung hình dài hay ngắn đều về đích với cùng một nhịp.
            // Dùng thời gian thật để bảng tạm dừng (timeScale = 0) không làm tư thế đứng im.
            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

            ApplyHipDrop(beat.hipDrop, k);
            ApplyTorso(beat, k);

            ApplyArm(rightArm, HandTarget(beat.rightHand), PoleDirection(beat.rightElbow), k);
            ApplyArm(leftArm, HandTarget(beat.leftHand), PoleDirection(beat.leftElbow), k);

            // Chân luôn được giải lại: hông hạ xuống bao nhiêu thì gối tự khuỵu, bàn chân đứng nguyên chỗ.
            ApplyLeg(rightLeg, LegPole(1f), k);
            ApplyLeg(leftLeg, LegPole(-1f), k);
        }

        private void ApplyHipDrop(float hipDrop, float k)
        {
            if (hips == null || hips.parent == null) return;

            // Độ dời tính bằng mét trong không gian cục bộ của cha, nên hệ số tỉ lệ của rig cũng đúng.
            Vector3 target = baseHipsLocalPosition + hips.parent.InverseTransformVector(Vector3.down * hipDrop);
            hips.localPosition = Vector3.Lerp(hips.localPosition, target, k);
        }

        private void ApplyTorso(KamehamehaPoseBeat beat, float k)
        {
            ApplyTorsoBone(hips, beat.lean * HipsLeanShare, beat.twist * HipsTwistShare, beat.sideBend * HipsSideShare, k);
            ApplyTorsoBone(spine, beat.lean * SpineLeanShare, beat.twist * SpineTwistShare, beat.sideBend * SpineSideShare, k);
            ApplyTorsoBone(spine01, beat.lean * Spine01LeanShare, beat.twist * Spine01TwistShare, beat.sideBend * Spine01SideShare, k);
            ApplyTorsoBone(spine02, beat.lean * Spine02LeanShare, beat.twist * Spine02TwistShare, beat.sideBend * Spine02SideShare, k);
            ApplyTorsoBone(neck, beat.lean * NeckCounterShare, 0f, 0f, k);

            // Đầu: ngửa lại bớt độ cúi của thân, rồi cúi thêm theo headPitch.
            ApplyTorsoBone(head, beat.lean * HeadCounterShare + beat.headPitch, 0f, 0f, k);
        }

        /// <summary>
        /// Xoay thêm một đốt quanh trục thế giới. Tính ra localRotation đích theo công thức
        /// desiredLocal = cha⁻¹ · delta · (cha · dángGốc), nên xoay được đúng chiều mà không cần biết
        /// trục cục bộ của xương nằm đâu.
        /// </summary>
        private void ApplyTorsoBone(Transform bone, float lean, float twist, float sideBend, float k)
        {
            if (bone == null || bone.parent == null) return;
            if (Mathf.Abs(lean) < 0.0001f && Mathf.Abs(twist) < 0.0001f && Mathf.Abs(sideBend) < 0.0001f) return;

            Quaternion delta = Quaternion.AngleAxis(-sideBend, poseForward)
                             * Quaternion.AngleAxis(twist, Vector3.up)
                             * Quaternion.AngleAxis(lean, poseRight);

            Quaternion baseWorldUnderNewParent = bone.parent.rotation * baseLocal[bone];
            Quaternion desiredWorld = delta * baseWorldUnderNewParent;
            Quaternion desiredLocal = Quaternion.Inverse(bone.parent.rotation) * desiredWorld;

            bone.localRotation = Quaternion.Slerp(bone.localRotation, desiredLocal, k);
        }

        private void ApplyArm(TwoBoneChain chain, Vector3 target, Vector3 poleDirection, float k)
        {
            if (!chain.IsValid) return;

            Vector3 root = chain.upper.position;
            Vector3 elbow;

            if (!SolveElbow(root, target, poleDirection, chain.upperLength, chain.lowerLength, out elbow)) return;

            SetBoneAim(chain.upper, chain.upperLocalAxis, (elbow - root).normalized, k);
            SetBoneAim(chain.lower, chain.lowerLocalAxis, (target - elbow).normalized, k);
        }

        /// <summary>Chân: đích là chỗ bàn chân đang đứng, nên hông hạ xuống là gối tự khuỵu.</summary>
        private void ApplyLeg(TwoBoneChain chain, Vector3 poleDirection, float k)
        {
            if (!chain.IsValid) return;

            Vector3 root = chain.upper.position;
            Vector3 knee;

            if (!SolveElbow(root, chain.tipWorldPosition, poleDirection, chain.upperLength, chain.lowerLength, out knee)) return;

            SetBoneAim(chain.upper, chain.upperLocalAxis, (knee - root).normalized, k);
            SetBoneAim(chain.lower, chain.lowerLocalAxis, (chain.tipWorldPosition - knee).normalized, k);

            // Bàn chân giữ hướng cũ để nó không vểnh lên theo cẳng chân.
            chain.tip.rotation = Quaternion.Slerp(chain.tip.rotation, chain.tipWorldRotation, k);
        }

        /// <summary>
        /// Giải vị trí khuỷu (hoặc gối) bằng định lý cos: khuỷu nằm trên mặt phẳng chứa đường tới đích và
        /// hướng khuỷu. Đích xa hơn tầm với thì kẹp lại — tay duỗi thẳng chứ không bị kéo giãn.
        /// </summary>
        private static bool SolveElbow(Vector3 root, Vector3 target, Vector3 poleDirection,
                                       float upperLength, float lowerLength, out Vector3 elbow)
        {
            elbow = root;

            Vector3 toTarget = target - root;
            float distance = toTarget.magnitude;
            if (distance < 0.0001f || upperLength < 0.0001f || lowerLength < 0.0001f) return false;

            Vector3 direction = toTarget / distance;
            distance = Mathf.Clamp(distance,
                                   Mathf.Abs(upperLength - lowerLength) + 0.0001f,
                                   upperLength + lowerLength - 0.0001f);

            float cosAngle = (upperLength * upperLength + distance * distance - lowerLength * lowerLength)
                             / (2f * upperLength * distance);
            float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f));

            Vector3 pole = poleDirection - direction * Vector3.Dot(poleDirection, direction);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(direction, Vector3.up);
            pole = pole.normalized;

            elbow = root + direction * (upperLength * Mathf.Cos(angle)) + pole * (upperLength * Mathf.Sin(angle));
            return true;
        }

        /// <summary>
        /// Quay một xương sao cho trục "tới xương con" của nó trỏ đúng hướng muốn. Trục đó lấy từ chính
        /// bộ xương lúc chạy, nên rig dựng theo kiểu trục nào cũng chịu.
        /// </summary>
        private static void SetBoneAim(Transform bone, Vector3 localAxis, Vector3 desiredWorldDirection, float k)
        {
            if (bone == null || bone.parent == null) return;
            if (desiredWorldDirection.sqrMagnitude < 0.000001f) return;

            Vector3 currentWorldAxis = bone.rotation * localAxis;
            Quaternion align = Quaternion.FromToRotation(currentWorldAxis, desiredWorldDirection);
            Quaternion desiredWorld = align * bone.rotation;
            Quaternion desiredLocal = Quaternion.Inverse(bone.parent.rotation) * desiredWorld;

            bone.localRotation = Quaternion.Slerp(bone.localRotation, desiredLocal, k);
        }

        // ------------------------------------------------------------------ dò xương / đích

        private void CaptureChain(ref TwoBoneChain chain, string upperName, string lowerName, string tipName)
        {
            chain.upper = FindBone(upperName);
            chain.lower = FindBone(lowerName);
            chain.tip = FindBone(tipName);

            if (!chain.IsValid) return;

            chain.upperLocalAxis = chain.upper.InverseTransformDirection(chain.lower.position - chain.upper.position).normalized;
            chain.lowerLocalAxis = chain.lower.InverseTransformDirection(chain.tip.position - chain.lower.position).normalized;
            chain.upperLength = Vector3.Distance(chain.upper.position, chain.lower.position);
            chain.lowerLength = Vector3.Distance(chain.lower.position, chain.tip.position);

            // Chỗ bàn chân/bàn tay đang đứng lúc bắt đầu: chân bám vào đây, tay thì tính lại mỗi frame.
            chain.tipWorldPosition = chain.tip.position;
            chain.tipWorldRotation = chain.tip.rotation;
        }

        private void CaptureBase(Transform bone)
        {
            if (bone != null && !baseLocal.ContainsKey(bone)) baseLocal.Add(bone, bone.localRotation);
        }

        /// <summary>Tìm xương theo tên (chịu được cả "neck"/"Neck", "Spine1"/"Spine01").</summary>
        private Transform FindBone(params string[] names)
        {
            if (playerRoot == null) return null;

            foreach (Transform bone in playerRoot.GetComponentsInChildren<Transform>(true))
                foreach (string name in names)
                    if (string.Equals(bone.name, name, StringComparison.OrdinalIgnoreCase))
                        return bone;

            return null;
        }

        /// <summary>Điểm đích cho bàn tay, tính theo mét từ hông theo trục của nhân vật.</summary>
        private Vector3 HandTarget(Vector3 offset)
        {
            return hips.position + poseRight * offset.x + Vector3.up * offset.y + poseForward * offset.z;
        }

        private Vector3 PoleDirection(Vector3 local)
        {
            return poseRight * local.x + Vector3.up * local.y + poseForward * local.z;
        }

        /// <summary>Hướng gối: ra trước, hơi chếch ra ngoài cho hai đầu gối không chụm vào nhau.</summary>
        private Vector3 LegPole(float side)
        {
            return poseForward + poseRight * (0.25f * side) + Vector3.up * 0.15f;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.normalized;
        }
    }
}
