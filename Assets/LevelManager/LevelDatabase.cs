using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets; // Addressables library use karne ke liye

[System.Serializable]
public struct LevelData
{
    public int levelNumber;
    public string levelName;
    public AssetReference levelPrefabReference; // 🔥 Soft-reference (RAM bachane ka jadu)
}

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Game/Level Database")]
public class LevelDatabase : ScriptableObject
{
    public List<LevelData> allLevels;
}