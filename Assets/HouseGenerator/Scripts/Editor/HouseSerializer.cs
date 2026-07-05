using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HouseGenerator
{

    public class HouseSerializer
    {
        [MenuItem("Tools/House Generator/Export Selected House to JSON", false, 1)]
        public static void ExportSelectedHouse()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Export Error", "Please select a root GameObject (e.g. Procedural House) to export.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export House to JSON", "Assets", selected.name + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                SerializedNode rootNode = SerializeGameObject(selected);
                HouseData data = new HouseData
                {
                    rootName = selected.name,
                    rootNode = rootNode
                };

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Export Success", $"Successfully exported house structure to:\n{path}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", "Error serializing house: " + ex.Message, "OK");
                Debug.LogException(ex);
            }
        }

        [MenuItem("Tools/House Generator/Import House from JSON", false, 2)]
        public static void ImportHouse()
        {
            string path = EditorUtility.OpenFilePanel("Import House from JSON", "Assets", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                HouseData data = JsonUtility.FromJson<HouseData>(json);

                if (data == null || data.rootNode == null)
                {
                    EditorUtility.DisplayDialog("Import Error", "Failed to parse JSON house data.", "OK");
                    return;
                }

                GameObject root = DeserializeGameObject(data.rootNode, null);
                if (root != null)
                {
                    root.name = data.rootName;
                    Undo.RegisterCreatedObjectUndo(root, "Import House from JSON");
                    Selection.activeGameObject = root;
                    SceneView.FrameLastActiveSceneView();
                    EditorUtility.DisplayDialog("Import Success", "Successfully reconstructed house and interior!", "OK");
                }
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Import Failed", "Error deserializing house: " + ex.Message, "OK");
                Debug.LogException(ex);
            }
        }

        private static SerializedNode SerializeGameObject(GameObject go)
        {
            SerializedNode node = new SerializedNode();
            node.name = go.name;
            node.localPosition = go.transform.localPosition;
            node.localRotation = go.transform.localRotation.eulerAngles;
            node.localScale = go.transform.localScale;

            // Check if this is a prefab instance root
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset != null)
                {
                    node.prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                }
            }

            // Save Mesh details if it has a MeshFilter (e.g. custom duplicated doors or primitives)
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                node.meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                node.meshName = filter.sharedMesh.name;
            }

            // Save Material details
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                node.materialPaths = new List<string>();
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        node.materialPaths.Add(AssetDatabase.GetAssetPath(mat));
                    }
                    else
                    {
                        node.materialPaths.Add("");
                    }
                }
            }

            // Recursively serialize children
            foreach (Transform child in go.transform)
            {
                node.children.Add(SerializeGameObject(child.gameObject));
            }

            return node;
        }

        private static GameObject DeserializeGameObject(SerializedNode node, Transform parent)
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

            // If it doesn't exist, we must instantiate or create it
            if (go == null)
            {
                isNew = true;
                if (!string.IsNullOrEmpty(node.prefabPath))
                {
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(node.prefabPath);
                    if (prefabAsset != null)
                    {
                        go = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                    }
                }

                if (go == null)
                {
                    go = new GameObject(node.name);
                }
            }

            // Parent the new object
            if (parent != null && isNew)
            {
                go.transform.SetParent(parent);
            }

            // Set Transforms relative to parent
            go.transform.localPosition = node.localPosition;
            go.transform.localRotation = Quaternion.Euler(node.localRotation);
            go.transform.localScale = node.localScale;

            // Apply Mesh and Materials to newly created gameobjects (like primitives/custom objects)
            if (isNew)
            {
                if (!string.IsNullOrEmpty(node.meshPath) && !string.IsNullOrEmpty(node.meshName))
                {
                    MeshFilter filter = go.GetComponent<MeshFilter>();
                    if (filter == null) filter = go.AddComponent<MeshFilter>();

                    Object[] assets = AssetDatabase.LoadAllAssetsAtPath(node.meshPath);
                    foreach (Object asset in assets)
                    {
                        if (asset is Mesh && asset.name == node.meshName)
                        {
                            filter.sharedMesh = (Mesh)asset;
                            break;
                        }
                    }

                    MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                    if (meshRenderer == null) go.AddComponent<MeshRenderer>();
                }

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
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(node.materialPaths[i]);
                            }
                        }
                        renderer.sharedMaterials = mats;
                    }
                }
            }

            // Recursively deserialize children
            foreach (SerializedNode childNode in node.children)
            {
                DeserializeGameObject(childNode, go.transform);
            }

            return go;
        }
    }
}
