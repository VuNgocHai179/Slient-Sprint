using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using SilentSprint.Core;
using UnityEngine.SceneManagement;

namespace SilentSprint.Gameplay
{
    public class JumpscareController : Singleton<JumpscareController>
    {
        [Header("Camera & Targets")]
        public Transform playerCamera;
        public GameObject jumpscareMonster;
        public GameObject playerModel;
        public GameObject playerFlashlightObject;

        [Header("UI References")]
        public GameObject mainHUD;
        public Image scaryBloodOverlay;
        public CanvasGroup gameOverPanel;

        [Header("Timing & Game Feel")]
        private float turnDuration = 0.22f;
        private float lungeDuration = 0.2f;
        private float lungeStartOffsetZ = 1.8f;
        private float lungeFinalOffsetZ = 0f;
        private float screamDuration = 2.6f;
        private float cameraShakeStrength = 0.9f;

        private bool isScaring = false;
        private Vector3 targetEndPos;

        void Start()
        {
            if (jumpscareMonster != null)
            {
                targetEndPos = jumpscareMonster.transform.localPosition;
                jumpscareMonster.SetActive(false);
            }

            if (scaryBloodOverlay != null)
            {
                scaryBloodOverlay.gameObject.SetActive(false);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.alpha = 0f;
                gameOverPanel.gameObject.SetActive(false);
            }
        }

        public void TriggerCaughtJumpscare(Vector3 monsterWorldPosition, GameObject realMonster)
        {
            if (isScaring) return;
            StartCoroutine(ExecuteCaughtSequence(monsterWorldPosition, realMonster));
        }

        private IEnumerator ExecuteCaughtSequence(Vector3 monsterWorldPosition, GameObject realMonster)
        {
            isScaring = true;

            if (mainHUD != null) mainHUD.SetActive(false);

            if (playerModel != null)
            {
                playerModel.SetActive(false);
            }
            else if (PlayerController.Instance != null)
            {
                var mesh = PlayerController.Instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (mesh != null) mesh.gameObject.SetActive(false);
            }

            if (playerFlashlightObject != null)
            {
                playerFlashlightObject.SetActive(false);
            }

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.enabled = false;

                if (PlayerController.Instance.flashlightSource != null)
                {
                    StartCoroutine(FlickerFlashlight(PlayerController.Instance.flashlightSource));
                }
            }

            if (playerCamera != null)
            {
                Vector3 lookTarget = monsterWorldPosition + Vector3.up * 1.5f;
                playerCamera.DOLookAt(lookTarget, turnDuration).SetEase(Ease.OutCubic);
            }

            yield return new WaitForSeconds(turnDuration);

            if (realMonster != null)
            {
                realMonster.SetActive(false);
            }

            if (jumpscareMonster != null)
            {
                Vector3 startPos = targetEndPos + new Vector3(0f, -0.1f, lungeStartOffsetZ);
                Vector3 finalFacePos = targetEndPos + new Vector3(0f, 0f, lungeFinalOffsetZ);

                jumpscareMonster.transform.localPosition = startPos;
                jumpscareMonster.SetActive(true);

                jumpscareMonster.transform.DOLocalMove(finalFacePos, lungeDuration).SetEase(Ease.InCubic);
            }

            yield return new WaitForSeconds(lungeDuration);

            if (playerCamera != null)
            {
                playerCamera.DOShakePosition(screamDuration, cameraShakeStrength, 40, 90f, false, true);
                playerCamera.DOShakeRotation(screamDuration, 30f, 35, 90f, true);
            }

            if (scaryBloodOverlay != null)
            {
                scaryBloodOverlay.gameObject.SetActive(true);
                scaryBloodOverlay.DOFade(0.4f, 0.3f);
            }

            yield return new WaitForSeconds(screamDuration);

            if (jumpscareMonster != null)
            {
                jumpscareMonster.transform.localPosition = targetEndPos;
                jumpscareMonster.SetActive(false);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.gameObject.SetActive(true);
                gameOverPanel.DOFade(1f, 1f).OnComplete(() =>
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                });
            }
        }

        private IEnumerator FlickerFlashlight(GameObject flashlight)
        {
            for (int i = 0; i < 4; i++)
            {
                flashlight.SetActive(!flashlight.activeSelf);
                yield return new WaitForSeconds(0.05f);
            }
            flashlight.SetActive(false);
        }

        public void RestartGame()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}