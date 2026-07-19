using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace DogEscape
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class CatcherAI : MonoBehaviour
    {
        public enum State
        {
            Patrol,
            Investigate,
            Chase,
            Return
        }

        [Header("AI State")]
        public State currentState = State.Patrol;

        [Header("Movement speeds")]
        public float patrolSpeed = 2.5f;
        public float chaseSpeed = 4.5f;

        [Header("Senses")]
        public float viewDistance = 7.0f;
        public float viewAngle = 90.0f;
        public float scentRadius = 2.5f;
        public float scentAlertDelay = 1.5f;
        public LayerMask obstacleLayer;

        [Header("Patrol Settings")]
        public Transform[] patrolWaypoints;
        public float waypointWaitTime = 1.5f;

        [Header("Ref References")]
        public Transform player;

        private NavMeshAgent agent;
        private Vector3 lastNoisePosition;
        private bool hasNoiseAlert;
        private int currentWaypointIndex;
        private bool isWaiting;
        private float scentTimer = 0f;
        private Vector3 startPosition;

        private void OnDrawGizmosSelected()
        {
            // Draw view distance arc
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewDistance);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewDistance);

            // Draw scent circle
            Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, scentRadius);
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            startPosition = transform.position;
        }

        private void Start()
        {
            if (player == null)
            {
                var dog = GameObject.FindAnyObjectByType<DogController>();
                if (dog != null) player = dog.transform;
            }

            GoToNextWaypoint();
        }

        private void Update()
        {
            CheckSenses();

            switch (currentState)
            {
                case State.Patrol:
                    PatrolUpdate();
                    break;
                case State.Investigate:
                    InvestigateUpdate();
                    break;
                case State.Chase:
                    ChaseUpdate();
                    break;
                case State.Return:
                    ReturnUpdate();
                    break;
            }
        }
     private void CheckSenses()
        {
            if (player == null) return;

            // 1. Sight check
            if (CanSeePlayer())
            {
                TriggerChase();
                return;
            }

            // 2. Scent check
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer <= scentRadius)
            {
                scentTimer += Time.deltaTime;
                if (scentTimer >= scentAlertDelay)
                {
                    HearNoise(player.position); 
                }
            }
            else
            {
                scentTimer = 0f;
            }
        }

        private bool CanSeePlayer()
        {
            if (player == null) return false;

            Vector3 dirToPlayer = player.position - transform.position;
            float distance = dirToPlayer.magnitude;

            if (distance > viewDistance) return false;

            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > viewAngle / 2f) return false;

            // Line of sight raycast
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToPlayer.normalized, out hit, distance, obstacleLayer))
            {
                if (hit.transform != player) return false; // Blocked by wall
            }

            return true;
        }

        public void HearNoise(Vector3 noiseSource)
        {
            if (currentState == State.Chase) return; // Keep chasing if already chasing

            lastNoisePosition = noiseSource;
            hasNoiseAlert = true;
            currentState = State.Investigate;
            agent.speed = patrolSpeed;
            agent.SetDestination(lastNoisePosition);
            isWaiting = false;
        }

      private void TriggerChase()
        {
            currentState = State.Chase;
            agent.speed = chaseSpeed;
            agent.isStopped = false;
            isWaiting = false;
        }
        private void PatrolUpdate()
        {
            if (patrolWaypoints.Length == 0)
            {
                // Idle at start position if no waypoints
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    agent.SetDestination(startPosition);
                }
                return;
            }

            if (!agent.pathPending && agent.remainingDistance < 0.5f && !isWaiting)
            {
                StartCoroutine(WaitAtWaypoint());
            }
        }

        private IEnumerator WaitAtWaypoint()
        {
            isWaiting = true;
            yield return new WaitForSeconds(waypointWaitTime);
            
            if (currentState == State.Patrol) // Verify state hasn't changed
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                GoToNextWaypoint();
            }
            isWaiting = false;
        }

        private void GoToNextWaypoint()
        {
            if (patrolWaypoints.Length == 0) return;
            agent.speed = patrolSpeed;
            agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);
        }

        private void InvestigateUpdate()
        {
            if (!agent.pathPending && agent.remainingDistance < 0.5f && !isWaiting)
            {
                StartCoroutine(WaitAndSearchNoise());
            }
        }

        private IEnumerator WaitAndSearchNoise()
        {
            isWaiting = true;
            yield return new WaitForSeconds(3.0f); // Search area for 3 seconds
            
            if (currentState == State.Investigate)
            {
                hasNoiseAlert = false;
                currentState = State.Return;
            }
            isWaiting = false;
        }

        private void ChaseUpdate()
        {
            if (player == null) return;

            agent.SetDestination(player.position);

            // Re-evaluate line of sight
            if (!CanSeePlayer())
            {
                // Lost sight of player, head to last seen position
                HearNoise(player.position);
            }

            // Check capture distance
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance < 0.8f)
            {
                CapturePlayer();
            }
        }

        private void ReturnUpdate()
        {
            if (patrolWaypoints.Length > 0)
            {
                agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    currentState = State.Patrol;
                }
            }
            else
            {
                agent.SetDestination(startPosition);
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    currentState = State.Patrol;
                }
            }
        } 
    

       private void CapturePlayer()
        {
            Debug.Log("DOG CAPTURED! Game Over.");
            
            // 🎯 Foran capture sound play karo game over hone par!
            if (UniversalSoundManager.Instance != null)
            {
                 Debug.Log("PLayed Sound.");
                UniversalSoundManager.Instance.PlayHunterCapture(transform.position);
            }

            // Reset player position or trigger Game Manager event
            if (player != null)
            {
                // Quick reset for test demo
                player.position = new Vector3(0, 1.0f, 0); 
                currentState = State.Return;
            }
        }
    }
}
