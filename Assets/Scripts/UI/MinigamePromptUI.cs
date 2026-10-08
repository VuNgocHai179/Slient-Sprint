using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SilentSprint.UI
{
    /// <summary>
    /// Popup hỏi người chơi có vào mini game hay không.
    /// Do MinigameSystemManager sinh ra trên Canvas của HUD khi người chơi lại gần StonePedestal.
    /// </summary>
    public class MinigamePromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Panel chứa nội dung popup, bật/tắt khi hiện hoặc ẩn.")]
        public GameObject panelRoot;
        public TextMeshProUGUI txtTitle;
        public TextMeshProUGUI txtDescription;
        public Button btnEnter;
        public Button btnCancel;

        /// <summary>Người chơi bấm nút VÀO CHƠI.</summary>
        public event Action OnConfirm;
        /// <summary>Người chơi bấm nút BỎ QUA.</summary>
        public event Action OnCancel;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;

            if (btnEnter != null) btnEnter.onClick.AddListener(HandleConfirmClicked);
            if (btnCancel != null) btnCancel.onClick.AddListener(HandleCancelClicked);

            panelRoot.SetActive(false);
        }

        public void Show(string title, string description)
        {
            if (txtTitle != null) txtTitle.text = title;
            if (txtDescription != null) txtDescription.text = description;
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void HandleConfirmClicked()
        {
            Hide();
            OnConfirm?.Invoke();
        }

        private void HandleCancelClicked()
        {
            Hide();
            OnCancel?.Invoke();
        }
    }
}
