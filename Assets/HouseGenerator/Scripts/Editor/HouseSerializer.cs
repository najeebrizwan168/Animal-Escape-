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
            node.tag = go.tag;
            node.layer = go.layer;
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
                node.materials = new List<SerializedMaterial>();
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        node.materialPaths.Add(AssetDatabase.GetAssetPath(mat));

                        SerializedMaterial sMat = new SerializedMaterial();
                        sMat.name = mat.name;
                        sMat.assetPath = AssetDatabase.GetAssetPath(mat);
                        sMat.shaderName = mat.shader != null ? mat.shader.name : "Universal Render Pipeline/Lit";

                        // Save base color
                        if (mat.HasProperty("_BaseColor"))
                            sMat.color = mat.GetColor("_BaseColor");
                        else if (mat.HasProperty("_Color"))
                            sMat.color = mat.GetColor("_Color");
                        else
                            sMat.color = Color.white;

                        // Save main texture path
                        if (mat.mainTexture != null)
                        {
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.mainTexture);
                        }
                        else if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                        {
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap"));
                        }
                        else if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                        {
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.GetTexture("_MainTex"));
                        }

                        node.materials.Add(sMat);
                    }
                    else
                    {
                        node.materialPaths.Add("");
                        node.materials.Add(null);
                    }
                }
            }

            // =========================================================================
            // 🔥 NEW: CUSTOM COMPONENT & VALUE SERIALIZATION
            // =========================================================================
            node.customizedComponents = new List<CustomComponentData>();

            // 1. Save HunterController specific properties if it exists
            HunterController hunter = go.GetComponent<HunterController>();
            if (hunter != null)
            {
                CustomComponentData hunterData = new CustomComponentData();
                hunterData.componentType = "HunterController";

                hunterData.propertyKeys.Add("distanceToPointA");
                hunterData.propertyValues.Add(hunter.distanceToPointA.ToString());

                hunterData.propertyKeys.Add("distanceToPointB");
                hunterData.propertyValues.Add(hunter.distanceToPointB.ToString());

                hunterData.propertyKeys.Add("moveSpeed");
                hunterData.propertyValues.Add(hunter.moveSpeed.ToString());

                hunterData.propertyKeys.Add("canMove");
                hunterData.propertyValues.Add(hunter.canMove.ToString());

                node.customizedComponents.Add(hunterData);
            }

            // 2. Save BoxCollider if it was added dynamically or modified
            BoxCollider boxCollider = go.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                CustomComponentData boxData = new CustomComponentData();
                boxData.componentType = "BoxCollider";
                boxData.propertyKeys.Add("center");
                boxData.propertyValues.Add(JsonUtility.ToJson(boxCollider.center));

                boxData.propertyKeys.Add("size");
                boxData.propertyValues.Add(JsonUtility.ToJson(boxCollider.size));

                node.customizedComponents.Add(boxData);
            }
            // =========================================================================

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

            // Restore Tag
            if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
            {
                try { go.tag = node.tag; }
                catch { /* Tag not in project — skip */ }
            }

            // Restore Layer (skip 0 = Default, that's already the default)
            if (node.layer > 0 && node.layer <= 31)
            {
                go.layer = node.layer;
            }

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

                            // 1. Try loading directly from AssetDatabase if it has a valid asset path
                            if (!string.IsNullOrEmpty(sMat.assetPath))
                            {
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(sMat.assetPath);
                            }

                            // 2. If no asset found (e.g. procedurally created/instanced material), recreate it
                            if (mats[i] == null)
                            {
                                Shader shader = Shader.Find(sMat.shaderName);
                                if (shader == null)
                                {
                                    shader = Shader.Find("Universal Render Pipeline/Lit");
                                }
                                if (shader == null)
                                {
                                    shader = Shader.Find("Standard");
                                }

                                if (shader != null)
                                {
                                    Material newMat = new Material(shader);
                                    newMat.name = sMat.name;

                                    if (newMat.HasProperty("_BaseColor"))
                                        newMat.SetColor("_BaseColor", sMat.color);
                                    else if (newMat.HasProperty("_Color"))
                                        newMat.SetColor("_Color", sMat.color);

                                    if (!string.IsNullOrEmpty(sMat.mainTexturePath))
                                    {
                                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(sMat.mainTexturePath);
                                        if (tex != null)
                                        {
                                            newMat.mainTexture = tex;
                                            
                                            // Set texture property for URP/Standard shader explicitly if needed
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

            // =========================================================================
            // 🔥 NEW: RESTORE CUSTOM COMPONENT VALUES AT RUNTIME / IMPORT
            // =========================================================================
            if (node.customizedComponents != null && node.customizedComponents.Count > 0)
            {
                foreach (var compData in node.customizedComponents)
                {
                    // 1. Restore HunterController properties
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

                    // 2. Restore BoxCollider properties
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
            // =========================================================================

            return go;
        }
    }
}
