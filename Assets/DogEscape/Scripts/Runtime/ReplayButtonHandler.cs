using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DogEscape
{
    public class ReplayButtonHandler : MonoBehaviour
    {
        public void ReplayGame()
        {
            Debug.Log("[ReplayButtonHandler] Replaying game/reloading scene...");
            
            // Reload the active scene
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }
    }
}
