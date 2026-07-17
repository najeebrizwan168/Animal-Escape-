using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HouseGenerator
{
    /// <summary>
    /// Runtime level generator that loads levels from a LevelDatabase ScriptableObject.
    /// Attach this to an empty GameObject in your game scene.
    /// Call LoadLevel(int levelNumber) to generate any level during gameplay.
    /// 
    /// All prefabs and materials are auto-resolved from the asset paths stored in the JSON —
    /// no manual mapping required (same as HouseSerializer.ImportHouse).
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        [Header("Level Database")]
        [Tooltip("Assign the LevelDatabase ScriptableObject that contains all level JSON references.")]
        public LevelDatabase levelDatabase;

        [Header("Settings")]
        [Tooltip("If true, automatically loads a level on Start.")]
        public bool loadOnStart = false;

        [Tooltip("Level number to load on Start (1-based). Only used if loadOnStart is true.")]
        public int startLevelNumber = 1;

        /// <summary>
        /// The level number currently loaded. 0 means no level is loaded.
        /// </summary>
        public int CurrentLevel { get; private set; } = 0;

        /// <summary>
        /// The root GameObject of the currently loaded level. Null if no level is loaded.
        /// </summary>
        public GameObject CurrentLevelRoot { get; private set; } = null;

        private void Start()
        {
            if (loadOnStart && levelDatabase != null)
            {
                LoadLevel(startLevelNumber);
            }
        }

        /// <summary>
        /// Destroys the currently loaded level (all children under this transform).
        /// </summary>
        public void UnloadCurrentLevel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }

            CurrentLevel = 0;
            CurrentLevelRoot = null;
        }

        /// <summary>
        /// Loads a level by its number (1-based index).
        /// Automatically destroys the previous level before loading the new one.
        /// All prefabs/materials are auto-loaded from the paths inside the JSON.
        /// </summary>
        /// <param name="levelNumber">1-based level number (1 = first level).</param>
        /// <returns>The root GameObject of the generated level, or null on failure.</returns>
        public GameObject LoadLevel(int levelNumber)
        {
            if (levelDatabase == null)
            {
                Debug.LogError("[LevelGenerator] LevelDatabase is not assigned!");
                return null;
            }

            TextAsset jsonFile = levelDatabase.GetLevelJson(levelNumber);
            if (jsonFile == null)
            {
                Debug.LogError($"[LevelGenerator] Failed to get JSON for level {levelNumber}.");
                return null;
            }

            // Destroy the previous level
            UnloadCurrentLevel();

            // Parse JSON
            HouseData data = JsonUtility.FromJson<HouseData>(jsonFile.text);
            if (data == null || data.rootNode == null)
            {
                Debug.LogError($"[LevelGenerator] Failed to parse JSON data for level {levelNumber}.");
                return null;
            }

            // Generate the level hierarchy under this transform
            GameObject root = DeserializeGameObject(data.rootNode, transform, true);

            if (root != null)
            {
                CurrentLevel = levelNumber;
                CurrentLevelRoot = root;
                Debug.Log($"[LevelGenerator] Level {levelNumber} loaded successfully. Root: '{root.name}'");
            }

            return root;
        }

        /// <summary>
        /// Loads the next level (CurrentLevel + 1).
        /// </summary>
        public GameObject LoadNextLevel()
        {
            return LoadLevel(CurrentLevel + 1);
        }

        /// <summary>
        /// Reloads the current level from scratch.
        /// </summary>
        public GameObject ReloadCurrentLevel()
        {
            if (CurrentLevel <= 0)
            {
                Debug.LogWarning("[LevelGenerator] No level is currently loaded to reload.");
                return null;
            }
            return LoadLevel(CurrentLevel);
        }

        /// <summary>
        /// Checks if a given level number exists in the database.
        /// </summary>
        public bool HasLevel(int levelNumber)
        {
            if (levelDatabase == null) return false;
            int index = levelNumber - 1;
            return index >= 0 && index < levelDatabase.TotalLevels && levelDatabase.levelJsonFiles[index] != null;
        }

        // =====================================================================
        // DESERIALIZATION — Same logic as HouseSerializer.ImportHouse
        // No manual mapping needed, auto-loads from AssetDatabase paths.
        // =====================================================================

        private GameObject DeserializeGameObject(SerializedNode node, Transform parent, bool isRoot = false)
        {
            GameObject go = null;
            bool isNew = false;

            // Check if this object already exists as part of a parent prefab
            if (parent != null)
            {
                Transform existing = parent.Find(node.name);
                if (existing != null)
                {
                    go = existing.gameObject;
                }
            }

            // If not found, instantiate or create
            if (go == null)
            {
                isNew = true;

                // Try prefab instantiation directly from path
                if (!string.IsNullOrEmpty(node.prefabPath))
                {
#if UNITY_EDITOR
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(node.prefabPath);
                    if (prefabAsset != null)
                    {
                        go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                    }
#else
                    // In builds, load from Resources or Addressables if needed
                    Debug.LogWarning($"[LevelGenerator] Cannot load prefab '{node.prefabPath}' at runtime in builds without Addressables.");
#endif
                }

                // Try mesh/primitive creation
                if (go == null)
                {
                    if (!string.IsNullOrEmpty(node.meshName))
                    {
                        switch (node.meshName)
                        {
                            case "Cube": go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                            case "Plane": go = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                            case "Sphere": go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                            case "Cylinder": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                            case "Capsule": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                            default:
#if UNITY_EDITOR
                                // Try loading the mesh from its asset path
                                if (!string.IsNullOrEmpty(node.meshPath))
                                {
                                    MeshFilter newFilter = null;
                                    go = new GameObject(node.name);
                                    newFilter = go.AddComponent<MeshFilter>();
                                    go.AddComponent<MeshRenderer>();

                                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(node.meshPath);
                                    foreach (Object asset in assets)
                                    {
                                        if (asset is Mesh m && m.name == node.meshName)
                                        {
                                            newFilter.sharedMesh = m;
                                            break;
                                        }
                                    }
                                }
#endif
                                // Fallback: try finding a template from already instantiated objects
                                if (go == null)
                                {
                                    GameObject template = FindTemplateByMeshName(transform, node.meshName);
                                    if (template != null)
                                    {
                                        go = Instantiate(template);
                                        for (int i = go.transform.childCount - 1; i >= 0; i--)
                                        {
                                            if (Application.isPlaying)
                                                Destroy(go.transform.GetChild(i).gameObject);
                                            else
                                                DestroyImmediate(go.transform.GetChild(i).gameObject);
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    if (go == null)
                        go = new GameObject(node.name);
                }
            }

            // Set basic properties
            go.name = node.name;
            go.SetActive(node.isActive);

            if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
            {
                try { go.tag = node.tag; }
                catch { }
            }

            if (node.layer > 0 && node.layer <= 31)
                go.layer = node.layer;

            // Parent and transform
            if (parent != null && isNew)
                go.transform.SetParent(parent);

            if (isRoot)
            {
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

            // Apply Mesh and Materials (same as HouseSerializer.ImportHouse)
            if (isNew)
            {
                // Mesh
                if (!string.IsNullOrEmpty(node.meshPath) && !string.IsNullOrEmpty(node.meshName))
                {
                    MeshFilter filter = go.GetComponent<MeshFilter>();
                    if (filter == null) filter = go.AddComponent<MeshFilter>();

#if UNITY_EDITOR
                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(node.meshPath);
                    foreach (Object asset in assets)
                    {
                        if (asset is Mesh m && m.name == node.meshName)
                        {
                            filter.sharedMesh = m;
                            break;
                        }
                    }
#endif

                    MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                    if (meshRenderer == null) go.AddComponent<MeshRenderer>();
                }

                // Materials
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (node.materials != null && node.materials.Count > 0)
                    {
                        Material[] mats = new Material[node.materials.Count];
                        for (int i = 0; i < node.materials.Count; i++)
                        {
                            var sMat = node.materials[i];
                            if (sMat == null) continue;

                            // 1. Try loading directly from asset path (same as HouseSerializer)
#if UNITY_EDITOR
                            if (!string.IsNullOrEmpty(sMat.assetPath))
                            {
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(sMat.assetPath);
                            }
#endif

                            // 2. Fallback: recreate material dynamically
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

#if UNITY_EDITOR
                                    if (!string.IsNullOrEmpty(sMat.mainTexturePath))
                                    {
                                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sMat.mainTexturePath);
                                        if (tex != null)
                                        {
                                            newMat.mainTexture = tex;
                                            if (newMat.HasProperty("_BaseMap"))
                                                newMat.SetTexture("_BaseMap", tex);
                                            else if (newMat.HasProperty("_MainTex"))
                                                newMat.SetTexture("_MainTex", tex);
                                        }
                                    }
#endif
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
#if UNITY_EDITOR
                            if (!string.IsNullOrEmpty(node.materialPaths[i]))
                            {
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(node.materialPaths[i]);
                            }
#endif
                        }
                        renderer.sharedMaterials = mats;
                    }
                }
            }

            // =====================================================================
            // RECURSE ALL CHILDREN
            // =====================================================================
            foreach (SerializedNode childNode in node.children)
            {
                DeserializeGameObject(childNode, go.transform, false);
            }

            // =====================================================================
            // RESTORE ALL GENERIC COMPONENTS
            // =====================================================================
            if (node.components != null && node.components.Count > 0)
            {
                foreach (var sc in node.components)
                {
                    if (sc.typeName == "UnityEngine.Transform" || sc.typeName == "UnityEngine.MeshFilter" ||
                        sc.typeName == "UnityEngine.MeshRenderer") continue;

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
                        Debug.LogWarning($"[LevelGenerator] Cannot find type '{sc.typeName}' to restore on '{go.name}'");
                        continue;
                    }

                    Component comp = go.GetComponent(compType);
                    if (comp == null)
                        comp = go.AddComponent(compType);
                    if (comp == null)
                    {
                        Debug.LogWarning($"[LevelGenerator] Failed to add component '{sc.typeName}' on '{go.name}'");
                        continue;
                    }

                    // Restore enabled state
                    if (comp is Behaviour b)
                        b.enabled = sc.enabled;
                    else if (comp is Collider col)
                        col.enabled = sc.enabled;

#if UNITY_EDITOR
                    // Use SerializedObject for maximum accuracy (same as HouseSerializer.ImportHouse)
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
                                    sp.animationCurveValue = JsonUtility.FromJson<AnimationCurve>(prop.value);
                                    break;
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
                            Debug.LogWarning($"[LevelGenerator] Failed to restore '{prop.name}' on '{sc.typeName}': {ex.Message}");
                        }
                    }
                    so.ApplyModifiedProperties();
#else
                    // Runtime fallback: use reflection
                    RestoreViaReflection(comp, sc);
#endif
                }
            }

            // Legacy custom component restore (backward compatibility with old JSONs)
            if (node.customizedComponents != null && node.customizedComponents.Count > 0)
            {
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

            return go;
        }

        // =====================================================================
        // RUNTIME REFLECTION FALLBACK (for builds without AssetDatabase)
        // =====================================================================

#if !UNITY_EDITOR
        private void RestoreViaReflection(Component comp, SerializedComponent sc)
        {
            System.Type type = comp.GetType();
            foreach (var prop in sc.properties)
            {
                try
                {
                    string fieldName = prop.name;
                    var field = type.GetField(fieldName,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (field != null)
                    {
                        object val = ConvertValue(prop, field.FieldType);
                        if (val != null) field.SetValue(comp, val);
                        continue;
                    }

                    // Try C# property (m_Mass -> mass)
                    string csPropName = ConvertSerializedName(fieldName);
                    var csProp = type.GetProperty(csPropName,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (csProp != null && csProp.CanWrite)
                    {
                        object val = ConvertValue(prop, csProp.PropertyType);
                        if (val != null) csProp.SetValue(comp, val);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LevelGenerator] Reflection restore failed for '{prop.name}': {ex.Message}");
                }
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

        private object ConvertValue(SerializedField prop, System.Type targetType)
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

        // =====================================================================
        // UTILITY
        // =====================================================================

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
}
