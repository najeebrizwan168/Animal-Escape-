using UnityEngine;
using System.Collections;

public class FoodItem : MonoBehaviour
{
    [Header("Growth Settings")]
    [SerializeField] private Vector3 sizeIncrease = new Vector3(0.02f, 0.02f, 0.02f);

    [Header("VFX Settings")]
    [Tooltip("The exact name of the child GameObject on the player that holds the Aura Particle System.")]
    [SerializeField] private string auraVfxChildName = "AuraEffect"; 
    [SerializeField] private float vfxDuration = 20f;

    private Rigidbody[] blockDoorRigidbodies;
    private bool hasBeenEaten = false;

    private void Start()
    {
        GameObject blockDoorObj = GameObject.FindWithTag("blockDoor");

        if (blockDoorObj != null)
        {
            blockDoorRigidbodies = blockDoorObj.GetComponentsInChildren<Rigidbody>();
            
            if (blockDoorRigidbodies.Length > 0)
            {
                foreach (Rigidbody rb in blockDoorRigidbodies)
                {
                    rb.isKinematic = true;
                }
            }
            else
            {
                Debug.LogWarning("[FoodItem] Found 'blockDoor' object, but no Rigidbodies found in its children.", this);
            }
        }
        else
        {
            Debug.LogError("[FoodItem] No GameObject found with the tag 'blockDoor' in the scene.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasBeenEaten) return;

        if (other.CompareTag("Player"))
        {
            CharacterController playerController = other.GetComponent<CharacterController>();
            
            if (playerController != null)
            {
                EatFood(other.gameObject);
            }
        }
    }

    private void EatFood(GameObject player)
    {
        hasBeenEaten = true;

        // 1. Scale up the animal
        player.transform.localScale += sizeIncrease;

        // 2. Handle the Aura VFX System safely without attaching scripts to the player
        HandleAuraEffect(player);

        // 3. Re-enable physics on the block door elements
        if (blockDoorRigidbodies != null)
        {
            foreach (Rigidbody rb in blockDoorRigidbodies)
            {
                rb.isKinematic = false;
            }
        }

        // Hide the food mesh and disable its collider immediately so it feels "eaten"
        // but don't Destroy(gameObject) right away, or the 20-second timer Coroutine will stop running!
        GetComponent<Collider>().enabled = false;
        
        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.enabled = false;

        // Destroy the food asset completely after the VFX duration finishes
        Destroy(gameObject,0); 
    }

    private void HandleAuraEffect(GameObject player)
    {
        // Search the player's children for the specific VFX object name
        Transform vfxTransform = player.transform.Find(auraVfxChildName);

        if (vfxTransform == null)
        {
            // If it's nested deep, let's look through all children recursively
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == auraVfxChildName)
                {
                    vfxTransform = child;
                    break;
                }
            }
        }

        if (vfxTransform != null)
        {
            ParticleSystem auraParticle = vfxTransform.GetComponent<ParticleSystem>();

            if (auraParticle != null)
            {
                // Start the Coroutine to run the timer
                StartCoroutine(RunAuraTimer(auraParticle));
            }
            else
            {
                Debug.LogWarning($"[FoodItem] Found '{auraVfxChildName}', but it missing a ParticleSystem component.", player);
            }
        }
        else
        {
            Debug.LogWarning($"[FoodItem] Could not find a child GameObject named '{auraVfxChildName}' on the player.", player);
        }
    }

    private IEnumerator RunAuraTimer(ParticleSystem aura)
    {
        // Turn on the Particle System
        var emission = aura.emission;
        emission.enabled = true;
        aura.Play();

        // Wait exactly 20 seconds
        yield return new WaitForSeconds(vfxDuration);

        // Turn off the emission cleanly so existing particles fade out nicely
        if (aura != null)
        {
            emission.enabled = false;
            aura.Stop();
        }
    }
}