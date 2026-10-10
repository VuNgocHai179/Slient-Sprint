using System;
using UnityEngine;

namespace SilentSprint.Core
{
    /// <summary>
    /// Bộ quản lý âm thanh của game, gom làm ba nhóm:
    ///  - <b>Nhạc nền</b> theo màn: bảng chính nghe "music mainmenu", màn chơi nghe "BG_Music".
    ///  - <b>Tiếng nền</b>: tiếng mưa rơi suốt màn chơi (bám theo hiệu ứng "Rain Particles" có sẵn trong scene).
    ///  - <b>Tiếng hiệu ứng</b>: tiếng chạy, tiếng jumpscare, tiếng kamehameha.
    ///
    /// Object này nằm TRONG TỪNG SCENE, không phải object sống xuyên scene: mỗi màn tự chọn nhạc và tiếng nền
    /// của nó nên không cần bảng dò "scene này phát gì" ở đâu cả, và mọi ô gán clip đều nhìn thấy trong
    /// Inspector. Khi đổi màn, object của màn cũ bị huỷ theo scene; <see cref="SceneFlow"/> gọi
    /// <see cref="FadeOutForSceneChange"/> ngay trước khi mờ màn nên nhạc tắt dần trong lúc màn còn tối.
    ///
    /// Scene không có object này (hai cảnh phim) thì mọi lời gọi đều lặng lẽ bỏ qua, không lỗi.
    ///
    /// Gắn vào scene: mở scene rồi menu <b>Tools ▸ Silent Sprint ▸ Set Up Audio Manager</b>
    /// (xem <c>AudioManagerSetup</c>) — tool tạo object "[AudioManager]" và gán sẵn các clip trong Assets/Audio.
    ///
    /// Ba nhóm âm lượng đều chạy theo kiểu "đặt đích rồi trượt dần" (xem <see cref="Update"/>): mỗi khung hình
    /// nguồn phát tự bò về mức đích, nên bật/tắt giữa chừng không bao giờ để lại tiếng kẹt ở nửa âm lượng. Trượt
    /// theo thời gian thật (unscaledDeltaTime) để bảng tạm dừng (timeScale = 0) không làm tiếng đứng hình — cùng
    /// lý do <see cref="ScreenFader"/> và <see cref="KamehamehaEnding"/> đều đếm thời gian thật.
    ///
    /// <b>AudioListener:</b> scene màn chơi chỉ có đúng một listener, và nó nằm trên camera của người chơi
    /// (trong <c>Player.prefab</c>). Mini game tắt cả GameObject người chơi để dựng phòng riêng, nên listener
    /// đó cũng tắt theo và cả scene câm tiếng — xem <see cref="SetFallbackListener"/>. Listener dự phòng là
    /// object con "Listener" của chính object này, MẶC ĐỊNH TẮT, chỉ bật trong lúc camera người chơi ngủ.
    /// </summary>
    public sealed class AudioManager : Singleton<AudioManager>
    {
        // Tên các object con tự dựng, để nhìn Hierarchy là biết ngay nguồn nào phát tiếng gì.
        private const string MusicSourceName = "Music";
        private const string AmbienceSourceName = "Ambience";
        private const string LoopSourceName = "LoopSfx";
        private const string SfxSourceName = "Sfx";
        private const string FallbackListenerName = "Listener";

        // ------------------------------------------------------------------ nhạc nền

        [Header("Nhạc nền của màn này")]
        [Tooltip("Nhạc phát ngay khi vào scene. Bảng chính: Assets/Audio/music mainmenu.mp3. " +
                 "Màn chơi: Assets/Audio/BG_Music.wav. Để trống thì màn này không có nhạc nền.")]
        public AudioClip sceneMusic;

        [Tooltip("Âm lượng nhạc nền (0-1).")]
        [Range(0f, 1f)] public float musicVolume = 0.55f;

        [Tooltip("Nhạc vào dần trong bao lâu (giây) — thời gian để âm lượng đi hết dải 0→1, nên đặt nhỏ hơn 1 " +
                 "thì vào nhanh hơn. 0 = vào ngay.")]
        [Min(0f)] public float musicFade = 1.2f;

