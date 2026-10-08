using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SilentSprint.Gameplay.AI;

namespace SilentSprint.Gameplay
{
    /// <summary>
    /// AI cua quai, kien truc HYBRID hai lop:
    ///
    ///   LOP QUYET DINH - <see cref="MonsterBrain"/> (model ONNX qua Inference Engine)
    ///       Quyet dinh nen Patrol / Investigate / Chase, va 4 tham so tinh cach
    ///       (aggression, searchRadius, flank, alertness). Chay o 10Hz.
    ///
    ///   LOP PHAN XA - chinh file nay
    ///       Cac luat cung luon thang model: khoang cach bat duoc, nhin thay o cu ly sat,
    ///       tieng dong ngay canh, va cac "chan doan an toan" (khong the duoi thu no khong biet).
    ///       Nho vay model co the "ngoc" ma game khong vo.
    ///
    /// NavMeshAgent van chiu trach nhiem tim duong; brain chi chon muc tieu va sac thai.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        public enum MonsterState { Patrol, Investigate, Chase }

        [Header("Current State")]
        public MonsterState currentState = MonsterState.Patrol;

        [Header("Movement Settings")]
        public float patrolSpeed = 3.5f;
        public float chaseSpeed = 9f;
        public float stalkRadius = 25f;

        [Header("Senses")]
        public Transform player;
        public float sightDistance = 20f;
        public float sightAngle = 45f;
        public float hearingDistance = 25f;
        public float eyeHeight = 2.5f;
        public float catchDistance = 1.6f;
        [Tooltip("Layer cua cac VAT CAN tam nhin (tuong, cua, dia hinh) - vi du Default | Obstacle. " +
                 "KHONG duoc chua layer Player: tia kiem tra di tu mat quai toi player, de Player trong mask " +
                 "thi tia luon dung ngay tren nguoi player va quai khong bao gio thay gi.")]
        public LayerMask obstacleMask;

        [Header("Brain (ONNX / Inference Engine)")]
        [Tooltip("Bo trong thi tu tim MonsterBrain tren cung GameObject, neu khong co thi tu them vao. " +
                 "Khi do file Assets/Resources/MonsterBrain/monster_brain.onnx se duoc nap tu dong.")]
        public MonsterBrain brain;

        [Header("Reflex Layer (luon thang quyet dinh cua brain)")]
        [Tooltip("Thay player trong khoang sightDistance * he so nay thi bat buoc chuyen sang Chase.")]
        [Range(0f, 1f)]
        public float pointBlankChaseFactor = 0.4f;

        [Tooltip("Nghe tieng dong trong khoang hearingDistance * he so nay thi it nhat cung phai Investigate.")]
        [Range(0f, 1f)]
        public float closeAudibleRange = 0.5f;

        [Tooltip("Bien do uu tien doi trang thai. Cang cao thi quai cang i 'do du' truoc khi doi muc tieu.")]
        [Range(0f, 1f)]
        public float stateSwitchMargin = 0.15f;

        [Header("Search Behaviour")]
        [Tooltip("Ban kinh toi da (met) cua vong luc soat quanh vi tri nho duoc, nhan voi searchRadius cua brain.")]
        public float maxSearchRadius = 12f;
        public float searchDurationMin = 3f;
        public float searchDurationMax = 8f;

        [Header("Chase Behaviour")]
        [Tooltip("So giay du doan ve phia truoc theo van toc player khi brain yeu cau cat duong.")]
        public float flankPredictionTime = 1.2f;

        [Header("Patrol")]
        public float patrolWaitTime = 2f;

        [Header("Debug")]
        public bool logStateChanges;
        public bool logBrainDecision;

        private NavMeshAgent agent;
        private Animator animator;
        private PlayerController playerController;

        private readonly MonsterPerception perception = new MonsterPerception();

        private Vector3 investigateTarget;
        private float waitTimer;
        private float searchTimer;
        private bool isSearching;
        private bool hasCaughtPlayer;

        /// <summary>Hội thoại đang mở nên quái bị đóng băng (xem <see cref="Pause"/>).</summary>
        public bool IsPaused { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

            if (brain == null) brain = GetComponent<MonsterBrain>();
            if (brain == null)
            {
                brain = gameObject.AddComponent<MonsterBrain>();
                Debug.Log("[MonsterAI] Chua co MonsterBrain tren prefab - da tu them vao luc chay. " +
                          "Them component nay vao prefab de cau hinh duoc trong Inspector.", this);
            }

            // Chi bat them khi nguoi dung yeu cau tu MonsterAI; khong ghi de cau hinh san co tren MonsterBrain.
            if (logBrainDecision) brain.logDecisions = true;
        }

