using UnityEngine;

namespace SilentSprint.Cutscenes
{
    /// <summary>
    /// Cho quả cầu disco trong quán bar (CutScene1) thật sự phát sáng, thay vì chỉ là một khối
    /// kim loại chờ đèn khác chiếu vào.
    ///
    /// Ba điều đã phải thử rồi mới ra được cấu hình đúng, ghi lại để sau này khỏi sửa nhầm:
    ///
    /// 1. Đèn phải là <b>Spot chúc thẳng xuống</b>, không phải Point. Quả cầu treo sát trần
    ///    (~0,7 m) mà đèn Point không đổ bóng thì bình phương khoảng cách làm trần nhận sáng gấp
    ///    hàng chục lần sàn — kết quả là một vệt loé trên trần còn quanh phòng vẫn tối. Chúc
    ///    xuống với góc rộng (~170°, gần nửa mặt cầu dưới) thì đèn chiếu hết phần phòng từ ngang
    ///    tầm quả cầu trở xuống mà không thể với tới trần. Vì vậy <b>đừng</b> đổi Light Type về
    ///    Point trong Inspector, sẽ quay lại đúng cái lỗi cũ.
    /// 2. Đèn nằm ở TÂM quả cầu nên không chiếu sáng được mặt ngoài của chính nó: pháp tuyến mặt
    ///    cầu hướng ra ngoài, tức là ngược hướng với hướng từ mặt cầu tới đèn, nên N·L &lt; 0.
    ///    Muốn thấy quả cầu sáng thì phải bật emission trên material, mà material của model .glb
    ///    là material của asset dùng chung (lambert1 của disco_ball.glb) — sửa thẳng vào asset là
    ///    ảnh hưởng mọi nơi dùng model đó. Nên script tự lấy bản sao riêng của material trên
    ///    renderer (<c>renderer.material</c>) lúc chạy rồi đổi <c>_EmissionColor</c> theo màu đèn —
    ///    chỉ tốn một bản material cho một object, không đụng tới asset.
    /// 3. Quả cầu tự QUAY. Quả cầu disco thật luôn quay nên các mảng phản chiếu mới nhấp nháy, để
    ///    đứng yên thì nhìn ra một khối cầu chết. Script quay object cha (chính quả cầu) quanh
    ///    trục đứng của THẾ GIỚI: quả cầu treo trên trần nên phương quay phải là phương thẳng đứng
    ///    thật, không phụ thuộc cha (Bar) có bị nghiêng trong scene hay không. Model .glb có tâm
    ///    mesh đúng ở gốc (accessor POSITION: min/max = ±50) nên quay tại chỗ, không bị đảo.
    ///
    /// Màu chạy vòng trên bánh xe màu kèm nhấp nháy nhẹ cho giống đèn xoay của quán bar. Dùng
    /// <c>Time.unscaledTime</c> để hiệu ứng không đứng hình khi cutscene pause (Time.timeScale = 0).
    ///
    /// Gắn component này lên object con nằm ở tâm quả cầu; <see cref="RequireComponent"/> bảo đảm
    /// object đó luôn có <see cref="Light"/> (đèn là thứ chiếu sáng căn phòng, còn emission làm
    /// chính quả cầu sáng lên).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class DiscoBallLight : MonoBehaviour
    {
        [Header("Màu chạy vòng")]
        [Tooltip("Số giây để quay hết một vòng màu.")]
        public float cycleSeconds = 9f;

        [Range(0f, 1f)]
        [Tooltip("0 = trắng, 1 = màu rực nhất.")]
        public float saturation = 0.75f;

        [Range(0f, 1f)]
        [Tooltip("Độ sáng (value) của màu.")]
        public float brightness = 1f;

        [Range(0f, 1f)]
        [Tooltip("Lệch pha 0..1, để nhiều quả cầu không đổi màu giống hệt nhau.")]
        public float phase;

        [Header("Quay")]
        [Tooltip("Tốc độ quay quanh trục đứng, độ/giây (số âm = quay ngược). Đặt 0 để đứng yên.")]
        public float spinDegreesPerSecond = 30f;

        [Header("Độ sáng của đèn")]
        [Tooltip("Độ sáng thấp nhất trong một nhịp nhấp nháy (candela).")]
        public float minIntensity = 5f;

        [Tooltip("Độ sáng cao nhất trong một nhịp nhấp nháy (candela). Đèn của bar là 3, " +
                 "nhưng đèn này phải phủ cả phòng nên đặt cao hơn.")]
        public float maxIntensity = 8f;

        [Tooltip("Số lần nhấp nháy trong một vòng màu. Đặt 0 để giữ độ sáng cố định.")]
        public float pulsesPerCycle = 3f;

        [Header("Mặt cầu phát sáng")]
        [Tooltip("Bật để chính mặt quả cầu sáng lên theo màu đèn (dùng emission của material).")]
        public bool emissiveBall = true;

        [Range(0f, 8f)]
        [Tooltip("Hệ số khuếch đại màu emission. Đặt 0 để không phát sáng.")]
        public float emissiveBoost = 2f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        Light discoLight;
        Transform ballRoot;              // object cha của đèn, cũng chính là quả cầu
        Material[] ballMaterials = new Material[0];

        void Awake()
        {
            discoLight = GetComponent<Light>();
            ballRoot = transform.parent;

            if (emissiveBall)
                CollectBallMaterials();
        }

        void OnValidate()
        {
            cycleSeconds = Mathf.Max(0.01f, cycleSeconds);
            minIntensity = Mathf.Max(0f, minIntensity);
            maxIntensity = Mathf.Max(minIntensity, maxIntensity);
            pulsesPerCycle = Mathf.Max(0f, pulsesPerCycle);
        }

        void Update()
        {
            // Quay quanh trục đứng của THẾ GIỚI, không phải trục local của cha: quả cầu treo trên
            // trần nên phương quay phải là phương thẳng đứng thật. Unscaled để còn quay khi cutscene pause.
            if (spinDegreesPerSecond != 0f && ballRoot != null)
                ballRoot.Rotate(Vector3.up * (spinDegreesPerSecond * Time.unscaledDeltaTime), Space.World);

            float turns = Time.unscaledTime / Mathf.Max(0.01f, cycleSeconds) + phase;
            Color color = Color.HSVToRGB(Mathf.Repeat(turns, 1f), saturation, brightness);

            discoLight.color = color;

            if (pulsesPerCycle > 0f)
            {
                float pulse = Mathf.Sin(turns * Mathf.PI * 2f * pulsesPerCycle) * 0.5f + 0.5f; // 0..1
                discoLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, pulse);
            }

            if (!emissiveBall || emissiveBoost <= 0f)
                return;

            for (int i = 0; i < ballMaterials.Length; i++)
            {
                if (ballMaterials[i] != null)
                    ballMaterials[i].SetColor(EmissionColorId, color * emissiveBoost);
            }
        }

        /// <summary>
        /// Lấy bản sao material của mọi renderer thuộc quả cầu (object cha của đèn) rồi bật
        /// emission. disco_ball.glb chỉ có một material lambert1, nhưng viết theo mảng để model
        /// nhiều phần (cầu + dây treo) vẫn sáng đều.
        /// </summary>
        void CollectBallMaterials()
        {
            if (ballRoot == null)
                return;

            Renderer[] renderers = ballRoot.GetComponentsInChildren<Renderer>(true);
            Material[] materials = new Material[renderers.Length];
            int count = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].material; // bản sao riêng, không sửa asset .glb
                if (material == null || !material.HasProperty(EmissionColorId))
                    continue;

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                materials[count++] = material;
            }

            if (count == 0)
            {
                Debug.LogWarning(
                    $"[DiscoBallLight] Không thấy material nào có _EmissionColor ở '{ballRoot.name}' — " +
                    "quả cầu sẽ không tự phát sáng, chỉ còn đèn chiếu sáng căn phòng.",
                    this);
                return;
            }

            ballMaterials = materials;
            System.Array.Resize(ref ballMaterials, count);
        }
    }
}
