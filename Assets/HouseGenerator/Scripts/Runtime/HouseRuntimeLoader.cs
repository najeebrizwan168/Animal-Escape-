using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HouseGenerator
{
    [System.Serializable]
    public class SerializedMaterial
    {
        public string name;
        public string assetPath;
        public string shaderName;
        public Color color = Color.white;
        public string mainTexturePath;
    }

    // Generic property storage: key=fieldName, value=json or string representation
    [System.Serializable]
    public class SerializedField
    {
        public string name;
        public string type;  // "float","int","bool","string","Vector3","Quaternion","Color","ObjectRef","enum"
        public string value;
        public string objectRefPath; // AssetDatabase path for object references
    }

    // Generic component storage
    [System.Serializable]
    public class SerializedComponent
    {
        public string typeName;           // Full type name e.g. "UnityEngine.BoxCollider", "HunterController"
        public string assemblyQualifiedName; // For AddComponent via reflection
        public bool enabled = true;
        public List<SerializedField> properties = new List<SerializedField>();
    }

    // Legacy support
    [System.Serializable]
    public class CustomComponentData
    {
        public string componentType;
        public List<string> propertyKeys = new List<string>();
        public List<string> propertyValues = new List<string>();
    }

    [System.Serializable]
    public class SerializedNode
    {
        public string name;
        public string prefabPath;
        public string meshPath;
        public string meshName;
        public string tag;
        public int layer;
        public bool isActive = true;
        public List<string> materialPaths;
        public List<SerializedMaterial> materials = new List<SerializedMaterial>();
        public Vector3 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;
        public List<SerializedNode> children = new List<SerializedNode>();

        // NEW: generic component data
        public List<SerializedComponent> components = new List<SerializedComponent>();

        // Legacy
        public List<CustomComponentData> customizedComponents = new List<CustomComponentData>();
    }

    [System.Serializable]
    public class HouseData
    {
        public string rootName;
        public SerializedNode rootNode;
    }

    [System.Serializable]
    public struct PrefabMapping
    {
        public string prefabPath;
        public GameObject prefab;
    }

    [System.Serializable]
    public struct MaterialMapping
    {
        public string materialPath;
        public Material material;
    }

    public class HouseRuntimeLoader : MonoBehaviour
    {
        [Header("Config")]
        public TextAsset houseJsonFile;
        public bool generateOnStart = true;

        [Header("Asset Mappings")]
        public List<PrefabMapping> prefabMappings = new List<PrefabMapping>();
        public List<MaterialMapping> materialMappings = new List<MaterialMapping>();

        private Dictionary<string, GameObject> prefabDict = new Dictionary<string, GameObject>();
        private Dictionary<string, Material> materialDict = new Dictionary<string, Material>();

        void Start()
        {
            if (generateOnStart && houseJsonFile != null)
            {
                GenerateHouse();
            }
        }

        public void GenerateHouse()
        {
            prefabDict.Clear();
            foreach (var mapping in prefabMappings)
            {
                if (!string.IsNullOrEmpty(mapping.prefabPath) && mapping.prefab != null)
                {
                    prefabDict[mapping.prefabPath] = mapping.prefab;
                }
            }

            materialDict.Clear();
            foreach (var mapping in materialMappings)
            {
                if (!string.IsNullOrEmpty(mapping.materialPath) && mapping.material != null)
                {
                    materialDict[mapping.materialPath] = mapping.material;
                }
            }

            if (houseJsonFile == null) return;
            HouseData data = JsonUtility.FromJson<HouseData>(houseJsonFile.text);
            if (data == null || data.rootNode == null) return;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }

            InstantiateNode(data.rootNode, transform, null, true);
        }

        private GameObject InstantiateNode(SerializedNode node, Transform parent, Transform existingGo = null, bool isRoot = false)
        {
            GameObject go = existingGo != null ? existingGo.gameObject : null;
            bool isNew = (go == null);

            if (go == null)
            {
                if (!string.IsNullOrEmpty(node.prefabPath))
                {
                    if (prefabDict.TryGetValue(node.prefabPath, out GameObject prefab))
                    {
                        go = Instantiate(prefab);
                    }
                    else
                    {
                        Debug.LogWarning($"Prefab '{node.prefabPath}' not found in mappings. Creating empty GameObject.");
                    }
                }

                if (go == null)
                {
                    if (!string.IsNullOrEmpty(node.meshName))
                    {
                        if (node.meshName == "Cube")
                            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        else if (node.meshName == "Plane")
                            go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                        else if (node.meshName == "Sphere")
                            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        else if (node.meshName == "Cylinder")
                            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        else if (node.meshName == "Capsule")
                            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        else
                        {
                            GameObject template = FindTemplateByMeshName(this.transform, node.meshName);
                            if (template != null)
                            {
                                go = Instantiate(template);
                                for (int i = go.transform.childCount - 1; i >= 0; i--)
                                    DestroyImmediate(go.transform.GetChild(i).gameObject);
                            }
                        }
                    }

                    if (go == null)
                        go = new GameObject(node.name);
                }
            }

            go.name = node.name;
            go.SetActive(node.isActive);

            if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
            {
                try { go.tag = node.tag; }
                catch { }
            }

            if (node.layer > 0 && node.layer <= 31)
                go.layer = node.layer;

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

            // Apply materials
            if (isNew)
            {
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

                            if (!string.IsNullOrEmpty(sMat.assetPath) && materialDict.TryGetValue(sMat.assetPath, out Material mappedMat))
                                mats[i] = mappedMat;
#if UNITY_EDITOR
                            if (mats[i] == null && !string.IsNullOrEmpty(sMat.assetPath))
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(sMat.assetPath);
#endif
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
                            if (!string.IsNullOrEmpty(node.materialPaths[i]))
                            {
                                if (materialDict.TryGetValue(node.materialPaths[i], out Material mat))
                                    mats[i] = mat;
                            }
                        }
                        renderer.sharedMaterials = mats;
                    }
                }
            }

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
                InstantiateNode(childNode, go.transform, foundExisting, false);
            }

            foreach (var leftover in availableChildren)
            {
                if (Application.isPlaying) Destroy(leftover.gameObject);
                else DestroyImmediate(leftover.gameObject);
            }

            // Restore generic components
            RestoreComponents(go, node);

            // Legacy custom component restore
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

        private void RestoreComponents(GameObject go, SerializedNode node)
        {
            if (node.components == null || node.components.Count == 0) return;

#if UNITY_EDITOR
            foreach (var sc in node.components)
            {
                // Skip Transform, MeshFilter, MeshRenderer, Renderer — already handled
                if (sc.typeName == "UnityEngine.Transform" || sc.typeName == "UnityEngine.MeshFilter" ||
                    sc.typeName == "UnityEngine.MeshRenderer") continue;

                System.Type compType = System.Type.GetType(sc.assemblyQualifiedName);
                if (compType == null)
                {
                    // Try finding in all loaded assemblies
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        compType = asm.GetType(sc.typeName);
                        if (compType != null) break;
                    }
                }
                if (compType == null)
                {
                    Debug.LogWarning($"[HouseSerializer] Cannot find type '{sc.typeName}' to restore on '{go.name}'");
                    continue;
                }

                Component comp = go.GetComponent(compType);
                if (comp == null)
                {
                    comp = go.AddComponent(compType);
                }

                if (comp == null)
                {
                    Debug.LogWarning($"[HouseSerializer] Failed to add component '{sc.typeName}' on '{go.name}'");
                    continue;
                }

                // Restore enabled state for Behaviours
                if (comp is Behaviour b)
                    b.enabled = sc.enabled;
                else if (comp is Collider col)
                    col.enabled = sc.enabled;

                // Restore properties via SerializedObject
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
                            case "ObjectRef":
                                if (!string.IsNullOrEmpty(prop.objectRefPath))
                                {
                                    var obj = AssetDatabase.LoadAssetAtPath<Object>(prop.objectRefPath);
                                    if (obj != null)
                                        sp.objectReferenceValue = obj;
                                }
                                break;
                            case "SceneRef":
                                // Scene object references by name/path — try to find in scene
                                if (!string.IsNullOrEmpty(prop.value))
                                {
                                    GameObject found = GameObject.Find(prop.value);
                                    if (found != null)
                                    {
                                        if (sp.propertyType == SerializedPropertyType.ObjectReference)
                                            sp.objectReferenceValue = found;
                                    }
                                }
                                break;
                            case "LayerMask": sp.intValue = int.Parse(prop.value); break;
                            case "AnimationCurve":
                                sp.animationCurveValue = JsonUtility.FromJson<AnimationCurve>(prop.value);
                                break;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[HouseSerializer] Failed to restore property '{prop.name}' on '{sc.typeName}': {ex.Message}");
                    }
                }
                so.ApplyModifiedProperties();
            }
