using UnityEngine;
using SilentSprint.Gameplay;

namespace SilentSprint.Mechanics
{
    [RequireComponent(typeof(Collider))]
    public class FreedomStone : MonoBehaviour
    {
        [Header("Settings")]
        public int amountValue = 1;
        public float rotateSpeed = 60f;

        private void Start()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Update()
        {
            transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            { 
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.CollectFreedomStone(amountValue);
                }

                Destroy(gameObject);
            }
        }
    }
}