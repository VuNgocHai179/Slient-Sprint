using SilentSprint.Core;
using SilentSprint.UI.Dialogs;
using UnityEngine;
using UnityEngine.UI;

namespace SilentSprint.UI
{
    /// <summary>
    /// Nối các nút của bảng MainMenu vào luồng scene (xem <see cref="SceneFlow"/>):
    ///  - Play Game: ván mới — xoá bản lưu cũ rồi xem cảnh phim mở đầu (CutScene1 -> CutScene2 -> Gameplay).
    ///  - Continue: chỉ hiện khi có bản lưu, tức là đang chơi dở ở màn Gameplay rồi quay ra bảng chính.
    ///  - Settings: mở bảng cài đặt.
    ///  - Quit: thoát game.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Nút của bảng chính")]
        public Button btnPlayGame;
        public Button btnContinue;
        public Button btnSetting;
        public Button btnQuit;

        [Header("Scene")]
        [Tooltip("Bấm Play Game: mở màn bằng cảnh phim.")]
        public string playScene = SceneNames.CutScene1;

        [Tooltip("Bấm Continue: vào lại màn chơi đang chơi dở.")]
        public string continueScene = SceneNames.Gameplay;

        private void Start()
        {
            // Bảng chính luôn phải thấy chuột: nút chơi lại ở màn Game Over có thể đã tắt con trỏ đi.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Bind(btnPlayGame, StartNewGame);
            Bind(btnQuit, SceneFlow.QuitGame);
            Bind(btnSetting, OpenSettings);
            SetUpContinueButton();
        }

        /// <summary>
        /// Continue chỉ có nghĩa khi có bản lưu (đang chơi dở ở màn Gameplay, hoặc đã thoát app giữa chừng).
        /// Chưa chơi gì thì giấu nút đi.
        /// </summary>
        private void SetUpContinueButton()
        {
            if (btnContinue == null) return;

            if (!GameSave.HasSave)
            {
                btnContinue.gameObject.SetActive(false);
                return;
            }

            btnContinue.gameObject.SetActive(true);
            Bind(btnContinue, ContinueRun);
        }

        private void StartNewGame()
        {
            // Ván mới thì bản lưu cũ không còn dùng nữa.
            GameSave.Delete();
            SceneFlow.Load(playScene);
        }

        private void ContinueRun()
        {
            // Đọc bản lưu ra trước; màn Gameplay sẽ dựng lại trạng thái này ở frame đầu tiên.
            GameSave.QueueLoad();
            SceneFlow.Load(continueScene);
        }

        private void OpenSettings()
        {
            if (DialogManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuController] Bảng chính chưa có DialogManager nên không mở được bảng cài đặt.", this);
                return;
            }

            DialogManager.Instance.Show<SettingDialog>();
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
