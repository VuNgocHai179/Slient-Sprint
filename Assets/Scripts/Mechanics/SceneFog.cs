using UnityEngine;

namespace SilentSprint.Mechanics
{
    /// <summary>
    /// Sương mù theo khoảng cách để cản tầm nhìn người chơi.
    ///
    /// Component này chỉ chỉnh đúng hệ thống Fog sẵn có của Unity (<see cref="RenderSettings"/>),
    /// không thêm shader hay renderer feature nào, nên chạy được trên cả URP lẫn Built-in và gần như
    /// không tốn thêm hiệu năng. Vật ở xa mờ dần rồi chìm vào <see cref="color"/>, còn vật ở gần vẫn
    /// hiện rõ nên đèn pin của người chơi vẫn giữ được tác dụng.
    ///
    /// Mặc định dùng <see cref="FogMode.ExponentialSquared"/> vì nó tắt dần nhanh và "ngột ngạt" hơn
    /// Linear, hợp với không khí kinh dị. Muốn nhìn xa hơn thì giảm <see cref="density"/>; muốn bí bách
    /// hơn thì tăng lên (khoảng cách nhìn thấy được xấp xỉ 2 / density mét).
    ///
    /// Component cũng cập nhật ngay trong Editor khi chỉnh thông số, và có sẵn vài hàm để gameplay
    /// thay đổi độ đặc / màu sương theo tình huống (ví dụ: dày lên khi quái tới gần).
    /// </summary>
    [AddComponentMenu("Silent Sprint/Scene Fog")]
    [DisallowMultipleComponent]
    public class SceneFog : MonoBehaviour
    {
        [Header("Sương mù")]
        [Tooltip("Bật/tắt sương mù.")]
        public bool fogEnabled = true;

        [Tooltip("Kiểu sương mù. Exponential Squared cho cảm giác ngột ngạt, tắt dần nhanh theo khoảng cách.")]
        public FogMode mode = FogMode.ExponentialSquared;

        [Tooltip("Màu sương mù. Vật ở xa sẽ hoà dần vào màu này, nên để tối và hơi ngả xanh cho hợp không khí ban đêm.")]
        public Color color = new Color(0.06f, 0.07f, 0.09f, 1f);

        [Tooltip("Độ đặc của sương mù (dùng cho kiểu Exponential và Exponential Squared). Càng lớn nhìn càng gần. Khoảng cách nhìn thấy được xấp xỉ 2 / density mét.")]
        [Min(0f)]
        public float density = 0.06f;

        [Header("Chỉ dùng khi mode = Linear")]
        [Tooltip("Khoảng cách bắt đầu có sương (Linear).")]
        public float linearStart = 8f;

        [Tooltip("Khoảng cách sương đặc hoàn toàn (Linear).")]
        public float linearEnd = 45f;

        void Awake()
        {
            Apply();
        }

        void OnValidate()
        {
            Apply();
        }

        /// <summary>Đẩy toàn bộ thông số đang có xuống hệ thống Fog của Unity.</summary>
        public void Apply()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = mode;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
            RenderSettings.fogStartDistance = linearStart;
            RenderSettings.fogEndDistance = linearEnd;
        }

        /// <summary>Đổi độ đặc lúc đang chơi (ví dụ khi quái đuổi tới gần).</summary>
        public void SetDensity(float value)
        {
            density = Mathf.Max(0f, value);
            RenderSettings.fogDensity = density;
        }

        /// <summary>Đổi màu sương lúc đang chơi.</summary>
        public void SetColor(Color value)
        {
            color = value;
            RenderSettings.fogColor = color;
        }

        /// <summary>Bật/tắt sương mù lúc đang chơi.</summary>
        public void SetEnabled(bool value)
        {
            fogEnabled = value;
            RenderSettings.fog = value;
        }
    }
}
