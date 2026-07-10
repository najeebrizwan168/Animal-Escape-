using UnityEngine;

namespace DogEscape
{
    [RequireComponent(typeof(BoxCollider))]
    public class DeathTrap : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The tag of the animal/player that this trap should kill.")]
        public string targetTag = "animation"; 

        private void Awake()
        {
            // Automatically ensure the BoxCollider is set to be a trigger
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if the object entering the trigger has the correct tag
            if (other.CompareTag(targetTag))
            {
                EliminateTarget(other.gameObject);
            }
        }

        private void EliminateTarget(GameObject target)
        {
            // 1. Trigger the Death animation
            Animator animator = target.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Death");
            }

            // 2. Disable the PegiumController
            // Using string reference so it compiles even if PegiumController is in a different namespace
            MonoBehaviour pegiumController = target.GetComponent("PegiumController") as MonoBehaviour;
            if (pegiumController != null)
            {
                pegiumController.enabled = false;
                Debug.Log("[DeathTrap] Disabled PegiumController on " + target.name);
            }

            // 3. Disable the CharacterController to instantly freeze movement/gravity
            CharacterController cc = target.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }
        }
    }
}