using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;

namespace SilentSprint.Gameplay.AI
{
    /// <summary>
    /// "Bo nao" cua quai: chay model ONNX bang Inference Engine (Unity) va tra ve
    /// <see cref="MonsterDecision"/>.
    ///
    /// Kien truc hybrid:
    ///   - <b>MonsterBrain (lop nay)</b> = lop QUYET DINH. Nhan observation 16 chieu, tra ve
    ///     3 xac suat trang thai + 4 tham so tinh cach. Chay o tan so thap (mac dinh 10Hz)
    ///     de khong ton CPU/GPU moi frame.
    ///   - <b>MonsterAI</b> = lop PHAN XA. Cac luat cung (khoang cach bat duoc, nhin thay o
    ///     khoang cach sat, nhin thay thi khong the gia vo khong thay) luon thang quyet dinh
    ///     cua brain. Nho vay model khong the lam quai hanh xu vo ly.
    ///
    /// Neu khong tim thay/khong load duoc model thi tu dong roi ve
    /// <see cref="MonsterHeuristicPolicy"/> (cung chinh sach da dung de sinh nhan luc train),
    /// nen game khong bao gio bi vo vi thieu file .onnx.
    /// </summary>
    [DisallowMultipleComponent]
    public class MonsterBrain : MonoBehaviour
    {
        public enum DecisionSource
        {
            /// <summary>Dang chay model ONNX qua Inference Engine.</summary>
            OnnxModel,
            /// <summary>Khong co model - dang dung chinh sach viet tay.</summary>
            HeuristicFallback,
        }

        [Header("Model")]
        [Tooltip("Duong dan trong Resources (khong gom duoi file). " +
                 "Mac dinh tro toi Assets/Resources/MonsterBrain/monster_brain.onnx")]
        public string modelResourcePath = "MonsterBrain/monster_brain";

        [Tooltip("Gan truc tiep trong Inspector de de override. Bo trong thi load tu Resources.")]
        public ModelAsset brainModel;

        [Tooltip("CPU on dinh va khong gay stall do doc nguoc tu GPU. Doi sang GPUCompute neu mo hinh lon hon.")]
        public BackendType backendType = BackendType.CPU;

        [Header("Nhip do suy luan")]
        [Tooltip("So giay giua hai lan suy luan. 0.1 = 10Hz la qua du cho AI kieu nay.")]
        [Range(0.02f, 2f)]
        public float inferenceInterval = 0.1f;

        [Header("Debug")]
        [Tooltip("In ra Console quyet dinh cua model moi lan suy luan.")]
        public bool logDecisions;

        /// <summary>Nguon quyet dinh hien tai.</summary>
        public DecisionSource Source { get; private set; } = DecisionSource.HeuristicFallback;

        /// <summary>Quyet dinh moi nhat. Chi duoc cap nhat theo <see cref="inferenceInterval"/>.</summary>
        public MonsterDecision Decision { get; private set; } = MonsterDecision.Neutral;

        /// <summary>True neu dang chay bang model ONNX.</summary>
        public bool IsModelDriven => Source == DecisionSource.OnnxModel;

        /// <summary>So lan suy luan da chay - huu ich khi do hieu nang.</summary>
        public int InferenceCount { get; private set; }

        /// <summary>True neu da chay qua <see cref="Initialise"/> it nhat mot lan.</summary>
        public bool IsInitialised => _initialised;

        private Worker _worker;
        private Tensor<float> _inputTensor;
        private float[] _observationBuffer;
        private float _timer;
        private bool _initialised;
        private bool _loggedFallbackReason;

        private void Start()
        {
            Initialise();
        }

        private void OnDestroy()
        {
            DisposeWorker();
        }

        private void OnDisable()
        {
            DisposeWorker();
        }

        private void OnEnable()
        {
            // Sau khi bi disable/enable lai, worker da bi don nen phai nap lai.
            if (_initialised && Source == DecisionSource.OnnxModel && _worker == null)
                Initialise();
        }

        /// <summary>
        /// Nap model. Duoc goi tu dong trong Start; goi lai duoc neu muon doi model luc chay.
        /// Ham nay idempotent: goi nhieu lan khong nap lai model dang chay tot.
        /// </summary>
        public void Initialise()
        {
            if (_initialised && Source == DecisionSource.OnnxModel && _worker != null)
                return;

            _initialised = true;
            if (_observationBuffer == null || _observationBuffer.Length != MonsterObservationLayout.Size)
                _observationBuffer = new float[MonsterObservationLayout.Size];

            DisposeWorker();

            ModelAsset asset = brainModel;
            if (asset == null && !string.IsNullOrEmpty(modelResourcePath))
                asset = Resources.Load<ModelAsset>(modelResourcePath);

            if (asset == null)
            {
                Source = DecisionSource.HeuristicFallback;
                LogFallbackOnce(string.Format(
                    "Khong tim thay model tai Resources/{0}. " +
                    "Quai se dung chinh sach viet tay (van hoat dong binh thuong). " +
                    "Chay Tools/MonsterBrain/generate_monster_brain.py de sinh model.",
                    modelResourcePath));
                return;
            }

            try
            {
                Model model = ModelLoader.Load(asset);

                // Kiem tra som: sai ten input/output thi bao loi ngay chu khong doi den luc chay.
                if (!HasTensorNamed(model.inputs, MonsterBrainOutputLayout.InputName))
                    throw new InvalidOperationException(
                        "Model thieu input '" + MonsterBrainOutputLayout.InputName + "'.");
                if (!HasTensorNamed(model.outputs, MonsterBrainOutputLayout.StateProbsName) ||
                    !HasTensorNamed(model.outputs, MonsterBrainOutputLayout.IntentsName))
                    throw new InvalidOperationException(
                        "Model thieu output '" + MonsterBrainOutputLayout.StateProbsName +
                        "' hoac '" + MonsterBrainOutputLayout.IntentsName + "'.");

                _worker = new Worker(model, backendType);

                // Input tensor duoc giu lai suot vong doi va chi ghi de bang Upload(),
                // vi Inference Engine khong so huu (khong dispose ho) tensor do nguoi dung dua vao.
                _inputTensor = new Tensor<float>(
                    new TensorShape(1, MonsterObservationLayout.Size), _observationBuffer);

                Source = DecisionSource.OnnxModel;
                _loggedFallbackReason = false;

                if (logDecisions)
                {
                    Debug.Log(string.Format(
                        "[MonsterBrain] Da nap model '{0}' (backend {1}, {2} input / {3} output).",
                        modelResourcePath, backendType, model.inputs.Count, model.outputs.Count), this);
                }
            }
            catch (Exception exception)
            {
                Source = DecisionSource.HeuristicFallback;
                DisposeWorker();
                Debug.LogError(string.Format(
                    "[MonsterBrain] Loi khi nap model '{0}': {1}", modelResourcePath, exception.Message), this);
            }
        }

