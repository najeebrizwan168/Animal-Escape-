using UnityEngine;

public class UniversalSoundManager : MonoBehaviour
{
    public static UniversalSoundManager Instance;

    [Header("Hunter Audio Clips")]
    public AudioClip hunterCaptureSound; // Yahan apna Game Over ya Catching sound clip lagayein
    
    [Header("Saloon Door Audio Clips")]
    public AudioClip saloonDoorSound;

    [Header("Bricks Wall Audio Clips")]
    public AudioClip bricksWallSound;
    public AudioClip bricksWallRemoveSound;

    [Header("Food Audio Clips")]
    public AudioClip foodEatingSound;


    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 🔥 Sirf Capture hone par yeh call hoga
    public void PlayHunterCapture(Vector3 position)
    {
        if (hunterCaptureSound != null) 
        {
            AudioSource.PlayClipAtPoint(hunterCaptureSound, position);
        }
    }

    public void PlaySaloonDoorSound(Vector3 position)
    {
        if (saloonDoorSound != null) 
        {
            AudioSource.PlayClipAtPoint(saloonDoorSound, position);
        }
    }
    
    public void PlayBrickWall(Vector3 position)
    {
        if (bricksWallSound != null) 
        {
            AudioSource.PlayClipAtPoint(bricksWallSound, position);
        }
    }
    public void PlayBrickWallRemove(Vector3 position)
    {
        if (bricksWallRemoveSound != null) 
        {
            AudioSource.PlayClipAtPoint(bricksWallRemoveSound, position);
        }
    }

    public void PlayFoodEatingSound(Vector3 position)
    {
        if (foodEatingSound != null) 
        {
            AudioSource.PlayClipAtPoint(foodEatingSound, position);
        }
    }

    // =====================================================================
    // ANIMAL-SPECIFIC CAPTURE SOUND (set by AnimalController at runtime)
    // =====================================================================

    private AudioClip currentAnimalCaptureSound;

    /// <summary>
    /// Called by AnimalController on Awake/Start to register the current animal's capture voice.
    /// </summary>
    public void SetAnimalCaptureSound(AudioClip clip)
    {
        currentAnimalCaptureSound = clip;
    }

    /// <summary>
    /// Plays the current animal's capture voice. Falls back to hunterCaptureSound if no animal voice is set.
    /// </summary>
    public void PlayAnimalCaptureSound(Vector3 position)
    {
        if (currentAnimalCaptureSound != null)
        {
            AudioSource.PlayClipAtPoint(currentAnimalCaptureSound, position);
        }
        else if (hunterCaptureSound != null)
        {
            AudioSource.PlayClipAtPoint(hunterCaptureSound, position);
        }
    }
}