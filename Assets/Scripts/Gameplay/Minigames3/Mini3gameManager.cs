using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Random = UnityEngine.Random;

namespace SilentSprint.Gameplay.Minigames3
{
    public class Mini3gameManager : MonoBehaviour
    {
        [Header("Setup 20 Buttons & Camera")]
        public BombButtonItem[] buttons;
        public Camera roomCamera;
        public Transform bombModel;

        [Header("Cheat")]
        [Tooltip("Nút cheat trong phòng (object btn_cheat): bấm là thắng ngay ván này. Để trống sẽ tự tìm theo tên.")]
        public Button btnCheat;

        [Header("Screen UI")]
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI remainingText;
        public TextMeshProUGUI dangerRateText;

        [Header("Bomb Detonation (player loses)")]
        [Tooltip("Optional VFX prefab (smoke / fire choreography) spawned at the bomb position when it blows up.")]
        public GameObject explosionVfxPrefab;

        [Tooltip("How long the bomb rattles before it goes off. Jumpscare only starts after the detonation.")]
        public float preExplosionShakeDuration = 0.8f;

        [Tooltip("Rattle amplitude of the bomb.")]
        public float preExplosionShakeStrength = 0.25f;

        [Tooltip("Total length of the detonation beat before the room camera cuts away.")]
        public float explosionDuration = 0.7f;

        [Tooltip("How many times the bomb swells up right before it bursts.")]
        public float explosionSwellMultiplier = 1.8f;

        [Tooltip("Diameter (world units) of the expanding fireball flash.")]
        public float explosionBurstDiameter = 8f;

        [Tooltip("Color of the fireball and of the flash light.")]
        public Color explosionBurstColor = new Color(1f, 0.68f, 0.25f, 1f);

        [Tooltip("Light intensity at the moment of detonation.")]
        public float explosionLightIntensity = 6f;

        [Tooltip("Illumination radius of the flash light.")]
        public float explosionLightRange = 40f;

        [Tooltip("Tint of the full screen flash.")]
        public Color explosionScreenFlashColor = new Color(1f, 0.92f, 0.78f, 1f);

        [Tooltip("Peak opacity of the full screen flash (0-1).")]
        [Range(0f, 1f)]
        public float explosionScreenFlashAlpha = 0.9f;

        [Tooltip("Camera shake strength during the detonation.")]
        public float explosionCameraShakeStrength = 0.55f;

        public event Action OnGameWon;
        public event Action OnGameLost;

        private int explosiveButtonIndex;
        private List<int> availableButtonIndices = new List<int>();
        private bool isPlayerTurn = false;
        private bool isGameOver = false;

        private void Awake()
        {
            MinigameCheatButton.Bind(this, btnCheat, ForceWin);
        }

        public void StartMinigame()
        {
            if (roomCamera != null) roomCamera.gameObject.SetActive(true);

            isGameOver = false;
            availableButtonIndices.Clear();

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Setup(i, this);
                    availableButtonIndices.Add(i);
                }
            }

            if (availableButtonIndices.Count > 0)
            {
                explosiveButtonIndex = availableButtonIndices[Random.Range(0, availableButtonIndices.Count)];
            }

