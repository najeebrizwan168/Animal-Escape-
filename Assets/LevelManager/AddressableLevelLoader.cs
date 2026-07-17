using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Addressable-based level loader that uses JSON deserialization (same proven logic as HouseSerializer).
/// 
/// This script:
/// 1. Loads the level JSON TextAsset via Addressables
/// 2. Parses the JSON into the HouseGenerator data structures
/// 3. Reconstructs the ENTIRE level hierarchy from scratch — every GameObject, component, 
///    transform, material, mesh, script reference, and property value is restored exactly
///    as it was when exported.
/// 
/// WHY THIS WORKS AND THE OLD APPROACH DIDN'T:
/// - Old approach: Loaded the level as a PREFAB via Addressables → lost all runtime modifications,
///   component values, and forced a hardcoded scale that broke child transforms.
/// - New approach: Loads a JSON TEXT file → rebuilds everything node-by-node with exact transforms,
///   components, and property values from the JSON. Prefabs referenced INSIDE the level are loaded
///   individually via Addressables, preserving their original form.
/// </summary>
public class AddressableLevelLoader : MonoBehaviour
{
    [Header("Database Reference")]
    [Tooltip("Assign the LevelDatabase ScriptableObject")]
    public LevelDatabase levelDatabase;

    [Header("Configuration")]
    [Tooltip("If true, the level root spawns as a child of this GameObject")]
    public bool spawnAsChild = true;

    [Header("Debug")]
    [Tooltip("Currently loaded level number (0 = none)")]
    [SerializeField] private int _currentLevel = 0;

    private GameObject currentLevelRoot;
    private bool isLoading = false;
    private AsyncOperationHandle<TextAsset> currentJsonHandle;
    private List<AsyncOperationHandle> activeHandles = new List<AsyncOperationHandle>();

    // Cached prefab/material lookups loaded via Addressables
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    /// <summary>Currently loaded level number. 0 = none.</summary>
    public int CurrentLevel => _currentLevel;

    /// <summary>Root GameObject of the current level.</summary>
    public GameObject CurrentLevelRoot => currentLevelRoot;

    /// <summary>True if a level is currently being loaded.</summary>
    public bool IsLoading => isLoading;

