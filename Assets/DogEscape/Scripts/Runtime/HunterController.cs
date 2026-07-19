using System.Collections;
using UnityEngine;

public class HunterController : MonoBehaviour
{
    [Header("Patrol Settings")]
    public bool canMove = true;
    public Transform pointA;
    public Transform pointB;
    public float moveSpeed = 3.0f;
    public float distanceToPointA;
    public float distanceToPointB;
    private Transform currentWaypoint;

    [Header("Rotation Settings")]
    public bool canRotate = true;
    public float maxRotationAngle = 45.0f; // How far left/right it looks
    public float rotationSpeed = 60.0f;    // How fast it turns (degrees per second)
    public float pauseDuration = 1.0f;     // Break time when reaching the edge
    
    [Header("Vision Cone Settings")]
    public Transform eyes;
    public float viewRadius = 10.0f;
    [Range(0, 360)] 
    public float viewAngle = 60.0f;
    public string targetTag = "animation"; // The tag you specified
    public LayerMask wallLayer; // Set to "Wall" layer in Inspector

    [Header("References")]
    public Animator animator;
    public GameObject AnimalCage;
    public ParticleSystem particleSystem;
    // Internal State
    private bool isAttacking = false;
    private float currentSweepAngle = 0f;
    private float targetSweepAngle = 0f;
    private Quaternion baseRotation;

    private void Start()
    {
        // Start patrol at Point B if it exists
        if (pointB != null) currentWaypoint = pointB;
        
        baseRotation = transform.rotation;
        
        // Start the rotation pattern
        StartCoroutine(RotationRoutine());
    }

    private void Update()
    {
        if (isAttacking) return; // Stop doing normal things if attacking

        HandleMovement();
        ApplyRotation();
        CheckVisionCone();
    }

    // ─── Patrol Logic ────────────────────────────────────────────────────────
    private void HandleMovement()
    {
        if (!canMove || pointA == null || pointB == null) 
            return;

        // Move towards the current waypoint
        transform.position = Vector3.MoveTowards(transform.position, currentWaypoint.position, moveSpeed * Time.deltaTime);

        // Calculate where the robot's body *should* be facing (base rotation)
        Vector3 direction = (currentWaypoint.position - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            baseRotation = Quaternion.LookRotation(direction);
        }

        // Switch waypoint if we reached the destination
        if (Vector3.Distance(transform.position, currentWaypoint.position) < 0.2f)
        {
            currentWaypoint = currentWaypoint == pointA ? pointB : pointA;
        }
    }

    // ─── Rotation Logic ──────────────────────────────────────────────────────
    private void ApplyRotation()
    {
        // If neither moving nor rotating, just stand facing forward.
        if (!canRotate && !canMove)
        {
            currentSweepAngle = Mathf.MoveTowards(currentSweepAngle, 0f, rotationSpeed * Time.deltaTime);
        }

        // Combine the base direction with the sweeping camera/body offset
        transform.rotation = baseRotation * Quaternion.Euler(0, currentSweepAngle, 0);
    }

    private IEnumerator RotationRoutine()
    {
        while (true)
        {
            // If rotation is disabled or we are attacking, center the rotation and wait
            if (!canRotate || isAttacking)
            {
                targetSweepAngle = 0f;
                yield return null;
                continue;
            }

            // Target Right Edge
            targetSweepAngle = maxRotationAngle;
            while (Mathf.Abs(currentSweepAngle - targetSweepAngle) > 0.1f && canRotate && !isAttacking)
            {
                currentSweepAngle = Mathf.MoveTowards(currentSweepAngle, targetSweepAngle, rotationSpeed * Time.deltaTime);
                yield return null;
            }

            // Break / Stop at the edge
            yield return new WaitForSeconds(pauseDuration);

            // Target Left Edge
            targetSweepAngle = -maxRotationAngle;
            while (Mathf.Abs(currentSweepAngle - targetSweepAngle) > 0.1f && canRotate && !isAttacking)
            {
                currentSweepAngle = Mathf.MoveTowards(currentSweepAngle, targetSweepAngle, rotationSpeed * Time.deltaTime);
                yield return null;
            }

            // Break / Stop at the edge
            yield return new WaitForSeconds(pauseDuration); 
        }
    }

