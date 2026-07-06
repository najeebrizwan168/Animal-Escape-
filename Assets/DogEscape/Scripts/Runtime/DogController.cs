using UnityEngine;

namespace DogEscape
{
    // CharacterController handles all movement and gravity.
    // No Rigidbody needed — they conflict and cause jitter/spinning.
    [RequireComponent(typeof(CharacterController))]
    public class DogController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5.0f;
        public float runSpeed = 8.0f;
        public float rotationSpeed = 720.0f;
        public float gravity = -20.0f;

        [Header("Noise System")]
        public float walkNoiseRadius = 2.0f;
        public float runNoiseRadius = 6.0f;
        public LayerMask groundLayer;

        [Header("Mobile Controls")]
        public float minDragDistance = 20.0f;

        // Private state
        private CharacterController cc;
        private Animator animator;
        private Vector3 moveInput;
        private bool isRunning;
        private float verticalVelocity;
        private float currentNoiseRadius = 0f;
        private bool wasMoving = false;
        private Vector3 touchStartPos;
        private bool isDragging = false;

        private void OnDrawGizmosSelected()
        {
            if (currentNoiseRadius > 0.1f)
            {
                Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, currentNoiseRadius);
            }
        }

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            // Animation plays visually only — CharacterController moves the body.
            if (animator != null)
                animator.applyRootMotion = false;

            // Remove Rigidbody if someone left it on — it conflicts with CC.
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb);
                Debug.Log("[DogController] Removed conflicting Rigidbody from " + gameObject.name);
            }
        }

        private void Update()
        {
            HandleInput();
            MoveCharacter();
            HandleAnimations();
        }

        private void FixedUpdate()
        {
            HandleNoise();
        }

        // ─── Input ────────────────────────────────────────────────────────────

        private void HandleInput()
        {
            // Touch / mouse drag → move in drag direction
            if (Input.GetMouseButtonDown(0))
            {
                touchStartPos = Input.mousePosition;
                isDragging = true;
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 delta = Input.mousePosition - touchStartPos;
                if (delta.magnitude > minDragDistance)
                {
                    moveInput = new Vector3(delta.x, 0f, delta.y).normalized;
                    isRunning = delta.magnitude > minDragDistance * 2.5f;
                }
                else
                {
                    moveInput = Vector3.zero;
                    isRunning = false;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                moveInput = Vector3.zero;
                isRunning = false;
            }

            // WASD / arrow key fallback for editor testing
            if (!isDragging)
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
                {
                    moveInput = new Vector3(h, 0f, v).normalized;
                    isRunning = Input.GetKey(KeyCode.LeftShift);
                }
                else
                {
                    moveInput = Vector3.zero;
                    isRunning = false;
                }
            }
        }

        // ─── Movement ─────────────────────────────────────────────────────────

        private void MoveCharacter()
        {
            // Guard: CC must be fully initialized and active in the physics scene before we can call Move
            if (cc == null || !cc.enabled || !cc.gameObject.activeInHierarchy) return;

            // Apply gravity — CharacterController doesn't do this automatically
            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small negative keeps isGrounded reliable
            else
                verticalVelocity += gravity * Time.deltaTime;

            // Build final movement vector
            float speed = isRunning ? runSpeed : moveSpeed;
            Vector3 horizontalMove = moveInput * speed;
            Vector3 finalMove = new Vector3(horizontalMove.x, verticalVelocity, horizontalMove.z);

            cc.Move(finalMove * Time.deltaTime);

            // Smooth rotation toward movement direction
            if (moveInput.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveInput);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }


        // ─── Animations ───────────────────────────────────────────────────────

        private void HandleAnimations()
        {
            if (animator == null) return;

            bool isMoving = moveInput.magnitude > 0.1f;
            if (isMoving == wasMoving) return;

            wasMoving = isMoving;
            if (isMoving)
                animator.SetTrigger("Walk");
            else
                animator.SetTrigger("Idle");
        }

        // ─── Noise System ─────────────────────────────────────────────────────

        private void HandleNoise()
        {
            if (moveInput.magnitude < 0.1f)
            {
                currentNoiseRadius = 0f;
                return;
            }

            float multiplier = 1.0f;
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 0.5f, groundLayer))
            {
                string n = hit.collider.name.ToLower();
                if (n.Contains("carpet") || n.Contains("grass"))
                    multiplier = 0f;
                else if (n.Contains("grate") || n.Contains("metal") || n.Contains("kibble"))
                    multiplier = 2f;
            }

            currentNoiseRadius = (isRunning ? runNoiseRadius : walkNoiseRadius) * multiplier;

            if (currentNoiseRadius > 0.1f)
                AlertGuards();
        }

        private void AlertGuards()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, currentNoiseRadius);
            foreach (var col in colliders)
            {
                CatcherAI catcher = col.GetComponent<CatcherAI>();
                if (catcher != null)
                    catcher.HearNoise(transform.position);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            // No rigidbody or is kinematic
            if (body == null || body.isKinematic)
                return;

            // We don't want to push objects below us
            if (hit.moveDirection.y < -0.3f)
                return;

            // Calculate push direction from move direction,
            // we only push objects to the sides never up and down
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

            // Apply the push
            float pushPower = 2.5f;
            body.AddForce(pushDir * pushPower, ForceMode.VelocityChange);
        }
    }
}
