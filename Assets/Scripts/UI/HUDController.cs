using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SilentSprint.Gameplay;
using SilentSprint.UI.Dialogs;

namespace SilentSprint.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Action Buttons")]
        public Button btnJump;
        public GameObject btnRun;
        public Button btnFlashlight;

        [Header("La bàn")]
        [Tooltip("Nút bật/tắt la bàn trên MainHUD. Chỉ hiện sau khi người chơi nhận được la bàn.")]
        public Button btnCompass;

        [Header("Tạm dừng")]
        [Tooltip("Nút tạm dừng trên MainHUD, mở bảng DlgPause.")]
        public Button btnPause;

        [Header("Status UI")]
        public Image imgStamina;

        [Header("Item Counter UI")]
        public TextMeshProUGUI txtFreedomStone;

        private PlayerController player;

        void Start()
        {
            player = PlayerController.Instance;

            SetupJumpButton();
            SetupRunButton();
            SetupFlashlightButton();
            SetupCompassButton();
            SetupPauseButton();

            if (ItemManager.Instance != null)
            {
                UpdateFreedomStoneDisplay(ItemManager.Instance.currentFreedomStones, ItemManager.Instance.maxFreedomStones);
            }
        }

        void OnEnable()
        {
            ItemManager.OnFreedomStoneChanged += UpdateFreedomStoneDisplay;
        }

        void OnDisable()
        {
            ItemManager.OnFreedomStoneChanged -= UpdateFreedomStoneDisplay;
        }

        public void UpdateFreedomStoneDisplay(int current, int max)
        {
            if (txtFreedomStone != null)
            {
                txtFreedomStone.text = $"{current}/{max}";
            }
        }

        private void Update()
        {
            if (player != null && imgStamina != null)
            {
                imgStamina.fillAmount = player.currentStamina / player.maxStamina;
            }
        }

        private void SetupJumpButton()
        {
            if (btnJump != null)
            {
                btnJump.onClick.RemoveAllListeners();
                btnJump.onClick.AddListener(() =>
                {
                    if (player != null) player.Jump();
                });
            }
        }

        private void SetupRunButton()
        {
            if (btnRun == null) return;

            EventTrigger trigger = btnRun.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btnRun.AddComponent<EventTrigger>();
            else trigger.triggers.Clear();

            EventTrigger.Entry pointerDown = new EventTrigger.Entry();
            pointerDown.eventID = EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) =>
            {
                if (player != null) player.StartRun();
            });
            trigger.triggers.Add(pointerDown);

            EventTrigger.Entry pointerUp = new EventTrigger.Entry();
            pointerUp.eventID = EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) =>
            {
                if (player != null) player.StopRun();
            });
            trigger.triggers.Add(pointerUp);

            EventTrigger.Entry pointerExit = new EventTrigger.Entry();
            pointerExit.eventID = EventTriggerType.PointerExit;
            pointerExit.callback.AddListener((data) =>
            {
                if (player != null) player.StopRun();
            });
            trigger.triggers.Add(pointerExit);
        }

        private void SetupFlashlightButton()
        {
            if (btnFlashlight != null)
            {
                btnFlashlight.onClick.RemoveAllListeners();
                btnFlashlight.onClick.AddListener(() =>
                {
                    if (player != null) player.ToggleFlashlight();
                });
            }
        }

        /// <summary>Bật/tắt nút BtnCompass. Nút này chỉ hiện sau khi người chơi nhận được la bàn.</summary>
        public void SetCompassButtonVisible(bool visible)
        {
            if (btnCompass == null) return;

            if (btnCompass.gameObject.activeSelf != visible)
            {
                btnCompass.gameObject.SetActive(visible);
            }
        }

        private void SetupCompassButton()
        {
            if (btnCompass == null)
            {
                // Có thể chưa gán tay trong Inspector: tìm luôn nút cùng tên nằm trên MainHUD.
                Transform found = transform.Find("BtnCompass");
                if (found != null) btnCompass = found.GetComponent<Button>();
            }

            if (btnCompass == null) return;

            btnCompass.onClick.RemoveAllListeners();
            btnCompass.onClick.AddListener(() =>
            {
                if (CompassSystem.Instance != null) CompassSystem.Instance.ToggleCompass();
            });

            // Chưa nhặt đủ mảnh đá thì chưa có la bàn, nút phải ẩn đi.
            if (CompassSystem.Instance == null || !CompassSystem.Instance.IsUnlocked)
            {
                SetCompassButtonVisible(false);
            }
        }

        private void SetupPauseButton()
        {
            if (btnPause == null)
            {
                // Có thể chưa gán tay trong Inspector: tìm luôn nút cùng tên nằm trên MainHUD.
                Transform found = transform.Find("btn_Pause");
                if (found != null) btnPause = found.GetComponent<Button>();
            }

            if (btnPause == null) return;

            btnPause.onClick.RemoveAllListeners();
            btnPause.onClick.AddListener(() =>
            {
                if (DialogManager.Instance != null) DialogManager.Instance.Toggle<PauseDialog>();
            });
        }
    }
}