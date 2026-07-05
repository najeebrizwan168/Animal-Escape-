using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogEscape
{
    public class DogEscapeGameManager : MonoBehaviour
    {
        public static DogEscapeGameManager Instance { get; private set; }

        [Header("Level Settings")]
        public int totalPuppiesToRescue = 1;
        public string nextLevelScene = "";

        private int rescuedPuppiesCount = 0;
        private bool isGameOver = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }

            InteractableObject.ClearInventory();
        }

        public void RescuePuppy()
        {
            rescuedPuppiesCount++;
            Debug.Log($"Puppy rescued! Total: {rescuedPuppiesCount}/{totalPuppiesToRescue}");

            if (rescuedPuppiesCount >= totalPuppiesToRescue)
            {
                CheckLevelWinConditions();
            }
        }

        private void CheckLevelWinConditions()
        {
            // Allow exit portal/door to open
            Debug.Log("All puppies rescued! The exit door is now OPEN.");
        }

        public void CompleteLevel()
        {
            if (isGameOver) return;

            Debug.Log("LEVEL COMPLETED! Loading next stage...");
            if (!string.IsNullOrEmpty(nextLevelScene))
            {
                SceneManager.LoadScene(nextLevelScene);
            }
            else
            {
                // Reload current scene for loop/sandbox demo
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void FailLevel()
        {
            if (isGameOver) return;
            isGameOver = true;

            Debug.Log("LEVEL FAILED! You were captured.");
            StartCoroutine(RestartLevelRoutine());
        }

        private System.Collections.IEnumerator RestartLevelRoutine()
        {
            yield return new WaitForSeconds(2.0f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
