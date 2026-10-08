using System.Collections.Generic;
using SilentSprint.Core;
using SilentSprint.Dialogues;
using SilentSprint.Gameplay.Minigames1;
using SilentSprint.Gameplay.Minigames2;
using SilentSprint.Gameplay.Minigames3;
using UnityEngine;

namespace SilentSprint.Gameplay
{
    public class MinigameSystemManager : Singleton<MinigameSystemManager>
    {
        [Header("Global Setup")]
        public GameObject mainHUD;
        public Transform playerReturnPoint;
        public Transform minigameSpawnPoint;
        public GameObject keyReward;

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

        // Pedestal đang kích hoạt popup (giữ lại để không hiện lại popup khi người chơi vẫn đứng trong vùng).
        private MinigamePedestal currentPedestal;
        // Pedestal đang được chơi, dùng để đánh dấu hoàn thành khi thắng.
        private MinigamePedestal activePedestal;

        private GameObject spawnedMinigameObj;
        private Mini1gameManager activeCupGameInstance;
        private Mini2gameManager activeMemoryGameInstance;
        private Mini3gameManager activeBombGameInstance;

        private bool IsMinigameRunning => spawnedMinigameObj != null;

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

            if (playIntroDialogueOnStart)
            {
                dialogueManager.Play(GameplayDialogues.Intro());
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
                if (pedestal == null || pedestal.IsCompleted) continue;
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

            MinigameKind kind = pedestal.minigameKind;

            dialogueManager.Play(GameplayDialogues.PedestalConversation(kind), result =>
            {
                if (result.closed) return;
                if (result.choiceIndex != GameplayDialogues.ChoiceEnterGame) return;
                if (IsMinigameRunning) return;

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
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.enabled = false;
                PlayerController.Instance.gameObject.SetActive(false);
            }
        }

        private void HandleMinigameWon()
        {
            if (activePedestal != null)
            {
                activePedestal.MarkCompleted();
                activePedestal = null;
            }

            CleanupActiveMinigame();
            if (keyReward != null) keyReward.SetActive(true);
            RestorePlayerAfterWin();
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
            }

            if (mainHUD != null) mainHUD.SetActive(true);

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
            }

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
