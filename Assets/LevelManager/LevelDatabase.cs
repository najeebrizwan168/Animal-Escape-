using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// ScriptableObject that holds all level JSON references as Addressable TextAssets.
/// Create via: Assets > Create > Game > Addressable Level Database
/// 
/// HOW TO USE:
/// 1. Create this ScriptableObject
/// 2. For each level, add an entry with the level number and the Addressable reference to the JSON TextAsset
/// 3. Assign this to the AddressableLevelLoader in your scene
/// 
/// NOTE: Your JSON files and the prefabs/materials they reference must be marked as Addressable.
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Game/Addressable Level Database")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelData> allLevels = new List<LevelData>();

    /// <summary>
    /// Find a level entry by its number.
    /// </summary>
    public LevelData? GetLevel(int levelNumber)
    {
        for (int i = 0; i < allLevels.Count; i++)
        {
            if (allLevels[i].levelNumber == levelNumber)
                return allLevels[i];
        }
        return null;
    }

    public int TotalLevels => allLevels.Count;
}

[System.Serializable]
public struct LevelData
{
    [Tooltip("Level number (1-based)")]
    public int levelNumber;

    [Tooltip("Display name for this level")]
    public string levelName;

    [Tooltip("Addressable reference to the level JSON TextAsset file")]
    public AssetReference levelJsonReference;
}