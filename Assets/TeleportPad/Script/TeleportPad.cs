using UnityEngine;

public class TeleportPad : MonoBehaviour
{
    [Header("Teleport Configuration")]
    [SerializeField] private Transform destinationPad; // Drag the OTHER pad's Transform here
    [SerializeField] private bool canTeleportFromHere = true; // Is this an entrance, or just an exit?

    // Static variable shared across ALL instances of this script to track the active destination
    private static Transform justTeleportedTo = null;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if the object is the player
        if (!other.CompareTag("Player")) return;

        // 2. Prevent infinite loops: If this pad was the recent destination, ignore the player
        if (justTeleportedTo == transform) return;

        // 3. Check if this specific pad is allowed to teleport players
        if (!canTeleportFromHere) return;

        // 4. Ensure a destination is actually assigned in the inspector
        if (destinationPad != null)
        {
            TeleportPlayer(other.gameObject);
        }
        else
        {
            Debug.LogWarning($"[TeleportPad] {gameObject.name} has no destination pad assigned!", this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Once the player leaves this pad's trigger area, clear the lock
        if (other.CompareTag("Player") && justTeleportedTo == transform)
        {
            justTeleportedTo = null;
        }
    }

    private void TeleportPlayer(GameObject player)
    {
        // Tell the system that the destination pad shouldn't immediately re-teleport the player
        justTeleportedTo = destinationPad;

        // If using CharacterController, we must temporarily disable it to override its position
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        // Move the player to the destination pad's exact position
        player.transform.position = destinationPad.position;

        // Re-enable the CharacterController after positioning
        if (cc != null)
        {
            cc.enabled = true;
        }
    }
}