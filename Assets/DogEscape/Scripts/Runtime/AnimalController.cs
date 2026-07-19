using UnityEngine;
using System.Collections.Generic;

namespace DogEscape
{
    [RequireComponent(typeof(CharacterController))]
    public class AnimalController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 8.0f; // Set to your original run speed
        public float rotationSpeed = 720.0f;
        public float gravity = -20.0f;

        [Header("Mobile Controls")]
        public float minDragDistance = 20.0f;

        [Header("Animal Data")]
        [Tooltip("Assign the AnimalData ScriptableObject for the current animal.")]
        public AnimalData animalData;

        [Header("Footstep Settings")]
        [Tooltip("Distance between each footstep placement.")]
        public float footstepInterval = 1.2f;
        [Tooltip("How long footsteps stay visible before fading.")]
        public float footstepLifetime = 2.0f;
        [Tooltip("Height offset above the ground for footstep decals.")]
        public float footstepGroundOffset = 0.02f;
        [Tooltip("Scale of the footstep decal.")]
        public float footstepScale = 0.5f;
        [Tooltip("Rotation offset to align footstep sprite with movement direction.")]
        public Vector3 footstepRotationOffset = new Vector3(90f, 0f, 0f);

        private CharacterController cc;
        private Animator animator;
        private Vector3 moveInput;
        private float verticalVelocity;
        private bool wasMoving = false;
        private Vector3 touchStartPos;
        private bool isDragging = false;
        private bool isCaptured = false;

        // Footstep tracking
        private float distanceSinceLastFootstep;
        private List<FootstepDecal> activeFootsteps = new List<FootstepDecal>();

        private class FootstepDecal
        {
            public GameObject gameObject;
            public SpriteRenderer spriteRenderer;
            public float spawnTime;
            public float startAlpha;
        }

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

            // Register this animal's capture voice with the UniversalSoundManager
            if (animalData != null && animalData.captureVoice != null && UniversalSoundManager.Instance != null)
            {
                UniversalSoundManager.Instance.SetAnimalCaptureSound(animalData.captureVoice);
            }