    // ─── Detection Logic ─────────────────────────────────────────────────────
    private void CheckVisionCone()
    {
        if (eyes == null) return;

        // Find all colliders within the distance
        Collider[] targetsInRadius = Physics.OverlapSphere(eyes.position, viewRadius);

        foreach (Collider col in targetsInRadius)
        {
            // Catch if targetTag matches OR is tagged "Player"
            if (col.CompareTag(targetTag) || col.CompareTag("Player"))
            {
                // Calculate horizontal flat direction to target (disregard height differences for angle check)
                Vector3 targetPosFlat = new Vector3(col.transform.position.x, eyes.position.y, col.transform.position.z);
                Vector3 dirToTargetFlat = (targetPosFlat - eyes.position).normalized;
                
                // Use hunter's root forward direction (aligned horizontally) instead of eyes.forward
                Vector3 hunterForwardFlat = transform.forward;
                hunterForwardFlat.y = 0f;
                hunterForwardFlat.Normalize();

                float angle = Vector3.Angle(hunterForwardFlat, dirToTargetFlat);

                // Draw a debug line in scene view showing player in range
                Debug.DrawLine(eyes.position, col.transform.position, Color.yellow);
                  
                // Wall-blocking check: raycast from eyes to target — skip if wall is in between
                Vector3 toTarget = col.transform.position - eyes.position;
                bool wallBlocked = Physics.Raycast(eyes.position, toTarget.normalized, toTarget.magnitude, wallLayer);

                if (angle < viewAngle / 2f && !wallBlocked)
                {
                    // Play the particle system
                    Instantiate(particleSystem,col.transform.position,Quaternion.identity);
                    // Draw detection line in red for 2 seconds
                    Debug.DrawLine(eyes.position, col.transform.position, Color.red, 2.0f);
                    if (UniversalSoundManager.Instance != null)
            {
                 Debug.Log("PLayed Sound.");
                UniversalSoundManager.Instance.PlayHunterCapture(transform.position);
                UniversalSoundManager.Instance.PlayAnimalCaptureSound(transform.position);
            }

                    // Console log as requested
                    Debug.Log($"[Player Capture] Detected player '{col.name}' at angle {angle:F1}° (vision limit: {viewAngle/2f}°) inside view radius!");
                    Instantiate(AnimalCage,col.transform.position,Quaternion.identity);
                    
                    EngageTarget(col.transform);
                    // Freeze the animal so it can't move after capture
                    var animalCtrl = col.GetComponent<DogEscape.AnimalController>();
                    if (animalCtrl != null) animalCtrl.Capture();
                    break; 
                }
            }
        }
    }

    private void EngageTarget(Transform target)
    {
        isAttacking = true;
        
        // Face the player directly
        Vector3 lookPos = new Vector3(target.position.x, transform.position.y, target.position.z);
        transform.LookAt(lookPos);

        // Play Animation
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Disable PenguinController
        MonoBehaviour penguinController = target.GetComponent("PenguinController") as MonoBehaviour;
        if (penguinController != null)
        {
            penguinController.enabled = false;
            Debug.Log("[RobotHunter] Disabled PenguinController on " + target.name);
        }

        // Disable DogController
        MonoBehaviour dogController = target.GetComponent("DogController") as MonoBehaviour;
        if (dogController != null)
        {
            dogController.enabled = false;
            Debug.Log("[RobotHunter] Disabled DogController on " + target.name);
        }

        // Trigger Level Fail in GameManager to restart the game
        if (DogEscape.DogEscapeGameManager.Instance != null)
        {
            DogEscape.DogEscapeGameManager.Instance.FailLevel();
        }
    }

    // ─── Visual Gizmos ───────────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        // 1. Draw Patrol Path
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawWireSphere(pointA.position, 0.3f);
            Gizmos.DrawWireSphere(pointB.position, 0.3f);
        }

        // 2. Draw Vision Cone
        if (eyes != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f); // Transparent Red

            // Forward line (hunter root facing direction)
            Vector3 forwardDir = transform.forward;
            Gizmos.DrawLine(eyes.position, eyes.position + forwardDir * viewRadius);

            // Left and Right edges of the cone
            Vector3 rightLimit = Quaternion.Euler(0, viewAngle / 2, 0) * forwardDir;
            Vector3 leftLimit = Quaternion.Euler(0, -viewAngle / 2, 0) * forwardDir;

            Gizmos.DrawLine(eyes.position, eyes.position + rightLimit * viewRadius);
            Gizmos.DrawLine(eyes.position, eyes.position + leftLimit * viewRadius);
            
            // Draw a rough circle at the end to visualize the shape
            Gizmos.DrawWireSphere(eyes.position, viewRadius);
        }
    }
}