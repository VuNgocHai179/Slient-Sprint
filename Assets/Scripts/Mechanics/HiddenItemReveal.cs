using UnityEngine;
using SilentSprint.Gameplay;

namespace SilentSprint.Mechanics
{
    public class HiddenItemReveal : MonoBehaviour
    {
        [Header("Reveal Settings")]
        public LayerMask obstacleMask;

        private Renderer[] renderers;
        private Collider itemCollider;

        private Light cachedFlashlight;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            itemCollider = GetComponent<Collider>();

            SetVisibility(false);
        }

        void Update()
        {
            if (PlayerController.Instance == null || PlayerController.Instance.flashlightSource == null) return;

            GameObject flashlightObj = PlayerController.Instance.flashlightSource;

            if (cachedFlashlight == null)
            {
                cachedFlashlight = flashlightObj.GetComponent<Light>();
            }

            if (cachedFlashlight == null || !flashlightObj.activeInHierarchy)
            {
                SetVisibility(false);
                return;
            }

            Vector3 lightPos = flashlightObj.transform.position;
            Vector3 itemPos = transform.position + Vector3.up * 0.2f;
            Vector3 dirToItem = itemPos - lightPos;
            float distance = dirToItem.magnitude;

            if (distance <= cachedFlashlight.range)
            {
                float angle = Vector3.Angle(flashlightObj.transform.forward, dirToItem);
                if (angle <= cachedFlashlight.spotAngle / 2f)
                {
                    if (!Physics.Raycast(lightPos, dirToItem.normalized, distance, obstacleMask))
                    {
                        SetVisibility(true);
                        return;
                    }
                }
            }

            SetVisibility(false);
        }

        private void SetVisibility(bool isVisible)
        {
            foreach (var r in renderers)
            {
                if (r.enabled != isVisible)
                    r.enabled = isVisible;
            }

            if (itemCollider != null && itemCollider.enabled != isVisible)
            {
                itemCollider.enabled = isVisible;
            }
        }
    }
}