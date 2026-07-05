using UnityEngine;

namespace DogEscape
{
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Offset Settings")]
        public Vector3 offset = new Vector3(0f, 6.0f, -6.0f);
        public float smoothSpeed = 8.0f;

        [Header("Wall Collision")]
        public LayerMask obstacleLayer;
        public float minCollisionDistance = 1.0f;
        public float cameraSafetyRadius = 0.25f;

        private Vector3 desiredPosition;

        private void Start()
        {
            if (target == null)
            {
                var dog = GameObject.FindAnyObjectByType<DogController>();
                if (dog != null) target = dog.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Calculate desired position
            desiredPosition = target.position + offset;

            // 2. Perform Wall Detection (Collision resolution)
            Vector3 targetHeadPos = target.position + Vector3.up * 0.5f; // Cast from character's center/head
            Vector3 rayDirection = desiredPosition - targetHeadPos;
            float maxDistance = rayDirection.magnitude;

            RaycastHit hit;
            // Use SphereCast to prevent corner clipping
            if (Physics.SphereCast(targetHeadPos, cameraSafetyRadius, rayDirection.normalized, out hit, maxDistance, obstacleLayer))
            {
                // Clamp distance to hit distance, ensuring it doesn't get closer than minCollisionDistance
                float clampedDistance = Mathf.Max(minCollisionDistance, hit.distance);
                desiredPosition = targetHeadPos + rayDirection.normalized * clampedDistance;
            }

            // 3. Smoothly move camera
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // 4. Smoothly rotate camera to look at target
            Vector3 lookTarget = target.position + Vector3.up * 0.5f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
        }
    }
}
