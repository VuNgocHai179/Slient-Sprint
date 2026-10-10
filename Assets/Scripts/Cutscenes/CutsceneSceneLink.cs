using System.Collections;
using SilentSprint.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace SilentSprint.Cutscenes
{
    /// <summary>
    /// Nối một cảnh phim vào cảnh kế tiếp của luồng MainMenu -> CutScene1 -> CutScene2 -> Gameplay.
    ///
    /// Gắn lên chính object đang giữ Timeline (PlayableDirector):
    ///  - Timeline chạy hết thì tự chuyển sang <see cref="nextScene"/>.
    ///  - Hiện nút "Skip" ở góc trên bên phải để người chơi bỏ qua cảnh phim. Game làm cho mobile
    ///    nên nút là đường duy nhất để thoát cảnh phim — không dùng phím tắt.
    /// </summary>
    [RequireComponent(typeof(PlayableDirector))]
    public class CutsceneSceneLink : MonoBehaviour
    {
        // Timeline hết ở đâu thì coi là hết: chừa một khoảng nhỏ vì thời gian của director
        // là số thực và có thể hụt vài phần nghìn giây so với duration.
        private const double EndEpsilon = 0.05;

        [Header("Chuyển scene")]
        [Tooltip("Timeline của cảnh phim. Để trống thì tự lấy PlayableDirector trên chính object này.")]
        public PlayableDirector director;

        [Tooltip("Scene load khi Timeline chạy hết (CutScene2, Gameplay...).")]
        public string nextScene;

        [Tooltip("Tự chuyển scene khi Timeline chạy hết.")]
        public bool autoAdvanceOnEnd = true;

        [Tooltip("Chờ thêm bao lâu sau khi Timeline hết mới chuyển, để khung cuối kịp đọng lại (giây).")]
        public float delayAfterEnd = 0.25f;

        [Header("Nút bỏ qua")]
        [Tooltip("Hiện nút Skip ở góc trên bên phải.")]
        public bool showSkipButton = true;

        [Tooltip("Nút Skip dựng sẵn trong scene/prefab. Để trống thì tự tạo một nút bằng code.")]
        public Button skipButton;

        [Tooltip("Chữ trên nút Skip (nút tự tạo mới dùng).")]
        public string skipLabel = "Skip";

        public float skipButtonWidth = 220f;
        public float skipButtonHeight = 96f;
        public float skipFontSize = 40f;

        [Tooltip("Cách mép trên/phải màn hình bao xa (đơn vị canvas).")]
        public float skipMargin = 24f;

        private bool advancing;
        private Canvas skipCanvas;
        private RectTransform skipRect;

        private void Awake()
        {
            if (director == null) director = GetComponent<PlayableDirector>();

            if (director == null)
            {
                Debug.LogError(
                    "[CutsceneSceneLink] Không tìm thấy PlayableDirector nên cảnh phim sẽ không tự chuyển scene. " +
                    "Gán ô Director hoặc gắn script này lên object có Timeline.", this);
            }
            else
            {
                director.stopped += HandleDirectorStopped;
            }

            SetUpSkipButton();
        }

        private void OnDestroy()
        {
            if (director != null) director.stopped -= HandleDirectorStopped;
        }

        private void Update()
        {
            UpdateSkipButtonPlacement();

            if (advancing || !autoAdvanceOnEnd || director == null) return;
            if (HasTimelineEnded()) Advance(false);
        }

        /// <summary>Bỏ qua cảnh phim: chuyển scene ngay, không đợi Timeline hết.</summary>
        public void Skip()
        {
            Advance(true);
        }

        // ------------------------------------------------------------------ Theo dõi Timeline

        private void HandleDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (!autoAdvanceOnEnd) return;
            Advance(false);
        }

        private bool HasTimelineEnded()
        {
            double duration = director.duration;
            if (duration <= 0.0) return false;

            // Wrap Mode = None: hết Timeline là director dừng, thời gian đứng lại ở khung cuối.
            if (director.time >= duration - EndEpsilon) return true;

            // Wrap Mode = Hold: hết Timeline thì graph chỉ tạm dừng, thời gian vẫn ở khung cuối.
            return !IsDirectorPlaying() && director.time > EndEpsilon;
        }

        /// <summary>Graph của director còn chạy không (graph chưa dựng hoặc đã dừng đều là false).</summary>
        private bool IsDirectorPlaying()
        {
            PlayableGraph graph = director.playableGraph;
            return graph.IsValid() && graph.IsPlaying();
        }

        private void Advance(bool immediate)
        {
            if (advancing) return;

            advancing = true;
            StartCoroutine(AdvanceRoutine(immediate));
        }

        private IEnumerator AdvanceRoutine(bool immediate)
        {
            if (!immediate && delayAfterEnd > 0f) yield return new WaitForSecondsRealtime(delayAfterEnd);

            SceneFlow.Load(nextScene);

            // SceneFlow từ chối (scene chưa có trong Build Settings...) thì mở lại đường
            // để lần sau còn thử tiếp, kẻo cảnh phim kẹt ở khung cuối mãi.
            if (!SceneFlow.IsLoading) advancing = false;
        }

        // ------------------------------------------------------------------ Nút Skip

        private void SetUpSkipButton()
        {
            if (!showSkipButton)
            {
                if (skipButton != null) skipButton.gameObject.SetActive(false);
                return;
            }

            if (skipButton == null)
            {
                skipButton = CreateSkipButton();
            }
            else
            {
                skipRect = skipButton.transform as RectTransform;
                skipCanvas = skipButton.GetComponentInParent<Canvas>();
            }

            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }

        /// <summary>
        /// Dựng nút Skip bằng code: cảnh phim không có sẵn nút nào, mà tự tạo thì khỏi phải
        /// sửa tay từng scene. Muốn nút do mình thiết kế thì gán vào ô Skip Button, script sẽ dùng nút đó.
        /// </summary>
        private Button CreateSkipButton()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                Debug.LogWarning(
                    "[CutsceneSceneLink] Scene không có EventSystem nên nút Skip sẽ không bấm được.", this);
            }

            var canvasObject = new GameObject("SkipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            skipCanvas = canvasObject.GetComponent<Canvas>();
            skipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            skipCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var buttonObject = new GameObject("btn_skip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);

            skipRect = (RectTransform)buttonObject.transform;
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.sizeDelta = new Vector2(skipButtonWidth, skipButtonHeight);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = skipLabel;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = skipFontSize;
            label.color = Color.white;
            label.raycastTarget = false;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;

            return button;
        }

        /// <summary>
        /// Mobile có tai thỏ / thanh điều hướng, nên mỗi frame đặt lại nút theo Safe Area
        /// (màn hình xoay hay đổi tỉ lệ cũng tự chỉnh).
        /// </summary>
        private void UpdateSkipButtonPlacement()
        {
            if (skipRect == null || skipCanvas == null) return;

            float scale = skipCanvas.scaleFactor > 0f ? skipCanvas.scaleFactor : 1f;
            Rect safeArea = Screen.safeArea;

            float insetX = (Screen.width - safeArea.xMax) / scale;
            float insetY = (Screen.height - safeArea.yMax) / scale;

            var position = new Vector2(-(skipMargin + insetX), -(skipMargin + insetY));
            if (skipRect.anchoredPosition != position) skipRect.anchoredPosition = position;
        }
    }
}