        private void Start()
        {
            if (player == null)
            {
                playerController = FindAnyObjectByType<PlayerController>();
                if (playerController != null)
                {
                    player = playerController.transform;
                }
                else
                {
                    Debug.LogError("PlayerController not found in the Scene!");
                }
            }
            else
            {
                playerController = player.GetComponent<PlayerController>();
            }

            SyncPerceptionSettings();
            perception.Bind(playerController);

            if (brain != null && !brain.IsInitialised)
                brain.Initialise();

            if (logBrainDecision)
            {
                Debug.Log(string.Format("[MonsterAI] Nguon quyet dinh: {0}",
                    brain != null && brain.IsModelDriven ? "ONNX model (Inference Engine)" : "chinh sach viet tay"), this);
            }
        }

        private void Update()
        {
            // Hội thoại đang mở: đóng băng quái để người chơi không bị tóm trong lúc đọc.
            if (IsPaused) return;

            if (player == null || hasCaughtPlayer) return;

            SyncPerceptionSettings();
            perception.Bind(playerController);
            perception.Sample(transform, brain != null ? brain.Decision.Alertness : 0.25f, Time.deltaTime);

            if (brain != null)
                brain.Tick(BuildObservation(), Time.deltaTime);

            UpdateStateFromDecision();
            UpdateAnimation();

            switch (currentState)
            {
                case MonsterState.Patrol:
                    PatrolBehavior();
                    break;
                case MonsterState.Investigate:
                    InvestigateBehavior();
                    break;
                case MonsterState.Chase:
                    ChaseBehavior();
                    break;
            }
        }

        // ------------------------------------------------------------------ Dong bang

        /// <summary>
        /// Đóng băng quái: dừng NavMeshAgent và tư thế đi, nhưng giữ nguyên đường đang đi
        /// nên <see cref="Resume"/> xong quái tiếp tục như cũ. Cũng chặn luôn
        /// MonsterBrain.Tick vì Tick được gọi từ Update.
        /// </summary>
        public void Pause()
        {
            if (IsPaused) return;

            IsPaused = true;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (animator != null)
            {
                animator.SetBool("isWalking", false);
                animator.speed = 1f;
            }
        }

        /// <summary>Cho quái chạy tiếp sau khi hết đóng băng.</summary>
        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;

            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        // Quái đã bị PauseAll đóng băng, để ResumeAll chỉ thả đúng những con đó.
        private static readonly List<MonsterAI> FrozenByAll = new List<MonsterAI>();

