using UnityEngine;
using UnityEngine.AI;

namespace SilentSprint.Gameplay
{
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
        public LayerMask obstacleMask;

        private NavMeshAgent agent;
        private Animator animator;
        private PlayerController playerController;

        private Vector3 investigateTarget;
        private float waitTimer = 0f;
        private float patrolWaitTime = 2f;
        private bool hasCaughtPlayer = false;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

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
        }

        void Update()
        {
            if (player == null || hasCaughtPlayer) return;

            UpdateAnimation();
            CheckSenses();

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

        private void CheckSenses()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            // 1. VISUAL CHECK
            if (distanceToPlayer <= sightDistance)
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToPlayer);

                if (angle < sightAngle)
                {
                    Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;

                    if (!Physics.Raycast(eyePosition, directionToPlayer, distanceToPlayer, obstacleMask))
                    {
                        currentState = MonsterState.Chase;
                        return;
                    }
                }
            }

            // 2. HEARING CHECK
            if (currentState != MonsterState.Chase && distanceToPlayer <= hearingDistance)
            {
                Animator playerAnim = player.GetComponentInChildren<Animator>();
                if (playerAnim != null && playerAnim.GetBool("isRunning"))
                {
                    investigateTarget = player.position;
                    currentState = MonsterState.Investigate;
                }
            }

            // 3. LOSING THE TARGET
            if (currentState == MonsterState.Chase && distanceToPlayer > sightDistance * 1.5f)
            {
                currentState = MonsterState.Investigate;
                investigateTarget = player.position;
            }
        }

        private void PatrolBehavior()
        {
            agent.speed = patrolSpeed;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= patrolWaitTime)
                {
                    Vector3 randomDirection = Random.insideUnitSphere * stalkRadius;
                    randomDirection += player.position;

                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(randomDirection, out hit, stalkRadius, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                    waitTimer = 0f;
                }
            }
        }

        private void InvestigateBehavior()
        {
            agent.speed = patrolSpeed;
            agent.SetDestination(investigateTarget);

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= 3f)
                {
                    currentState = MonsterState.Patrol;
                    waitTimer = 0f;
                }
            }
        }

        private void ChaseBehavior()
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= catchDistance)
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

            if (player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eyePosition, player.position + Vector3.up);
            }
        }
    }
}