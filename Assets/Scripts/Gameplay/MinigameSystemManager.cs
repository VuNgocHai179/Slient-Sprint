using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Dialogues;
using SilentSprint.Gameplay.Minigames1;
using SilentSprint.Gameplay.Minigames2;
using SilentSprint.Gameplay.Minigames3;
using SilentSprint.Mechanics;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    public class MinigameSystemManager : Singleton<MinigameSystemManager>
    {
        [Header("Global Setup")]
        public GameObject mainHUD;
        public Transform playerReturnPoint;
        public Transform minigameSpawnPoint;

        [Header("Vật phẩm thưởng")]
        [Tooltip("Prefab vật phẩm (Item1) mà tượng thiên thần nhả ra sau khi người chơi thắng mini game. " +
                 "Nên có sẵn component QuestItemReward; chưa có thì gắn tạm lúc chạy.")]
        public GameObject itemRewardPrefab;

        [Tooltip("Chờ bao lâu sau khi người chơi về lại màn chơi mới nhả vật phẩm ra khỏi tượng (giây).")]
        public float rewardStartDelay = 0.4f;

        [Tooltip("Vật phẩm bay từ tượng vào người chơi mất bao lâu (giây).")]
        public float rewardFlyDuration = 2.5f;

        [Header("Minigame Prefabs")]
        public GameObject cupGamePrefab;
        public GameObject memoryGamePrefab;
        public GameObject bombGamePrefab;

        [Header("Punishment Jumpscare")]
        public GameObject jumpscareMonster;

        [Header("Đóng băng quái")]
        [Tooltip("Đóng băng quái trong lúc chơi mini game, để người chơi không bị tóm khi đang không điều khiển được nhân vật.")]
        public bool freezeMonstersDuringMinigame = true;

        [Header("Hội thoại")]
        [Tooltip("Bộ điều phối hội thoại. Mọi cuộc nói chuyện ở StonePedestal và phần hướng dẫn đều đi qua đây.")]
        public DialogueManager dialogueManager;

        [Tooltip("Phát đoạn hướng dẫn cách chơi ngay khi vào màn.")]
        public bool playIntroDialogueOnStart = true;

        private readonly List<MinigamePedestal> pedestals = new List<MinigamePedestal>();

        // Trò nào đã thắng. Một trò có thể có nhiều tượng trùng nhau (Map giữ bản sao của từng tượng,
        // scene cũng đặt thêm tượng của riêng nó), nên phải nhớ theo TRÒ chứ không theo từng tượng:
        // thắng rồi thì mọi tượng của trò đó đều im lặng, không rủ người chơi vào chơi lại.
        private readonly HashSet<MinigameKind> completedKinds = new HashSet<MinigameKind>();

        // Pedestal đang kích hoạt popup (giữ lại để không hiện lại popup khi người chơi vẫn đứng trong vùng).
        private MinigamePedestal currentPedestal;
        // Pedestal đang được chơi, dùng để đánh dấu hoàn thành khi thắng.
        private MinigamePedestal activePedestal;

        private GameObject spawnedMinigameObj;
        private Mini1gameManager activeCupGameInstance;
        private Mini2gameManager activeMemoryGameInstance;
        private Mini3gameManager activeBombGameInstance;

        /// <summary>Đang có mini game chạy (lúc đó người chơi bị cất khỏi màn chơi).</summary>
        public bool IsMinigameRunning => spawnedMinigameObj != null;

        protected override void Awake()
        {
            base.Awake();

            pedestals.AddRange(Object.FindObjectsByType<MinigamePedestal>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None));

            if (pedestals.Count == 0)
            {
                Debug.LogWarning("[MinigameSystemManager] Không tìm thấy MinigamePedestal nào đang bật trong scene: người chơi vào vùng StonePedestal cũng sẽ không thấy popup.", this);
            }
        }

        private void Start()
        {
            if (dialogueManager == null) dialogueManager = DialogueManager.Instance;

            if (dialogueManager == null)
            {
                Debug.LogError("[MinigameSystemManager] Chưa gán DialogueManager nên không thể nói chuyện ở StonePedestal.", this);
                return;
            }

            // Vào màn bằng Continue thì người chơi đã nghe lời dẫn rồi, không phát lại.
            // Vào màn từ cảnh phim thì GameplayWakeUp phát lời dẫn sau khi hết đoạn "vừa tỉnh dậy".
            if (playIntroDialogueOnStart && !GameSave.IsRestoring && !GameplayWakeUp.WillPlayIntroDialogue)
            {
                PlayIntroDialogue();
            }
        }

        private void Update()
        {
            if (dialogueManager == null) return;

            // Đang đối thoại thì không dò StonePedestal: người chơi đang bị khoá nên không đi đâu được,
            // và phải đợi hội thoại kết thúc mới xét lại.
            if (dialogueManager.IsPlaying) return;

            if (IsMinigameRunning)
            {
                SetCurrentPedestal(null);
                return;
            }

            PlayerController player = PlayerController.Instance;
            // isActiveAndEnabled (không chỉ activeInHierarchy): sau khi thua mini game
            // player bị disable nên không được hiện lại popup đè lên jumpscare.
            if (player == null || !player.isActiveAndEnabled)
            {
                SetCurrentPedestal(null);
                return;
            }

            SetCurrentPedestal(FindNearestPedestal(player.transform.position));
        }

        private MinigamePedestal FindNearestPedestal(Vector3 playerPosition)
        {
            MinigamePedestal nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < pedestals.Count; i++)
            {
                MinigamePedestal pedestal = pedestals[i];
                if (IsWon(pedestal)) continue;
                if (!pedestal.IsPlayerInside(playerPosition)) continue;

                float sqrDistance = (pedestal.ZoneCenter - playerPosition).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = pedestal;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Trò của tượng này đã thắng rồi thì không rủ người chơi vào chơi lại nữa.
        /// Xét cả theo tượng (đã đánh dấu xong) lẫn theo trò, vì cùng một trò có thể có nhiều tượng trùng nhau.
        /// </summary>
        private bool IsWon(MinigamePedestal pedestal)
        {
            return pedestal != null && (pedestal.IsCompleted || completedKinds.Contains(pedestal.minigameKind));
        }

        private void SetCurrentPedestal(MinigamePedestal pedestal)
        {
            if (currentPedestal == pedestal) return;

            currentPedestal = pedestal;

            if (currentPedestal == null) return;

            // currentPedestal được giữ nguyên sau khi hội thoại kết thúc: người chơi phải rời vùng
            // rồi quay lại thì cuộc nói chuyện mới bắt đầu lại, tránh mở lại liên tục.
            StartPedestalDialogue(currentPedestal);
        }

        private void StartPedestalDialogue(MinigamePedestal pedestal)
        {
            if (dialogueManager == null) return;

            // Tượng của một trò đã thắng rồi thì không mở hội thoại rủ vào chơi lại.
            if (IsWon(pedestal)) return;

            MinigameKind kind = pedestal.minigameKind;

            dialogueManager.Play(GameplayDialogues.PedestalConversation(kind), result =>
            {
                if (result.closed) return;
                if (result.choiceIndex != GameplayDialogues.ChoiceEnterGame) return;
                if (IsMinigameRunning) return;

                // Lúc hội thoại còn mở, người chơi có thể đã thắng trò này ở một tượng khác.
                if (IsWon(pedestal)) return;

                StartMinigame(kind);
            });
        }

        private void StartMinigame(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Cup:
                    PlayCupMinigame();
                    break;
                case MinigameKind.Memory:
                    PlayMemoryMinigame();
                    break;
                case MinigameKind.Bomb:
                    PlayBombMinigame();
                    break;
            }
        }

        /// <summary>
        /// Phát lời dẫn mở màn. Tách riêng để màn chơi phát trễ được: vào màn từ cảnh phim thì
        /// <see cref="GameplayWakeUp"/> gọi hàm này sau khi người chơi "tỉnh dậy".
        /// </summary>
        public void PlayIntroDialogue()
        {
            if (dialogueManager == null) dialogueManager = DialogueManager.Instance;
            if (dialogueManager == null) return;

            dialogueManager.Play(GameplayDialogues.Intro());
        }

        /// <summary>Các mini game đã thắng, dạng <c>(int)MinigameKind</c> — để ghi xuống bản lưu.</summary>
        public int[] CompletedMinigames()
        {
            var kinds = new int[completedKinds.Count];
            int index = 0;
            foreach (MinigameKind kind in completedKinds) kinds[index++] = (int)kind;
            return kinds;
        }

        /// <summary>
        /// Khôi phục các mini game đã thắng từ bản lưu: đánh dấu mọi tượng của trò đó là xong,
        /// để chúng không rủ người chơi vào chơi lại. Không nhả lại vật phẩm thưởng.
        /// </summary>
        public void RestoreCompleted(int[] kinds)
        {
            if (kinds == null) return;

            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = (MinigameKind)kinds[i];
                completedKinds.Add(kind);

                for (int j = 0; j < pedestals.Count; j++)
                {
                    MinigamePedestal pedestal = pedestals[j];
                    if (pedestal != null && pedestal.minigameKind == kind) pedestal.MarkCompleted();
                }
            }
        }

        public void PlayCupMinigame()
        {
            if (cupGamePrefab == null || IsMinigameRunning) return;

            BeginMinigameSession(cupGamePrefab);
            activeCupGameInstance = spawnedMinigameObj != null ? spawnedMinigameObj.GetComponent<Mini1gameManager>() : null;
            if (activeCupGameInstance != null)
            {
                activeCupGameInstance.OnGameWon += HandleMinigameWon;
                activeCupGameInstance.OnGameLost += HandleMinigameLost;
                activeCupGameInstance.StartMinigame();
            }
        }

        public void PlayMemoryMinigame()
        {
            if (memoryGamePrefab == null || IsMinigameRunning) return;

            BeginMinigameSession(memoryGamePrefab);
            activeMemoryGameInstance = spawnedMinigameObj != null ? spawnedMinigameObj.GetComponent<Mini2gameManager>() : null;
            if (activeMemoryGameInstance != null)
            {
                activeMemoryGameInstance.OnGameWon += HandleMinigameWon;
                activeMemoryGameInstance.OnGameLost += HandleMinigameLost;
                activeMemoryGameInstance.StartMinigame();
            }
        }

        public void PlayBombMinigame()
        {
            if (bombGamePrefab == null || IsMinigameRunning) return;

            BeginMinigameSession(bombGamePrefab);
            activeBombGameInstance = spawnedMinigameObj != null ? spawnedMinigameObj.GetComponent<Mini3gameManager>() : null;
            if (activeBombGameInstance != null)
            {
                activeBombGameInstance.OnGameWon += HandleMinigameWon;
                activeBombGameInstance.OnGameLost += HandleMinigameLost;
                activeBombGameInstance.StartMinigame();
            }
        }

        private void BeginMinigameSession(GameObject prefab)
        {
            activePedestal = currentPedestal;
            PreparePlayerForMinigame();
            SpawnGame(prefab);
        }

        private void SpawnGame(GameObject prefab)
        {
            Vector3 spawnPos = minigameSpawnPoint != null ? minigameSpawnPoint.position : new Vector3(0, -100, 0);
            Quaternion spawnRot = minigameSpawnPoint != null ? minigameSpawnPoint.rotation : Quaternion.identity;
            spawnedMinigameObj = Instantiate(prefab, spawnPos, spawnRot, minigameSpawnPoint);
        }

        private void PreparePlayerForMinigame()
        {
            if (freezeMonstersDuringMinigame) MonsterAI.PauseAll();
            if (mainHUD != null) mainHUD.SetActive(false);

            // Phòng mini game là chỗ riêng nên tiếng mưa của màn chơi tắt đi; nhạc nền thì vẫn chạy.
            AudioManager.SetAmbienceSuspended(true);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.enabled = false;
                PlayerController.Instance.gameObject.SetActive(false);

                // AudioListener duy nhất của scene nằm trên camera người chơi và vừa tắt theo người chơi.
                // Bật listener dự phòng ngay trong khung hình này, không thì cả scene câm tiếng suốt mini game.
                AudioManager.SetFallbackListener(true);
            }
        }

        private void HandleMinigameWon()
        {
            // Giữ lại pedestal vừa thắng: tượng thiên thần của nó là chỗ nhả vật phẩm thưởng.
            MinigamePedestal wonPedestal = activePedestal;
            activePedestal = null;

            if (wonPedestal != null) MarkWon(wonPedestal);

            CleanupActiveMinigame();
            RestorePlayerAfterWin();

            // Nhả vật phẩm sau khi người chơi đã về lại màn chơi, để nó bay từ tượng vào đúng người chơi.
            SpawnRewardItem(wonPedestal);
        }

        /// <summary>
        /// Ghi nhận một trò vừa thắng: nhớ theo trò và đánh dấu mọi tượng của trò đó trong scene.
        /// Nhờ vậy những tượng trùng (Map giữ bản sao của từng tượng) cũng im lặng, không rủ vào chơi lại.
        /// </summary>
        private void MarkWon(MinigamePedestal wonPedestal)
        {
            completedKinds.Add(wonPedestal.minigameKind);
            wonPedestal.MarkCompleted();

            for (int i = 0; i < pedestals.Count; i++)
            {
                MinigamePedestal other = pedestals[i];
                if (other != null && other.minigameKind == wonPedestal.minigameKind)
                {
                    other.MarkCompleted();
                }
            }
        }

        /// <summary>
        /// Nhả vật phẩm thưởng (Item1) từ tượng thiên thần của pedestal vừa thắng. Vật phẩm hiện ra tại tượng
        /// rồi từ từ bay vào người chơi; tới nơi mới cộng vào bộ đếm vật phẩm cần tìm trên HUD.
        /// </summary>
        private void SpawnRewardItem(MinigamePedestal pedestal)
        {
            if (itemRewardPrefab == null)
            {
                Debug.LogError("[MinigameSystemManager] Chưa gán itemRewardPrefab nên không nhả được vật phẩm thưởng.", this);
                return;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null || !player.isActiveAndEnabled) return;

            Vector3 spawnPosition = RewardSpawnPosition(pedestal, player.transform);

            // Không cho vật phẩm làm con của tượng: tượng bị phóng to trong scene, vật phẩm sẽ phình theo.
            GameObject instance = Instantiate(itemRewardPrefab, spawnPosition, itemRewardPrefab.transform.rotation);

            QuestItemReward reward = instance.GetComponent<QuestItemReward>();
            if (reward == null)
            {
                // Prefab chưa gắn sẵn component thì gắn lúc chạy, khỏi phải sửa prefab.
                Debug.Log("[MinigameSystemManager] Prefab vật phẩm chưa gắn QuestItemReward, gắn tạm lúc chạy.", instance);
                reward = instance.AddComponent<QuestItemReward>();
            }

            reward.BeginFly(player.transform, rewardStartDelay, rewardFlyDuration);
        }

        /// <summary>
        /// Chỗ vật phẩm hiện ra: tâm khối tượng thiên thần (tự đúng cả khi tượng bị phóng to/nhỏ trong từng pedestal).
        /// Không tìm thấy tượng thì lấy trên đầu pedestal làm chỗ tạm.
        /// </summary>
        private Vector3 RewardSpawnPosition(MinigamePedestal pedestal, Transform player)
        {
            Transform angel = pedestal != null ? pedestal.AngelAnchor : null;

            if (angel == null)
            {
                Vector3 fallback = pedestal != null ? pedestal.transform.position : player.position;
                Debug.LogWarning("[MinigameSystemManager] Pedestal không có tượng thiên thần nào nên nhả vật phẩm tạm ở trên đầu pedestal.", pedestal);
                return fallback + Vector3.up * 2f;
            }

            Renderer[] renderers = angel.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return angel.position;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.center;
        }

        private void RestorePlayerAfterWin()
        {
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.gameObject.SetActive(true);
                if (playerReturnPoint != null)
                {
                    PlayerController.Instance.transform.position = playerReturnPoint.position;
                }
                PlayerController.Instance.enabled = true;

                // Listener của người chơi đã trở lại nên trả listener dự phòng về trạng thái tắt.
                AudioManager.SetFallbackListener(false);
            }

            if (mainHUD != null) mainHUD.SetActive(true);

            AudioManager.SetAmbienceSuspended(false);

            if (freezeMonstersDuringMinigame) MonsterAI.ResumeAll();
        }

        private void HandleMinigameLost()
        {
            activePedestal = null;
            CleanupActiveMinigame();

            // Ván mini game đã kết thúc nên thả quái ra; phần jumpscare phía dưới là kịch bản riêng.
            if (freezeMonstersDuringMinigame) MonsterAI.ResumeAll();

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.gameObject.SetActive(true);
                if (playerReturnPoint != null)
                {
                    PlayerController.Instance.transform.position = playerReturnPoint.position;
                }
                PlayerController.Instance.enabled = false;

                // Người chơi đã bật lại (dù controller còn tắt vì jumpscare) nên listener của camera
                // đã sống lại: trả listener dự phòng về trạng thái tắt.
                AudioManager.SetFallbackListener(false);
            }

            AudioManager.SetAmbienceSuspended(false);

            if (jumpscareMonster != null) jumpscareMonster.SetActive(true);

            if (JumpscareController.Instance != null)
            {
                Vector3 jumpscarePos = jumpscareMonster != null ? jumpscareMonster.transform.position :
                                      (playerReturnPoint != null ? playerReturnPoint.position : transform.position);

                JumpscareController.Instance.TriggerCaughtJumpscare(jumpscarePos, jumpscareMonster);
            }
        }

        private void CleanupActiveMinigame()
        {
            if (activeCupGameInstance != null)
            {
                activeCupGameInstance.OnGameWon -= HandleMinigameWon;
                activeCupGameInstance.OnGameLost -= HandleMinigameLost;
                activeCupGameInstance.DisposeTweens();
                activeCupGameInstance = null;
            }

            if (activeMemoryGameInstance != null)
            {
                activeMemoryGameInstance.OnGameWon -= HandleMinigameWon;
                activeMemoryGameInstance.OnGameLost -= HandleMinigameLost;
                activeMemoryGameInstance.DisposeTweens();
                activeMemoryGameInstance = null;
            }

            if (activeBombGameInstance != null)
            {
                activeBombGameInstance.OnGameWon -= HandleMinigameWon;
                activeBombGameInstance.OnGameLost -= HandleMinigameLost;
                activeBombGameInstance.DisposeTweens();
                activeBombGameInstance = null;
            }

            if (spawnedMinigameObj != null)
            {
                Destroy(spawnedMinigameObj);
                spawnedMinigameObj = null;
            }
        }
    }
}
