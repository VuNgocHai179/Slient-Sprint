using System.IO;
using SilentSprint.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilentSprint.EditorTools
{
    /// <summary>
    /// Cắm bộ quản lý âm thanh (<see cref="AudioManager"/>) vào scene.
    ///
    /// Cách dùng: menu <b>Tools ▸ Silent Sprint ▸ Set Up Audio Manager</b> cho scene đang mở, hoặc
    /// <b>Tools ▸ Silent Sprint ▸ Set Up Audio Manager In MainMenu + Gameplay</b> để làm luôn cả hai màn
    /// (tool tự mở và lưu từng scene). Bấm nhiều lần cũng được: lần sau dùng lại đúng object cũ chứ không
    /// nhân đôi.
    ///
    /// Vì sao làm tool thay vì sửa tay file .unity: phải gán tham chiếu tới các clip trong Assets/Audio.
    /// Gán qua AssetDatabase thì không phải đoán fileID trong YAML, và cũng không đụng vào file scene khi
    /// Editor đang mở — cùng lý do với <c>KamehamehaEndingSetup</c>.
    ///
    /// Tool GHI ĐÈ các ô clip theo bảng dưới đây (không giữ giá trị đã sửa tay), để bấm lại là về đúng bộ
    /// chuẩn. Các ô âm lượng / thời gian mờ thì không đụng tới.
    ///
    ///   MainMenu : nhạc "music mainmenu.mp3", không có tiếng mưa
    ///   Gameplay : nhạc "BG_Music.wav", tiếng mưa "Rain.mp3" bật sẵn
    ///   cả hai   : tiếng chạy "run.mp3", jumpscare "jumpscare.mp3", kamehameha "kamehameha.mp3"
    ///
    /// Ngoài bốn nguồn phát, tool còn dựng object con "Listener" giữ AudioListener dự phòng và ĐỂ TẮT SẴN:
    /// listener chính của màn chơi nằm trên camera người chơi, mà mini game tắt cả người chơi nên phải có
    /// cái dự phòng (xem <see cref="AudioManager.SetFallbackListener"/>).
    ///
    /// Scene khác hai màn này (cảnh phim) thì tool vẫn gắn object nhưng KHÔNG tự chọn nhạc — để trống cho
    /// tuỳ nghi, và không ghi đè ô nào đã có clip.
    /// </summary>
    public static class AudioManagerSetup
    {
        const string MenuRoot = "Tools/Silent Sprint/";
        const string ObjectName = "[AudioManager]";
        const string AudioFolder = "Assets/Audio/";

        // Tên file clip trong Assets/Audio.
        const string MainMenuMusicFile = "music mainmenu.mp3";
        const string GameplayMusicFile = "BG_Music.wav";
        const string RainFile = "Rain.mp3";
        const string RunFile = "run.mp3";
        const string JumpscareFile = "jumpscare.mp3";
        const string KamehamehaFile = "kamehameha.mp3";

        const string MainMenuScenePath = "Assets/Scenes/" + SceneNames.MainMenu + ".unity";
        const string GameplayScenePath = "Assets/Scenes/" + SceneNames.Gameplay + ".unity";

        // Tên object con dựng nguồn phát — phải khớp hằng số trong AudioManager.
        const string MusicSourceName = "Music";
        const string AmbienceSourceName = "Ambience";
        const string LoopSourceName = "LoopSfx";
        const string SfxSourceName = "Sfx";
        const string FallbackListenerName = "Listener";

        // ------------------------------------------------------------------ menu

        [MenuItem(MenuRoot + "Set Up Audio Manager", false, 220)]
        public static void SetUpFromMenu()
        {
            SetUp(SceneManager.GetActiveScene());
        }

        [MenuItem(MenuRoot + "Set Up Audio Manager", true)]
        static bool ValidateSetUpFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && SceneManager.GetActiveScene().IsValid()
                   && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
        }

        [MenuItem(MenuRoot + "Set Up Audio Manager In MainMenu + Gameplay", false, 221)]
        public static void SetUpBothScenesFromMenu()
        {
            // Tool phải mở scene khác, nên hỏi lưu trước; người dùng bấm huỷ thì thôi.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string returnPath = SceneManager.GetActiveScene().path;

            SetUpSceneAtPath(MainMenuScenePath);
            SetUpSceneAtPath(GameplayScenePath);

            // Trả lại scene người dùng đang mở, cho khỏi mất chỗ làm việc.
            if (!string.IsNullOrEmpty(returnPath) && File.Exists(returnPath) &&
                SceneManager.GetActiveScene().path != returnPath)
            {
                EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
            }
        }

        [MenuItem(MenuRoot + "Set Up Audio Manager In MainMenu + Gameplay", true)]
        static bool ValidateSetUpBothScenesFromMenu()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // ------------------------------------------------------------------ cài đặt

        /// <summary>
        /// Tạo (hoặc dùng lại) object âm thanh trong scene đang mở rồi gán nguồn phát và các clip cần thiết.
        /// </summary>
        /// <returns>Component quản lý âm thanh trong scene, hoặc null nếu scene không dùng được.</returns>
        public static AudioManager SetUp(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning("[AudioManager] Scene này chưa lưu thành file nên tool không chạy được. " +
                                 "Lưu scene (Ctrl+S) rồi chạy lại.");
                return null;
            }

            AudioManager manager = FindExisting(scene);
            bool created = false;

            if (manager == null)
            {
                var gameObject = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(gameObject, "Create AudioManager");

                manager = Undo.AddComponent<AudioManager>(gameObject);
                created = true;
            }

            manager.musicSource = EnsureSource(manager, MusicSourceName, loop: true);
            manager.ambienceSource = EnsureSource(manager, AmbienceSourceName, loop: true);
            manager.loopSource = EnsureSource(manager, LoopSourceName, loop: true);
            manager.sfxSource = EnsureSource(manager, SfxSourceName, loop: false);
            EnsureFallbackListener(manager);

            bool isMainMenu = scene.name == SceneNames.MainMenu;
            bool isGameplay = scene.name == SceneNames.Gameplay;

            string musicNote = "để nguyên (scene này không nằm trong bảng clip tự gán)";
            string rainNote = "để nguyên";

            if (isMainMenu || isGameplay)
            {
                AudioClip music = LoadClip(isMainMenu ? MainMenuMusicFile : GameplayMusicFile);
                manager.sceneMusic = music;
                musicNote = music != null ? (isMainMenu ? MainMenuMusicFile : GameplayMusicFile) : "KHÔNG THẤY FILE";

                AudioClip rain = isGameplay ? LoadClip(RainFile) : null;
                manager.rainClip = rain;
                manager.rainOnStart = isGameplay && rain != null;
                rainNote = isGameplay
                    ? (rain != null ? RainFile + " (bật sẵn)" : "KHÔNG THẤY FILE")
                    : "không có (bảng chính chỉ vẽ mưa bằng UI, không có tiếng)";

                // Tiếng chạy và hai tiếng đoạn kết là của màn chơi, nhưng gán luôn ở cả hai scene cho khỏi
                // phải nhớ: bảng chính không có PlayerController nên các ô đó nằm im.
                manager.runClip = LoadClip(RunFile);
                manager.jumpscareClip = LoadClip(JumpscareFile);
                manager.kamehamehaClip = LoadClip(KamehamehaFile);
            }

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = manager.gameObject;

            Debug.Log("[AudioManager] " + (created ? "Đã tạo object" : "Đã cập nhật object") + " \"" + ObjectName +
                      "\" trong scene " + scene.name + ". Nhớ Ctrl+S để lưu scene.\n" +
                      "  • Nhạc nền: " + musicNote + "\n" +
                      "  • Tiếng mưa: " + rainNote + "\n" +
                      "  • Tiếng chạy / jumpscare / kamehameha: " + RunFile + " / " + JumpscareFile + " / " + KamehamehaFile + "\n" +
                      "  • Bốn nguồn phát nằm ngay dưới object này (" + MusicSourceName + ", " + AmbienceSourceName +
                      ", " + LoopSourceName + ", " + SfxSourceName +
                      ") nên muốn đổi âm lượng riêng hay gán vào Audio Mixer thì sửa tại đó.\n" +
                      "  • Object con \"" + FallbackListenerName + "\" giữ AudioListener dự phòng, ĐỂ TẮT SẴN: " +
                      "mini game tắt người chơi (và camera của họ) nên lúc đó mới bật cái này để scene không câm tiếng.");

            return manager;
        }

        static void SetUpSceneAtPath(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError("[AudioManager] Không thấy scene " + scenePath + " nên bỏ qua.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            AudioManager manager = SetUp(scene);

            if (manager != null) EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Object âm thanh đã có trong scene chưa (bấm tool lần hai thì dùng lại, không nhân đôi).</summary>
        static AudioManager FindExisting(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AudioManager manager = root.GetComponent<AudioManager>();
                if (manager != null) return manager;
            }

            return Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
        }

        /// <summary>Dựng (hoặc dùng lại) một object con giữ nguồn phát, để chỉnh được ngay trong Inspector.</summary>
        static AudioSource EnsureSource(AudioManager manager, string childName, bool loop)
        {
            Transform existing = manager.transform.Find(childName);

            GameObject child;
            if (existing != null)
            {
                child = existing.gameObject;
            }
            else
            {
                child = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(child, "Create AudioManager source");
                child.transform.SetParent(manager.transform, false);
            }

            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = Undo.AddComponent<AudioSource>(child);

            source.playOnAwake = false;   // tiếng do AudioManager bật, không tự kêu lúc scene vừa hiện
            source.spatialBlend = 0f;     // 2D: tiếng của chính người chơi
            source.loop = loop;
            source.volume = 1f;

            return source;
        }

        /// <summary>
        /// Dựng (hoặc dùng lại) object con giữ AudioListener dự phòng, để TẮT sẵn.
        ///
        /// Listener chính của màn chơi nằm trên camera người chơi; mini game tắt cả GameObject người chơi nên
        /// cần listener này giữ cho scene khỏi câm tiếng — xem <see cref="AudioManager.SetFallbackListener"/>.
        /// Tool để nó tắt sẵn cho giống hệt lúc chạy, và để nhìn thấy trong Hierarchy.
        /// </summary>
        static void EnsureFallbackListener(AudioManager manager)
        {
            Transform existing = manager.transform.Find(FallbackListenerName);

            GameObject child;
            if (existing != null)
            {
                child = existing.gameObject;
            }
            else
            {
                child = new GameObject(FallbackListenerName);
                Undo.RegisterCreatedObjectUndo(child, "Create AudioManager listener");
                child.transform.SetParent(manager.transform, false);
            }

            if (child.GetComponent<AudioListener>() == null) Undo.AddComponent<AudioListener>(child);

            child.SetActive(false);   // camera người chơi mới là listener chính; cái này chỉ bật lúc mini game
        }

        static AudioClip LoadClip(string fileName)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + fileName);

            if (clip == null)
                Debug.LogWarning("[AudioManager] Không thấy " + AudioFolder + fileName +
                                 " nên ô này sẽ để trống.");

            return clip;
        }
    }
}
