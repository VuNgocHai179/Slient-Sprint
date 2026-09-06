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
        public float stalkRadius = 25f; // Radius to roam around the Player's area

        [Header("Senses")]
        public Transform player;
        public float sightDistance = 20f;     // Sight range
        public float sightAngle = 45f;        // Field of view angle (Total 90 degrees)
        public float hearingDistance = 25f;   // Distance to hear the player running
        public LayerMask obstacleMask;        // Layer of walls (to prevent seeing through walls)

        private NavMeshAgent agent;
        private Animator animator;
        private PlayerController playerController;

        private Vector3 investigateTarget;
        private float waitTimer = 0f;
        private float patrolWaitTime = 2f;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

            // FIND PLAYER VIA SCRIPT INSTEAD OF USING TAGS
            if (player == null)
            {
                // Find the object in the scene with the PlayerController script attached
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
            if (player == null) return;

            UpdateAnimation();
            CheckSenses();

            // Execute logic based on the current state
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

        // --- SENSES ---
        private void CheckSenses()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            // 1. VISUAL CHECK (Seeing the Player)
            if (distanceToPlayer <= sightDistance)
            {
                Vector3 directionToPlayer = (player.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToPlayer);

                // Within field of view
                if (angle < sightAngle)
                {
                    // Raycast to check for obstacles (walls)
                    if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, obstacleMask))
                    {
                        currentState = MonsterState.Chase; // Spotted! Switch to chase immediately
                        return; // Highest priority, exit function
                    }
                }
            }

            // 2. HEARING CHECK (Hearing the Player run)
            if (currentState != MonsterState.Chase && distanceToPlayer <= hearingDistance)
            {
                // Check the Player's Animator to see if isRunning is true
                Animator playerAnim = player.GetComponentInChildren<Animator>();
                if (playerAnim != null && playerAnim.GetBool("isRunning"))
                {
                    investigateTarget = player.position; // Remember the location of the noise
                    currentState = MonsterState.Investigate;
                }
            }

            // 3. LOSING THE TARGET (Running too far out of sight)
            if (currentState == MonsterState.Chase && distanceToPlayer > sightDistance * 1.5f)
            {
                currentState = MonsterState.Investigate; // Lost track, stand and investigate
                investigateTarget = player.position;
            }
        }

        // --- AI STATES ---
        private void PatrolBehavior()
        {
            agent.speed = patrolSpeed;

            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer >= patrolWaitTime)
                {
                    // Random point around the PLAYER'S POSITION to always stay close indirectly
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
                // Arrived and found nothing, look around a bit then resume patrol
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
            agent.SetDestination(player.position); // Rush straight to the Player
        }

        // --- ANIMATION ---
        private void UpdateAnimation()
        {
            if (animator == null) return;

            float speed = agent.velocity.magnitude;

            // Enable walking animation if moving
            animator.SetBool("isWalking", speed > 0.1f);

            // Fast-forward footstep speed when chasing to create a terrifying effect
            if (speed > 0.1f)
            {
                animator.speed = speed / patrolSpeed;
            }
            else
            {
                animator.speed = 1f;
            }
        }
    }
}