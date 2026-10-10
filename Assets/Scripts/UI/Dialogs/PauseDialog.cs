using SilentSprint.Core;
using SilentSprint.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SilentSprint.UI.Dialogs
{
    /// <summary>
    /// Bảng tạm dừng (prefab DlgPause): Resume/Close để chơi tiếp, Cheat để mở hộp cheat.
    ///
    /// Hộp cheat dùng để đỡ phải đi nhặt đủ 20 mảnh đá: bấm "Item2 + 1" là cộng thẳng vào bộ đếm
    /// mảnh đá tự do, nên tới mốc 10 là la bàn vẫn rơi xuống như nhặt thật.
    ///
    /// "Item1 + 1" cộng thẳng vào bộ đếm vật phẩm cần tìm — thứ mà tượng thiên thần thưởng mỗi khi thắng mini game.
    /// </summary>
    public class PauseDialog : DialogView
    {
        [Header("Nút của bảng")]
        public Button btnResume;
        public Button btnClose;
        public Button btnSetting;
        public Button btnQuit;

        [Header("Hộp cheat")]
        public Button btnCheat;
        public GameObject boxCheat;
        public Button btnAddItem1;
        public Button btnAddItem2;

        [Tooltip("Số vật phẩm cần tìm cộng thêm mỗi lần bấm 'Item1 + 1'.")]
        public int addItem1Amount = 1;

        [Tooltip("Số mảnh đá tự do cộng thêm mỗi lần bấm 'Item2 + 1'.")]
        public int addItem2Amount = 1;

        private void Awake()
        {
            Bind(btnResume, Hide);
            Bind(btnClose, Hide);
            Bind(btnSetting, OpenSettings);
            Bind(btnQuit, QuitToMainMenu);

            Bind(btnCheat, ToggleCheatBox);
            Bind(btnAddItem1, AddItem1);
            Bind(btnAddItem2, AddItem2);

            if (boxCheat != null) boxCheat.SetActive(false);
        }

        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Mở bảng cài đặt (prefab DlgSetting) — trong đó có hai ô tick bật/tắt nhạc và tiếng hiệu ứng.</summary>
        private void OpenSettings()
        {
            if (DialogManager.Instance == null)
            {
                Debug.LogWarning("[PauseDialog] Chưa có DialogManager nên không mở được bảng cài đặt.", this);
                return;
            }

            DialogManager.Instance.Show<SettingDialog>();
        }

        /// <summary>
        /// Thoát ra bảng chính. Đóng bảng trước để DialogManager trả timeScale lại cho game
        /// (bảng đang mở thì game bị dừng), rồi mới đổi scene.
        /// </summary>
        private void QuitToMainMenu()
        {
            // Ra bảng chính thì lưu lại tiến trình đang chơi để nút Continue còn quay lại được.
            if (GameplaySaveManager.Instance != null) GameplaySaveManager.Instance.SaveNow();

            Hide();
            SceneFlow.Load(SceneNames.MainMenu);
        }

        private void ToggleCheatBox()
        {
            if (boxCheat != null) boxCheat.SetActive(!boxCheat.activeSelf);
        }

        private void AddItem2()
        {
            if (ItemManager.Instance == null)
            {
                Debug.LogWarning("[PauseDialog] Chưa có ItemManager nên không cộng được mảnh đá.", this);
                return;
            }

            ItemManager.Instance.CollectFreedomStone(Mathf.Max(1, addItem2Amount));
        }

        private void AddItem1()
        {
            if (ItemManager.Instance == null)
            {
                Debug.LogWarning("[PauseDialog] Chưa có ItemManager nên không cộng được vật phẩm cần tìm.", this);
                return;
            }

            ItemManager.Instance.CollectQuestItem(Mathf.Max(1, addItem1Amount));
        }
    }
}
