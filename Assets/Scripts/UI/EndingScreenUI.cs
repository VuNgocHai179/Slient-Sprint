using System.Collections;
using SilentSprint.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SilentSprint.UI
{
    /// <summary>
    /// Bảng "phá đảo" của đoạn kết, dựng hoàn toàn bằng code (không cần prefab, không phải chỉnh tay trong scene).
    ///
    /// Vì sao dựng bằng code: cùng lý do <see cref="SilentSprint.Cutscenes.CutsceneSceneLink"/> tự dựng nút Skip —
    /// bảng này chỉ có một chỗ dùng, làm prefab riêng thì mỗi lần sửa chữ hay cỡ chữ lại phải giữ prefab đồng bộ
    /// với code. Chữ và nhịp đều là tham số truyền vào từ <see cref="SilentSprint.Gameplay.KamehamehaEnding"/>.
    ///
    /// Bảng có ba phần, xếp từ trên xuống: <b>tên người nói</b> (chữ đỏ, mặc định "Người bí ẩn"),
    /// <b>thông điệp cuối</b> (lời nhắn, xuống dòng tự do) và nút về bảng chính. Không có dòng tiêu đề —
    /// bảng chỉ để đọc lời nhắn cuối. Mọi chữ trên bảng dùng chung font <see cref="FontResourcePath"/> — bảng
    /// dựng bằng code nên không có ô Font Asset nào để gán tay trong Inspector, đành nạp qua Resources
    /// (xem <see cref="ResolveFont"/>).
    /// </summary>
    public sealed class EndingScreenUI : MonoBehaviour
    {
        // Trên mọi Canvas của game (0) nhưng dưới tấm màn đen của ScreenFader (30000):
        // lúc chuyển về bảng chính thì tấm màn của ScreenFader phải che hết bảng này.
        private const int CanvasSortingOrder = 25000;

        /// <summary>
        /// Font của bảng, tính từ gốc thư mục Resources của TextMesh Pro
        /// (Assets/TextMesh Pro/Examples &amp; Extras/Resources/Fonts &amp; Materials/Roboto-Bold SDF.asset).
        ///
        /// Chọn Roboto Bold SDF vì đây là font asset động (m_AtlasPopulationMode = Dynamic) và font nguồn
        /// Roboto-Bold.ttf có đủ dấu tiếng Việt: thông điệp của bảng là câu tiếng Việt dài, font nào thiếu glyph
        /// thì TextMesh Pro vẽ ra ô vuông. Bản Oswald Bold SDF trong project thì thiếu (font nguồn là Oswald 2.002,
        /// không có codepage 1258) nên không dùng được cho câu này.
        /// </summary>
        private const string FontResourcePath = "Fonts & Materials/Roboto-Bold SDF";

        private static readonly Color SpeakerColor = new Color(1f, 0f, 0f, 1f);
        private static readonly Color BodyColor = new Color(0.9f, 0.93f, 0.98f, 1f);

        private static TMP_FontAsset font;
        private static bool fontResolved;

        private static EndingScreenUI instance;

        private Image black;
        private CanvasGroup panel;
        private TextMeshProUGUI speaker;
        private TextMeshProUGUI message;
        private Button backButton;
        private TextMeshProUGUI backLabel;

        private bool clearSave;
        private string nextScene = SceneNames.MainMenu;

        /// <summary>Bảng phá đảo của scene hiện tại; chưa có thì dựng mới.</summary>
        public static EndingScreenUI GetOrCreate()
        {
            if (instance != null) return instance;

            var host = new GameObject("[EndingScreenUI]");
            return host.AddComponent<EndingScreenUI>();
        }

        private void Awake()
        {
            instance = this;
            Build();
        }

        private void OnDestroy()
        {
            // Scene bị thay thì lần sau vào lại phải dựng bảng mới.
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Mờ dần sang màn đen rồi hiện bảng phá đảo. Nút ở bảng chỉ ăn được sau khi bảng hiện xong,
        /// để người chơi không bấm trúng trong lúc bảng còn đang hiện ra.
        /// </summary>
        /// <param name="speakerText">Tên người nói của thông điệp, hiện ở dòng trên cùng (chữ đỏ).</param>
        /// <param name="messageText">Thông điệp cuối, hiện ở giữa bảng.</param>
        /// <param name="buttonText">Chữ trên nút.</param>
        /// <param name="fadeDuration">Mờ tối mất bao lâu (giây).</param>
        /// <param name="deleteSave">Xoá bản lưu khi rời đi (phá đảo là hết ván, bảng chính không nên còn nút chơi tiếp).</param>
        /// <param name="sceneName">Scene quay về khi bấm nút.</param>
        public void ShowEnding(string speakerText, string messageText, string buttonText, float fadeDuration,
                               bool deleteSave, string sceneName)
        {
            clearSave = deleteSave;
            if (!string.IsNullOrEmpty(sceneName)) nextScene = sceneName;

            if (speaker != null) speaker.text = speakerText;
            if (message != null) message.text = messageText;
            if (backLabel != null) backLabel.text = buttonText;

            // Mobile không có con trỏ, nhưng bản chạy trên máy tính thì cần con trỏ để bấm nút.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (black != null) black.raycastTarget = true;
            if (panel != null) panel.gameObject.SetActive(true);
            if (backButton != null) backButton.interactable = false;

            StartCoroutine(ShowEndingRoutine(Mathf.Max(0.05f, fadeDuration)));
        }

        // ------------------------------------------------------------------ dựng UI

        private void Build()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning(
                    "[EndingScreenUI] Scene không có EventSystem nên nút ở bảng phá đảo sẽ không bấm được.", this);
            }

            var canvasObject = new GameObject("EndingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            black = CreateBlack(canvasObject.transform);
            panel = CreatePanel(canvasObject.transform, out speaker, out message, out backButton, out backLabel);
        }

        /// <summary>
        /// Font chữ của bảng, nạp một lần rồi dùng chung cho mọi lần dựng bảng.
        ///
        /// Không nạp được (asset bị dời/chưa import) thì để TextMesh Pro dùng font mặc định của project và báo ra
        /// Console: bảng vẫn đọc được, chỉ là không đúng font đã chọn.
        /// </summary>
        private static TMP_FontAsset ResolveFont()
        {
            if (fontResolved) return font;

            fontResolved = true;
            font = Resources.Load<TMP_FontAsset>(FontResourcePath);

            if (font == null)
            {
                Debug.LogWarning($"[EndingScreenUI] Không nạp được font \"{FontResourcePath}\" trong Resources nên " +
                                 "bảng phá đảo dùng font mặc định của TextMesh Pro.");
            }

            return font;
        }

        private static Image CreateBlack(Transform parent)
        {
            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            return image;
        }

        private CanvasGroup CreatePanel(Transform parent, out TextMeshProUGUI speaker, out TextMeshProUGUI message,
                                       out Button button, out TextMeshProUGUI buttonLabel)
        {
            var panelObject = new GameObject("EndingPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(parent, false);

            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup group = panelObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            // Bảng không có dòng tiêu đề, nên ba phần chia dọc màn 1080: tên người nói (+270), thông điệp (+20)
            // và nút (-330). Thông điệp là câu dài nên ô chữ cao tới 460 và cỡ chữ để 46.
            //
            // Font Style để Normal chứ không Bold: font asset đang dùng là Roboto-Bold SDF, tức mặt chữ đã là bản
            // Bold rồi; bật thêm Bold nữa thì TextMesh Pro bồi đậm lần hai (không có font đậm riêng để đổi sang).
            //
            // Tên người nói để chữ ĐỎ cho khớp hộp hội thoại (DialogBox.prefab cũng hiện "Người bí ẩn" màu đỏ).
            speaker = CreateLabel(panelObject.transform, "Speaker", new Vector2(0f, 270f), new Vector2(1400f, 90f),
                                  64f, FontStyles.Normal, SpeakerColor);

            message = CreateLabel(panelObject.transform, "Message", new Vector2(0f, 20f), new Vector2(1560f, 460f),
                                  46f, FontStyles.Normal, BodyColor);

            button = CreateButton(panelObject.transform, new Vector2(0f, -330f), new Vector2(620f, 140f), out buttonLabel);

            panelObject.SetActive(false);
            return group;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 position, Vector2 size,
                                                  float fontSize, FontStyles style, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.raycastTarget = false;

            // Font của bảng (Roboto Bold SDF). Đổi font asset thì cỡ chữ không phải chỉnh lại: TextMesh Pro lấy
            // point size của chính font asset làm gốc, mà Roboto-Bold SDF để 90 còn font mặc định của project 86
            // — lệch ~4%, nhìn không thấy khác.
            TMP_FontAsset labelFont = ResolveFont();
            if (labelFont != null) label.font = labelFont;

            return label;
        }

        private Button CreateButton(Transform parent, Vector2 position, Vector2 size, out TextMeshProUGUI label)
        {
            var buttonObject = new GameObject("btn_back_to_menu",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(1f, 0.83f, 0.35f, 0.92f);

            label = CreateLabel(buttonObject.transform, "Label", Vector2.zero, size, 52f, FontStyles.Normal,
                                new Color(0.09f, 0.08f, 0.07f, 1f));

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.interactable = false;
            button.onClick.AddListener(BackToMenu);
            return button;
        }

        // ------------------------------------------------------------------ chạy

        private IEnumerator ShowEndingRoutine(float fadeDuration)
        {
            if (black != null) yield return FadeImage(black, black.color.a, 1f, fadeDuration);
            if (panel != null) yield return FadeGroup(panel, 0f, 1f, 0.7f);

            // Để người chơi kịp đọc chữ trước khi nút ăn được, kẻo cú bấm cuối trận (chạy/nhảy) rơi trúng nút.
            yield return Wait(0.4f);

            if (backButton != null) backButton.interactable = true;
        }

        /// <summary>Rời bảng phá đảo về bảng chính.</summary>
        private void BackToMenu()
        {
            if (backButton != null) backButton.interactable = false;

            // Phá đảo là hết ván: xoá bản lưu để bảng chính không còn nút chơi tiếp
            // (giống lúc bị quái tóm — xem GameplaySaveManager.NotifyRunLost).
            if (clearSave) GameSave.Delete();

            SceneFlow.Load(nextScene);
        }

        // ------------------------------------------------------------------ tiện ích

        private static IEnumerator FadeImage(Image image, float from, float to, float duration)
        {
            float time = Mathf.Max(0.01f, duration);

            SetImageAlpha(image, from);

            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                SetImageAlpha(image, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / time)));
                yield return null;
            }

            SetImageAlpha(image, to);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            float time = Mathf.Max(0.01f, duration);

            group.alpha = from;

            float elapsed = 0f;
            while (elapsed < time)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / time));
                yield return null;
            }

            group.alpha = to;
        }

        private static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