            StartPlayerTurn();
        }

        /// <summary>Nút cheat trong phòng: thắng ngay, y như thực thể kia bấm trúng nút nổ.</summary>
        public void ForceWin()
        {
            if (isGameOver) return;

            isGameOver = true;
            isPlayerTurn = false;

            if (roomCamera != null) roomCamera.gameObject.SetActive(false);

            OnGameWon?.Invoke();
        }

        private void UpdateUI()
        {
            if (remainingText != null)
            {
                remainingText.text = $"NÚT CÒN LẠI: {availableButtonIndices.Count}/20";
            }

            if (dangerRateText != null && availableButtonIndices.Count > 0)
            {
                float risk = (1f / availableButtonIndices.Count) * 100f;
                dangerRateText.text = $"TỈ LỆ NỔ: {risk:F1}%";
            }
        }

        private void StartPlayerTurn()
        {
            if (isGameOver) return;
            isPlayerTurn = true;

            if (statusText != null)
            {
                statusText.text = "<color=#00FF44>LƯỢT CỦA BẠN</color>";
            }

            UpdateUI();
        }

        private void StartAITurn()
        {
            if (isGameOver) return;
            isPlayerTurn = false;

            if (statusText != null)
            {
                statusText.text = "<color=#FF2222>THỰC THỂ ĐANG CHỌN...</color>";
            }

            UpdateUI();
            StartCoroutine(AITurnRoutine());
        }

        private IEnumerator AITurnRoutine()
        {
            yield return new WaitForSeconds(Random.Range(1.2f, 2.0f));

            if (isGameOver || availableButtonIndices.Count == 0) yield break;

            int randomPick = Random.Range(0, availableButtonIndices.Count);
            int chosenIndex = availableButtonIndices[randomPick];

            if (buttons[chosenIndex] != null)
            {
                buttons[chosenIndex].PressButton(byPlayer: false);
            }
        }

        void Update()
        {
            if (!isPlayerTurn || isGameOver) return;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        return;

                    HandleInteraction(touch.position);
                }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                HandleInteraction(Input.mousePosition);
            }
        }

        private void HandleInteraction(Vector2 screenPos)
        {
            if (roomCamera == null) return;

            Ray ray = roomCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                Debug.Log("Raycast trúng: " + hit.collider.gameObject.name);
                BombButtonItem btn = hit.collider.GetComponent<BombButtonItem>();
                if (btn != null && !btn.IsPressed())
                {
                    isPlayerTurn = false;
                    btn.PressButton(byPlayer: true);
                }
            }
        }

        public void OnButtonPressed(int pressedIndex, bool byPlayer)
        {
            if (isGameOver) return;

            availableButtonIndices.Remove(pressedIndex);

            if (pressedIndex == explosiveButtonIndex)
            {
                isGameOver = true;

                if (bombModel != null)
                {
                    bombModel.DOShakePosition(preExplosionShakeDuration, preExplosionShakeStrength, 30, 90, false, true);
                }

                if (byPlayer)
                {
                    StartCoroutine(EndGameRoutine(isPlayerWon: false));
                }
                else
                {
                    StartCoroutine(EndGameRoutine(isPlayerWon: true));
                }
                return;
            }

            if (byPlayer)
            {
                StartAITurn();
            }
            else
            {
                StartPlayerTurn();
            }
        }

        private IEnumerator EndGameRoutine(bool isPlayerWon)
        {
            if (isPlayerWon)
            {
                yield return new WaitForSeconds(0.7f);
            }
            else
            {
                // Thua: quả boom rung lắc, phát nổ, rồi mới cắt sang jumpscare.
                yield return new WaitForSeconds(preExplosionShakeDuration);
                yield return ExplodeBombRoutine();
            }

            if (roomCamera != null) roomCamera.gameObject.SetActive(false);

            if (isPlayerWon)
            {
                OnGameWon?.Invoke();
            }
            else
            {
                OnGameLost?.Invoke();
            }
        }

        /// <summary>
        /// Plays the bomb detonation: fireball, light flash, screen flash, camera shake.
        /// Runs on the room camera before the jumpscare takes over.
        /// </summary>
        private IEnumerator ExplodeBombRoutine()
        {
            Vector3 center = bombModel != null ? bombModel.position : transform.position;

            if (explosionVfxPrefab != null)
            {
                Instantiate(explosionVfxPrefab, center, Quaternion.identity);
            }

            GameObject fireball = SpawnFireball(center);
            GameObject flashLightObject = SpawnFlashLight(center);
            CanvasGroup screenFlash = SpawnScreenFlash();

            float burstTime = Mathf.Max(0.05f, explosionDuration * 0.35f);
            float fadeTime = Mathf.Max(0.05f, explosionDuration - burstTime);

            if (fireball != null)
            {
                fireball.transform.DOScale(Vector3.one * explosionBurstDiameter, burstTime).SetEase(Ease.OutQuad);
            }

            if (bombModel != null)
            {
                bombModel.DOScale(bombModel.localScale * explosionSwellMultiplier, explosionDuration * 0.5f).SetEase(Ease.OutQuad);
            }

            if (flashLightObject != null)
            {
                Light flashLight = flashLightObject.GetComponent<Light>();
                DOTween.To(() => flashLight.intensity, value => flashLight.intensity = value, explosionLightIntensity, burstTime)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => DOTween.To(() => flashLight.intensity, value => flashLight.intensity = value, 0f, fadeTime));
            }

            if (screenFlash != null)
            {
                screenFlash.alpha = explosionScreenFlashAlpha;
                screenFlash.DOFade(0f, explosionDuration * 0.8f).SetEase(Ease.OutQuad);
            }

            if (roomCamera != null)
            {
                roomCamera.transform.DOShakePosition(explosionDuration, explosionCameraShakeStrength, 30, 90f, false, true);
            }

            yield return new WaitForSeconds(explosionDuration);

            if (bombModel != null) bombModel.gameObject.SetActive(false);
            if (fireball != null) Destroy(fireball);
            if (flashLightObject != null) Destroy(flashLightObject);
            if (screenFlash != null) Destroy(screenFlash.gameObject);
        }

        /// <summary>
        /// Creates the expanding fireball sphere standing in for the blast core.
        /// </summary>
        private GameObject SpawnFireball(Vector3 worldPosition)
        {
            GameObject fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireball.name = "BombFireball";

            // Bỏ collider để không ảnh hưởng gì tới physics của phòng.
            Collider fireballCollider = fireball.GetComponent<Collider>();
            if (fireballCollider != null) Destroy(fireballCollider);

            fireball.transform.position = worldPosition;

            MeshRenderer fireballRenderer = fireball.GetComponent<MeshRenderer>();
            if (fireballRenderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    Material material = new Material(shader);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", explosionBurstColor);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", explosionBurstColor);
                    fireballRenderer.sharedMaterial = material;
                }
            }

            return fireball;
        }

        /// <summary>
        /// Creates the point light that lights the room up for a moment when the bomb blows.
        /// </summary>
        private GameObject SpawnFlashLight(Vector3 worldPosition)
        {
            GameObject flashLightObject = new GameObject("BombFlashLight");
            flashLightObject.transform.position = worldPosition;

            Light flashLight = flashLightObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = explosionBurstColor;
            flashLight.range = explosionLightRange;
            flashLight.intensity = 0f;
            flashLight.shadows = LightShadows.None;

            return flashLightObject;
        }

        /// <summary>
        /// Creates a self-contained overlay canvas used for the full screen detonation flash.
        /// </summary>
        private CanvasGroup SpawnScreenFlash()
        {
            GameObject overlayObject = new GameObject("BombScreenFlash", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));

            Canvas overlayCanvas = overlayObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 5000;

            GameObject flashObject = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashObject.transform.SetParent(overlayObject.transform, false);

            RectTransform flashRect = flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;

            Image flashImage = flashObject.GetComponent<Image>();
            flashImage.color = explosionScreenFlashColor;
            flashImage.raycastTarget = false;

            CanvasGroup canvasGroup = overlayObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = explosionScreenFlashAlpha;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            return canvasGroup;
        }

        public void DisposeTweens()
        {
            StopAllCoroutines();
            if (bombModel != null) bombModel.DOKill();

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].ResetButton();
                }
            }
        }
    }
}