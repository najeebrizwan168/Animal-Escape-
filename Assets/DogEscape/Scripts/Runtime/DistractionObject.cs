using UnityEngine;

namespace DogEscape
{
    [RequireComponent(typeof(Rigidbody))]
    public class DistractionObject : MonoBehaviour
    {
        [Header("Distraction Parameters")]
        public float distractionRadius = 8.0f;
        public float distractionDuration = 4.0f;
        public LayerMask catcherLayer;

        private Rigidbody rb;
        private bool hasLanded = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Throw(Vector3 direction, float force)
        {
            rb.AddForce(direction * force, ForceMode.Impulse);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!hasLanded)
            {
                hasLanded = true;
                MakeNoise();
            }
        }

        private void MakeNoise()
        {
            // Find catchers and distract them to this point
            Collider[] colliders = Physics.OverlapSphere(transform.position, distractionRadius, catcherLayer);
            foreach (var col in colliders)
            {
                CatcherAI catcher = col.GetComponent<CatcherAI>();
                if (catcher != null)
                {
                    catcher.HearNoise(transform.position);
                }
            }

            // Play squeaky sound / spawn impact particles
            Debug.Log("Distraction toy landed! Alerted nearby catchers.");

            // Self-destroy after a few seconds
            Destroy(gameObject, distractionDuration);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, distractionRadius);
        }
    }
}
