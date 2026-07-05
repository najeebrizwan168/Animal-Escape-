using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HouseGenerator
{
    [System.Serializable]
    public class SerializedNode
    {
        public string name;
        public string prefabPath;
        public string meshPath;
        public string meshName;
        public List<string> materialPaths;
        public Vector3 localPosition;
        public Vector3 localRotation; // Store as Euler angles
        public Vector3 localScale;
        public List<SerializedNode> children = new List<SerializedNode>();
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
            // Build lookup dictionaries
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

            // Destroy any existing children under this transform
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }

            // Reconstruct the root node so its prefab (if any) is properly instantiated!
            InstantiateNode(data.rootNode, transform, null, true);
        }

        private GameObject InstantiateNode(SerializedNode node, Transform parent, Transform existingGo = null, bool isRoot = false)
        {
            GameObject go = existingGo != null ? existingGo.gameObject : null;
            bool isNew = (go == null);

            if (go == null)
            {
                // Try to spawn from prefab mappings
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

                // If not spawned (or not a prefab), create standard GameObject or Primitive
                if (go == null)
                {
                    if (!string.IsNullOrEmpty(node.meshName))
                    {
                        if (node.meshName == "Cube")
                        {
                            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        }
                        else if (node.meshName == "Plane")
                        {
                            go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                        }
                        else
                        {
                            // Try to find a template in the already instantiated objects (e.g. duplicate door)
                            GameObject template = FindTemplateByMeshName(this.transform, node.meshName);
                            if (template != null)
                            {
                                go = Instantiate(template);
                                // Destroy any children on the duplicated template, as they might represent
                                // nested objects that we will recreate from the JSON anyway.
                                for (int i = go.transform.childCount - 1; i >= 0; i--)
                                {
                                    DestroyImmediate(go.transform.GetChild(i).gameObject);
                                }
                            }
                        }
                    }

                    if (go == null)
                    {
                        go = new GameObject(node.name);
                    }
                }
            }

            go.name = node.name;
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

            // Apply materials if custom mesh/primitive
            if (isNew)
            {
                if (node.materialPaths != null && node.materialPaths.Count > 0)
                {
                    Renderer renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Material[] mats = new Material[node.materialPaths.Count];
                        for (int i = 0; i < node.materialPaths.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(node.materialPaths[i]))
                            {
                                if (materialDict.TryGetValue(node.materialPaths[i], out Material mat))
                                {
                                    mats[i] = mat;
                                }
                            }
                        }
                        renderer.sharedMaterials = mats;
                    }
                }
            }

            // Gather all available children of `go`
            List<Transform> availableChildren = new List<Transform>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                availableChildren.Add(go.transform.GetChild(i));
            }

            // Recursively instantiate children, correctly mapping to existing prefab children
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

            // Any remaining transforms in `availableChildren` were NOT in the JSON!
            // (e.g. the user deleted some default walls/doors from the base prefab)
            foreach (var leftover in availableChildren)
            {
                if (Application.isPlaying) Destroy(leftover.gameObject);
                else DestroyImmediate(leftover.gameObject);
            }

            return go;
        }

        private GameObject FindTemplateByMeshName(Transform root, string meshName)
        {
            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == meshName)
            {
                return root.gameObject;
            }
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

            // Update prefab mappings
            var newPrefabMappings = new List<PrefabMapping>();
            foreach (string path in prefabPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                newPrefabMappings.Add(new PrefabMapping { prefabPath = path, prefab = prefab });
            }
            prefabMappings = newPrefabMappings;

            // Update material mappings
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
