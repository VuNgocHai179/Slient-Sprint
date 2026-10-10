using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Random = UnityEngine.Random;

namespace SilentSprint.Gameplay.Minigames2
{
    public class Mini2gameManager : MonoBehaviour
    {
        [Header("Buttons Setup")]
        public RuneButtonItem[] buttons;
        public Camera roomCamera;

        [Header("Cheat")]
        [Tooltip("Nút cheat trong phòng (object btn_cheat): bấm là thắng ngay ván này. Để trống sẽ tự tìm theo tên.")]
        public Button btnCheat;

        [Header("Screen UI")]
        public CanvasGroup bgCanvasGroup;
        public GameObject setGamePanel;
        public Image[] displaySlots;
        public Sprite[] runeIcons;

        [Header("Progress Tracking")]
        [SerializeField] private int currentRound = 1;
        private const int TOTAL_ROUNDS = 5;

        [Header("Runtime Tuning")]
        private int sequenceLength = 4;
        private float displayDuration = 4.5f;

        public event Action OnGameWon;
        public event Action OnGameLost;

        private List<int> targetSequence = new List<int>();
        private int currentInputIndex = 0;
        private bool canInteract = false;
        private bool isFinished = false;
        private bool isInitialized = false;

        void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Setup(i, this);
                }
            }

            if (roomCamera != null)
            {
                roomCamera.gameObject.SetActive(false);
            }

            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.gameObject.SetActive(false);
                bgCanvasGroup.alpha = 0f;
            }

            if (setGamePanel != null)
            {
                setGamePanel.SetActive(false);
            }

            MinigameCheatButton.Bind(this, btnCheat, ForceWin);

            isInitialized = true;
        }

        public void StartMinigame()
        {
            Initialize();

            if (roomCamera != null)
            {
                roomCamera.gameObject.SetActive(true);
            }

            currentRound = 1;
            isFinished = false;
            StartCoroutine(PlayRoundRoutine());
        }

        private void ConfigureRoundDifficulty(int round)
        {
            sequenceLength = 8;

            switch (round)
            {
                case 1:
                    displayDuration = 6.0f;
                    break;
                case 2:
                    displayDuration = 5.0f;
                    break;
                case 3:
                    displayDuration = 4.0f;
                    break;
                case 4:
                    displayDuration = 3.0f;
                    break;
                case 5:
                    displayDuration = 2.2f;
                    break;
            }
        }

        private IEnumerator PlayRoundRoutine()
        {
            canInteract = false;
            currentInputIndex = 0;
            targetSequence.Clear();

            ConfigureRoundDifficulty(currentRound);

            for (int i = 0; i < sequenceLength; i++)
            {
                int randomId = Random.Range(0, buttons.Length);
                targetSequence.Add(randomId);
            }

            for (int i = 0; i < displaySlots.Length; i++)
            {
                if (i < targetSequence.Count)
                {
                    displaySlots[i].gameObject.SetActive(true);
                    displaySlots[i].sprite = runeIcons[targetSequence[i]];
                }
                else
                {
                    displaySlots[i].gameObject.SetActive(false);
                }
            }

            if (bgCanvasGroup != null)
            {
                bgCanvasGroup.gameObject.SetActive(true);
                bgCanvasGroup.alpha = 0f;

                yield return bgCanvasGroup.DOFade(1f, 0.6f).WaitForCompletion();

                yield return new WaitForSeconds(1.0f);

                yield return bgCanvasGroup.DOFade(0.25f, 0.12f).WaitForCompletion();
                yield return bgCanvasGroup.DOFade(1f, 0.12f).WaitForCompletion();
                yield return bgCanvasGroup.DOFade(0.15f, 0.12f).WaitForCompletion();
                yield return bgCanvasGroup.DOFade(0.9f, 0.12f).WaitForCompletion();

                yield return new WaitForSeconds(0.3f);

                yield return bgCanvasGroup.DOFade(0f, 0.6f).WaitForCompletion();
                bgCanvasGroup.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(0.25f);

            if (setGamePanel != null)
            {
                setGamePanel.SetActive(true);
            }

            yield return new WaitForSeconds(displayDuration);

            if (setGamePanel != null)
            {
                setGamePanel.SetActive(false);
            }

            canInteract = true;
        }

        void Update()
        {
            if (!canInteract) return;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        return;

                    CheckRaycastHit(touch.position);
                }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                CheckRaycastHit(Input.mousePosition);
            }
        }

        private void CheckRaycastHit(Vector2 screenPos)
        {
            if (roomCamera == null) return;

            Ray ray = roomCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
            {
                RuneButtonItem btn = hit.collider.GetComponent<RuneButtonItem>();
                if (btn != null)
                {
                    btn.OnPressed();
                }
            }
        }

        public void OnRuneButtonPressed(int pressedButtonId)
        {
            if (!canInteract) return;

            if (pressedButtonId == targetSequence[currentInputIndex])
            {
                currentInputIndex++;

                if (currentInputIndex >= targetSequence.Count)
                {
                    canInteract = false;
                    StartCoroutine(HandleRoundSuccessRoutine());
                }
            }
            else
            {
                canInteract = false;
                StartCoroutine(HandleLoseRoutine());
            }
        }

        private IEnumerator HandleRoundSuccessRoutine()
        {
            yield return new WaitForSeconds(0.4f);

            if (currentRound < TOTAL_ROUNDS)
            {
                currentRound++;
                yield return new WaitForSeconds(0.5f);
                StartCoroutine(PlayRoundRoutine());
            }
            else
            {
                FinishGame(won: true);
            }
        }

        private IEnumerator HandleLoseRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            FinishGame(won: false);
        }

        /// <summary>
        /// Kết thúc ván: ẩn camera phòng rồi bắn sự kiện thắng/thua. Mọi đường ra khỏi ván đều đi qua đây
        /// (thắng thật, thua, và nút cheat) nên mỗi ván chỉ kết thúc được một lần.
        /// </summary>
        private void FinishGame(bool won)
        {
            if (isFinished) return;
            isFinished = true;
            canInteract = false;

            if (roomCamera != null) roomCamera.gameObject.SetActive(false);

            if (won) OnGameWon?.Invoke();
            else OnGameLost?.Invoke();
        }

        /// <summary>Nút cheat trong phòng: thắng ngay ván này, đi đúng đường thắng thật.</summary>
        public void ForceWin()
        {
            FinishGame(won: true);
        }

        public void DisposeTweens()
        {
            StopAllCoroutines();
            if (bgCanvasGroup != null) bgCanvasGroup.DOKill();

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].ResetPosition();
                }
            }
        }
    }
}