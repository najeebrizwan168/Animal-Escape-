using System.Collections.Generic;
using UnityEngine;

namespace HouseGenerator
{
    /// <summary>
    /// ScriptableObject that holds references to all level JSON files.
    /// Create via: Assets > Create > House Generator > Level Database
    /// Just drag your exported level JSON files into the list — no mapping needed.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "House Generator/Level Database", order = 1)]
    public class LevelDatabase : ScriptableObject
    {
        [Header("Level JSON Files (Index 0 = Level 1, Index 1 = Level 2, etc.)")]
        [Tooltip("Add your exported level JSON files here in order. Index 0 corresponds to level 1.")]
        public List<TextAsset> levelJsonFiles = new List<TextAsset>();

        /// <summary>
        /// Gets the JSON TextAsset for a given level number (1-based).
        /// Returns null if the level doesn't exist.
        /// </summary>
        public TextAsset GetLevelJson(int levelNumber)
        {
            int index = levelNumber - 1;
            if (index < 0 || index >= levelJsonFiles.Count)
            {
                Debug.LogError($"[LevelDatabase] Level {levelNumber} does not exist! Valid range: 1 to {levelJsonFiles.Count}.");
                return null;
            }

            TextAsset json = levelJsonFiles[index];
            if (json == null)
            {
                Debug.LogError($"[LevelDatabase] Level {levelNumber} JSON slot is empty (null reference at index {index}).");
            }

            return json;
        }

        /// <summary>
        /// Returns the total number of levels available.
        /// </summary>
        public int TotalLevels => levelJsonFiles.Count;
    }
}
