using UnityEngine;

namespace SilentSprint.Gameplay
{
    /// <summary>Mini game nào được gắn vào một StonePedestal.</summary>
    public enum MinigameKind
    {
        Cup = 0,
        Memory = 1,
        Bomb = 2
    }

    /// <summary>
    /// Gắn trên từng StonePedestal. Giữ nhiệm vụ mini game của pedestal đó,
    /// vùng kích hoạt popup và trạng thái đã hoàn thành.
    /// </summary>
    public class MinigamePedestal : MonoBehaviour
    {
        [Header("Nhiệm vụ")]
        [Tooltip("Mini game mà StonePedestal này phụ trách.")]
        public MinigameKind minigameKind = MinigameKind.Cup;

        [Header("Vùng kích hoạt")]
        [Tooltip("Bán kính (world unit) tính từ tâm pedestal. Người chơi vào vùng này sẽ hiện popup.")]
        public float interactRadius = 12f;

        [Header("Trạng thái")]
        [SerializeField] private bool isCompleted = false;

        /// <summary>Đã thắng mini game này rồi thì không hiện popup nữa.</summary>
        public bool IsCompleted => isCompleted;

        /// <summary>Tâm vùng kích hoạt, tính theo tọa độ world.</summary>
        public Vector3 ZoneCenter => transform.position;

        public bool IsPlayerInside(Vector3 playerPosition)
        {
            return (playerPosition - transform.position).sqrMagnitude <= interactRadius * interactRadius;
        }

        public void MarkCompleted()
        {
            isCompleted = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isCompleted ? new Color(0.4f, 0.4f, 0.4f, 0.5f) : new Color(0.2f, 0.9f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
#endif
    }
}
