using UnityEngine;
using System.Collections.Generic;

namespace SilentSprint.Gameplay
{
    public class ItemManager : MonoBehaviour
    {
        [Header("Item Settings")]
        public GameObject thachTriPrefab;
        private int numberOfItemsToSpawn = 10;

        [Header("Spawn Locations")]
        public List<Transform> spawnPoints;

        void Start()
        {
            SpawnItemsRandomly();
        }

        private void SpawnItemsRandomly()
        {
            if (thachTriPrefab == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("Missing Prefab or Spawn Points in ItemManager!");
                return;
            }

            int spawnCount = Mathf.Min(numberOfItemsToSpawn, spawnPoints.Count);
            List<Transform> availablePoints = new List<Transform>(spawnPoints);

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, availablePoints.Count);
                Transform selectedPoint = availablePoints[randomIndex];

                Instantiate(thachTriPrefab, selectedPoint.position, selectedPoint.rotation);
                availablePoints.RemoveAt(randomIndex);
            }
        }
    }
}