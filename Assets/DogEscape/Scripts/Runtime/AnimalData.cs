using UnityEngine;

/// <summary>
/// ScriptableObject holding per-animal data: capture voice, footstep sprite, and prefab reference.
/// Create via: Assets > Create > DogEscape > Animal Data
/// </summary>
[CreateAssetMenu(fileName = "NewAnimalData", menuName = "DogEscape/Animal Data")]
public class AnimalData : ScriptableObject
{
    [Header("Animal Identity")]
    public string animalName;
    public GameObject animalPrefab;

    [Header("Capture Sound")]
    [Tooltip("The sound that plays when this animal gets captured by a hunter.")]
    public AudioClip captureVoice;

    [Header("Footstep Marks")]
    [Tooltip("Sprite containing footstep marks (3 footsteps in one image).")]
    public Sprite footstepSprite;
}
