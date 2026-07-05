using UnityEngine;

namespace DogEscape
{
    [RequireComponent(typeof(CharacterController))]
    public class PenguinController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 8.0f; // Set to your original run speed
        public float rotationSpeed = 720.0f;
        public float gravity = -20.0f;

        [Header("Mobile Controls")]
        public float minDragDistance = 20.0f;

        private CharacterController cc;
        private Animator animator;
        private Vector3 moveInput;
        private float verticalVelocity;
        private bool wasMoving = false;
        private Vector3 touchStartPos;
        private bool isDragging = false;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            if (animator != null)
                animator.applyRootMotion = false;

            // Clean up any rogue rigidbodies
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb);
            }
        }

        private void Update()
        {
            HandleTouchInput();
            HandleAnimations();
            MoveCharacter();
        }

        private void HandleTouchInput()
        {
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
                }
                else
                {
                    moveInput = Vector3.zero;
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                moveInput = Vector3.zero;
            }
        }

        private void MoveCharacter()
        {
            if (!cc.enabled || !cc.gameObject.activeInHierarchy) return;

            // Apply simple gravity
            if (cc.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            else
                verticalVelocity += gravity * Time.deltaTime;

            // Calculate movement
            Vector3 horizontalMove = moveInput * moveSpeed;
            Vector3 finalMove = new Vector3(horizontalMove.x, verticalVelocity, horizontalMove.z);

            cc.Move(finalMove * Time.deltaTime);

            // Handle rotation toward touch direction
            if (moveInput.magnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveInput);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

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
    }
}