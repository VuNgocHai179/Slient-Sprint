using SilentSprint.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SilentSprint.Gameplay
{
    public class ItemManager : Singleton<ItemManager>
    {
        [Header("Item Settings")]
        public int maxFreedomStones = 20;
        public int currentFreedomStones = 0;

        public static event Action<int, int> OnFreedomStoneChanged;

        [Header("Vật phẩm cần tìm (Item1)")]
        [Tooltip("Tổng số vật phẩm phải tìm trong màn. Bằng số tượng thiên thần sa ngã: mỗi tượng thưởng một vật phẩm.")]
        public int maxQuestItems = 3;
        public int currentQuestItems = 0;

        /// <summary>Số vật phẩm cần tìm đã thu được thay đổi (đã có, tổng số).</summary>
        public static event Action<int, int> OnQuestItemChanged;

        [Header("Spawn Locations")]
        public List<Transform> spawnPoints;

        void Start()
        {
            // Vào màn bằng Continue thì bản lưu dựng lại đúng chỗ đá cũ (xem GameplaySaveManager),
            // không chia ngẫu nhiên chồng lên nữa.
            if (GameSave.IsRestoring) return;

            SpawnItemsRandomly();
        }

        private void SpawnItemsRandomly()
        {
            if (spawnPoints.Count == 0)
            {
                return;
            }

            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    point.gameObject.SetActive(false);
                }
            }

            int spawnCount = Mathf.Min(maxFreedomStones, spawnPoints.Count);
            List<Transform> availablePoints = new List<Transform>(spawnPoints);

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, availablePoints.Count);
                Transform selectedPoint = availablePoints[randomIndex];

                selectedPoint.gameObject.SetActive(true);

                availablePoints.RemoveAt(randomIndex);
            }
        }

        public void CollectFreedomStone(int amount = 1)
        {
            currentFreedomStones += amount;
            if (currentFreedomStones > maxFreedomStones)
            {
                currentFreedomStones = maxFreedomStones;
            }

            OnFreedomStoneChanged?.Invoke(currentFreedomStones, maxFreedomStones);

            if (currentFreedomStones >= maxFreedomStones)
            {
                Debug.Log("DONE FreedomStone!");
            }
        }

        /// <summary>
        /// Cộng vật phẩm cần tìm (Item1). Tượng thiên thần sa ngã thưởng vật phẩm theo đường này:
        /// vật phẩm bay vào tới người chơi rồi mới gọi vào đây, nên bộ đếm trên HUD nhích lên đúng lúc vật phẩm nhập vào người.
        /// </summary>
        public void CollectQuestItem(int amount = 1)
        {
            currentQuestItems = Mathf.Clamp(currentQuestItems + amount, 0, maxQuestItems);

            OnQuestItemChanged?.Invoke(currentQuestItems, maxQuestItems);

            if (currentQuestItems >= maxQuestItems)
            {
                Debug.Log("DONE QuestItem!");
            }
        }
    }
}