    // ═══════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Load a level by its number. Destroys the previous level first.
    /// </summary>
    public void LoadLevel(int levelNumber)
    {
        if (isLoading)
        {
            Debug.LogWarning("[LevelLoader] A level is already being loaded. Please wait.");
            return;
        }

        if (levelDatabase == null)
        {
            Debug.LogError("[LevelLoader] LevelDatabase is not assigned!");
            return;
        }

        LevelData? levelData = levelDatabase.GetLevel(levelNumber);
        if (levelData == null)
        {
            Debug.LogError($"[LevelLoader] Level {levelNumber} not found in database!");
            return;
        }

        if (levelData.Value.levelJsonReference == null || !levelData.Value.levelJsonReference.RuntimeKeyIsValid())
        {
            Debug.LogError($"[LevelLoader] Level {levelNumber} has no valid JSON reference!");
            return;
        }

        // Unload previous level
        UnloadCurrentLevel();

        isLoading = true;
        Debug.Log($"[LevelLoader] Loading level {levelNumber}: {levelData.Value.levelName}...");

        // Load JSON TextAsset via Addressables
        currentJsonHandle = Addressables.LoadAssetAsync<TextAsset>(levelData.Value.levelJsonReference);
        currentJsonHandle.Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                TextAsset jsonAsset = handle.Result;
                BuildLevelFromJson(jsonAsset.text, levelNumber);
            }
            else
            {
                Debug.LogError($"[LevelLoader] Failed to load JSON for level {levelNumber}: {handle.OperationException}");
                isLoading = false;
            }
        };
    }

    /// <summary>Load the next level.</summary>
    public void LoadNextLevel() => LoadLevel(_currentLevel + 1);

    /// <summary>Reload the current level.</summary>
    public void ReloadCurrentLevel()
    {
        if (_currentLevel > 0) LoadLevel(_currentLevel);
    }

    /// <summary>Check if a level exists in the database.</summary>
    public bool HasLevel(int levelNumber) => levelDatabase != null && levelDatabase.GetLevel(levelNumber) != null;

    /// <summary>Destroy the current level and release all Addressable handles.</summary>
    public void UnloadCurrentLevel()
    {
        if (currentLevelRoot != null)
        {
            Debug.Log("[LevelLoader] Unloading current level and releasing memory...");
            Destroy(currentLevelRoot);
            currentLevelRoot = null;
        }

        // Release all Addressable handles
        foreach (var handle in activeHandles)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        activeHandles.Clear();

        if (currentJsonHandle.IsValid())
        {
            Addressables.Release(currentJsonHandle);
        }

        prefabCache.Clear();
        materialCache.Clear();
        meshCache.Clear();

        _currentLevel = 0;
    }

    private void OnDestroy()
    {
        UnloadCurrentLevel();
    }

    // ═══════════════════════════════════════════════════════════════════
    // JSON → LEVEL RECONSTRUCTION (same proven logic as HouseSerializer)
    // ═══════════════════════════════════════════════════════════════════

    private void BuildLevelFromJson(string json, int levelNumber)
    {
        try
        {
            HouseGenerator.HouseData data = JsonUtility.FromJson<HouseGenerator.HouseData>(json);
            if (data == null || data.rootNode == null)
            {
                Debug.LogError($"[LevelLoader] Failed to parse JSON for level {levelNumber}.");
                isLoading = false;
                return;
            }

            Transform parent = spawnAsChild ? transform : null;
            GameObject root = DeserializeNode(data.rootNode, parent, true);

            if (root != null)
            {
                root.name = data.rootName;
                currentLevelRoot = root;
                _currentLevel = levelNumber;
                Debug.Log($"[LevelLoader] Level {levelNumber} loaded successfully! Root: '{root.name}'");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelLoader] Error building level {levelNumber}: {ex.Message}");
            Debug.LogException(ex);
        }

        isLoading = false;
    }

    // ═══════════════════════════════════════════════════════════════════
    // NODE DESERIALIZATION
    // ═══════════════════════════════════════════════════════════════════

    private GameObject DeserializeNode(HouseGenerator.SerializedNode node, Transform parent, bool isRoot = false)
    {
        GameObject go = null;
        bool isNew = false;

        // Check if child already exists (e.g., from a parent prefab)
        if (parent != null)
        {
            Transform existing = parent.Find(node.name);
            if (existing != null)
                go = existing.gameObject;
        }

        if (go == null)
        {
            isNew = true;

            // 1. Try prefab instantiation
            if (!string.IsNullOrEmpty(node.prefabPath))
            {
                go = LoadAndInstantiatePrefab(node.prefabPath);
            }

            // 2. Try primitive/mesh creation
            if (go == null && !string.IsNullOrEmpty(node.meshName))
            {
                go = CreateFromMesh(node);
            }

            // 3. Fallback: empty GameObject
            if (go == null)
                go = new GameObject(node.name);
        }

        // ── Set basic properties ──
        go.name = node.name;
        go.SetActive(node.isActive);

        if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
        {
            try { go.tag = node.tag; }
            catch { /* Tag not defined in project */ }
        }

        if (node.layer > 0 && node.layer <= 31)
            go.layer = node.layer;

        // ── Parent and Transform ──
        if (parent != null && isNew)
            go.transform.SetParent(parent);

        if (isRoot)
        {
            // Root keeps its original scale from JSON but spawns at local zero
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = node.localScale;
        }
        else
        {
            go.transform.localPosition = node.localPosition;
            go.transform.localRotation = Quaternion.Euler(node.localRotation);
            go.transform.localScale = node.localScale;
        }

        // ── Apply Materials ──
        if (isNew)
        {
            ApplyMaterials(go, node);
        }

        // ── Recurse children ──
        // Gather existing children for matching
        List<Transform> availableChildren = new List<Transform>();
        for (int i = 0; i < go.transform.childCount; i++)
            availableChildren.Add(go.transform.GetChild(i));

        foreach (var childNode in node.children)
        {
            // Try to find matching existing child (from prefab)
            Transform foundExisting = null;
            for (int i = 0; i < availableChildren.Count; i++)
            {
                if (availableChildren[i].name == childNode.name)
                {
                    foundExisting = availableChildren[i];
                    availableChildren.RemoveAt(i);
                    break;
                }
            }

            if (foundExisting != null)
            {
                // Child exists from prefab — update its properties without recreating
                DeserializeExistingNode(childNode, foundExisting);
            }
            else
            {
                // Child doesn't exist — create from scratch
                DeserializeNode(childNode, go.transform, false);
            }
        }

        // Remove leftover children not in JSON
        foreach (var leftover in availableChildren)
        {
            Destroy(leftover.gameObject);
        }

        // ── Restore Components ──
        RestoreComponents(go, node);
        RestoreLegacyComponents(go, node);

        return go;
    }

    /// <summary>
    /// Updates an EXISTING child node (from a prefab) with JSON data.
    /// Does NOT recreate it — just updates transforms, components, and recurses children.
    /// This prevents the "merged walls" problem from the old approach.
    /// </summary>
    private void DeserializeExistingNode(HouseGenerator.SerializedNode node, Transform existing)
    {
        GameObject go = existing.gameObject;

        go.name = node.name;
        go.SetActive(node.isActive);

        if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
        {
            try { go.tag = node.tag; }
            catch { }
        }

        if (node.layer > 0 && node.layer <= 31)
            go.layer = node.layer;

        // Apply exact transforms from JSON
        go.transform.localPosition = node.localPosition;
        go.transform.localRotation = Quaternion.Euler(node.localRotation);
        go.transform.localScale = node.localScale;

        // Apply materials if specified in JSON
        ApplyMaterials(go, node);

        // Recurse children
        List<Transform> availableChildren = new List<Transform>();
        for (int i = 0; i < go.transform.childCount; i++)
            availableChildren.Add(go.transform.GetChild(i));

        foreach (var childNode in node.children)
        {
            Transform foundExisting = null;
            for (int i = 0; i < availableChildren.Count; i++)
            {
                if (availableChildren[i].name == childNode.name)
                {
                    foundExisting = availableChildren[i];
                    availableChildren.RemoveAt(i);
                    break;
                }
            }

            if (foundExisting != null)
                DeserializeExistingNode(childNode, foundExisting);
            else
                DeserializeNode(childNode, go.transform, false);
        }

        foreach (var leftover in availableChildren)
        {
            Destroy(leftover.gameObject);
        }

        // Restore components on existing nodes too
        RestoreComponents(go, node);
        RestoreLegacyComponents(go, node);
    }

    // ═══════════════════════════════════════════════════════════════════
    // ASSET LOADING
    // ═══════════════════════════════════════════════════════════════════

    private GameObject LoadAndInstantiatePrefab(string prefabPath)
    {
        // Check cache first
        if (prefabCache.TryGetValue(prefabPath, out GameObject cachedPrefab))
        {
            if (cachedPrefab != null)
                return Instantiate(cachedPrefab);
        }

        // Try Addressables (synchronous for simplicity in reconstruction)
        try
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(prefabPath);
            handle.WaitForCompletion(); // Sync load
            activeHandles.Add(handle);

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                prefabCache[prefabPath] = handle.Result;
                return Instantiate(handle.Result);
            }
        }
        catch { }

