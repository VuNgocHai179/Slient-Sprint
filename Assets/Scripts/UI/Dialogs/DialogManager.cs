using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Gameplay;
using UnityEngine;

namespace SilentSprint.UI.Dialogs
{
    /// <summary>
    /// Bộ quản lý các dialog dạng bảng trên UI. Mọi bảng đều đi qua đây: nó giữ bản sao của từng
    /// prefab đã đăng ký, bảo đảm mỗi lúc chỉ mở một bảng, và xử lý phần "dừng game" khi bảng mở.
    ///
    /// Không liên quan tới SilentSprint.Dialogues (hội thoại trong màn chơi) — hai hệ thống độc lập,
    /// chung nhau chỉ ở chỗ đều là UI.
    ///
    /// Thêm một bảng mới: gắn lớp kế thừa DialogView lên prefab, thả prefab vào <see cref="dialogs"/>,
    /// rồi gọi DialogManager.Instance.Show&lt;TenBang&gt;().
    /// </summary>
    public class DialogManager : Singleton<DialogManager>
    {
        [Header("Các bảng")]
        [Tooltip("Prefab của mọi dialog hệ thống này quản lý. Mỗi prefab được sinh sẵn một bản (ẩn) khi vào màn.")]
        public DialogView[] dialogs;

        [Header("Chỗ đặt bảng")]
        [Tooltip("Object cha của các bảng khi sinh ra. Để trống sẽ lấy Canvas của MainHUD.")]
        public Transform dialogParent;

        [Header("Trong lúc bảng mở")]
        [Tooltip("HUD chính. Để trống sẽ tự lấy từ HUDController.")]
        public GameObject mainHUD;

        [Tooltip("Ẩn HUD chính khi bảng mở. Bảng tạm dừng có nền mờ riêng nên mặc định để HUD nằm dưới nền mờ.")]
        public bool hideHudWhileOpen;

        /// <summary>Có bảng nào đang mở không.</summary>
        public bool IsAnyOpen => current != null;

        /// <summary>Bảng đang mở, null nếu không có.</summary>
        public DialogView Current => current;

        private readonly Dictionary<DialogView, DialogView> instances = new Dictionary<DialogView, DialogView>();

        private DialogView current;
        private bool hudHidden;
        private bool gameplayPaused;
        private bool playerWasEnabled;
        private float previousTimeScale;

        protected override void Awake()
        {
            base.Awake();

            // Singleton hủy bản trùng ở cuối frame, nên phải tự chặn kẻo sinh bảng hai lần.
            if (Instance != this) return;

            ResolveReferences();
            CreateInstances();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<DialogView, DialogView> pair in instances)
            {
                if (pair.Value == null) continue;
                pair.Value.OnClosed -= HandleDialogClosed;
            }
        }

        // ------------------------------------------------------------------ Mở / đóng

        /// <summary>Mở một bảng theo prefab. Trả về bản đang hiện.</summary>
        public DialogView Show(DialogView prefab)
        {
            DialogView instance = GetInstance(prefab);
            if (instance == null) return null;
            if (current == instance) return instance;

            // Mỗi lúc chỉ một bảng: mở bảng mới thì đóng bảng đang mở.
            if (current != null) current.Hide();

            current = instance;
            instance.transform.SetAsLastSibling();

            if (hideHudWhileOpen && mainHUD != null && mainHUD.activeSelf)
            {
                mainHUD.SetActive(false);
                hudHidden = true;
            }

            if (instance.pauseGameplay) PauseGameplay();

            instance.Show();
            return instance;
        }

        /// <summary>Mở bảng theo loại, tìm trong danh sách đã đăng ký.</summary>
        public T Show<T>() where T : DialogView
        {
            T prefab = FindRegistered<T>();
            if (prefab == null)
            {
                Debug.LogError($"[DialogManager] Chưa đăng ký bảng {typeof(T).Name} trong danh sách dialogs.", this);
                return null;
            }

            return Show(prefab) as T;
        }

        /// <summary>Đang mở thì đóng, đang đóng thì mở.</summary>
        public void Toggle(DialogView prefab)
        {
            DialogView instance = GetInstance(prefab);
            if (instance == null) return;

            if (current == instance) instance.Hide();
            else Show(prefab);
        }

