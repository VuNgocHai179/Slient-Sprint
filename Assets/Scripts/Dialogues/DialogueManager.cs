using System;
using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Gameplay;
using SilentSprint.UI;
using UnityEngine;

namespace SilentSprint.Dialogues
{
    /// <summary>
    /// Bộ điều phối hội thoại của game. Mọi cuộc nói chuyện đều chạy qua đây:
    /// nói chuyện ở StonePedestal để vào mini game, và các đoạn hướng dẫn cách chơi.
    ///
    /// Trong lúc hội thoại mở, MainHUD được ẩn đi và người chơi bị khoá di chuyển.
    /// Người chơi bấm chuột / chạm màn hình để đọc câu tiếp theo; khi câu thoại có đáp án
    /// thì phải bấm một nút chọn. Khi hội thoại kết thúc, DialogueManager trả kết quả
    /// (chỉ số đáp án đã chọn) cho người gọi qua callback.
    /// </summary>
    public class DialogueManager : Singleton<DialogueManager>
    {
        [Header("Giao diện")]
        [Tooltip("Prefab DialogBox. Sinh ra trên Canvas khi bắt đầu nếu trong scene chưa có sẵn.")]
        public DialogBoxUI dialogBoxPrefab;

        [Tooltip("Hộp thoại đã đặt sẵn trong scene. Để trống thì lấy theo prefab ở trên.")]
        public DialogBoxUI dialogBox;

        [Header("Trong lúc hội thoại")]
        [Tooltip("HUD chính, sẽ bị ẩn khi hội thoại mở.")]
        public GameObject mainHUD;

        [Tooltip("Khoá di chuyển của người chơi trong lúc hội thoại.")]
        public bool freezePlayer = true;

        [Tooltip("Đóng băng quái (MonsterAI) trong lúc hội thoại, để người chơi không bị tóm khi đang đọc.")]
        public bool pauseMonsters = true;

        [Tooltip("Chờ bao lâu trước khi nhận cú bấm tiếp theo, tránh bấm nhầm lúc câu thoại vừa hiện.")]
        public float inputDelay = 0.15f;

        [Tooltip("Chờ bao lâu trước khi nhận cú bấm chọn đáp án, tránh chọn nhầm khi nút vừa hiện dưới ngón tay.")]
        public float choiceDelay = 0.2f;

        public bool IsPlaying { get; private set; }

        private IList<DialogueLine> lines;
        private int currentIndex;
        private Action<DialogueResult> onFinished;

        private float inputUnlockTime;
        private bool playerWasEnabled;
        private bool hudWasActive;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
        }

