using UnityEngine;
using SilentSprint.Gameplay.Minigames1;
using SilentSprint.Gameplay.Minigames2;
using UnityEngine.UI;

namespace SilentSprint.Gameplay
{
    public class MinigameSystemManager : MonoBehaviour
    {
        [Header("Global Setup")]
        public GameObject mainHUD;
        public Transform playerReturnPoint;
        public Transform minigameSpawnPoint;
        public GameObject keyReward;

        [Header("Minigame Prefabs")]
        public GameObject cupGamePrefab;
        public GameObject memoryGamePrefab;

        [Header("Punishment Jumpscare")]
        public GameObject jumpscareMonster;

        [Header("Test Buttons")]
        public Button _btnTestGame1;
        public Button _btnTestGame2;

        private GameObject spawnedMinigameObj;
        private Mini1gameManager activeCupGameInstance;
        private Mini2gameManager activeMemoryGameInstance;

        private void OnEnable()
        {
            if (_btnTestGame1 != null)
            {
                _btnTestGame1.onClick.AddListener(PlayCupMinigame);
            }

            if (_btnTestGame2 != null)
            {
                _btnTestGame2.onClick.AddListener(PlayMemoryMinigame);
            }
        }

        private void OnDisable()
        {
            if (_btnTestGame1 != null)
            {
                _btnTestGame1.onClick.RemoveListener(PlayCupMinigame);
            }

            if (_btnTestGame2 != null)
            {
                _btnTestGame2.onClick.RemoveListener(PlayMemoryMinigame);
            }

            CleanupActiveMinigame();
        }

        public void PlayCupMinigame()
        {
            if (cupGamePrefab == null || spawnedMinigameObj != null) return;

            PreparePlayerForMinigame();

            Vector3 spawnPos = minigameSpawnPoint != null ? minigameSpawnPoint.position : new Vector3(0, -100, 0);
            Quaternion spawnRot = minigameSpawnPoint != null ? minigameSpawnPoint.rotation : Quaternion.identity;

            spawnedMinigameObj = Instantiate(cupGamePrefab, spawnPos, spawnRot, minigameSpawnPoint);

            activeCupGameInstance = spawnedMinigameObj.GetComponent<Mini1gameManager>();
            if (activeCupGameInstance != null)
            {
                activeCupGameInstance.OnGameWon += HandleMinigameWon;
                activeCupGameInstance.OnGameLost += HandleMinigameLost;
                activeCupGameInstance.StartMinigame();
            }
        }

        public void PlayMemoryMinigame()
        {
            if (memoryGamePrefab == null || spawnedMinigameObj != null) return;

            PreparePlayerForMinigame();

            Vector3 spawnPos = minigameSpawnPoint != null ? minigameSpawnPoint.position : new Vector3(0, -100, 0);
            Quaternion spawnRot = minigameSpawnPoint != null ? minigameSpawnPoint.rotation : Quaternion.identity;

            spawnedMinigameObj = Instantiate(memoryGamePrefab, spawnPos, spawnRot, minigameSpawnPoint);

            activeMemoryGameInstance = spawnedMinigameObj.GetComponent<Mini2gameManager>();
            if (activeMemoryGameInstance != null)
            {
                activeMemoryGameInstance.OnGameWon += HandleMinigameWon;
                activeMemoryGameInstance.OnGameLost += HandleMinigameLost;
                activeMemoryGameInstance.StartMinigame();
            }
        }

        private void PreparePlayerForMinigame()
        {
            if (mainHUD != null) mainHUD.SetActive(false);
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.enabled = false;
                PlayerController.Instance.gameObject.SetActive(false);
            }
        }

        private void HandleMinigameWon()
        {
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
        }

        private void HandleMinigameLost()
        {
            CleanupActiveMinigame();

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.gameObject.SetActive(true);
                if (playerReturnPoint != null)
                {
                    PlayerController.Instance.transform.position = playerReturnPoint.position;
                }
                PlayerController.Instance.enabled = false;
            }

            if (jumpscareMonster != null)
            {
                jumpscareMonster.SetActive(true);
            }

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

            if (spawnedMinigameObj != null)
            {
                Destroy(spawnedMinigameObj);
                spawnedMinigameObj = null;
            }
        }
    }
}