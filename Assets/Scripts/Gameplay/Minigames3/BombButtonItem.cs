using UnityEngine;
using DG.Tweening;

namespace SilentSprint.Gameplay.Minigames3
{
    public class BombButtonItem : MonoBehaviour
    {
        [Tooltip("Button index from 0 to 19")]
        public int buttonIndex;

        [Header("VFX Prefab")]
        [Tooltip("VFX prefab to instantiate on top of the button")]
        public GameObject vfxPrefab;

        [Header("VFX Transform Settings")]
        [Tooltip("Local position offset calibrated to match the button face")]
        public Vector3 vfxLocalOffset = new Vector3(0f, 0.116f, -0.027f);

        [Tooltip("Local rotation angles for VFX alignment")]
        public Vector3 vfxLocalRotation = new Vector3(62.181f, 0f, 0f);

        [Tooltip("Local scale proportions for VFX display")]
        public Vector3 vfxLocalScale = new Vector3(0.59f, 0.43f, 1f);

        [Header("Movement Settings")]
        [Tooltip("Depression depth when button is pressed")]
        public float pressDepth = 0.12f;

        private Mini3gameManager manager;
        private bool isPressed = false;
        private Collider col;
        private Vector3 initialLocalPos;
        private GameObject activeVfxInstance;

        /// <summary>
        /// Initializes button state, collision, position, and VFX setup.
        /// </summary>
        public void Setup(int index, Mini3gameManager mgr)
        {
            buttonIndex = index;
            manager = mgr;
            isPressed = false;

            // Ensure collider presence for physics raycasting
            col = GetComponent<Collider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.enabled = true;

            // Cache base position and reset existing tweens
            initialLocalPos = transform.localPosition;
            transform.DOKill();
            transform.localPosition = initialLocalPos;

            // Instantiate VFX instance once as a child transform
            if (vfxPrefab != null && activeVfxInstance == null)
            {
                activeVfxInstance = Instantiate(vfxPrefab, transform);
                activeVfxInstance.transform.localPosition = vfxLocalOffset;
                activeVfxInstance.transform.localEulerAngles = vfxLocalRotation;
                activeVfxInstance.transform.localScale = vfxLocalScale;
            }

            // Restore VFX visibility
            if (activeVfxInstance != null)
            {
                activeVfxInstance.SetActive(true);
            }
        }

        /// <summary>
        /// Handles button pressing action, disables interaction, hides VFX, and animates depression.
        /// </summary>
        public void PressButton(bool byPlayer)
        {
            if (isPressed) return;
            isPressed = true;

            // Disable collider to prevent duplicate trigger events
            if (col != null) col.enabled = false;

            // Deactivate button visual effect immediately upon press
            if (activeVfxInstance != null)
            {
                activeVfxInstance.SetActive(false);
            }

            // Animate inward depression along local Z axis
            transform.DOKill();
            transform.DOLocalMoveZ(initialLocalPos.z - pressDepth, 0.15f).SetEase(Ease.OutQuad);

            // Notify minigame manager
            manager?.OnButtonPressed(buttonIndex, byPlayer);
        }

        public bool IsPressed() => isPressed;

        /// <summary>
        /// Resets the button back to its initial idle state.
        /// </summary>
        public void ResetButton()
        {
            transform.DOKill();
            transform.localPosition = initialLocalPos;
            isPressed = false;

            if (col != null) col.enabled = true;

            if (activeVfxInstance != null)
            {
                activeVfxInstance.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();

            if (activeVfxInstance != null)
            {
                Destroy(activeVfxInstance);
            }
        }
    }
}