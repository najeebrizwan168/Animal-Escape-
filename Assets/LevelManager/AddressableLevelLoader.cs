using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressableLevelLoader : MonoBehaviour
{
    [Header("Database Reference")]
    public LevelDatabase levelDatabase; // Inspector mein apna LevelDatabase drag karein

    [Header("Spawn Configuration")]
    public Transform levelParent; // Scene mein LevelSpwan object yahan assign karein

    private GameObject currentSpawnedLevel;
    private bool isCurrentlyLoading = false;

    // ─── LEVEL LOAD KARNE KA FUNCTION ───────────────────────────────────────
    public void LoadLevelByNumber(int targetLevelNumber)
    {
        if (isCurrentlyLoading)
        {
            Debug.LogWarning("[LevelLoader] Pehle se ek level load ho raha hai, thoda sukoon karein!");
            return;
        }

        // 1. Pehle purani memory aur level ko scene se bilkul saaf (flush) karein
        UnloadCurrentLevel();

        // 2. Database mein se target level dhoondein
        LevelData selectedLevelData = FindLevelData(targetLevelNumber);

        if (selectedLevelData.levelPrefabReference == null)
        {
            Debug.LogError($"[LevelLoader] Level {targetLevelNumber} ka prefab database mein missing hai!");
            return;
        }

        isCurrentlyLoading = true;
        Debug.Log($"[LevelLoader] Disk (ROM) se {selectedLevelData.levelName} ka binary bundle read hona shuru ho gaya hai...");

        // 3. Addressables ke zariye async (background) loading shuru
        AsyncOperationHandle<GameObject> loadHandle;
        
        if (levelParent != null)
        {
            loadHandle = selectedLevelData.levelPrefabReference.InstantiateAsync(levelParent);
        }
        else
        {
            loadHandle = selectedLevelData.levelPrefabReference.InstantiateAsync();
        }

        // Loading complete hone par callback trigger hoga
        loadHandle.Completed += (handle) => {
            isCurrentlyLoading = false;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                currentSpawnedLevel = handle.Result;

                // 🔥 THE PERFECT PARENT MATRIX & SALOON DOOR SCALE FIX
                if (levelParent != null)
                {
                    // (1, 1, 1) karne ke bajaye parent ke scale ko neutralize kar rahe hain
                    // taaki level ka wahi exact scale (0.6, 0.3, 0.7) aaye jo aapne design kiya tha!
                    currentSpawnedLevel.transform.localScale = new Vector3(
                        0.6f / levelParent.localScale.x,
                        0.3f / levelParent.localScale.y,
                        0.7f / levelParent.localScale.z
                    );

                    // Local coordinates ko parent ke upar zero-lock karenge
                    currentSpawnedLevel.transform.localPosition = Vector3.zero;
                    currentSpawnedLevel.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    // Agar koi parent nahi hai, toh default scale aur angles force karenge
                    currentSpawnedLevel.transform.position = Vector3.zero;
                    currentSpawnedLevel.transform.rotation = Quaternion.identity;
                    currentSpawnedLevel.transform.localScale = new Vector3(0.6f, 0.3f, 0.7f);
                }

                Debug.Log($"[LevelLoader] {selectedLevelData.levelName} Transform Matrix Normalized perfectly without door compression!");
            }
            else
            {
                Debug.LogError($"[LevelLoader] Prefab load karne mein masla aaya: {handle.OperationException}");
            }
        };
    }

    // ─── RAM SE DATA FLUSH KARNE KA FUNCTION ────────────────────────────────
    public void UnloadCurrentLevel()
    {
        if (currentSpawnedLevel != null)
        {
            Debug.Log("[LevelLoader] RAM ko choke hone se bachane ke liye purana level flush kiya ja raha hai...");
            
            // Scene se GameObject delete bhi karegi aur RAM se asset bundle flush bhi karegi
            Addressables.ReleaseInstance(currentSpawnedLevel);
            currentSpawnedLevel = null;
        }
    }

    // Database search helper
    private LevelData FindLevelData(int levelNum)
    {
        if (levelDatabase != null && levelDatabase.allLevels != null)
        {
            foreach (var level in levelDatabase.allLevels)
            {
                if (level.levelNumber == levelNum) return level;
            }
        }
        return default;
    }

    private void OnDestroy()
    {
        // Safe side execution: Game quit ya scene change par memory automatically clear ho jaye
        UnloadCurrentLevel();
    }
}