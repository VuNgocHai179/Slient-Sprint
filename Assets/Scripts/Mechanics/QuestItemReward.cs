using SilentSprint.Gameplay;
using UnityEngine;

namespace SilentSprint.Mechanics
{
    /// <summary>
    /// Vật phẩm thưởng (Item1) mà tượng thiên thần sa ngã nhả ra sau khi người chơi thắng mini game của tượng đó.
    ///
    /// Vật phẩm hiện ra ngay tại tượng, đứng yên một nhịp cho người chơi kịp thấy, rồi từ từ bay vào người chơi.
    /// Chạm tới người chơi thì cộng một vật phẩm vào bộ đếm trên HUD rồi tự xoá.
    ///
    /// Đích bay bám theo người chơi (cập nhật mỗi frame) nên người chơi có chạy cũng không bị hụt. Dùng thời gian
    /// không theo timeScale: đang bay mà có hội thoại mở ra (timeScale = 0) thì vật phẩm vẫn bay tới nơi,
    /// không đứng hình giữa không trung.
    /// </summary>
    public class QuestItemReward : MonoBehaviour
    {
        [Header("Điểm nhắm trên người chơi")]
        [Tooltip("Lệch so với gốc Player, thường là ngang ngực để vật phẩm bay vào giữa người.")]
        public Vector3 playerAnchorOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Dáng bay")]
        [Tooltip("Độ cao của đường bay cong, tính theo world unit. 0 = bay thẳng.")]
        public float arcHeight = 0.75f;

        [Tooltip("Tốc độ tự xoay của vật phẩm trong lúc bay, độ mỗi giây.")]
        public float spinSpeed = 180f;

        private Transform player;
        private Vector3 startPosition;
        private bool flying;

        // Nhịp bay do nơi thưởng vật phẩm đặt (MinigameSystemManager), vì đó mới là chỗ quyết định nhịp thưởng.
        private float startDelay;
        private float flyDuration;
        private float timer;

        /// <summary>
        /// Bắt đầu hiệu ứng: đứng yên tại chỗ đang đứng một nhịp rồi bay vào người chơi.
        /// </summary>
        /// <param name="playerTarget">Transform của người chơi — vật phẩm bám theo cái này.</param>
        /// <param name="delay">Đứng yên tại tượng bao lâu trước khi bay (giây).</param>
        /// <param name="duration">Bay từ tượng tới người chơi mất bao lâu (giây).</param>
        public void BeginFly(Transform playerTarget, float delay, float duration)
        {
            player = playerTarget;
            startDelay = Mathf.Max(0f, delay);
            flyDuration = Mathf.Max(0.01f, duration);

            startPosition = transform.position;
            timer = 0f;
            flying = true;
        }

        private void Update()
        {
            if (!flying) return;

            // Người chơi biến mất (hết màn) thì vật phẩm ở lại chỗ cũ thay vì bay vào hư không.
            if (player == null)
            {
                flying = false;
                return;
            }

            timer += Time.unscaledDeltaTime;

            if (timer < startDelay) return;

            float t = Mathf.Clamp01((timer - startDelay) / flyDuration);

            // Khởi đầu chậm rồi nhanh dần: vật phẩm nhích ra khỏi tượng rồi lao vào người chơi.
            Vector3 position = Vector3.LerpUnclamped(startPosition, TargetPosition(), t * t);

            // Cong nhẹ cho đường bay khỏi thẳng đơ.
            position += Vector3.up * (arcHeight * Mathf.Sin(Mathf.PI * t));

            transform.position = position;
            transform.Rotate(Vector3.up * (spinSpeed * Time.unscaledDeltaTime), Space.World);

            if (t >= 1f)
            {
                Collect();
            }
        }

        private Vector3 TargetPosition()
        {
            return player.position + playerAnchorOffset;
        }

        /// <summary>Vật phẩm đã nhập vào người chơi: cộng bộ đếm rồi biến mất.</summary>
        private void Collect()
        {
            flying = false;
            transform.position = TargetPosition();

            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.CollectQuestItem();
            }

            Destroy(gameObject);
        }
    }
}