        private static bool HasTensorNamed(List<Model.Input> tensors, string name)
        {
            for (int i = 0; i < tensors.Count; i++)
                if (tensors[i].name == name) return true;
            return false;
        }

        private static bool HasTensorNamed(List<Model.Output> tensors, string name)
        {
            for (int i = 0; i < tensors.Count; i++)
                if (tensors[i].name == name) return true;
            return false;
        }

        /// <summary>
        /// Cap nhat quyet dinh. Goi moi frame; ham nay tu lo viec giam tan so.
        /// </summary>
        /// <param name="observation">Giac quan cua frame hien tai.</param>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(in MonsterObservation observation, float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f)
                return;

            // Neu frame bi giat (deltaTime lon) thi khong don bu nhieu lan lien tiep.
            _timer = Mathf.Max(inferenceInterval, 0f);

            Decision = Source == DecisionSource.OnnxModel
                ? RunModel(observation)
                : MonsterHeuristicPolicy.Evaluate(observation);
        }

        /// <summary>Ep brain chay lai ngay o frame ke tiep.</summary>
        public void RequestImmediateInference()
        {
            _timer = 0f;
        }

        private MonsterDecision RunModel(in MonsterObservation observation)
        {
            if (_observationBuffer == null || _inputTensor == null || _worker == null)
                return MonsterHeuristicPolicy.Evaluate(observation);

            observation.WriteTo(_observationBuffer);

            try
            {
                // Ghi de len input tensor co dinh thay vi cap phat tensor moi:
                // Inference Engine giu reference chu khong dispose ho tensor nguoi dung dua vao.
                _inputTensor.Upload(_observationBuffer);
                _worker.Schedule(_inputTensor);

                // PeekOutput tra ve tensor DO WORKER SO HUU - khong duoc Dispose.
                // Chi ban clone khi doc duoc tra ve moi thuoc ve minh.
                var stateTensor = _worker.PeekOutput(MonsterBrainOutputLayout.StateProbsName) as Tensor<float>;
                var intentTensor = _worker.PeekOutput(MonsterBrainOutputLayout.IntentsName) as Tensor<float>;

                if (stateTensor == null || intentTensor == null)
                    throw new InvalidOperationException(
                        "Model khong tra ve dung output '" + MonsterBrainOutputLayout.StateProbsName +
                        "' / '" + MonsterBrainOutputLayout.IntentsName + "'.");

                float[] stateProbs;
                float[] intents;
                using (Tensor<float> cpuState = stateTensor.ReadbackAndClone())
                using (Tensor<float> cpuIntent = intentTensor.ReadbackAndClone())
                {
                    stateProbs = cpuState.DownloadToArray();
                    intents = cpuIntent.DownloadToArray();
                }

                InferenceCount++;

                var decision = new MonsterDecision();
                decision.SetStateProbabilities(
                    Get(stateProbs, MonsterBrainOutputLayout.StatePatrol),
                    Get(stateProbs, MonsterBrainOutputLayout.StateInvestigate),
                    Get(stateProbs, MonsterBrainOutputLayout.StateChase));
                decision.SetIntents(
                    Get(intents, MonsterBrainOutputLayout.IntentAggression),
                    Get(intents, MonsterBrainOutputLayout.IntentSearchRadius),
                    Get(intents, MonsterBrainOutputLayout.IntentFlank),
                    Get(intents, MonsterBrainOutputLayout.IntentAlertness));

                if (logDecisions)
                    Debug.Log(string.Format("[MonsterBrain] {0}", decision), this);

                return decision;
            }
            catch (Exception exception)
            {
                // Mot lan loi suy luan khong nen lam chet AI: roi ve chinh sach viet tay.
                Source = DecisionSource.HeuristicFallback;
                DisposeWorker();
                Debug.LogError(string.Format(
                    "[MonsterBrain] Suy luan that bai, chuyen sang chinh sach viet tay: {0}",
                    exception.Message), this);
                return MonsterHeuristicPolicy.Evaluate(observation);
            }
        }

        private static float Get(float[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
        }

        private void LogFallbackOnce(string message)
        {
            if (_loggedFallbackReason) return;
            _loggedFallbackReason = true;
            Debug.LogWarning("[MonsterBrain] " + message, this);
        }

        private void DisposeWorker()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }

            // Input tensor do minh so huu (Inference Engine khong dispose ho), nen phai tu don.
            if (_inputTensor != null)
            {
                _inputTensor.Dispose();
                _inputTensor = null;
            }
        }
    }
}