        private void Update()
        {
            if (!IsPlaying) return;

            // Trên PC có thể bấm Space/Enter cho nhanh khi test; mobile dùng cú chạm trên DialogBox.
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Advance();
            }
#endif
        }

        /// <summary>
        /// Chạy một cuộc hội thoại. Trả về false nếu không mở được (đang có hội thoại khác chạy).
        /// </summary>
        public bool Play(IList<DialogueLine> dialogueLines, Action<DialogueResult> onComplete = null)
        {
            if (dialogueLines == null || dialogueLines.Count == 0)
            {
                onComplete?.Invoke(DialogueResult.Finished());
                return false;
            }

            if (IsPlaying)
            {
                Debug.LogWarning("[DialogueManager] Đang có hội thoại khác chạy, bỏ qua yêu cầu mở hội thoại mới.", this);
                return false;
            }

            ResolveReferences();
            if (dialogBox == null)
            {
                onComplete?.Invoke(DialogueResult.Closed());
                return false;
            }

            lines = dialogueLines;
            currentIndex = 0;
            onFinished = onComplete;
            IsPlaying = true;
            inputUnlockTime = Time.unscaledTime + inputDelay;

            EnterDialogueState();
            ShowCurrentLine();
            return true;
        }

        /// <summary>Người chơi bấm để đọc câu tiếp theo.</summary>
        public void Advance()
        {
            if (!IsPlaying || lines == null) return;
            if (Time.unscaledTime < inputUnlockTime) return;

            DialogueLine line = lines[currentIndex];

            // Câu có đáp án thì phải bấm nút chọn, bấm ra nền không tính.
            if (line.HasChoices) return;

            if (currentIndex + 1 < lines.Count)
            {
                currentIndex++;
                ShowCurrentLine();
                return;
            }

            Finish(DialogueResult.Finished());
        }

        /// <summary>Đóng hội thoại ngay, người gọi nhận được kết quả closed.</summary>
        public void Close()
        {
            if (!IsPlaying)
            {
                if (dialogBox != null) dialogBox.Hide();
                return;
            }

            Finish(DialogueResult.Closed());
        }

        private void ShowCurrentLine()
        {
            DialogueLine line = lines[currentIndex];

            dialogBox.SetLine(line.speaker, line.content);
            dialogBox.Show();

            if (line.HasChoices)
            {
                dialogBox.SetContinueHintVisible(false);
                dialogBox.SetChoices(GetChoiceLabels(line));
                // Nút vừa hiện ra ngay dưới ngón tay người chơi, chờ một nhịp rồi mới nhận chọn.
                inputUnlockTime = Time.unscaledTime + choiceDelay;
            }
            else
            {
                dialogBox.SetChoices(null);
                dialogBox.SetContinueHintVisible(true);
                inputUnlockTime = Time.unscaledTime + inputDelay;
            }
        }

        private static List<string> GetChoiceLabels(DialogueLine line)
        {
            List<string> labels = new List<string>(line.choices.Count);
            for (int i = 0; i < line.choices.Count; i++)
            {
                labels.Add(line.choices[i] != null ? line.choices[i].label : string.Empty);
            }
            return labels;
        }

        private void HandleAdvanceRequested()
        {
            Advance();
        }

        private void HandleChoicePicked(int index)
        {
            if (!IsPlaying || lines == null) return;
            if (Time.unscaledTime < inputUnlockTime) return;

            DialogueLine line = lines[currentIndex];
            if (!line.HasChoices) return;
            if (index < 0 || index >= line.choices.Count) return;

            DialogueChoice choice = line.choices[index];

            // Đáp án có thể trỏ tới một dòng khác, không thì kết thúc luôn cuộc hội thoại.
            if (choice.nextLineIndex >= 0 && choice.nextLineIndex < lines.Count)
            {
                currentIndex = choice.nextLineIndex;
                ShowCurrentLine();
                return;
            }

            Finish(new DialogueResult { choiceIndex = index, choiceLabel = choice.label, closed = false });
        }

        private void Finish(DialogueResult result)
        {
            IsPlaying = false;
            lines = null;
            currentIndex = 0;

            if (dialogBox != null)
            {
                dialogBox.SetChoices(null);
                dialogBox.Hide();
            }

            ExitDialogueState();

            Action<DialogueResult> callback = onFinished;
            onFinished = null;
            callback?.Invoke(result);
        }

        private void EnterDialogueState()
        {
            if (pauseMonsters) MonsterAI.PauseAll();

            if (mainHUD != null && mainHUD.activeSelf)
            {
                hudWasActive = true;
                mainHUD.SetActive(false);
            }

            if (!freezePlayer) return;

            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            if (player.enabled)
            {
                playerWasEnabled = true;
                player.enabled = false;
            }

            // MainHUD (chứa joystick) vừa bị ẩn nên joystick không nhận được PointerUp nữa,
            // phải tự trả về 0 kẻo người chơi chạy tiếp ngay khi hội thoại đóng.
            if (player.movementJoystick != null) player.movementJoystick.ResetInput();
        }

        private void ExitDialogueState()
        {
            if (pauseMonsters) MonsterAI.ResumeAll();

            PlayerController player = PlayerController.Instance;

            if (player != null && playerWasEnabled)
            {
                player.enabled = true;
            }
            playerWasEnabled = false;

            if (mainHUD != null && hudWasActive)
            {
                mainHUD.SetActive(true);
            }
            hudWasActive = false;
        }

        private void ResolveReferences()
        {
            if (mainHUD == null)
            {
                HUDController hud = FindAnyObjectByType<HUDController>();
                if (hud != null) mainHUD = hud.gameObject;
            }

            if (dialogBox == null)
            {
                // Hộp thoại tự ẩn mình trong Awake nên phải tìm cả object đang tắt.
                dialogBox = FindAnyObjectByType<DialogBoxUI>(FindObjectsInactive.Include);
            }

            if (dialogBox == null && dialogBoxPrefab != null)
            {
                dialogBox = Instantiate(dialogBoxPrefab, ResolveUIParent(), false);
                dialogBox.name = "DialogBox";
            }

            if (dialogBox == null)
            {
                Debug.LogError(
                    "[DialogueManager] Chưa có hộp thoại nào để hiển thị. Hãy gán dialogBoxPrefab (DialogBox.prefab) hoặc đặt DialogBox vào scene.", this);
                return;
            }

            dialogBox.OnAdvance -= HandleAdvanceRequested;
            dialogBox.OnAdvance += HandleAdvanceRequested;
            dialogBox.OnChoicePicked -= HandleChoicePicked;
            dialogBox.OnChoicePicked += HandleChoicePicked;
            dialogBox.Hide();
        }

        private Transform ResolveUIParent()
        {
            if (mainHUD != null)
            {
                Canvas hudCanvas = mainHUD.GetComponentInParent<Canvas>();
                if (hudCanvas != null) return hudCanvas.transform;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            return canvas != null ? canvas.transform : transform;
        }

        private void OnDestroy()
        {
            if (dialogBox == null) return;

            dialogBox.OnAdvance -= HandleAdvanceRequested;
            dialogBox.OnChoicePicked -= HandleChoicePicked;
        }
    }
}
