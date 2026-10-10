using System;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Một góc máy của đoạn kết. Vị trí tính theo trục của nhân vật (phải / lên / về phía cổng) nên đổi chỗ
    /// đứng của nhân vật hay đổi cổng thì góc máy vẫn đúng.
    ///
    /// Bộ số mặc định của góc TỤ lấy đúng từ góc đã ngắm thử trên Main Camera (Position -2.7 / 2.3 / -1.1,
    /// Rotation 14.286 / 52.674): camera đứng lệch sang TRÁI, hơi cao và hơi lùi, nhìn xuống theo hướng cổng.
    /// Ba góc của đoạn kết đều đứng bên trái người chơi để lúc cắt cảnh cổng không nhảy từ bên này sang bên kia.
    ///
    /// Số mặc định nằm trong <see cref="ChargeDefault"/>, <see cref="FireDefault"/>, <see cref="ImpactDefault"/>
    /// chứ không rải trong phần khai báo field, vì Unity lưu giá trị ĐÃ GHI vào scene: field đã có sẵn trong
    /// scene giữ nguyên giá trị cũ, sửa số trong code không tự ghi đè. Có hàm gom một chỗ thì việc đồng bộ lại
    /// scene chỉ là gọi một hàm (xem KamehamehaEnding.ResetShotsToDefaults).
    ///
    /// Điểm nhìn diễn tả bằng hai số thay vì bằng Rotation: <see cref="lookAtGate"/> (từ hai bàn tay tới tâm cổng)
    /// và <see cref="lookOffset"/> (lệch thêm). Riêng <see cref="autoFrameGate"/> thì tự tính cả chỗ đứng lẫn
    /// điểm nhìn từ kích thước thật của cổng đo lúc chạy (xem cách dùng trong KamehamehaEnding).
    ///
    /// Chỗ đứng có ba cách, chọn bằng ô bật/tắt tương ứng: theo trục người chơi (<see cref="offset"/>), lùi khỏi
    /// CỔNG về phía người chơi (<see cref="fromGateBack"/>) và theo TRỤC CỦA CỔNG (<see cref="anchorToGate"/> +
    /// <see cref="gateOffset"/>). Hai ô bật/tắt đều tắt thì mới dùng tới <see cref="offset"/>.
    ///
    /// Góc neo vào cổng ngắm theo <see cref="gateLookOffset"/> chứ không dùng <see cref="lookAtGate"/> /
    /// <see cref="lookOffset"/>: chỗ đứng đã cố định theo cổng thì điểm nhìn cũng phải cố định theo cổng.
    /// </summary>
    [Serializable]
    public class KamehamehaShot
    {
        [Header("Chỗ đứng của camera")]
        [Tooltip("Lệch so với người chơi: x = sang phải, y = lên, z = về phía cổng (mét). " +
                 "Giá trị mặc định (-2.7, 2.3, -1.1) = lệch trái, cao hơn đầu, lùi về sau lưng.")]
        public Vector3 offset = new Vector3(-2.7f, 2.3f, -1.1f);

        [Tooltip("Neo góc máy vào CỔNG: lùi về phía người chơi bao nhiêu mét. 0 = neo vào người chơi như bình " +
                 "thường (lúc đó chỉ dùng x và y của ô Offset, z bị bỏ qua).")]
        public float fromGateBack = 0f;

        [Tooltip("Neo góc máy vào TRỤC CỦA CỔNG thay vì trục người chơi: chỗ đứng lấy đúng theo Gate Offset, " +
                 "không phụ thuộc người chơi đứng đâu và quay mặt về đâu. Dùng cho góc máy phải đứng nguyên một chỗ " +
                 "so với cổng — như góc cổng đổ đứng trong sân nhìn ra cổng: người chơi vòng ra ngoài cổng rồi kích " +
                 "hoạt thì camera vẫn ở trong sân.")]
        public bool anchorToGate = false;

        [Tooltip("Lệch so với GỐC CỔNG (chân cổng), theo trục của cổng: x = dọc theo cổng, y = cao lên, " +
                 "z = ăn vào trong map. Chỉ dùng khi bật Anchor To Gate. Góc cổng đổ đang dùng: " +
                 "(-40.8, 13.6, -6.2) = 40.8m dọc theo cổng, cao 13.6m trên chân cổng, hơi lệch ra ngoài 6.2m.")]
        public Vector3 gateOffset = Vector3.zero;

        [Tooltip("Điểm nhìn của góc neo vào cổng, cũng theo trục của cổng: x = dọc theo cổng, y = cao lên, " +
                 "z = vào trong map. Chỉ dùng khi bật Anchor To Gate; lúc đó Look At Gate và Look Offset bị bỏ qua. " +
                 "Góc cổng đổ đang dùng: (-8.8, 0, -0.7) = ngắm xuống mặt đất chỗ cổng nằm xuống, 8.8m dọc theo cổng.")]
        public Vector3 gateLookOffset = Vector3.zero;

        [Tooltip("Tự canh khung theo kích thước THẬT của cổng (đo lúc chạy): đứng NGANG hướng cổng đổ nên cổng " +
                 "đổ ngang qua khung chứ không đổ về phía camera, và lùi đủ xa để cả người chơi, cánh cổng lẫn " +
                 "chỗ nó nằm xuống đều vào hình. Bật thì Offset chỉ còn dùng DẤU của x (đứng bên nào), " +
                 "các ô Offset/From Gate Back/Look At Gate/Look Offset/Dolly/Rise đều bị bỏ qua.")]
        public bool autoFrameGate = false;

        [Tooltip("Lề thừa khi tự canh khung: 1.2 = chừa thêm 20%. Lớn hơn = lùi xa hơn, thấy rộng hơn.")]
        public float gateFramingMargin = 1.2f;

        [Tooltip("Điểm nhìn: 0 = hai bàn tay người chơi, 1 = tâm cổng.")]
        [Range(0f, 1f)] public float lookAtGate = 0.10f;

        [Tooltip("Lệch thêm của điểm nhìn (mét, theo trục nhân vật). y = 0.35 là ngắm vào ngực chứ không " +
                 "phải hai bàn tay đang khum ở hông (tay thấp hơn ngực khoảng 0.35m).")]
        public Vector3 lookOffset = new Vector3(0f, 0.35f, 0f);

        [Header("Chuyển động trong nhịp")]
        [Tooltip("Tiến lại gần bao xa, tính theo tỉ lệ khoảng cách đang đứng. Âm = tiến vào, dương = lùi ra. " +
                 "0 = đứng yên đúng chỗ đã đặt (để 0 nếu muốn giữ nguyên khung hình đã ngắm).")]
        [Range(-0.8f, 0.8f)] public float dolly = 0f;

        [Tooltip("Nâng lên trong suốt nhịp (mét). 0 = không trôi.")]
        public float rise = 0f;

        [Header("Máy")]
        [Tooltip("Tiêu cự (FOV dọc). 0 = giữ nguyên tiêu cự đang dùng (đúng bằng khung hình đã ngắm, camera " +
                 "của người chơi đang để 60). Số nhỏ hơn = ống kính dài hơn, hình nén lại nhưng khung chặt hơn.")]
        public float fieldOfView = 0f;

        [Tooltip("Thời gian chuyển sang góc này (giây). 0 = cắt thẳng, nhảy góc luôn cho dứt khoát.")]
        public float blendDuration = 0.8f;

        // ------------------------------------------------------------------ bộ số mặc định

        /// <summary>Góc TỤ: lệch trái, lùi sau lưng, cao hơn đầu, nhìn xuống theo hướng cổng.</summary>
        public static KamehamehaShot ChargeDefault()
        {
            return new KamehamehaShot();
        }

        /// <summary>
        /// Góc BẮN: đứng ngang hông bên TRÁI người chơi. Cổng (cách 7m) sẽ ra khỏi khung bên trái — đúng ý đồ
        /// "tia lao khỏi khung", rồi cắt sang góc cổng đổ để thấy cú trúng.
        /// </summary>
        public static KamehamehaShot FireDefault()
        {
            return new KamehamehaShot
            {
                offset = new Vector3(-4.6f, 1.75f, 0.9f),
                lookAtGate = 0.24f,
                lookOffset = new Vector3(0f, 0.10f, 0f),
                dolly = 0f,
                rise = 0f,
                fieldOfView = 45f,
                blendDuration = 0f,
            };
        }

        /// <summary>
        /// Góc CỔNG ĐỔ: đứng ở ĐẦU cổng, 40.8m dọc theo cổng từ gốc cụm, cao 13.6m trên chân cổng, hơi lệch ra
        /// ngoài 6.2m, nhìn dọc theo cổng xuống chỗ nó nằm xuống (xem <see cref="anchorToGate"/>,
        /// <see cref="gateOffset"/>, <see cref="gateLookOffset"/>). Neo vào trục cổng nên cụm cổng to nhỏ gì, Stand
        /// Distance hay người chơi đứng ở đâu cũng không đổi khung.
        ///
        /// Không dùng <see cref="autoFrameGate"/> cho góc này: cụm cổng trong Map.prefab rộng tới 86m (cánh cổng
        /// chính chỉ ~14m, còn lại là hàng rào dài cộng hai cột cổng), canh cho cả cụm vừa khung thì camera phải
        /// lùi ~96m — ra hẳn ngoài tường, cảnh kết thành ra đứng ngoài map.
        /// </summary>
        public static KamehamehaShot ImpactDefault()
        {
            return new KamehamehaShot
            {
                anchorToGate = true,
                gateOffset = new Vector3(-40.8f, 13.6f, -6.2f),
                gateLookOffset = new Vector3(-8.8f, 0f, -0.7f),
                autoFrameGate = false,
                gateFramingMargin = 1.2f,
                offset = new Vector3(-3.6f, 1.9f, 0f),
                fromGateBack = 3.4f,
                lookAtGate = 1f,
                lookOffset = Vector3.zero,
                dolly = 0f,
                rise = 0f,
                fieldOfView = 60f,
                blendDuration = 0f,
            };
        }
    }
}
