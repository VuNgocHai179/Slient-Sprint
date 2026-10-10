using UnityEngine;

namespace SilentSprint.Gameplay.AI
{
    /// <summary>
    /// Quái "khôn dần" theo tiến độ nhặt mảnh đá tự do.
    ///
    /// Vì sao cần: càng nhiều đá trong tay, người chơi càng gần lối ra, nên nửa sau màn phải siết lại.
    /// Cứ đủ một mốc đá là quái lên một BẬC, và mỗi bậc mở rộng ba thứ:
    ///
    ///   • GIÁC QUAN — tầm nhìn, tầm nghe, góc nhìn.
    ///   • Ý ĐỊNH    — nâng SÀN cho 4 tham số tính cách của brain (<see cref="MonsterDecision"/>:
    ///                 aggression, searchRadius, flank, alertness). Brain có trả về thấp thì quái
    ///                 vẫn không rình rề quá lâu, và mất dấu thì lục soát ráo riết hơn.
    ///   • PHẢN XẠ    — hạ ngưỡng ưu tiên khi đổi trạng thái, bớt "do dự" lúc đã có thông tin.
    ///
    /// Cố tình KHÔNG đụng vào tốc độ. Tốc độ do luật cân bằng bên <see cref="MonsterAI"/>
    /// (<c>maxChaseSpeedToPlayerRunRatio</c>) quyết định, và luật đó luôn thắng — người chơi bấm chạy
    /// phải luôn có cửa thoát, kể cả ở bậc cao nhất.
    ///
    /// Chỉ số là hàm bậc thang theo số đá. Với cấu hình mặc định (mốc 10, mỗi 5 đá một bậc, tối đa 3 bậc):
    ///
    /// | Bậc | Số đá | Tầm nhìn | Tầm nghe | Góc nhìn | Sàn ý định | Ngưỡng đổi trạng thái |
    /// |-----|-------|----------|----------|----------|------------|-----------------------|
    /// | 0   | 0-9   | 20,0 m   | 25,0 m   | ±45°     | 0,00       | 0,15                  |
    /// | 1   | 10-14 | 22,0 m   | 27,5 m   | ±50°     | 0,20       | 0,11                  |
    /// | 2   | 15-19 | 24,0 m   | 30,0 m   | ±55°     | 0,40       | 0,07                  |
    /// | 3   | 20    | 26,0 m   | 32,5 m   | ±60°     | 0,60       | 0,03                  |
    /// </summary>
    [System.Serializable]
    public class MonsterEscalation
    {
        [Tooltip("Tắt thì quái giữ nguyên chỉ số gốc suốt ván, không quan tâm người chơi nhặt được bao nhiêu đá.")]
        public bool enabled = true;

        [Tooltip("Số mảnh đá tự do đầu tiên làm quái lên một bậc.")]
        [Min(0)] public int firstTierAtStones = 10;

        [Tooltip("Cứ thêm bao nhiêu mảnh đá thì lên thêm một bậc.")]
        [Min(1)] public int stonesPerTier = 5;

        [Tooltip("Số bậc tối đa, để quái không mạnh vô hạn nếu sau này tăng maxFreedomStones.")]
        [Min(0)] public int maxTier = 3;

        [Header("Mỗi bậc cộng thêm")]
        [Tooltip("Tầm nhìn cộng thêm bao nhiêu phần trăm mỗi bậc (0,10 = +10%).")]
        public float sightPerTier = 0.10f;

        [Tooltip("Tầm nghe cộng thêm bao nhiêu phần trăm mỗi bậc.")]
        public float hearingPerTier = 0.10f;

        [Tooltip("Góc nhìn (độ, mỗi bên so với hướng mặt) nới thêm bao nhiêu mỗi bậc.")]
        public float sightAnglePerTier = 5f;

        [Tooltip("Sàn cho 4 tham số tính cách của brain, cộng thêm mỗi bậc. Kẹp trong 0..1.")]
        public float intentFloorPerTier = 0.20f;

        [Tooltip("Ngưỡng ưu tiên khi đổi trạng thái thay đổi bao nhiêu mỗi bậc. " +
                 "Số âm = đổi trạng thái sớm hơn, phản ứng nhanh hơn.")]
        public float stateSwitchMarginPerTier = -0.04f;

        /// <summary>Bậc leo thang hiện tại; 0 nghĩa là vẫn dùng chỉ số gốc.</summary>
        public int Tier { get; private set; }

        /// <summary>
        /// Đọc lại tiến độ và cập nhật <see cref="Tier"/>. Gọi mỗi frame cũng được: chỉ là vài phép so sánh.
        /// </summary>
        /// <param name="freedomStones">Số mảnh đá tự do người chơi đang có (ItemManager.currentFreedomStones).</param>
        public void Refresh(int freedomStones)
        {
            if (!enabled || freedomStones < firstTierAtStones || stonesPerTier <= 0)
            {
                Tier = 0;
                return;
            }

            int tier = 1 + (freedomStones - firstTierAtStones) / stonesPerTier;
            Tier = Mathf.Clamp(tier, 0, Mathf.Max(0, maxTier));
        }

        /// <summary>Hệ số nhân tầm nhìn ở bậc hiện tại.</summary>
        public float SightMultiplier => 1f + Tier * sightPerTier;

        /// <summary>Hệ số nhân tầm nghe ở bậc hiện tại.</summary>
        public float HearingMultiplier => 1f + Tier * hearingPerTier;

        /// <summary>Góc nhìn cộng thêm (độ) ở bậc hiện tại.</summary>
        public float SightAngleBonus => Tier * sightAnglePerTier;

        /// <summary>Sàn của 4 tham số tính cách: brain trả về thấp hơn mức này thì bị kéo lên.</summary>
        public float IntentFloor => Mathf.Clamp01(Tier * intentFloorPerTier);

        /// <summary>Độ lệch ngưỡng đổi trạng thái so với cấu hình gốc (thường là số âm).</summary>
        public float StateSwitchMarginOffset => Tier * stateSwitchMarginPerTier;

        /// <summary>Kéo một tham số tính cách của brain lên sàn của bậc hiện tại.</summary>
        public float ApplyIntentFloor(float value)
        {
            float floor = IntentFloor;
            return value < floor ? floor : value;
        }
    }
}
