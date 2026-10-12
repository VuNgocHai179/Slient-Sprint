using SilentSprint.Core;
using SilentSprint.Dialogues;
using SilentSprint.Mechanics;
using SilentSprint.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Phần "nhận la bàn" của màn chơi:
    /// - Nhặt đủ <see cref="unlockAtCount"/> mảnh đá tự do thì người bí ẩn lên tiếng cảnh báo.
    /// - Hội thoại xong thì thả một chiếc la bàn từ trên trời xuống trước mặt người chơi.
    /// - La bàn chạm đất thì bật nút BtnCompass trên MainHUD; nút đó dùng để bật/tắt la bàn về sau.
    ///
    /// Kim la bàn bám một mảnh đá tự do còn sót lại trên map. Mảnh đó bị nhặt thì mới chọn mảnh khác,
    /// nên mở/đóng la bàn liên tục vẫn chỉ đúng một hướng.
    /// </summary>
    public class CompassSystem : Singleton<CompassSystem>
    {
        [Header("Mốc nhận la bàn")]
        [Tooltip("Số mảnh đá tự do phải nhặt để người bí ẩn thả la bàn xuống.")]
        public int unlockAtCount = 5;

        [Header("La bàn")]
        [Tooltip("Prefab la bàn, sinh ra khi người chơi nhặt đủ mảnh đá. Phải có component GuidingCompass.")]
        public GameObject compassPrefab;

        [Tooltip("Object cha của la bàn khi sinh ra. Để trống sẽ lấy Player — la bàn là con của Player, giữ đúng vị trí/góc đã đặt trong prefab.")]
        public Transform compassParent;

        [Header("Hội thoại cảnh báo")]
        [Tooltip("Bộ điều phối hội thoại. Để trống sẽ tự lấy DialogueManager.Instance.")]
        public DialogueManager dialogueManager;

        [Tooltip("Phát lời cảnh báo của người bí ẩn trước khi thả la bàn.")]
        public bool playWarningDialogue = true;

        [Header("Giao diện")]
        [Tooltip("HUD chứa nút BtnCompass. Để trống sẽ tự tìm trong scene.")]
        public HUDController hud;

        private enum Stage
        {
            /// Chưa nhặt đủ mảnh đá.
            Waiting,
            /// Đang chờ phát lời cảnh báo (có thể phải đợi hội thoại khác xong).
            Announcing,
            /// Đã phát lời cảnh báo; đang chờ hội thoại đóng rồi thả la bàn, và chờ la bàn chạm đất.
            Dropping,
            /// La bàn đã trên tay người chơi.
            Ready
        }

        private Stage stage = Stage.Waiting;
        private GuidingCompass compass;
        private Transform currentTarget;
        private bool compassButtonShown;
        private bool setupBroken;

        /// <summary>Người chơi đã nhặt đủ mảnh đá để nhận la bàn chưa.</summary>
        public bool IsUnlocked => stage != Stage.Waiting;

        private void Start()
        {
            if (dialogueManager == null) dialogueManager = DialogueManager.Instance;
            if (hud == null) hud = FindAnyObjectByType<HUDController>();

            // Nhỡ người chơi đã nhặt đủ trước khi component này kịp bật.
            if (ItemManager.Instance != null && ItemManager.Instance.currentFreedomStones >= unlockAtCount)
            {
                BeginUnlock();
            }
        }

        private void OnEnable()
        {
            ItemManager.OnFreedomStoneChanged += HandleStonesChanged;
        }

        private void OnDisable()
        {
            ItemManager.OnFreedomStoneChanged -= HandleStonesChanged;
        }

        private void HandleStonesChanged(int current, int max)
        {
            if (current >= unlockAtCount)
            {
                BeginUnlock();
            }
        }

        private void BeginUnlock()
        {
            if (stage != Stage.Waiting || setupBroken) return;
            stage = Stage.Announcing;
        }

        private void Update()
        {
            switch (stage)
            {
                case Stage.Announcing:
                    AnnounceThenDrop();
                    break;

                case Stage.Dropping:
                    // La bàn chỉ được thả sau khi hội thoại đóng, nên lúc này compass còn null.
                    // Cứ đợi, không được coi là hỏng.
                    if (compass == null) break;

                    if (compass.HasLanded)
                    {
                        stage = Stage.Ready;
                        ShowCompassButton();
                        PickNewTarget();
                    }
                    break;

                case Stage.Ready:
                    // Mục tiêu vừa bị nhặt thì kim phải tìm mảnh khác.
                    if (currentTarget == null) PickNewTarget();
                    break;
            }
        }

        private void AnnounceThenDrop()
        {
            if (dialogueManager == null) dialogueManager = DialogueManager.Instance;

            // Đang có cuộc hội thoại khác thì đợi, kẻo lời cảnh báo bị nuốt mất.
            if (dialogueManager != null && dialogueManager.IsPlaying) return;

            stage = Stage.Dropping;

            if (playWarningDialogue && dialogueManager != null)
            {
                dialogueManager.Play(GameplayDialogues.CompassWarning(), _ => SpawnCompass());
                return;
            }

            SpawnCompass();
        }

        private void SpawnCompass()
        {
            if (compassPrefab == null)
            {
                // Thiếu prefab là lỗi setup: tắt hẳn tính năng thay vì thả la bàn rỗng.
                setupBroken = true;
                stage = Stage.Waiting;
                Debug.LogError("[CompassSystem] Chưa gán compassPrefab nên không thả được la bàn.", this);
                return;
            }

            Transform player = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (player == null)
            {
                setupBroken = true;
                stage = Stage.Waiting;
                Debug.LogError("[CompassSystem] Không tìm thấy PlayerController để thả la bàn trước mặt người chơi.", this);
                return;
            }

            // La bàn là con của Player, nên vị trí/góc/tỉ lệ lấy thẳng từ transform đặt trong prefab.
            Transform parent = compassParent != null ? compassParent : player;

            GameObject instance = Instantiate(compassPrefab, parent);
            compass = instance.GetComponent<GuidingCompass>();
            if (compass == null) compass = instance.GetComponentInChildren<GuidingCompass>(true);

            if (compass == null)
            {
                // Prefab chưa gắn sẵn component thì gắn lúc chạy, khỏi phải sửa prefab.
                Debug.Log("[CompassSystem] Prefab la bàn chưa gắn GuidingCompass, gắn tạm lúc chạy.", instance);
                compass = instance.AddComponent<GuidingCompass>();
            }

            compass.BeginDrop();
        }

        /// <summary>Nút BtnCompass gọi vào đây: đang hiện thì cất la bàn đi, đang cất thì lấy ra.</summary>
        public void ToggleCompass()
        {
            if (compass == null) return;
            compass.ToggleVisible();
        }

        /// <summary>La bàn đang lộ ra trước mặt người chơi không (cảnh kết hỏi trước khi cất đi).</summary>
        public bool IsCompassVisible => compass != null && compass.IsVisible;

        /// <summary>
        /// Cất la bàn đi hoặc lấy ra. Cảnh kết dùng để cất la bàn đang mở: la bàn là con của người chơi nên cứ lơ
        /// lửng trước mặt suốt đoạn phim.
        /// </summary>
        public void SetCompassVisible(bool visible)
        {
            if (compass != null) compass.SetVisible(visible);
        }

        private void ShowCompassButton()
        {
            if (compassButtonShown) return;
            compassButtonShown = true;

            if (hud == null) hud = FindAnyObjectByType<HUDController>();
            if (hud != null) hud.SetCompassButtonVisible(true);
        }

        private void PickNewTarget()
        {
            if (compass == null) return;

            FreedomStone[] remaining = FindObjectsByType<FreedomStone>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (remaining == null || remaining.Length == 0)
            {
                currentTarget = null;
                compass.SetTarget(null);
                return;
            }

            currentTarget = remaining[Random.Range(0, remaining.Length)].transform;
            compass.SetTarget(currentTarget);
        }
    }
}
