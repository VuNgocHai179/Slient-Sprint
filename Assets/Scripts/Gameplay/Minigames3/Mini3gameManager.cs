using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

        [Header("Screen UI")]
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI remainingText;
        public TextMeshProUGUI dangerRateText;

        public event Action OnGameWon;
        public event Action OnGameLost;

        private int explosiveButtonIndex;
        private List<int> availableButtonIndices = new List<int>();
        private bool isPlayerTurn = false;
        private bool isGameOver = false;

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
                    bombModel.DOShakePosition(0.8f, 0.25f, 30, 90, false, true);
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
            yield return new WaitForSeconds(0.7f);

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