            // --- Added Respawn Logic Here ---
            HandleInitialSpawnPosition();
        }

        private void Start()
        {
            // Re-register capture voice in Start in case UniversalSoundManager was not ready in Awake
            if (animalData != null && animalData.captureVoice != null && UniversalSoundManager.Instance != null)
            {
                UniversalSoundManager.Instance.SetAnimalCaptureSound(animalData.captureVoice);
            }
        }

        private void HandleInitialSpawnPosition()
        {
            // Find the main level object in the hierarchy window
            GameObject levelObj = GameObject.FindWithTag("Level");

            if (levelObj != null)
            {
                // Look through all children inside the level object
                Transform[] allChildren = levelObj.GetComponentsInChildren<Transform>(true);
                Transform respawnPoint = null;

                foreach (Transform child in allChildren)
                {
                    if (child.CompareTag("Respawn"))
                    {
                        respawnPoint = child;
                        break; // Stop searching once we find it
                    }
                }

                if (respawnPoint != null)
                {
                    // Temporarily disable CharacterController to manually set the position safely
                    if (cc != null) cc.enabled = false;

                    transform.position = respawnPoint.position;

                    if (cc != null) cc.enabled = true;
                }
                else
                {
                    Debug.LogWarning("[AnimalController] Found 'level' object, but no child with the tag 'Respawn' was found.", this);
                }
            }
            else
            {
                Debug.LogWarning("[AnimalController] Could not find any GameObject with the tag 'level' in the scene.", this);
            }
        }

        private void Update()
        {
            if (isCaptured) return;

            HandleTouchInput();
            HandleAnimations();
            MoveCharacter();
            UpdateFootsteps();
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

            // Track distance for footsteps
            if (cc.isGrounded && moveInput.magnitude > 0.1f)
            {
                distanceSinceLastFootstep += horizontalMove.magnitude * Time.deltaTime;

                if (distanceSinceLastFootstep >= footstepInterval)
                {
                    PlaceFootstep();
                    distanceSinceLastFootstep = 0f;
                }
            }

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

        // =====================================================================
        // FOOTSTEP SYSTEM
        // =====================================================================

        private void PlaceFootstep()
        {
            Sprite footSprite = null;

            // Get the footstep sprite from the AnimalData ScriptableObject
            if (animalData != null && animalData.footstepSprite != null)
            {
                footSprite = animalData.footstepSprite;
            }

            if (footSprite == null) return;

            // Create footstep decal on the ground
            GameObject footstepObj = new GameObject("Footstep");
            footstepObj.transform.position = new Vector3(
                transform.position.x,
                GetGroundY() + footstepGroundOffset,
                transform.position.z
            );

            // Rotate to face flat on the ground (face up) and match player's direction
            footstepObj.transform.rotation = Quaternion.Euler(
                footstepRotationOffset.x,
                transform.eulerAngles.y + footstepRotationOffset.y,
                footstepRotationOffset.z
            );
            footstepObj.transform.localScale = Vector3.one * footstepScale;

            SpriteRenderer sr = footstepObj.AddComponent<SpriteRenderer>();
            sr.sprite = footSprite;
            sr.sortingOrder = -1;
            sr.color = new Color(1f, 1f, 1f, 0.6f); // Start slightly transparent

            FootstepDecal decal = new FootstepDecal
            {
                gameObject = footstepObj,
                spriteRenderer = sr,
                spawnTime = Time.time,
                startAlpha = sr.color.a
            };

            activeFootsteps.Add(decal);
        }

        private float GetGroundY()
        {
            // Raycast down to find the ground surface
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                return hit.point.y;
            }
            return transform.position.y;
        }

        private void UpdateFootsteps()
        {
            // Fade and clean up footsteps from oldest to newest (back to forward)
            for (int i = activeFootsteps.Count - 1; i >= 0; i--)
            {
                FootstepDecal decal = activeFootsteps[i];

                if (decal.gameObject == null)
                {
                    activeFootsteps.RemoveAt(i);
                    continue;
                }

                float elapsed = Time.time - decal.spawnTime;

                if (elapsed >= footstepLifetime)
                {
                    // Destroy the footstep once its lifetime is over
                    Destroy(decal.gameObject);
                    activeFootsteps.RemoveAt(i);
                }
                else
                {
                    // Smooth fade out: older footsteps fade first (back to forward)
                    float fadeProgress = elapsed / footstepLifetime;
                    // Use smooth curve for beautiful fade
                    float alpha = decal.startAlpha * (1f - Mathf.SmoothStep(0f, 1f, fadeProgress));

                    Color c = decal.spriteRenderer.color;
                    c.a = alpha;
                    decal.spriteRenderer.color = c;
                }
            }
        }

        // =====================================================================
        // COLLISION & TAG DETECTION
        // =====================================================================

        private bool SafeCompareTag(GameObject go, string tagName)
        {
            try { return go != null && go.CompareTag(tagName); }
            catch { return false; }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && SafeCompareTag(other.gameObject, "Complete"))
            {
                Debug.Log("Complete");
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit != null && SafeCompareTag(hit.gameObject, "Complete"))
            {
                Debug.Log("Complete");
            }

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

        /// <summary>
        /// Call this to freeze the animal when captured. Stops all movement and input.
        /// </summary>
        public void Capture()
        {
            isCaptured = true;
            moveInput = Vector3.zero;
            if (animator != null)
                animator.SetTrigger("Idle");
        }

        private void OnDestroy()
        {
            // Clean up any remaining footsteps when the animal is destroyed
            foreach (var decal in activeFootsteps)
            {
                if (decal.gameObject != null)
                    Destroy(decal.gameObject);
            }
            activeFootsteps.Clear();
        }
    }
}
