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

        [Header("Spawn Locations")]
        public List<Transform> spawnPoints;

        void Start()
        {
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
    }
}