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
        /// <summary>Tên object của tượng thiên thần trong scene, dùng để tự tìm khi chưa gán angelAnchor.</summary>
        private const string AngelNamePrefix = "FallenAngel";

        [Header("Nhiệm vụ")]
        [Tooltip("Mini game mà StonePedestal này phụ trách.")]
        public MinigameKind minigameKind = MinigameKind.Cup;

        [Header("Vùng kích hoạt")]
        [Tooltip("Bán kính (world unit) tính từ tâm pedestal. Người chơi vào vùng này sẽ hiện popup.")]
        public float interactRadius = 12f;

        [Header("Tượng thiên thần")]
        [Tooltip("Tượng thiên thần sa ngã của pedestal này — nơi nhả vật phẩm thưởng khi người chơi thắng. " +
                 "Để trống sẽ tự tìm object con có tên bắt đầu bằng 'FallenAngel'.")]
        public Transform angelAnchor;

        private Transform cachedAngel;

        /// <summary>
        /// Tượng thiên thần của pedestal, dùng làm chỗ sinh ra vật phẩm thưởng. Null nếu không tìm thấy.
        /// </summary>
        public Transform AngelAnchor
        {
            get
            {
                if (angelAnchor != null) return angelAnchor;
                if (cachedAngel == null) cachedAngel = FindAngelChild();
                return cachedAngel;
            }
        }

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

        /// <summary>
        /// Tìm tượng thiên thần trong các object con. Tượng được thả tay vào từng pedestal trong scene
        /// (không nằm trong prefab) nên tên là thứ duy nhất nhận ra được chúng; muốn chắc thì gán thẳng vào angelAnchor.
        /// </summary>
        private Transform FindAngelChild()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                if (child.name.StartsWith(AngelNamePrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
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
