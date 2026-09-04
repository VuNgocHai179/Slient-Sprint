using UnityEngine;
using UnityEngine.AI;

namespace SilentSprint.Gameplay
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Patrol Settings")]
        public float patrolRadius = 30f; // The radius within which the monster will search for a new point
        public float waitTime = 2f;      // Wait time at the destination before moving again

        private NavMeshAgent agent;
        private Animator animator;       // Reference to the Animator component
        private float timer;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>(); // Get the Animator attached to the monster
            timer = waitTime;
        }

        void Update()
        {
            // If the agent's velocity is greater than 0.1, it means the monster is currently moving
            bool isMoving = agent.velocity.magnitude > 0.1f;

            // Send the boolean value to the Animator parameter named "isWalking"
            if (animator != null)
            {
                animator.SetBool("isWalking", isMoving);
            }
            // -----------------------

            // Check if the monster has reached its destination
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                timer += Time.deltaTime;

                // If the wait time is over, find a new point to patrol
                if (timer >= waitTime)
                {
                    FindNewPatrolPoint();
                    timer = 0f;
                }
            }
        }

        private void FindNewPatrolPoint()
        {
            // Generate a random direction around the monster's current position
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection += transform.position;

            NavMeshHit hit;
            // Find the closest valid position on the NavMesh (the blue walkable area)
            if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
            {
                // Command the monster to move to the valid point found
                agent.SetDestination(hit.position);
            }
        }
    }
}