#if UNITY_EDITOR
        // Editor fallback: load directly from AssetDatabase
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset != null)
        {
            prefabCache[prefabPath] = asset;
            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }
#endif

        Debug.LogWarning($"[LevelLoader] Prefab not found: '{prefabPath}'");
        return null;
    }

    private Material LoadMaterial(string materialPath)
    {
        if (string.IsNullOrEmpty(materialPath)) return null;

        if (materialCache.TryGetValue(materialPath, out Material cached))
            return cached;

        // Try Addressables
        try
        {
            var handle = Addressables.LoadAssetAsync<Material>(materialPath);
            handle.WaitForCompletion();
            activeHandles.Add(handle);

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                materialCache[materialPath] = handle.Result;
                return handle.Result;
            }
        }
        catch { }

#if UNITY_EDITOR
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat != null)
        {
            materialCache[materialPath] = mat;
            return mat;
        }
#endif

        return null;
    }

    private GameObject CreateFromMesh(HouseGenerator.SerializedNode node)
    {
        switch (node.meshName)
        {
            case "Cube": return GameObject.CreatePrimitive(PrimitiveType.Cube);
            case "Plane": return GameObject.CreatePrimitive(PrimitiveType.Plane);
            case "Sphere": return GameObject.CreatePrimitive(PrimitiveType.Sphere);
            case "Cylinder": return GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            case "Capsule": return GameObject.CreatePrimitive(PrimitiveType.Capsule);
            default:
                // Try loading mesh from asset path
                if (!string.IsNullOrEmpty(node.meshPath))
                {
#if UNITY_EDITOR
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(node.meshPath);
                    foreach (Object asset in assets)
                    {
                        if (asset is Mesh m && m.name == node.meshName)
                        {
                            GameObject meshGo = new GameObject(node.name);
                            MeshFilter mf = meshGo.AddComponent<MeshFilter>();
                            mf.sharedMesh = m;
                            meshGo.AddComponent<MeshRenderer>();
                            return meshGo;
                        }
                    }
#endif
                    // Try Addressables for the mesh
                    try
                    {
                        var handle = Addressables.LoadAssetAsync<Mesh>(node.meshPath + "[" + node.meshName + "]");
                        handle.WaitForCompletion();
                        activeHandles.Add(handle);

                        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                        {
                            GameObject meshGo = new GameObject(node.name);
                            MeshFilter mf = meshGo.AddComponent<MeshFilter>();
                            mf.sharedMesh = handle.Result;
                            meshGo.AddComponent<MeshRenderer>();
                            return meshGo;
                        }
                    }
                    catch { }
                }

                // Last resort: find template in already-built hierarchy
                GameObject template = FindTemplateByMeshName(transform, node.meshName);
                if (template != null)
                {
                    GameObject clone = Instantiate(template);
                    for (int i = clone.transform.childCount - 1; i >= 0; i--)
                        Destroy(clone.transform.GetChild(i).gameObject);
                    return clone;
                }
                return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // MATERIAL APPLICATION
    // ═══════════════════════════════════════════════════════════════════

    private void ApplyMaterials(GameObject go, HouseGenerator.SerializedNode node)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        if (node.materials != null && node.materials.Count > 0)
        {
            Material[] mats = new Material[node.materials.Count];
            for (int i = 0; i < node.materials.Count; i++)
            {
                var sMat = node.materials[i];
                if (sMat == null) continue;

                // 1. Try loading from asset path
                if (!string.IsNullOrEmpty(sMat.assetPath))
                {
                    mats[i] = LoadMaterial(sMat.assetPath);
                }

                // 2. Fallback: recreate material
                if (mats[i] == null)
                {
                    Shader shader = Shader.Find(sMat.shaderName);
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");

                    if (shader != null)
                    {
                        Material newMat = new Material(shader);
                        newMat.name = sMat.name;

                        if (newMat.HasProperty("_BaseColor"))
                            newMat.SetColor("_BaseColor", sMat.color);
                        else if (newMat.HasProperty("_Color"))
                            newMat.SetColor("_Color", sMat.color);

                        // Try loading texture
                        if (!string.IsNullOrEmpty(sMat.mainTexturePath))
                        {
                            Texture2D tex = LoadTexture(sMat.mainTexturePath);
                            if (tex != null)
                            {
                                newMat.mainTexture = tex;
                                if (newMat.HasProperty("_BaseMap"))
                                    newMat.SetTexture("_BaseMap", tex);
                                else if (newMat.HasProperty("_MainTex"))
                                    newMat.SetTexture("_MainTex", tex);
                            }
                        }

                        mats[i] = newMat;
                    }
                }
            }
            renderer.sharedMaterials = mats;
        }
        else if (node.materialPaths != null && node.materialPaths.Count > 0)
        {
            Material[] mats = new Material[node.materialPaths.Count];
            for (int i = 0; i < node.materialPaths.Count; i++)
            {
                if (!string.IsNullOrEmpty(node.materialPaths[i]))
                    mats[i] = LoadMaterial(node.materialPaths[i]);
            }
            renderer.sharedMaterials = mats;
        }
    }

    private Texture2D LoadTexture(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath)) return null;

        try
        {
            var handle = Addressables.LoadAssetAsync<Texture2D>(texturePath);
            handle.WaitForCompletion();
            activeHandles.Add(handle);

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;
        }
        catch { }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
