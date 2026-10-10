using System;
using UnityEngine;

namespace SilentSprint.UI.Dialogs
{
    /// <summary>
    /// Gốc của một dialog dạng bảng trên UI (bảng tạm dừng, bảng cài đặt...).
    ///
    /// Đây là hệ thống riêng cho các bảng UI, tách hẳn khỏi SilentSprint.Dialogues — hệ hội thoại
    /// chạy trong màn chơi (người bí ẩn, tượng đá). Muốn thêm một bảng mới: viết một lớp kế thừa
    /// lớp này gắn lên prefab, rồi để DialogManager mở nó ra.
    ///
    /// Lớp này chỉ biết bật/tắt chính mình và báo khi bị đóng. Việc ẩn HUD chính, dừng game, hay
    /// bảo đảm mỗi lúc chỉ mở một bảng là của DialogManager.
    /// </summary>
    public class DialogView : MonoBehaviour
    {
        [Tooltip("Dừng hẳn game (thời gian, người chơi) trong lúc bảng này mở.")]
        public bool pauseGameplay;

        /// <summary>Bảng vừa bị đóng (do người chơi bấm nút hay do hệ thống đóng).</summary>
        public event Action<DialogView> OnClosed;

        /// <summary>Bảng đang hiện.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Bật bảng lên. Thường để DialogManager gọi thay vì gọi thẳng.</summary>
        public void Show()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            OnShown();
        }

        /// <summary>Đóng bảng. Bảng tự đóng chính nó (nút Close/Resume) cũng dùng hàm này.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf) return;

            gameObject.SetActive(false);
            OnClosed?.Invoke(this);
        }

        /// <summary>Việc riêng của từng bảng khi vừa hiện ra.</summary>
        protected virtual void OnShown() { }
    }
}