        public void Toggle<T>() where T : DialogView
        {
            T prefab = FindRegistered<T>();
            if (prefab == null)
            {
                Debug.LogError($"[DialogManager] Chưa đăng ký bảng {typeof(T).Name} trong danh sách dialogs.", this);
                return;
            }

            Toggle(prefab);
        }

        /// <summary>Đóng bảng đang mở.</summary>
        public void HideCurrent()
        {
            if (current != null) current.Hide();
        }

        /// <summary>Đóng tất cả các bảng (kể cả bảng không phải bảng đang mở).</summary>
        public void HideAll()
        {
            foreach (KeyValuePair<DialogView, DialogView> pair in instances)
            {
                if (pair.Value != null) pair.Value.Hide();
            }
        }

        // ------------------------------------------------------------------ Nội bộ

        private void HandleDialogClosed(DialogView dialog)
        {
            // Bảng tự đóng: trả game về trạng thái thường nếu không còn bảng nào mở.
            if (current != dialog) return;

            current = null;
            RestoreState();
        }

        private void PauseGameplay()
        {
            if (gameplayPaused) return;

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            gameplayPaused = true;

            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            playerWasEnabled = player.enabled;
            player.enabled = false;

            // HUD bị chặn nên joystick không nhận được PointerUp nữa; trả cần về giữa
            // kẻo đóng bảng xong người chơi chạy tiếp.
            if (player.movementJoystick != null) player.movementJoystick.ResetInput();
        }

        private void RestoreState()
        {
            if (gameplayPaused)
            {
                Time.timeScale = previousTimeScale;
                gameplayPaused = false;

                if (playerWasEnabled && PlayerController.Instance != null)
                {
                    PlayerController.Instance.enabled = true;
                }
                playerWasEnabled = false;
            }

            if (hudHidden && mainHUD != null)
            {
                mainHUD.SetActive(true);
            }
            hudHidden = false;
        }

        private void ResolveReferences()
        {
            if (mainHUD == null)
            {
                HUDController hud = FindAnyObjectByType<HUDController>();
                if (hud != null) mainHUD = hud.gameObject;
            }

            if (dialogParent == null && mainHUD != null)
            {
                Canvas canvas = mainHUD.GetComponentInParent<Canvas>();
                if (canvas != null) dialogParent = canvas.transform;
            }
        }

        private void CreateInstances()
        {
            if (dialogs == null) return;

            WarnAboutInputBlockers();

            for (int i = 0; i < dialogs.Length; i++)
            {
                DialogView prefab = dialogs[i];
                if (prefab == null) continue;

                DialogView instance = Instantiate(prefab, dialogParent);
                instance.name = prefab.name;
                instance.gameObject.SetActive(false);

                instance.OnClosed += HandleDialogClosed;
                instances[prefab] = instance;
            }
        }

        /// <summary>
        /// Object cha mà thiếu cú bấm thì dialog vẫn hiện ra nhưng bấm không ăn — lỗi rất khó đoán,
        /// nên báo thẳng ra tên object và ô cần bật lại.
        /// </summary>
        private void WarnAboutInputBlockers()
        {
            if (dialogParent == null) return;

            CanvasGroup[] groups = dialogParent.GetComponentsInParent<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group.blocksRaycasts && group.interactable) continue;

                Debug.LogError(
                    $"[DialogManager] '{group.gameObject.name}' đang chặn cú bấm vào các bảng " +
                    $"(Canvas Group: Blocks Raycasts = {group.blocksRaycasts}, Interactable = {group.interactable}). " +
                    "Bấm vào bảng sẽ không ăn cho tới khi bật lại hai ô đó.", group);
            }
        }

        private DialogView GetInstance(DialogView prefab)
        {
            if (prefab == null) return null;

            if (instances.TryGetValue(prefab, out DialogView instance) && instance != null)
            {
                return instance;
            }

            Debug.LogError($"[DialogManager] Bảng '{prefab.name}' chưa được đăng ký trong danh sách dialogs.", this);
            return null;
        }

        private T FindRegistered<T>() where T : DialogView
        {
            if (dialogs == null) return null;

            for (int i = 0; i < dialogs.Length; i++)
            {
                if (dialogs[i] is T match) return match;
            }

            return null;
        }
    }
}
