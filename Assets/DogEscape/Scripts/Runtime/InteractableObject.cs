using UnityEngine;
using System.Collections.Generic;

namespace DogEscape
{
    public class InteractableObject : MonoBehaviour
    {
        public enum Type
        {
            Keycard,
            Button,
            Door
        }

        [Header("Settings")]
        public Type objectType = Type.Keycard;
        
        [Header("Door & Lock Setup")]
        public string keyId = "BlueKey";
        public InteractableObject linkedButton;
        public Vector3 openedOffset = new Vector3(0, -3.0f, 0); // Slide door down
        public float doorSpeed = 2.0f;

        [Header("Visual Colors")]
        public Color normalColor = Color.blue;
        public Color activatedColor = Color.green;

        private Renderer rend;
        private Vector3 closedPos;
        private Vector3 openedPos;
        private bool isActivated = false;
        private static HashSet<string> playerInventory = new HashSet<string>();

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            closedPos = transform.position;
            openedPos = closedPos + openedOffset;

            if (rend != null)
            {
                rend.material.color = normalColor;
            }
        }

        private void Update()
        {
            if (objectType == Type.Door)
            {
                bool shouldOpen = false;

                // Open if linked button is active
                if (linkedButton != null && linkedButton.isActivated)
                {
                    shouldOpen = true;
                }
                // Open if player inventory has matching keycard
                else if (!string.IsNullOrEmpty(keyId) && playerInventory.Contains(keyId))
                {
                    shouldOpen = true;
                }

                Vector3 target = shouldOpen ? openedPos : closedPos;
                transform.position = Vector3.MoveTowards(transform.position, target, doorSpeed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<DogController>() != null)
            {
                if (objectType == Type.Keycard)
                {
                    playerInventory.Add(keyId);
                    Debug.Log($"Collected Keycard: {keyId}");
                    Destroy(gameObject);
                }
                else if (objectType == Type.Button)
                {
                    isActivated = true;
                    if (rend != null) rend.material.color = activatedColor;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (objectType == Type.Button && (other.CompareTag("Player") || other.GetComponent<DogController>() != null))
            {
                isActivated = false;
                if (rend != null) rend.material.color = normalColor;
            }
        }

        public static void ClearInventory()
        {
            playerInventory.Clear();
        }
    }
}
