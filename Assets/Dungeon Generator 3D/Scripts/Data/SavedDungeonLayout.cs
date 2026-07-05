using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RoomLayoutData
{
    public Vector2Int position;
    public Vector2Int size;
    public bool isBossRoom;
}

[Serializable]
public struct SavedObjectData
{
    public string prefabName;
    public string tag;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public string parentName; // Name of the room or corridor parent
}

[CreateAssetMenu(menuName = "Saved Dungeon Layout")]
public class SavedDungeonLayout : ScriptableObject
{
    public int areaWidth;
    public int areaHeight;
    public List<RoomLayoutData> rooms = new List<RoomLayoutData>();
    public List<Vector2Int> corridorTiles = new List<Vector2Int>();
    public List<SavedObjectData> savedObjects = new List<SavedObjectData>();
}
