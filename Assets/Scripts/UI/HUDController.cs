using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SilentSprint.Gameplay;

namespace SilentSprint.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Action Buttons")]
        public Button btnJump;
        public GameObject btnRun;
        public Button btnFlashlight;

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
    }
}