        [Tooltip("Nhạc lặp lại khi hết bài.")]
        public bool musicLoop = true;

        // ------------------------------------------------------------------ tiếng nền

        [Header("Tiếng nền (mưa)")]
        [Tooltip("Tiếng mưa, phát vòng lặp suốt màn. Màn chơi dùng Assets/Audio/Rain.mp3; bảng chính để trống " +
                 "vì ở đó mưa chỉ được VẼ bằng UI (MainMenuStorm), không có tiếng.")]
        public AudioClip rainClip;

        [Tooltip("Phát tiếng mưa ngay khi vào scene. Bật ở màn chơi (scene có hiệu ứng Rain Particles), " +
                 "tắt ở bảng chính. Tắt ô này thì vẫn giữ được clip trong ô trên để bật lại khi cần.")]
        public bool rainOnStart;

        [Tooltip("Âm lượng tiếng nền (0-1).")]
        [Range(0f, 1f)] public float ambienceVolume = 0.4f;

        [Tooltip("Tiếng nền vào dần / tắt dần trong bao lâu (giây, cho dải 0→1). 0 = vào ngay.")]
        [Min(0f)] public float ambienceFade = 2f;

        // ------------------------------------------------------------------ tiếng chạy

        [Header("Tiếng chạy (chạy vòng lặp)")]
        [Tooltip("Tiếng bước chân khi chạy (Assets/Audio/run.mp3). Chỉ kêu lúc THẬT SỰ đang chạy và đang di " +
                 "chuyển — giữ Shift mà đứng yên thì không có tiếng.")]
        public AudioClip runClip;

        [Tooltip("Âm lượng tiếng chạy (0-1).")]
        [Range(0f, 1f)] public float runVolume = 0.5f;

        [Tooltip("Tiếng chạy vào dần / tắt dần trong bao lâu (giây, cho dải 0→1). Để ngắn cho khớp nhịp chân.")]
        [Min(0f)] public float runFade = 0.25f;

        // ------------------------------------------------------------------ tiếng một lần

        [Header("Tiếng hiệu ứng (một lần)")]
        [Tooltip("Tiếng hét khi bị quái tóm (Assets/Audio/jumpscare.mp3).")]
        public AudioClip jumpscareClip;

        [Tooltip("Tiếng bắn Kamehameha ở đoạn kết (Assets/Audio/kamehameha.mp3).")]
        public AudioClip kamehamehaClip;

        [Tooltip("Âm lượng chung của tiếng một lần (0-1).")]
        [Range(0f, 1f)] public float sfxVolume = 0.85f;

        // ------------------------------------------------------------------ tắt / bật tiếng

        // Khoá PlayerPrefs. Trạng thái tắt tiếng là thứ DUY NHẤT của bộ quản lý này còn sống ngoài scene:
        // nó phải nhớ được lúc bảng cài đặt ở bảng chính bấm xong rồi mới vào màn chơi, mà object
        // AudioManager thì nằm trong từng scene và bị huỷ theo scene.
        private const string MusicMutedKey = "silentsprint.audio.music.muted";
        private const string SoundMutedKey = "silentsprint.audio.sound.muted";

        private static bool muteLoaded;
        private static bool musicMuted;
        private static bool soundMuted;

        /// <summary>
        /// Vừa đổi trạng thái tắt/bật tiếng. Bảng cài đặt nghe sự kiện này để vẽ lại icon, nhờ vậy dù
        /// trạng thái đổi ở đâu thì mọi chỗ hiển thị vẫn khớp nhau.
        /// </summary>
        public static event Action MuteChanged;

        /// <summary>Nhạc nền đang bị tắt tiếng.</summary>
        public static bool MusicMuted
        {
            get { EnsureMuteLoaded(); return musicMuted; }
        }

        /// <summary>Tiếng hiệu ứng và tiếng nền (mưa) đang bị tắt tiếng.</summary>
        public static bool SoundMuted
        {
            get { EnsureMuteLoaded(); return soundMuted; }
        }

