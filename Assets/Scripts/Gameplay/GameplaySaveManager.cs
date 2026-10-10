using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Mechanics;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// Chụp và khôi phục tiến trình của màn chơi (xem <see cref="GameSave"/>).
    ///
    /// - Khôi phục: vào màn bằng nút Continue thì frame đầu tiên dựng lại đúng trạng thái đã lưu
    ///   (bộ đếm, mini game đã thắng, mảnh đá còn lại trên map, chỗ đứng của người chơi).
    /// - Ghi lại: tự lưu theo nhịp, lưu ngay khi tiến trình đổi, khi thoát ra bảng chính, khi app bị ẩn/tắt.
    ///
    /// Chạy sau các hệ thống khác (DefaultExecutionOrder) vì ItemManager chia đá ngẫu nhiên trong Start —
    /// phải để nó chia xong rồi mới ghi đè bằng bản lưu.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class GameplaySaveManager : Singleton<GameplaySaveManager>
    {
        [Header("Tự động lưu")]
        [Tooltip("Bao lâu tự lưu một lần trong lúc chơi, tính bằng giây. 0 = chỉ lưu theo sự kiện.")]
        public float autoSaveInterval = 15f;

        [Tooltip("Tạm ngưng ghi bản lưu. Dùng khi đang chạy đoạn kết (KamehamehaEnding): vị trí người chơi " +
                 "lúc đó là vị trí dàn dựng cảnh phim, không phải chỗ đứng thật trên map.")]
        public bool suspendSaving;

        private float timer;
        private bool applying;
        private bool restoring;
        private bool runLost;

        private void OnEnable()
        {
            ItemManager.OnFreedomStoneChanged += HandleProgressChanged;
            ItemManager.OnQuestItemChanged += HandleProgressChanged;
        }

        private void OnDisable()
        {
            ItemManager.OnFreedomStoneChanged -= HandleProgressChanged;
            ItemManager.OnQuestItemChanged -= HandleProgressChanged;
        }

        private void Start()
        {
            if (GameSave.IsRestoring)
            {
                restoring = true;
                Apply(GameSave.Pending);
                return;
            }

            // Vào màn chơi là đã có ván để chơi tiếp (kể cả vừa bắt đầu ván mới).
            SaveNow();
        }

        private void Update()
        {
            if (restoring)
            {
                // Chỉ quên dữ liệu đang chờ sau khi mọi Start đã chạy xong: ItemManager còn cần biết
                // là vào màn bằng Continue để không chia lại đá ngẫu nhiên.
                restoring = false;
                GameSave.ClearPending();
            }

            if (autoSaveInterval <= 0f) return;

            // Thời gian thật: bảng tạm dừng có thể đang để timeScale = 0.
            timer += Time.unscaledDeltaTime;
            if (timer < autoSaveInterval) return;

            SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            // Mobile: bị ẩn đi là có thể bị hệ điều hành dọn luôn, lưu trước cho chắc.
            if (paused) SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        // ------------------------------------------------------------------ Ghi

        /// <summary>Ghi trạng thái hiện tại xuống file lưu.</summary>
        public void SaveNow()
        {
            if (!CanSave()) return;

            GameSave.Write(Capture());
            timer = 0f;
        }

        /// <summary>
        /// Người chơi bị tóm: ván này coi như mất, xoá bản lưu để bảng chính không còn nút Continue.
        /// </summary>
        public void NotifyRunLost()
        {
            runLost = true;
            GameSave.Delete();
        }

        private bool CanSave()
        {
            if (runLost || applying || suspendSaving) return false;

            // Đang chơi mini game thì người chơi bị cất đi chỗ khác, vị trí hiện tại không phải
            // chỗ đứng thật trong màn, lưu vào sẽ hỏng.
            MinigameSystemManager minigames = MinigameSystemManager.Instance;
            if (minigames != null && minigames.IsMinigameRunning) return false;

            return PlayerController.Instance != null;
        }

        private GameSaveData Capture()
        {
            ItemManager items = ItemManager.Instance;
            PlayerController player = PlayerController.Instance;
            MinigameSystemManager minigames = MinigameSystemManager.Instance;

            return new GameSaveData
            {
                freedomStones = items != null ? items.currentFreedomStones : 0,
                maxFreedomStones = items != null ? items.maxFreedomStones : 0,
                questItems = items != null ? items.currentQuestItems : 0,
                maxQuestItems = items != null ? items.maxQuestItems : 0,
                completedMinigames = minigames != null ? minigames.CompletedMinigames() : new int[0],
                playerPosition = player.transform.position,
                playerRotation = player.transform.rotation,
                spawnPoints = CaptureSpawnPoints(items),
            };
        }

        private static List<SpawnPointState> CaptureSpawnPoints(ItemManager items)
        {
            var states = new List<SpawnPointState>();
            if (items == null || items.spawnPoints == null) return states;

            foreach (Transform point in items.spawnPoints)
            {
                if (point == null) continue;

                FreedomStone stone = point.GetComponentInChildren<FreedomStone>(true);

                states.Add(new SpawnPointState
                {
                    name = point.name,
                    active = point.gameObject.activeSelf,
                    hasStone = stone != null && stone.gameObject.activeInHierarchy,
                });
            }

            return states;
        }

        // ------------------------------------------------------------------ Khôi phục

        private void Apply(GameSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[GameplaySaveManager] Không có dữ liệu để khôi phục, màn chơi bắt đầu lại từ đầu.", this);
                return;
            }

            // Đang khôi phục thì các thông báo "tiến trình vừa đổi" là do chính mình tạo ra,
            // không được ghi đè file lưu bằng trạng thái nửa vời.
            applying = true;

            ApplySpawnPoints(data);
            ApplyProgress(data);
            ApplyPlayer(data);

            applying = false;
            timer = 0f;

            // Trạng thái vừa dựng lại đã trùng khớp bản lưu; ghi lại cho chắc và để mốc tự lưu bắt đầu lại.
            SaveNow();
        }

        private void ApplySpawnPoints(GameSaveData data)
        {
            ItemManager items = ItemManager.Instance;
            if (items == null || items.spawnPoints == null || data.spawnPoints == null) return;

            // Tên object là thứ duy nhất nhận ra điểm spawn sau khi mở lại scene.
            var byName = new Dictionary<string, Transform>();
            foreach (Transform point in items.spawnPoints)
            {
                if (point != null && !byName.ContainsKey(point.name)) byName.Add(point.name, point);
            }

            foreach (SpawnPointState state in data.spawnPoints)
            {
                if (state == null || string.IsNullOrEmpty(state.name)) continue;

                if (!byName.TryGetValue(state.name, out Transform point))
                {
                    Debug.LogWarning(
                        $"[GameplaySaveManager] Bản lưu có điểm spawn '{state.name}' không còn trong scene nên bỏ qua.", this);
                    continue;
                }

                point.gameObject.SetActive(state.active);

                // Điểm còn bật mà mảnh đá đã bị nhặt thì dọn nốt mảnh đá con đi.
                if (!state.active || state.hasStone) continue;

                foreach (FreedomStone stone in point.GetComponentsInChildren<FreedomStone>(true))
                {
                    Destroy(stone.gameObject);
                }
            }
        }

        private void ApplyProgress(GameSaveData data)
        {
            ItemManager items = ItemManager.Instance;

            if (items != null)
            {
                // Đưa về 0 rồi cộng lại qua API công khai: nhờ vậy HUD và la bàn nhận được đúng
                // thông báo như lúc chơi thật, không phải tự đi cập nhật từng chỗ.
                items.maxFreedomStones = Mathf.Max(1, data.maxFreedomStones);
                items.currentFreedomStones = 0;
                items.maxQuestItems = Mathf.Max(1, data.maxQuestItems);
                items.currentQuestItems = 0;

                if (data.freedomStones > 0) items.CollectFreedomStone(data.freedomStones);
                if (data.questItems > 0) items.CollectQuestItem(data.questItems);
            }

            MinigameSystemManager minigames = MinigameSystemManager.Instance;
            if (minigames != null) minigames.RestoreCompleted(data.completedMinigames);
        }

        private void ApplyPlayer(GameSaveData data)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            // CharacterController không nhận vị trí đặt thẳng vào transform, phải tắt trong lúc dời.
            CharacterController controller = player.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;

            if (controllerWasEnabled) controller.enabled = false;
            player.transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
            if (controllerWasEnabled) controller.enabled = true;
        }

        private void HandleProgressChanged(int current, int max)
        {
            SaveNow();
        }
    }
}