        /// <summary>
        /// Đóng băng toàn bộ quái đang hoạt động trong scene. Gọi khi hội thoại mở hoặc khi
        /// vào mini game — lúc người chơi không điều khiển được nhân vật.
        /// </summary>
        public static void PauseAll()
        {
            FrozenByAll.RemoveAll(monster => monster == null);

            MonsterAI[] monsters = FindObjectsByType<MonsterAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < monsters.Length; i++)
            {
                if (monsters[i] == null || monsters[i].IsPaused) continue;

                monsters[i].Pause();
                FrozenByAll.Add(monsters[i]);
            }
        }

        /// <summary>Thả đúng những con quái đã bị <see cref="PauseAll"/> đóng băng.</summary>
        public static void ResumeAll()
        {
            for (int i = 0; i < FrozenByAll.Count; i++)
            {
                if (FrozenByAll[i] != null) FrozenByAll[i].Resume();
            }

            FrozenByAll.Clear();
        }

        // ------------------------------------------------------------------ Giac quan

        private void SyncPerceptionSettings()
        {
            perception.SightDistance = sightDistance;
            perception.SightAngle = sightAngle;
            perception.EyeHeight = eyeHeight;
            perception.HearingDistance = hearingDistance;
            perception.StalkRadius = stalkRadius;
            perception.ObstacleMask = obstacleMask;
        }

        private MonsterObservation BuildObservation()
        {
            float sight = Mathf.Max(0.001f, perception.EffectiveSightDistance);
            float hear = Mathf.Max(0.001f, hearingDistance);
            float runReference = Mathf.Max(0.001f, perception.RunSpeedReference);

            var observation = new MonsterObservation();
            observation.DistNorm = Mathf.Clamp(perception.DistanceToPlayer / sight, 0f, 2f);
            observation.AngleNorm = Mathf.Clamp01(perception.AngleToPlayer / 180f);
            observation.LosClear = perception.LineOfSightClear ? 1f : 0f;
            observation.InSightCone = perception.InSightCone ? 1f : 0f;
            observation.RelDirX = perception.RelativeDirection.x;
            observation.RelDirZ = perception.RelativeDirection.z;
            observation.PlayerSpeedNorm = Mathf.Clamp01(perception.PlayerSpeed / runReference);
            observation.PlayerNoise = Mathf.Clamp01(perception.PlayerNoise);
            observation.StaminaNorm = Mathf.Clamp01(perception.PlayerStaminaNorm);
            observation.FlashlightOn = perception.FlashlightOn ? 1f : 0f;
            observation.Proximity = Mathf.Clamp01(perception.DistanceToPlayer / hear);
            observation.TimeSinceSeen = perception.TimeSinceLastSeenNormalized;
            observation.HasLastKnown = perception.HasLastKnownPosition ? 1f : 0f;
            observation.LastKnownDist = perception.HasLastKnownPosition
                ? Mathf.Clamp(Vector3.Distance(transform.position, perception.LastKnownPosition) /
                              Mathf.Max(1f, stalkRadius), 0f, 1.5f)
                : 0f;
            observation.PrevState = (float)currentState / 2f;
            return observation;
        }

        // ------------------------------------------------------------------ Quyet dinh + phan xa

        private void UpdateStateFromDecision()
        {
            SetState(ResolveDecisionState());

            // --- LOP PHAN XA -----------------------------------------------------------------
            // Cac luat duoi day khong the bi model ghi de, va chay sau cung nen luon thang.
            bool veryClose = perception.DistanceToPlayer <= catchDistance * 3f;
            bool pointBlank = perception.DistanceToPlayer <= sightDistance * pointBlankChaseFactor;

            if (perception.PlayerVisible && (veryClose || pointBlank))
            {
                SetState(MonsterState.Chase);
            }
            else if (perception.PlayerHeard &&
                     perception.DistanceToPlayer <= hearingDistance * closeAudibleRange &&
                     currentState == MonsterState.Patrol)
            {
                SetState(MonsterState.Investigate);
            }
        }

        /// <summary>
        /// Doi quyet dinh tho cua brain thanh trang thai FSM, sau khi da loc bo cac lua chon
        /// khong the xay ra ("chan doan an toan") va ap dung do tre doi trang thai.
        /// </summary>
        private MonsterState ResolveDecisionState()
        {
            if (brain == null) return currentState;

            MonsterDecision decision = brain.Decision;

            float patrol = Mathf.Clamp01(decision.PatrolProbability);
            float investigate = Mathf.Clamp01(decision.InvestigateProbability);
            float chase = Mathf.Clamp01(decision.ChaseProbability);

            // Chan doan an toan: khong the chon duoi mot thu ma no khong nhin thay.
            if (!perception.PlayerVisible)
            {
                chase = 0f;
                if (!perception.HasLastKnownPosition && !perception.PlayerHeard)
                    investigate = 0f;
            }

            int bestIndex = MonsterBrainOutputLayout.StatePatrol;
            float best = patrol;
            if (investigate > best)
            {
                best = investigate;
                bestIndex = MonsterBrainOutputLayout.StateInvestigate;
            }
            if (chase > best)
            {
                best = chase;
                bestIndex = MonsterBrainOutputLayout.StateChase;
            }

            var bestState = (MonsterState)bestIndex;

            // Do tre doi trang thai: tranh rung lac khi hai lua chon xap xi nhau.
            if (bestState != currentState)
            {
                float incumbent = ProbabilityForState(patrol, investigate, chase, currentState);
                if (best < incumbent + stateSwitchMargin)
                    return currentState;
            }

            return bestState;
        }

        private static float ProbabilityForState(float patrol, float investigate, float chase, MonsterState state)
        {
            switch (state)
            {
                case MonsterState.Investigate: return investigate;
                case MonsterState.Chase: return chase;
                default: return patrol;
            }
        }

        private void SetState(MonsterState next)
        {
            if (next == currentState) return;

            MonsterState previous = currentState;
            currentState = next;

            waitTimer = 0f;
            isSearching = false;

            switch (next)
            {
                case MonsterState.Investigate:
                    investigateTarget = perception.HasLastKnownPosition
                        ? perception.LastKnownPosition
                        : player.position;
                    searchTimer = Mathf.Lerp(searchDurationMin, searchDurationMax,
                        Mathf.Clamp01(brain != null ? brain.Decision.Alertness : 0.25f));
                    SearchFromHere();
                    break;

                case MonsterState.Chase:
                    agent.isStopped = false;
                    break;
            }

            if (logStateChanges)
                Debug.Log(string.Format("[MonsterAI] {0} -> {1}", previous, next), this);
        }

        // ------------------------------------------------------------------ Hanh vi

        private void PatrolBehavior()
        {
            float aggression = brain != null ? Mathf.Clamp01(brain.Decision.Aggression) : 0.2f;
            agent.isStopped = false;
            agent.speed = Mathf.Lerp(patrolSpeed * 0.8f, patrolSpeed, aggression);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= patrolWaitTime)
                {
                    // Di tuan quanh player, nhung lech ve phia lan cuoi con thay player.
                    Vector3 anchor = perception.HasLastKnownPosition
                        ? Vector3.Lerp(player.position, perception.LastKnownPosition, 0.5f)
                        : player.position;

                    Vector3 randomDirection = Random.insideUnitSphere * stalkRadius;
                    randomDirection += anchor;

                    if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, stalkRadius, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                    waitTimer = 0f;
                }
            }
        }

        private void InvestigateBehavior()
        {
            float aggression = brain != null ? Mathf.Clamp01(brain.Decision.Aggression) : 0.2f;
            agent.isStopped = false;
            agent.speed = Mathf.Lerp(patrolSpeed * 0.85f, patrolSpeed * 1.35f, aggression);

            if (!isSearching)
            {
                // Con dang tren duong toi cho nguoi ta thay lan cuoi.
                if (perception.HasLastKnownPosition && perception.TimeSinceLastSeen < 0.5f)
                {
                    investigateTarget = perception.LastKnownPosition;
                    agent.SetDestination(investigateTarget);
                }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    isSearching = true;
                    waitTimer = 0f;
                }
                return;
            }

            // Pha luc soat: di loan quanh vi tri cuoi trong ban kinh ty le voi searchRadius.
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f)
            {
                SetState(MonsterState.Patrol);
                return;
            }

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= 1f)
                {
                    float radius = Mathf.Max(2f, brain.Decision.SearchRadius * maxSearchRadius);
                    Vector3 candidate = investigateTarget + Random.insideUnitSphere * radius;
                    candidate.y = investigateTarget.y;

                    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);

                    waitTimer = 0f;
                }
            }
        }

        private void ChaseBehavior()
        {
            float aggression = brain != null ? Mathf.Clamp01(brain.Decision.Aggression) : 1f;
            float flank = brain != null ? Mathf.Clamp01(brain.Decision.Flank) : 0f;

            agent.isStopped = false;
            agent.speed = Mathf.Lerp(patrolSpeed, chaseSpeed, aggression);

            Vector3 destination = player.position;

            // Cat duong: doan truoc vi tri player theo van toc hien tai.
            if (flank > 0.35f && perception.PlayerVelocity.sqrMagnitude > 0.01f)
            {
                destination += perception.PlayerVelocity * (flankPredictionTime * flank);
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    destination = hit.position;
            }

            agent.SetDestination(destination);

            if (perception.DistanceToPlayer <= catchDistance)
            {
                CatchPlayer();
            }
        }

        private void CatchPlayer()
        {
            hasCaughtPlayer = true;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            if (animator != null)
            {
                animator.SetBool("isWalking", false);
                animator.speed = 1f;
            }

            if (JumpscareController.Instance != null)
            {
                JumpscareController.Instance.TriggerCaughtJumpscare(transform.position, gameObject);
            }
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;

            float speed = agent.velocity.magnitude;
            animator.SetBool("isWalking", speed > 0.1f);

            if (speed > 0.1f)
            {
                animator.speed = speed / patrolSpeed;
            }
            else
            {
                animator.speed = 1f;
            }
        }

        private void SearchFromHere()
        {
            if (!agent.isOnNavMesh)
                return;

            if (NavMesh.SamplePosition(investigateTarget, out NavMeshHit hit, stalkRadius, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
            else
                agent.SetDestination(investigateTarget);
        }

        // ------------------------------------------------------------------ Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightDistance);

            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Vector3 leftBoundary = Quaternion.Euler(0, -sightAngle, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, sightAngle, 0) * transform.forward;

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(eyePosition, leftBoundary * sightDistance);
            Gizmos.DrawRay(eyePosition, rightBoundary * sightDistance);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, hearingDistance);

            if (player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyePosition, player.position + Vector3.up);
            }

            // Chi hien khi dang chay de thay brain dang lam gi.
            if (!Application.isPlaying) return;

            if (perception.HasLastKnownPosition)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(perception.LastKnownPosition, 0.6f);
                Gizmos.DrawLine(transform.position + Vector3.up, perception.LastKnownPosition + Vector3.up);
            }

            if (currentState == MonsterState.Investigate)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
                float radius = brain != null
                    ? Mathf.Max(2f, brain.Decision.SearchRadius * maxSearchRadius)
                    : maxSearchRadius;
                Gizmos.DrawWireSphere(investigateTarget, radius);
            }

            if (currentState == MonsterState.Chase && perception.PlayerVelocity.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(player.position,
                    player.position + perception.PlayerVelocity * flankPredictionTime);
            }
        }
    }
}
