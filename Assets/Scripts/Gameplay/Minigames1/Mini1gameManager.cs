using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using Random = UnityEngine.Random;

namespace SilentSprint.Gameplay.Minigames1
{
    public class Mini1gameManager : MonoBehaviour
    {
        [Header("Cup Setup")]
        public CupItem[] cups;
        public Transform ball;
        public Camera roomCamera;

        [Header("Current Progress")]
        [SerializeField] private int currentRound = 1;
        private const int TOTAL_ROUNDS = 5;

        [Header("Runtime Tuning")]
        private float liftHeight = 2.0f;
        private int shuffleSwapsCount = 6;
        private float swapDuration = 0.42f;

        public event Action OnGameWon;
        public event Action OnGameLost;

        private int ballCupIndex;
        private bool canGuess = false;
        private Vector3[] slotPositions;
        private bool isInitialized = false;
        private float tableY;

        void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            if (cups != null && cups.Length > 0)
            {
                slotPositions = new Vector3[cups.Length];
                for (int i = 0; i < cups.Length; i++)
                {
                    if (cups[i] != null)
                    {
                        slotPositions[i] = cups[i].transform.position;
                        cups[i].Setup(i, this);
                    }
                }
            }

            if (roomCamera != null)
            {
                roomCamera.gameObject.SetActive(false);
            }

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
            StartCoroutine(PlayRoundRoutine());
        }

        private void ConfigureRoundDifficulty(int round)
        {
            switch (round)
            {
                case 1:
                    shuffleSwapsCount = 6;
                    swapDuration = 0.42f;
                    liftHeight = 2.0f;
                    break;
                case 2:
                    shuffleSwapsCount = 9;
                    swapDuration = 0.32f;
                    liftHeight = 1.9f;
                    break;
                case 3:
                    shuffleSwapsCount = 12;
                    swapDuration = 0.24f;
                    liftHeight = 1.7f;
                    break;
                case 4:
                    shuffleSwapsCount = 15;
                    swapDuration = 0.18f;
                    liftHeight = 1.5f;
                    break;
                case 5:
                    shuffleSwapsCount = 18;
                    swapDuration = 0.13f;
                    liftHeight = 1.3f;
                    break;
            }
        }

        private IEnumerator PlayRoundRoutine()
        {
            canGuess = false;

            if (cups == null || cups.Length == 0 || slotPositions == null || ball == null)
            {
                yield break;
            }

            ConfigureRoundDifficulty(currentRound);

            for (int i = 0; i < cups.Length; i++)
            {
                if (cups[i] != null)
                {
                    cups[i].transform.position = slotPositions[i];
                    cups[i].ResetPosition();
                }
            }

            tableY = ball.position.y;

            ballCupIndex = Random.Range(0, cups.Length);
            ball.gameObject.SetActive(true);

            Vector3 targetCupPos = cups[ballCupIndex].transform.position;
            ball.position = new Vector3(targetCupPos.x, tableY, targetCupPos.z);

            yield return new WaitForSeconds(0.4f);

            yield return cups[ballCupIndex].LiftUp(liftHeight, 0.45f).WaitForCompletion();
            yield return new WaitForSeconds(0.9f);
            yield return cups[ballCupIndex].PutDown(0.25f).WaitForCompletion();

            yield return new WaitForSeconds(0.3f);

            for (int step = 0; step < shuffleSwapsCount; step++)
            {
                int idxA = Random.Range(0, cups.Length);
                int idxB = Random.Range(0, cups.Length);
                while (idxA == idxB) idxB = Random.Range(0, cups.Length);

                yield return SwapTwoCups(idxA, idxB, swapDuration);
            }

            canGuess = true;
        }

        private IEnumerator SwapTwoCups(int indexA, int indexB, float duration)
        {
            CupItem cupA = cups[indexA];
            CupItem cupB = cups[indexB];

            Vector3 posA = cupA.transform.position;
            Vector3 posB = cupB.transform.position;

            Vector3 midA = (posA + posB) / 2f + Vector3.forward * 0.25f;
            Vector3 midB = (posA + posB) / 2f - Vector3.forward * 0.25f;

            cupA.transform.DOPath(new Vector3[] { midA, posB }, duration, PathType.CatmullRom).SetEase(Ease.Linear);
            cupB.transform.DOPath(new Vector3[] { midB, posA }, duration, PathType.CatmullRom).SetEase(Ease.Linear);

            if (ballCupIndex == indexA)
            {
                Vector3 ballMid = new Vector3(midA.x, tableY, midA.z);
                Vector3 ballDest = new Vector3(posB.x, tableY, posB.z);
                ball.DOPath(new Vector3[] { ballMid, ballDest }, duration, PathType.CatmullRom).SetEase(Ease.Linear);
                ballCupIndex = indexB;
            }
            else if (ballCupIndex == indexB)
            {
                Vector3 ballMid = new Vector3(midB.x, tableY, midB.z);
                Vector3 ballDest = new Vector3(posA.x, tableY, posA.z);
                ball.DOPath(new Vector3[] { ballMid, ballDest }, duration, PathType.CatmullRom).SetEase(Ease.Linear);
                ballCupIndex = indexA;
            }

            cups[indexA] = cupB;
            cups[indexB] = cupA;

            yield return new WaitForSeconds(duration);
        }

        void Update()
        {
            if (!canGuess) return;

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
                CupItem clickedCup = hit.collider.GetComponent<CupItem>();
                if (clickedCup != null)
                {
                    OnCupSelected(clickedCup);
                }
            }
        }

        public void OnCupSelected(CupItem selectedCup)
        {
            if (!canGuess) return;
            canGuess = false;

            StartCoroutine(EvaluateChoice(selectedCup));
        }

        private IEnumerator EvaluateChoice(CupItem selectedCup)
        {
            yield return selectedCup.LiftUp(liftHeight, 0.35f).WaitForCompletion();

            Vector2 cupXZ = new Vector2(selectedCup.transform.position.x, selectedCup.transform.position.z);
            Vector2 ballXZ = new Vector2(ball.position.x, ball.position.z);
            bool isCorrect = Vector2.Distance(cupXZ, ballXZ) < 0.3f;

            yield return new WaitForSeconds(0.7f);

            if (isCorrect)
            {
                yield return selectedCup.PutDown(0.25f).WaitForCompletion();

                if (currentRound < TOTAL_ROUNDS)
                {
                    currentRound++;
                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(PlayRoundRoutine());
                }
                else
                {
                    if (roomCamera != null) roomCamera.gameObject.SetActive(false);
                    OnGameWon?.Invoke();
                }
            }
            else
            {
                if (roomCamera != null) roomCamera.gameObject.SetActive(false);
                OnGameLost?.Invoke();
            }
        }

        public void DisposeTweens()
        {
            StopAllCoroutines();

            if (ball != null)
            {
                ball.DOKill();
            }

            if (cups != null)
            {
                for (int i = 0; i < cups.Length; i++)
                {
                    if (cups[i] != null)
                    {
                        cups[i].transform.DOKill();
                    }
                }
            }
        }
    }
}