#endif
        }

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

#if UNITY_EDITOR
        [ContextMenu("Auto Populate Mappings from JSON")]
        public void AutoPopulateMappings()
        {
            if (houseJsonFile == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a House JSON File first.", "OK");
                return;
            }

            HouseData data = JsonUtility.FromJson<HouseData>(houseJsonFile.text);
            if (data == null || data.rootNode == null)
            {
                EditorUtility.DisplayDialog("Error", "Failed to parse JSON file.", "OK");
                return;
            }

            HashSet<string> prefabPaths = new HashSet<string>();
            HashSet<string> materialPaths = new HashSet<string>();
            FindPaths(data.rootNode, prefabPaths, materialPaths);

            var newPrefabMappings = new List<PrefabMapping>();
            foreach (string path in prefabPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                newPrefabMappings.Add(new PrefabMapping { prefabPath = path, prefab = prefab });
            }
            prefabMappings = newPrefabMappings;

            var newMaterialMappings = new List<MaterialMapping>();
            foreach (string path in materialPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                newMaterialMappings.Add(new MaterialMapping { materialPath = path, material = material });
            }
            materialMappings = newMaterialMappings;

            EditorUtility.SetDirty(this);
            EditorUtility.DisplayDialog("Success", $"Populated {prefabMappings.Count} prefab mappings and {materialMappings.Count} material mappings!", "OK");
        }

        private void FindPaths(SerializedNode node, HashSet<string> prefabs, HashSet<string> materials)
        {
            if (!string.IsNullOrEmpty(node.prefabPath)) prefabs.Add(node.prefabPath);
            if (node.materials != null)
            {
                foreach (var mat in node.materials)
                {
                    if (mat != null && !string.IsNullOrEmpty(mat.assetPath)) materials.Add(mat.assetPath);
                }
            }
            if (node.materialPaths != null)
            {
                foreach (var matPath in node.materialPaths)
                {
                    if (!string.IsNullOrEmpty(matPath)) materials.Add(matPath);
                }
            }
            foreach (var child in node.children)
            {
                FindPaths(child, prefabs, materials);
            }
        }
#endif
    }
}
