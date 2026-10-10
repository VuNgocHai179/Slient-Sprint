using System;
using System.Collections;
using SilentSprint.Core;
using SilentSprint.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SilentSprint.Gameplay
{
    /// <summary>Một nhịp mí mắt: đưa tấm phủ tới <see cref="alpha"/> trong <see cref="duration"/> giây.</summary>
    [Serializable]
    public struct WakeUpPhase
    {
        [Tooltip("Độ mờ của tấm phủ sau nhịp này. 1 = nhắm hẳn, 0 = mở hẳn.")]
        public float alpha;

        [Tooltip("Chuyển tới độ mờ đó mất bao lâu (giây).")]
        public float duration;
    }

    /// <summary>
    /// Đoạn "vừa tỉnh dậy" khi màn chơi được vào bằng đường cảnh phim (CutScene2 -> Gameplay):
    /// màn hình nhấp nháy như mí mắt mở ra, hết nhấp nháy mới hiện DialogBox lời dẫn mở màn.
    ///
    /// Chỉ chạy khi <see cref="SceneFlow.CameFromCutscene"/> — vào màn bằng Continue hay chơi lại thì
    /// không có đoạn này (người chơi đâu có vừa tỉnh dậy), và khi đó lời dẫn do
    /// <see cref="MinigameSystemManager"/> phát ngay như cũ.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class GameplayWakeUp : MonoBehaviour
    {
        /// <summary>
        /// Hiệu ứng này sẽ phát lời dẫn mở màn, nên <see cref="MinigameSystemManager"/> không phát
        /// ngay lúc Start nữa mà đợi tới khi nhấp nháy xong. Cờ được đặt trong Awake.
        /// </summary>
        public static bool WillPlayIntroDialogue { get; private set; }

        [Header("Nhấp nháy mí mắt")]
        [Tooltip("Bật đoạn tỉnh dậy khi vào màn từ cảnh phim. Tắt thì lời dẫn phát ngay như bình thường.")]
        public bool playWakeUpOnCutsceneEntry = true;

        [Tooltip("Giữ màn đen bao lâu trước khi bắt đầu mở mắt (giây) — để màn mờ sáng của ScreenFader chạy xong.")]
        public float holdBlack = 0.35f;

        [Tooltip("Các nhịp mí mắt, theo thứ tự: mở hé, nhắm lại, mở rộng hơn...")]
        public WakeUpPhase[] phases =
        {
            new WakeUpPhase { alpha = 0.35f, duration = 0.24f },
            new WakeUpPhase { alpha = 0.85f, duration = 0.13f },
            new WakeUpPhase { alpha = 0.18f, duration = 0.28f },
            new WakeUpPhase { alpha = 0.62f, duration = 0.12f },
            new WakeUpPhase { alpha = 0.08f, duration = 0.30f },
            new WakeUpPhase { alpha = 0.35f, duration = 0.11f },
            new WakeUpPhase { alpha = 0.00f, duration = 0.26f },
        };

        [Header("Choé sáng lúc mở mắt")]
        [Tooltip("Màu choé sáng khi vừa mở mắt ra.")]
        public Color hazeColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        [Tooltip("Độ mờ lớn nhất của choé sáng.")]
        public float hazePeakAlpha = 0.35f;

        [Tooltip("Choé sáng hiện lên trong bao lâu (giây).")]
        public float hazeOpenDuration = 0.18f;

        [Tooltip("Choé sáng tan đi trong bao lâu (giây).")]
        public float hazeFadeDuration = 0.7f;

        [Header("Trong lúc tỉnh dậy")]
        [Tooltip("Khoá người chơi để không đi lại được trong lúc đang nhắm mắt.")]
        public bool freezePlayer = true;

        [Tooltip("Đóng băng quái trong lúc người chơi chưa mở mắt.")]
        public bool pauseMonsters = true;

        [Tooltip("Ẩn HUD trong lúc tỉnh dậy (hội thoại mở ra cũng ẩn HUD rồi).")]
        public bool hideHudDuringWakeUp = true;

        [Tooltip("HUD chính. Để trống sẽ tự tìm trong scene.")]
        public HUDController mainHUD;

        private Image overlay;

        private void Awake()
        {
            // Chỉ nhận phát lời dẫn khi thật sự có chạy hiệu ứng này; không thì MinigameSystemManager
            // phát ngay trong Start như cũ.
            WillPlayIntroDialogue = playWakeUpOnCutsceneEntry && SceneFlow.CameFromCutscene && !GameSave.IsRestoring;
        }

        private void Start()
        {
            if (!WillPlayIntroDialogue) return;

            CreateOverlay();
            StartCoroutine(WakeUpRoutine());
        }

        private void OnDisable()
        {
            // Rời màn thì đừng để cờ cũ dính sang lần vào màn sau.
            WillPlayIntroDialogue = false;
        }

        // ------------------------------------------------------------------ Hiệu ứng

        private IEnumerator WakeUpRoutine()
        {
            if (pauseMonsters) MonsterAI.PauseAll();
            if (freezePlayer) SetPlayerFrozen(true);
            if (hideHudDuringWakeUp) SetHudVisible(false);

            yield return Wait(holdBlack);

            // Mí mắt mở dần: mở ra một chút rồi nhắm lại, càng về sau càng mở rộng.
            float alpha = 1f;

            for (int i = 0; i < phases.Length; i++)
            {
                float target = Mathf.Clamp01(phases[i].alpha);
                yield return Fade(Color.black, alpha, target, phases[i].duration);
                alpha = target;
            }

            // Choé sáng rồi tan dần: cảm giác vừa mở mắt ra chỗ sáng.
            yield return Fade(hazeColor, 0f, Mathf.Clamp01(hazePeakAlpha), hazeOpenDuration);
            yield return Fade(hazeColor, Mathf.Clamp01(hazePeakAlpha), 0f, hazeFadeDuration);

            SetOverlayColor(hazeColor, 0f);

            // Tấm phủ còn nằm đó (mờ hẳn) thì nó chặn hết cú bấm của UI, phải nhả raycast ra.
            if (overlay != null) overlay.raycastTarget = false;

            // Mở khoá người chơi TRƯỚC khi mở hội thoại: DialogueManager chỉ trả lại quyền điều khiển
            // nếu lúc nó khoá thì người chơi đang bật, nên để nguyên trạng thái khoá là nhân vật đứng im mãi.
            if (pauseMonsters) MonsterAI.ResumeAll();
            if (freezePlayer) SetPlayerFrozen(false);
            if (hideHudDuringWakeUp) SetHudVisible(true);

            PlayIntroDialogue();
        }

        private void CreateOverlay()
        {
            var canvasObject = new GameObject("WakeUpCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Dưới tấm màn đen của ScreenFader (30000): lúc chuyển scene vẫn phải bị nó che hết.
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var imageObject = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlay = imageObject.GetComponent<Image>();
            // Đang nhắm mắt thì đừng để người chơi bấm trúng nút nào.
            overlay.raycastTarget = true;

            SetOverlayColor(Color.black, 1f);
        }

        private IEnumerator Fade(Color color, float from, float to, float duration)
        {
            SetOverlayColor(color, from);

            if (duration > 0f)
            {
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetOverlayColor(color, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                    yield return null;
                }
            }

            SetOverlayColor(color, to);
        }

        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetOverlayColor(Color color, float alpha)
        {
            if (overlay == null) return;

            color.a = alpha;
            overlay.color = color;
        }

        // ------------------------------------------------------------------ Khoá / mở

        private void SetPlayerFrozen(bool frozen)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            player.enabled = !frozen;

            // Cần gạt có thể đang giữ hướng cũ vì lúc bị khoá không có PointerUp nào được gọi.
            if (frozen && player.movementJoystick != null) player.movementJoystick.ResetInput();
        }

        private void SetHudVisible(bool visible)
        {
            if (mainHUD == null) mainHUD = FindAnyObjectByType<HUDController>();
            if (mainHUD == null) return;

            if (mainHUD.gameObject.activeSelf != visible) mainHUD.gameObject.SetActive(visible);
        }

        /// <summary>Hết nhấp nháy thì mới nhờ bộ quản lý mini game phát lời dẫn mở màn.</summary>
        private void PlayIntroDialogue()
        {
            MinigameSystemManager minigames = MinigameSystemManager.Instance;

            if (minigames == null)
            {
                Debug.LogWarning("[GameplayWakeUp] Không tìm thấy MinigameSystemManager nên không phát được lời dẫn mở màn.", this);
                return;
            }

            // Tôn trọng lựa chọn của scene: tắt lời dẫn mở màn thì thôi.
            if (minigames.playIntroDialogueOnStart) minigames.PlayIntroDialogue();
        }
    }
}
