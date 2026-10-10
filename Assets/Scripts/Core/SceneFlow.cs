using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilentSprint.Core
{
    /// <summary>
    /// Luồng scene của game: MainMenu -> CutScene1 -> CutScene2 -> Gameplay.
    ///
    /// Mọi chỗ cần đổi màn (nút Play Game ở bảng chính, Timeline chạy hết ở cảnh phim, nút QUIT
    /// trong bảng tạm dừng, nút chơi lại khi bị tóm) đều đi qua đây, để việc chuyển cảnh luôn mờ
    /// dần giống nhau và không bị gọi chồng lên nhau.
    /// </summary>
    public static class SceneFlow
    {
        /// <summary>Mờ tối trước khi rời scene hiện tại (giây).</summary>
        public const float DefaultFadeOut = 0.35f;

        /// <summary>Mờ sáng sau khi đã vào scene mới (giây).</summary>
        public const float DefaultFadeIn = 0.4f;

        private static bool loading;

        /// <summary>Đang chuyển scene: đã mờ tối, chưa hiện xong scene mới.</summary>
        public static bool IsLoading => loading;

        /// <summary>Scene vừa rời đi (scene đang mở lúc gọi <see cref="Load(string)"/>).</summary>
        public static string PreviousScene { get; private set; }

        /// <summary>Vào scene hiện tại bằng đường cảnh phim (từ CutScene1 hoặc CutScene2)?</summary>
        public static bool CameFromCutscene =>
            PreviousScene == SceneNames.CutScene1 || PreviousScene == SceneNames.CutScene2;

        /// <summary>Chuyển sang scene khác, kèm màn đen mờ dần.</summary>
        public static void Load(string sceneName)
        {
            Load(sceneName, DefaultFadeOut, DefaultFadeIn);
        }

        /// <summary>Chuyển sang scene khác, tự chọn thời gian mờ tối / mờ sáng.</summary>
        public static void Load(string sceneName, float fadeOutDuration, float fadeInDuration)
        {
            if (loading)
            {
                Debug.Log($"[SceneFlow] Đang chuyển scene, bỏ qua yêu cầu load '{sceneName}'.");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneFlow] Tên scene rỗng nên không load được.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"[SceneFlow] Scene '{sceneName}' chưa nằm trong Build Settings nên không load được. " +
                    "Mở File > Build Profiles (hoặc File > Build Settings) và thêm scene vào danh sách Scene List.");
                return;
            }

            // Nhớ đang đi từ scene nào: có hệ thống cần biết (ví dụ màn chơi vào từ cảnh phim thì
            // cho người chơi "tỉnh dậy" trước khi vào lời dẫn).
            PreviousScene = SceneManager.GetActiveScene().name;

            loading = true;

            // Bảng tạm dừng để timeScale = 0; không trả lại thì scene mới vừa vào đã đứng hình.
            Time.timeScale = 1f;

            // Nhạc và tiếng của màn đang mở tắt dần trong lúc màn còn tối. Object AudioManager nằm trong
            // từng scene nên nó sắp bị huỷ theo scene này — phải tắt trước khi nó biến mất mới không ai
            // nghe thấy chỗ cắt.
            AudioManager.FadeOutForSceneChange(Mathf.Max(0f, fadeOutDuration));

            ScreenFader.Begin(sceneName, Mathf.Max(0f, fadeOutDuration), Mathf.Max(0f, fadeInDuration));
        }

        /// <summary>Chơi lại chính scene đang mở.</summary>
        public static void Reload()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        /// <summary>Thoát game. Trong Editor thì tắt chế độ chơi (Application.Quit không ăn).</summary>
        public static void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("[SceneFlow] Thoát game: tắt chế độ chơi trong Editor.");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>ScreenFader gọi khi đã hiện xong scene mới.</summary>
        internal static void FinishLoading()
        {
            loading = false;
        }
    }
}