        /// <summary>
        /// Tắt/bật nhạc nền. Nhớ qua các lần mở game.
        ///
        /// Gọi được cả khi scene KHÔNG có object AudioManager (bảng chính chưa gắn chẳng hạn): trạng thái
        /// nằm ở biến tĩnh + PlayerPrefs, object của scene sau đọc lại lúc chạy.
        /// </summary>
        public static void SetMusicMuted(bool muted)
        {
            EnsureMuteLoaded();
            if (musicMuted == muted) return;

            musicMuted = muted;
            PlayerPrefs.SetInt(MusicMutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            MuteChanged?.Invoke();
        }

        /// <summary>
        /// Tắt/bật tiếng hiệu ứng — gồm cả tiếng mưa, vì mưa là tiếng nền chứ không phải nhạc.
        /// Nhớ qua các lần mở game.
        /// </summary>
        public static void SetSoundMuted(bool muted)
        {
            EnsureMuteLoaded();
            if (soundMuted == muted) return;

            soundMuted = muted;
            PlayerPrefs.SetInt(SoundMutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();

            MuteChanged?.Invoke();
        }

        /// <summary>Đọc trạng thái đã nhớ, một lần duy nhất ở chỗ dùng đầu tiên.</summary>
        private static void EnsureMuteLoaded()
        {
            if (muteLoaded) return;

            muteLoaded = true;
            musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
            soundMuted = PlayerPrefs.GetInt(SoundMutedKey, 0) == 1;
        }

        // ------------------------------------------------------------------ nguồn phát

        [Header("Nguồn phát (để trống thì tự dựng khi chạy)")]
        [Tooltip("Nguồn phát nhạc nền (vòng lặp).")]
        public AudioSource musicSource;

        [Tooltip("Nguồn phát tiếng nền (vòng lặp).")]
        public AudioSource ambienceSource;

        [Tooltip("Nguồn phát tiếng chạy (vòng lặp).")]
        public AudioSource loopSource;

        [Tooltip("Nguồn phát tiếng một lần (PlayOneShot).")]
        public AudioSource sfxSource;

        // Mức âm lượng ĐANG có, tự bò về mức đích mỗi khung hình. Đây là thứ duy nhất nhớ trạng thái trượt,
        // nên bật/tắt liên tục cũng không cần quản lý coroutine nào.
        private float musicAmount;
        private float ambienceAmount;
        private float loopAmount;

        private bool musicWanted;
        private bool ambienceWanted;
        private bool loopWanted;

        /// <summary>
        /// Tiếng nền đang bị tạm tắt mà KHÔNG quên nó vốn đang bật — dùng lúc vào mini game.
        /// Khác <see cref="ambienceWanted"/> ở chỗ: tắt xong bật lại là tiếng mưa trở về đúng như trước.
        /// </summary>
        private bool ambienceSuspended;

        /// <summary>Thời gian trượt dùng chung lúc chuyển scene; âm = mỗi nhóm dùng số riêng của nó.</summary>
        private float sceneChangeFade = -1f;

        /// <summary>
        /// Object con giữ AudioListener dự phòng. Tạo sẵn nhưng để TẮT: bình thường listener chính là
        /// camera của người chơi, chỉ khi camera đó ngủ (mini game tắt cả người chơi) mới cần tới nó.
        /// </summary>
        private GameObject fallbackListenerObject;

        private static bool warnedMissing;

        protected override void Awake()
        {
            base.Awake();

            // Singleton<T> huỷ bản trùng ở cuối khung hình, nên phải tự chặn kẻo dựng nguồn phát hai lần.
            if (Instance != this) return;

            EnsureSources();
            EnsureFallbackListener();
        }

        private void Start()
        {
            if (Instance != this) return;

            if (sceneMusic != null) PlayMusicInternal(sceneMusic);
            if (rainOnStart) StartAmbienceInternal(rainClip);
        }

        private void Update()
        {
            if (Instance != this) return;

            // Đang chuyển scene thì cả ba nhóm cùng tắt theo nhịp mờ màn, không thì mỗi nhóm theo số riêng.
            float fading = sceneChangeFade;

            // Nhạc nền KHÔNG có mặt trong danh sách tạm tắt: mini game vẫn phải nghe nhạc.
            Ramp(musicSource, ref musicAmount, musicWanted && !MusicMuted ? musicVolume : 0f,
                 fading >= 0f ? fading : musicFade);

            Ramp(ambienceSource, ref ambienceAmount,
                 ambienceWanted && !ambienceSuspended && !SoundMuted ? ambienceVolume : 0f,
                 fading >= 0f ? fading : ambienceFade);

            Ramp(loopSource, ref loopAmount, loopWanted && !SoundMuted ? runVolume : 0f,
                 fading >= 0f ? fading : runFade);
        }

        /// <summary>
        /// Đưa một nguồn phát về mức âm lượng đích. Tới 0 thì tự tắt nguồn đi (nhưng giữ lại clip để lần sau
        /// bật lại là kêu đúng bài đó).
        /// </summary>
        private static void Ramp(AudioSource source, ref float amount, float target, float fade)
        {
            if (source == null) return;

            // Bỏ tắt tiếng, hoặc nhạc vừa bị dừng vì tụt về 0: nguồn đang đứng im nên phải cho chạy lại
            // trước khi kéo âm lượng lên, không thì bật tiếng lại mà chẳng nghe thấy gì.
            if (target > 0f && !source.isPlaying && source.clip != null) source.Play();

            // Thời gian thật: bảng tạm dừng có thể đang để timeScale = 0.
            amount = fade <= 0f
                ? target
                : Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / fade);

            source.volume = amount;

            if (target <= 0f && amount <= 0f && source.isPlaying) source.Stop();
        }

        // ------------------------------------------------------------------ nhạc nền

        /// <summary>Phát nhạc nền. Clip trùng với bài đang phát thì chỉ bảo đảm nó đang chạy, không phát lại từ đầu.</summary>
        public static void PlayMusic(AudioClip clip)
        {
            AudioManager manager = Require();
            if (manager != null) manager.PlayMusicInternal(clip);
        }

        /// <summary>Tắt nhạc nền (tắt dần theo <see cref="musicFade"/>).</summary>
        public static void StopMusic()
        {
            AudioManager manager = Require();
            if (manager != null) manager.musicWanted = false;
        }

        private void PlayMusicInternal(AudioClip clip)
        {
            EnsureSources();

            if (clip == null)
            {
                musicWanted = false;
                return;
            }

            if (musicSource.clip != clip)
            {
                musicSource.clip = clip;
                musicSource.loop = musicLoop;
                musicSource.volume = 0f;
                musicAmount = 0f; // bài mới thì vào từ đầu, không kế thừa âm lượng của bài cũ
                musicSource.Play();
            }
            else if (!musicSource.isPlaying)
            {
                // Cùng bài nhưng đang bị tắt dần: bật lại và trượt lên từ mức đang có.
                musicSource.Play();
            }

            musicWanted = true;
        }

        // ------------------------------------------------------------------ tiếng nền

        /// <summary>Phát một tiếng nền vòng lặp (mưa).</summary>
        public static void StartAmbience(AudioClip clip)
        {
            AudioManager manager = Require();
            if (manager != null) manager.StartAmbienceInternal(clip);
        }

        /// <summary>Tắt tiếng nền (tắt dần theo <see cref="ambienceFade"/>).</summary>
        public static void StopAmbience()
        {
            AudioManager manager = Require();
            if (manager != null) manager.ambienceWanted = false;
        }

        /// <summary>Bật tiếng mưa, dùng clip đã gán trong <see cref="rainClip"/>.</summary>
        public static void StartRain()
        {
            AudioManager manager = Require();
            if (manager != null) manager.StartAmbienceInternal(manager.rainClip);
        }

        /// <summary>Tắt tiếng mưa.</summary>
        public static void StopRain()
        {
            StopAmbience();
        }

        private void StartAmbienceInternal(AudioClip clip)
        {
            EnsureSources();

            if (clip == null)
            {
                ambienceWanted = false;
                return;
            }

            if (ambienceSource.clip != clip)
            {
                ambienceSource.clip = clip;
                ambienceSource.loop = true;
                ambienceSource.volume = 0f;
                ambienceAmount = 0f;
                ambienceSource.Play();
            }
            else if (!ambienceSource.isPlaying)
            {
                ambienceSource.Play();
            }

            ambienceWanted = true;
        }

        // ------------------------------------------------------------------ tiếng chạy

        /// <summary>
        /// Bật tiếng chạy. Gọi mỗi khung hình cũng được: đang đúng tiếng đó rồi thì không làm gì.
        /// </summary>
        public static void StartRun()
        {
            AudioManager manager = Require();
            if (manager != null) manager.StartLoopInternal(manager.runClip);
        }

        /// <summary>Tắt tiếng chạy (tắt dần theo <see cref="runFade"/>).</summary>
        public static void StopRun()
        {
            AudioManager manager = Require();
            if (manager != null) manager.loopWanted = false;
        }

        private void StartLoopInternal(AudioClip clip)
        {
            EnsureSources();

            if (clip == null)
            {
                loopWanted = false;
                return;
            }

            if (loopSource.clip != clip)
            {
                loopSource.clip = clip;
                loopSource.loop = true;
                loopSource.volume = 0f;
                loopAmount = 0f;
                loopSource.Play();
            }
            else if (!loopSource.isPlaying)
            {
                // Đang tắt dần rồi bật lại thì phát tiếp và trượt ngược lên từ mức đang có, không cắt phựt.
                loopSource.Play();
            }

            loopWanted = true;
        }

        // ------------------------------------------------------------------ tiếng một lần

        /// <summary>Tiếng một lần, không lặp. <paramref name="volumeScale"/> nhân thêm vào <see cref="sfxVolume"/>.</summary>
        public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            AudioManager manager = Require();
            if (manager != null) manager.PlaySfxInternal(clip, volumeScale);
        }

