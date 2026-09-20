using UnityEngine;
using SilentSprint.Gameplay.Minigames1;
using SilentSprint.Gameplay.Minigames2;
using SilentSprint.Gameplay.Minigames3;
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
        public GameObject bombGamePrefab;

        [Header("Punishment Jumpscare")]
        public GameObject jumpscareMonster;

        [Header("Test Buttons")]
        public Button _btnTestGame1;
        public Button _btnTestGame2;
        public Button _btnTestGame3;

        private GameObject spawnedMinigameObj;
        private Mini1gameManager activeCupGameInstance;
        private Mini2gameManager activeMemoryGameInstance;
        private Mini3gameManager activeBombGameInstance;

        private void OnEnable()
        {
            if (_btnTestGame1 != null) _btnTestGame1.onClick.AddListener(PlayCupMinigame);
            if (_btnTestGame2 != null) _btnTestGame2.onClick.AddListener(PlayMemoryMinigame);
            if (_btnTestGame3 != null) _btnTestGame3.onClick.AddListener(PlayBombMinigame);
        }

        private void OnDisable()
        {
            if (_btnTestGame1 != null) _btnTestGame1.onClick.RemoveListener(PlayCupMinigame);
            if (_btnTestGame2 != null) _btnTestGame2.onClick.RemoveListener(PlayMemoryMinigame);
            if (_btnTestGame3 != null) _btnTestGame3.onClick.RemoveListener(PlayBombMinigame);

            CleanupActiveMinigame();
        }

        public void PlayCupMinigame()
        {
            if (cupGamePrefab == null || spawnedMinigameObj != null) return;
            PreparePlayerForMinigame();

            SpawnGame(cupGamePrefab);
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

            SpawnGame(memoryGamePrefab);
            activeMemoryGameInstance = spawnedMinigameObj.GetComponent<Mini2gameManager>();
            if (activeMemoryGameInstance != null)
            {
                activeMemoryGameInstance.OnGameWon += HandleMinigameWon;
                activeMemoryGameInstance.OnGameLost += HandleMinigameLost;
                activeMemoryGameInstance.StartMinigame();
            }
        }

        public void PlayBombMinigame()
        {
            if (bombGamePrefab == null || spawnedMinigameObj != null) return;
            PreparePlayerForMinigame();

            SpawnGame(bombGamePrefab);
            activeBombGameInstance = spawnedMinigameObj.GetComponent<Mini3gameManager>();
            if (activeBombGameInstance != null)
            {
                activeBombGameInstance.OnGameWon += HandleMinigameWon;
                activeBombGameInstance.OnGameLost += HandleMinigameLost;
                activeBombGameInstance.StartMinigame();
            }
        }

        private void SpawnGame(GameObject prefab)
        {
            Vector3 spawnPos = minigameSpawnPoint != null ? minigameSpawnPoint.position : new Vector3(0, -100, 0);
            Quaternion spawnRot = minigameSpawnPoint != null ? minigameSpawnPoint.rotation : Quaternion.identity;
            spawnedMinigameObj = Instantiate(prefab, spawnPos, spawnRot, minigameSpawnPoint);
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