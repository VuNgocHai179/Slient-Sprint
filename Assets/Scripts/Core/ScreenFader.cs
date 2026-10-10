using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SilentSprint.Core
{
    /// <summary>
    /// Tấm màn đen mờ dần dùng chung cho mọi lần chuyển scene. Object này sống qua các scene
    /// (DontDestroyOnLoad) nên chạy được liền một mạch: mờ tối -> load scene -> mờ sáng.
    ///
    /// Cố ý không dùng DOTween: JumpscareController gọi DOTween.KillAll() mỗi lần chơi lại, tween
    /// đang chạy trên tấm màn sẽ bị giết giữa chừng và để lại một màn đen không bao giờ tan.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        // Trên mọi Canvas của game (các Canvas trong scene đều để Sorting Order = 0).
        private const int OverlaySortingOrder = 30000;

        private static ScreenFader instance;

        private Image overlay;

        /// <summary>Bắt đầu chuyển scene. Chỉ <see cref="SceneFlow"/> gọi hàm này.</summary>
        internal static void Begin(string sceneName, float fadeOutDuration, float fadeInDuration)
        {
            if (instance == null)
            {
                var host = new GameObject("[ScreenFader]");
                DontDestroyOnLoad(host);

                instance = host.AddComponent<ScreenFader>();
                instance.CreateOverlay();
            }

            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.Transition(sceneName, fadeOutDuration, fadeInDuration));
        }

        private void CreateOverlay()
        {
            var canvasObject = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var imageObject = new GameObject("Black", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlay = imageObject.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.raycastTarget = false;
        }

        private IEnumerator Transition(string sceneName, float fadeOutDuration, float fadeInDuration)
        {
            // Chặn cú bấm trong lúc đang chuyển: người chơi bấm nút Skip lúc màn đã tối
            // thì không được ăn thêm lần nữa.
            overlay.raycastTarget = true;

            yield return Fade(0f, 1f, fadeOutDuration);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (load == null)
            {
                Debug.LogError($"[ScreenFader] Không load được scene '{sceneName}'.");
                overlay.raycastTarget = false;
                SetAlpha(0f);
                SceneFlow.FinishLoading();
                yield break;
            }

            while (!load.isDone) yield return null;

            overlay.raycastTarget = false;

            yield return Fade(1f, 0f, fadeInDuration);

            SceneFlow.FinishLoading();
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            // Dùng thời gian thật: bảng tạm dừng có thể đang để timeScale = 0, lúc đó
            // Time.deltaTime = 0 nên màn đen sẽ đứng im mãi.
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            SetAlpha(from);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (overlay == null) return;

            Color color = overlay.color;
            color.a = alpha;
            overlay.color = color;
        }
    }
}
