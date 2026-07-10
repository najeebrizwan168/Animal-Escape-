using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DogEscape
{
    public class HorizontalRodFence : MonoBehaviour
    {
        [Header("Fence Parts")]
        public List<Transform> slats = new List<Transform>();
        public Collider blockingCollider;

        [Header("Animation Settings")]
        public float animSpeed = 5.0f;
        public Vector3 closedRotation = Vector3.zero;
        public Vector3 openRotation = new Vector3(90f, 0f, 0f); // Rotate 90 degrees to open

        [Header("Current State")]
        public bool isOpen = false;

        private void Start()
        {
            // Initial state
            SetSlatRotations(isOpen ? openRotation : closedRotation);
            if (blockingCollider != null)
            {
                blockingCollider.enabled = !isOpen;
            }
        }

        private void Update()
        {
            Vector3 targetRot = isOpen ? openRotation : closedRotation;
            float step = animSpeed * Time.deltaTime;

            foreach (var slat in slats)
            {
                if (slat != null)
                {
                    slat.localRotation = Quaternion.Slerp(slat.localRotation, Quaternion.Euler(targetRot), step);
                }
            }

            if (blockingCollider != null && slats.Count > 0 && slats[0] != null)
            {
                // Disable blocking collider when fully or mostly open
                float angleDiff = Quaternion.Angle(slats[0].localRotation, Quaternion.Euler(openRotation));
                blockingCollider.enabled = angleDiff > 10f; // Enable collider only if not open
            }
        }

        public void Open()
        {
            isOpen = true;
        }

        public void Close()
        {
            isOpen = false;
        }

        public void Toggle()
        {
            isOpen = !isOpen;
        }

        private void SetSlatRotations(Vector3 rot)
        {
            foreach (var slat in slats)
            {
                if (slat != null)
                {
                    slat.localRotation = Quaternion.Euler(rot);
                }
            }
        }
    }
}
