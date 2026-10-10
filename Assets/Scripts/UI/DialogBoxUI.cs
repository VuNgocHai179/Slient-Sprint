using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentSprint.UI
{
    /// <summary>
    /// Phần hiển thị của hộp hội thoại. Component này nằm trên chính prefab DialogBox.
    ///
    /// Nó chỉ làm hai việc:
    /// - Hiện tên người nói + nội dung câu thoại, kèm dấu nhắc bấm để đọc tiếp.
    /// - Báo lại cho người điều phối khi người chơi bấm vào màn hình (OnAdvance)
    ///   hoặc bấm một nút đáp án (OnChoicePicked).
    ///
    /// Toàn bộ prefab (nền mờ + khung hội thoại) là một khối: bật/tắt bằng Show/Hide.
    /// Tấm nền mờ phủ kín màn hình nên vừa chặn thao tác vào game, vừa là vùng bắt cú chạm.
    /// </summary>
    public class DialogBoxUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("Nội dung hội thoại")]
        [SerializeField] private TextMeshProUGUI txtSpeaker;
        [SerializeField] private TextMeshProUGUI txtContent;

        [Header("Dấu nhắc bấm để đọc tiếp")]
        [Tooltip("Object chứa dấu nhắc, sẽ nhấp nháy khi câu thoại đang chờ người chơi bấm.")]
        [SerializeField] private GameObject continueHint;
        [SerializeField] private float hintBlinkSpeed = 3f;
        [SerializeField] private float hintMinAlpha = 0.15f;

        [Header("Nút đáp án")]
        [Tooltip("Các nút đáp án đặt sẵn trong prefab. Nút thừa sẽ tự ẩn khi câu thoại ít đáp án hơn.")]
        [SerializeField] private List<Button> choiceButtons = new List<Button>();

        /// <summary>Người chơi bấm vào màn hình để đọc câu tiếp theo.</summary>
        public event Action OnAdvance;

        /// <summary>Người chơi bấm một nút đáp án, tham số là chỉ số nút.</summary>
        public event Action<int> OnChoicePicked;

        public bool IsVisible => gameObject.activeSelf;

        public bool IsWaitingForChoice => choicesVisible;

        private readonly List<TextMeshProUGUI> choiceLabels = new List<TextMeshProUGUI>();
        private bool choicesVisible;
        private CanvasGroup hintCanvasGroup;

        private void Awake()
        {
            CacheChoiceButtons();
            SetChoices(null);
            Hide();
        }

        private void CacheChoiceButtons()
        {
            choiceLabels.Clear();

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                Button button = choiceButtons[i];
                if (button == null)
                {
                    choiceLabels.Add(null);
                    continue;
                }

                // Nhãn là TextMeshPro nằm trong nút; lấy sẵn để khỏi GetComponent mỗi lần hiện đáp án.
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                choiceLabels.Add(label);

                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnChoicePicked?.Invoke(index));
            }
        }

        private void Update()
        {
            if (continueHint == null) return;
            if (!continueHint.activeSelf) return;

            if (hintCanvasGroup == null)
            {
                hintCanvasGroup = continueHint.GetComponent<CanvasGroup>();
            }

            if (hintCanvasGroup == null) return;

            float t = (Mathf.Sin(Time.unscaledTime * hintBlinkSpeed) + 1f) * 0.5f;
            hintCanvasGroup.alpha = Mathf.Lerp(hintMinAlpha, 1f, t);
        }

        public void Show()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>Đổ nội dung một câu thoại lên hộp thoại.</summary>
        public void SetLine(string speaker, string content)
        {
            if (txtSpeaker != null)
            {
                bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
                txtSpeaker.text = hasSpeaker ? speaker : string.Empty;
                txtSpeaker.gameObject.SetActive(hasSpeaker);
            }

            if (txtContent != null) txtContent.text = content;
        }

        /// <summary>Hiện dấu nhắc bấm để đọc tiếp hay không (tắt khi câu thoại đang chờ chọn đáp án).</summary>
        public void SetContinueHintVisible(bool visible)
        {
            if (continueHint != null && continueHint.activeSelf != visible)
            {
                continueHint.SetActive(visible);
            }
        }

        /// <summary>Hiện danh sách nút đáp án. Truyền null hoặc rỗng để ẩn hết.</summary>
        public void SetChoices(IList<string> labels)
        {
            bool hasChoices = labels != null && labels.Count > 0;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                Button button = choiceButtons[i];
                if (button == null) continue;

                bool used = hasChoices && i < labels.Count;
                if (button.gameObject.activeSelf != used) button.gameObject.SetActive(used);

                if (!used) continue;

                TextMeshProUGUI label = i < choiceLabels.Count ? choiceLabels[i] : null;
                if (label != null) label.text = labels[i];
            }

            choicesVisible = hasChoices;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsVisible) return;

            // Đang chờ chọn đáp án thì cú bấm vào nền không được tính là "đọc tiếp".
            if (choicesVisible) return;

            OnAdvance?.Invoke();
        }
    }
}