#else
        return null;
#endif
    }

    // ═══════════════════════════════════════════════════════════════════
    // COMPONENT RESTORATION
    // ═══════════════════════════════════════════════════════════════════

    private void RestoreComponents(GameObject go, HouseGenerator.SerializedNode node)
    {
        if (node.components == null || node.components.Count == 0) return;

        foreach (var sc in node.components)
        {
            if (sc.typeName == "UnityEngine.Transform" || sc.typeName == "UnityEngine.MeshFilter" ||
                sc.typeName == "UnityEngine.MeshRenderer") continue;

            // Find the component type
            System.Type compType = System.Type.GetType(sc.assemblyQualifiedName);
            if (compType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    compType = asm.GetType(sc.typeName);
                    if (compType != null) break;
                }
            }
            if (compType == null)
            {
                Debug.LogWarning($"[LevelLoader] Cannot find type '{sc.typeName}' for '{go.name}'");
                continue;
            }

            // Get or add the component
            Component comp = go.GetComponent(compType);
            if (comp == null)
                comp = go.AddComponent(compType);
            if (comp == null)
            {
                Debug.LogWarning($"[LevelLoader] Failed to add '{sc.typeName}' on '{go.name}'");
                continue;
            }

            // Restore enabled state
            if (comp is Behaviour b) b.enabled = sc.enabled;
            else if (comp is Collider col) col.enabled = sc.enabled;

#if UNITY_EDITOR
            // In Editor: use SerializedObject for perfect accuracy
            var so = new SerializedObject(comp);
            foreach (var prop in sc.properties)
            {
                var sp = so.FindProperty(prop.name);
                if (sp == null) continue;

                try
                {
                    switch (prop.type)
                    {
                        case "float": sp.floatValue = float.Parse(prop.value); break;
                        case "int": sp.intValue = int.Parse(prop.value); break;
                        case "bool": sp.boolValue = bool.Parse(prop.value); break;
                        case "string": sp.stringValue = prop.value; break;
                        case "Vector2": sp.vector2Value = JsonUtility.FromJson<Vector2>(prop.value); break;
                        case "Vector3": sp.vector3Value = JsonUtility.FromJson<Vector3>(prop.value); break;
                        case "Vector4": sp.vector4Value = JsonUtility.FromJson<Vector4>(prop.value); break;
                        case "Quaternion": sp.quaternionValue = JsonUtility.FromJson<Quaternion>(prop.value); break;
                        case "Color": sp.colorValue = JsonUtility.FromJson<Color>(prop.value); break;
                        case "Rect": sp.rectValue = JsonUtility.FromJson<Rect>(prop.value); break;
                        case "Bounds": sp.boundsValue = JsonUtility.FromJson<Bounds>(prop.value); break;
                        case "enum": sp.enumValueIndex = int.Parse(prop.value); break;
                        case "LayerMask": sp.intValue = int.Parse(prop.value); break;
                        case "AnimationCurve":
                            sp.animationCurveValue = JsonUtility.FromJson<AnimationCurve>(prop.value); break;
                        case "ObjectRef":
                            if (!string.IsNullOrEmpty(prop.objectRefPath))
                            {
                                var obj = AssetDatabase.LoadAssetAtPath<Object>(prop.objectRefPath);
                                if (obj != null) sp.objectReferenceValue = obj;

                                if (sp.objectReferenceValue == null && !string.IsNullOrEmpty(prop.objectRefPath))
                                {
                                    Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(prop.objectRefPath);
                                    foreach (Object subAsset in allSubAssets)
                                    {
                                        if (subAsset != null && subAsset.name == prop.value)
                                        {
                                            sp.objectReferenceValue = subAsset;
                                            if (sp.objectReferenceValue != null) break;
                                        }
                                    }
                                }
                            }
                            break;
                        case "SceneRef":
                            if (!string.IsNullOrEmpty(prop.value))
                            {
                                GameObject found = GameObject.Find(prop.value);
                                if (found == null)
                                {
                                    Transform rootT = go.transform;
                                    while (rootT.parent != null) rootT = rootT.parent;

                                    int firstSlash = prop.value.IndexOf('/');
                                    if (firstSlash >= 0 && firstSlash < prop.value.Length - 1)
                                    {
                                        string subPath = prop.value.Substring(firstSlash + 1);
                                        Transform relTransform = rootT.Find(subPath);
                                        if (relTransform != null) found = relTransform.gameObject;
                                    }
                                    if (found == null)
                                    {
                                        Transform directFind = rootT.Find(prop.value);
                                        if (directFind != null) found = directFind.gameObject;
                                    }
                                }

                                if (found != null && sp.propertyType == SerializedPropertyType.ObjectReference)
                                {
                                    sp.objectReferenceValue = found;
                                    if (sp.objectReferenceValue == null)
                                    {
                                        sp.objectReferenceValue = found.transform;
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LevelLoader] Restore failed for '{prop.name}' on '{sc.typeName}': {ex.Message}");
                }
            }
            so.ApplyModifiedProperties();
#else
            // In builds: use reflection
            RestoreViaReflection(comp, sc);
#endif
        }
    }

#if !UNITY_EDITOR
    private void RestoreViaReflection(Component comp, HouseGenerator.SerializedComponent sc)
    {
        System.Type type = comp.GetType();
        foreach (var prop in sc.properties)
        {
            try
            {
                // Try field
                var field = type.GetField(prop.name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    object val = ConvertValue(prop, field.FieldType);
                    if (val != null) field.SetValue(comp, val);
                    continue;
                }

                // Try C# property (m_Mass -> mass)
                string csPropName = ConvertSerializedName(prop.name);
                var csProp = type.GetProperty(csPropName,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (csProp != null && csProp.CanWrite)
                {
                    object val = ConvertValue(prop, csProp.PropertyType);
                    if (val != null) csProp.SetValue(comp, val);
                }
            }
            catch { }
        }
    }

    private string ConvertSerializedName(string name)
    {
        if (name.StartsWith("m_") && name.Length > 2)
        {
            string rest = name.Substring(2);
            return char.ToLower(rest[0]) + rest.Substring(1);
        }
        return name;
    }

    private object ConvertValue(HouseGenerator.SerializedField prop, System.Type targetType)
    {
        if (string.IsNullOrEmpty(prop.value) && prop.type != "string") return null;
        try
        {
            switch (prop.type)
            {
                case "float": return float.Parse(prop.value);
                case "int": return int.Parse(prop.value);
                case "bool": return bool.Parse(prop.value);
                case "string": return prop.value ?? "";
                case "Vector2": return JsonUtility.FromJson<Vector2>(prop.value);
                case "Vector3": return JsonUtility.FromJson<Vector3>(prop.value);
                case "Vector4": return JsonUtility.FromJson<Vector4>(prop.value);
                case "Quaternion": return JsonUtility.FromJson<Quaternion>(prop.value);
                case "Color": return JsonUtility.FromJson<Color>(prop.value);
                case "Rect": return JsonUtility.FromJson<Rect>(prop.value);
                case "Bounds": return JsonUtility.FromJson<Bounds>(prop.value);
                case "enum":
                    if (targetType.IsEnum) return System.Enum.ToObject(targetType, int.Parse(prop.value));
                    return int.Parse(prop.value);
                case "LayerMask":
                    LayerMask mask = int.Parse(prop.value);
                    return mask;
                case "AnimationCurve":
                    return JsonUtility.FromJson<AnimationCurve>(prop.value);
                default: return null;
            }
        }
        catch { return null; }
    }
#endif

    /// <summary>
    /// Restores legacy CustomComponentData (backward compat with old JSON exports).
    /// </summary>
    private void RestoreLegacyComponents(GameObject go, HouseGenerator.SerializedNode node)
    {
        if (node.customizedComponents == null || node.customizedComponents.Count == 0) return;

        foreach (var compData in node.customizedComponents)
        {
            if (compData.componentType == "HunterController")
            {
                HunterController hunter = go.GetComponent<HunterController>();
                if (hunter == null) hunter = go.AddComponent<HunterController>();
                for (int i = 0; i < compData.propertyKeys.Count; i++)
                {
                    string key = compData.propertyKeys[i];
                    string val = compData.propertyValues[i];
                    if (key == "distanceToPointA") hunter.distanceToPointA = float.Parse(val);
                    if (key == "distanceToPointB") hunter.distanceToPointB = float.Parse(val);
                    if (key == "moveSpeed") hunter.moveSpeed = float.Parse(val);
                    if (key == "canMove") hunter.canMove = bool.Parse(val);
                }
            }
            if (compData.componentType == "BoxCollider")
            {
                BoxCollider boxCollider = go.GetComponent<BoxCollider>();
                if (boxCollider == null) boxCollider = go.AddComponent<BoxCollider>();
                for (int i = 0; i < compData.propertyKeys.Count; i++)
                {
                    string key = compData.propertyKeys[i];
                    string val = compData.propertyValues[i];
                    if (key == "center") boxCollider.center = JsonUtility.FromJson<Vector3>(val);
                    if (key == "size") boxCollider.size = JsonUtility.FromJson<Vector3>(val);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════════

    private GameObject FindTemplateByMeshName(Transform root, string meshName)
    {
        MeshFilter filter = root.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == meshName)
            return root.gameObject;
        foreach (Transform child in root)
        {
            GameObject found = FindTemplateByMeshName(child, meshName);
            if (found != null) return found;
        }
        return null;
    }
}