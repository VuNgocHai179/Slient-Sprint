using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Nút cheat "Win" đặt trong mỗi phòng mini game (object tên <see cref="ObjectName"/>).
    ///
    /// Mỗi mini game tự cắm nút này vào đường thắng thật của mình, nên bấm cheat cũng đi y hệt một ván thắng:
    /// ẩn camera phòng, bắn OnGameWon, rồi MinigameSystemManager mới đánh dấu tượng đã xong và nhả vật phẩm thưởng.
    /// Nhờ vậy không có đường tắt riêng nào phải bảo trì thêm.
    /// </summary>
    public static class MinigameCheatButton
    {
        /// <summary>Tên object của nút cheat trong các phòng mini game.</summary>
        public const string ObjectName = "btn_cheat";

        /// <summary>
        /// Gắn hành động thắng vào nút cheat của phòng và trả về nút đó (null nếu phòng không có nút nào).
        /// </summary>
        /// <param name="room">Object gốc của phòng mini game, nơi chứa nút cheat.</param>
        /// <param name="assigned">Nút gán tay trong Inspector. Để trống sẽ tự tìm object con tên <see cref="ObjectName"/>.</param>
        /// <param name="onCheat">Việc cần làm khi bấm nút — thường là hàm thắng ngay của mini game.</param>
        public static Button Bind(MonoBehaviour room, Button assigned, UnityAction onCheat)
        {
            if (room == null) return null;

            Button button = assigned != null ? assigned : Find(room);
            if (button == null) return null;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onCheat);
            return button;
        }

        /// <summary>Tìm nút cheat trong phòng, kể cả object đang tắt.</summary>
        public static Button Find(MonoBehaviour room)
        {
            foreach (Button button in room.GetComponentsInChildren<Button>(true))
            {
                if (button.name == ObjectName) return button;
            }

            return null;
        }
    }
}
