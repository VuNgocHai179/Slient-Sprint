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
    /// Nút "Item1 + 1" hiện chưa nối vì trong game chưa có hệ thống/bộ đếm Item1 nào.
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

        [Tooltip("Số mảnh đá tự do cộng thêm mỗi lần bấm 'Item2 + 1'.")]
        public int addItem2Amount = 1;

        private void Awake()
        {
            Bind(btnResume, Hide);
            Bind(btnClose, Hide);
            Bind(btnSetting, () => Debug.Log("[PauseDialog] Nút Setting chưa được nối vào đâu."));
            Bind(btnQuit, () => Debug.Log("[PauseDialog] Nút QUIT chưa được nối vào đâu."));

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
            Debug.LogWarning("[PauseDialog] Trong game chưa có hệ thống Item1 nào nên nút 'Item1 + 1' chưa làm gì.", this);
        }
    }
}