        /// <summary>Tiếng hét lúc bị quái tóm.</summary>
        public static void PlayJumpscare()
        {
            AudioManager manager = Require();
            if (manager != null) manager.PlaySfxInternal(manager.jumpscareClip, 1f);
        }

        /// <summary>Tiếng bắn Kamehameha ở đoạn kết.</summary>
        public static void PlayKamehameha()
        {
            AudioManager manager = Require();
            if (manager != null) manager.PlaySfxInternal(manager.kamehamehaClip, 1f);
        }

        private void PlaySfxInternal(AudioClip clip, float volumeScale)
        {
            // Tắt tiếng hiệu ứng thì thôi, không phát: tiếng một lần không trượt âm lượng được như
            // ba nhóm vòng lặp ở Update.
            if (clip == null || SoundMuted) return;

            EnsureSources();
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * Mathf.Max(0f, volumeScale)));
        }

        // ------------------------------------------------------------------ chuyển scene

        /// <summary>
        /// Tắt dần MỌI tiếng trong <paramref name="seconds"/> giây. <see cref="SceneFlow"/> gọi hàm này ngay
        /// trước khi mờ màn, nên nhạc tắt hết trong lúc màn còn tối và object của màn cũ bị huỷ lúc scene mới
        /// hiện ra — không ai nghe thấy chỗ cắt.
        ///
        /// Scene không có AudioManager thì gọi hàm này cũng vô hại.
        /// </summary>
        public static void FadeOutForSceneChange(float seconds)
        {
            AudioManager manager = Instance;
            if (manager != null) manager.FadeOutForSceneChangeInternal(seconds);
        }

        private void FadeOutForSceneChangeInternal(float seconds)
        {
            sceneChangeFade = Mathf.Max(0f, seconds);

            musicWanted = false;
            ambienceWanted = false;
            loopWanted = false;
        }

        // ------------------------------------------------------------------ nguồn phát

        /// <summary>
        /// Bảo đảm bốn nguồn phát đều có. Ô nào trống thì dùng lại object con cùng tên có sẵn trong scene
        /// (do tool gắn), không có nữa mới dựng mới — nên chạy tool hay không chạy đều ra cùng kết quả.
        /// </summary>
        private void EnsureSources()
        {
            musicSource = ResolveSource(musicSource, MusicSourceName);
            ambienceSource = ResolveSource(ambienceSource, AmbienceSourceName);
            loopSource = ResolveSource(loopSource, LoopSourceName);
            sfxSource = ResolveSource(sfxSource, SfxSourceName);

            // Tiếng một lần nhân âm lượng ở PlayOneShot chứ không trượt, nên nguồn để hết cỡ.
            sfxSource.volume = 1f;
        }

        private AudioSource ResolveSource(AudioSource source, string childName)
        {
            if (source != null) return source;

            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                source = existing.GetComponent<AudioSource>();
                if (source != null) return source;
            }

            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D: đây là tiếng của chính người chơi, không phải tiếng trong không gian
            source.loop = false;

            return source;
        }

        // ------------------------------------------------------------------ listener dự phòng

        /// <summary>
        /// Dựng object con giữ AudioListener dự phòng, để TẮT sẵn.
        ///
        /// Tạo ở trạng thái tắt (tắt object TRƯỚC khi gắn component) là cố ý: nếu dựng nó đang bật thì trong
        /// khung hình đó scene có hai listener và Unity in cảnh báo. Bật/tắt sau này đi qua
        /// <see cref="SetFallbackListener"/>.
        /// </summary>
        private void EnsureFallbackListener()
        {
            if (fallbackListenerObject != null) return;

            Transform existing = transform.Find(FallbackListenerName);
            if (existing != null)
            {
                fallbackListenerObject = existing.gameObject;
            }
            else
            {
                fallbackListenerObject = new GameObject(FallbackListenerName);
                fallbackListenerObject.transform.SetParent(transform, false);
                fallbackListenerObject.SetActive(false);
            }

            if (fallbackListenerObject.GetComponent<AudioListener>() == null)
                fallbackListenerObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// Bật/tắt AudioListener dự phòng.
        ///
        /// Vì sao cần: màn chơi chỉ có đúng một AudioListener và nó nằm trên camera của người chơi. Mini game
        /// tắt cả GameObject người chơi để dựng phòng riêng, kéo theo listener đó — scene mất hết tiếng, nhạc
        /// nền cũng câm. Bật listener dự phòng ngay trong lúc camera người chơi ngủ là cả scene vẫn có đúng
        /// một listener.
        ///
        /// Bên gọi phải giữ đúng thứ tự để không lúc nào có 0 hoặc 2 listener trong cùng một khung hình:
        /// tắt người chơi TRƯỚC rồi mới bật cái này, và bật người chơi TRƯỚC rồi mới tắt cái này.
        ///
        /// Scene không có AudioManager thì gọi hàm này cũng vô hại.
        /// </summary>
        public static void SetFallbackListener(bool active)
        {
            AudioManager manager = Require();
            if (manager == null) return;

            // Dựng nếu chưa có, để lời gọi này không im lặng vô ích khi Awake chưa kịp chạy.
            manager.EnsureFallbackListener();
            manager.fallbackListenerObject.SetActive(active);
        }

        /// <summary>
        /// Tạm tắt tiếng nền (mưa) mà không quên nó đang bật — dùng cho lúc vào mini game, để phòng chơi riêng
        /// không còn nghe tiếng mưa của màn chơi. Nhạc nền KHÔNG bị đụng tới.
        ///
        /// Đặt lại false là tiếng mưa trở về đúng trạng thái trước đó (kể cả trước đó nó đang tắt).
        /// Scene không có AudioManager thì gọi hàm này cũng vô hại.
        /// </summary>
        public static void SetAmbienceSuspended(bool suspended)
        {
            AudioManager manager = Require();
            if (manager != null) manager.ambienceSuspended = suspended;
        }

        /// <summary>Lấy bộ quản lý đang sống; chưa có thì nhắc một lần rồi thôi.</summary>
        private static AudioManager Require()
        {
            AudioManager manager = Instance;
            if (manager != null) return manager;

            if (!warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("[AudioManager] Scene này không có object AudioManager nên tiếng bị bỏ qua. " +
                                 "Mở scene rồi chạy Tools ▸ Silent Sprint ▸ Set Up Audio Manager để gắn vào.");
            }

            return null;
        }
    }
}
