using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HouseGenerator
{

    public class HouseSerializer
    {
        // Types we skip during generic serialization (handled separately or irrelevant)
        private static readonly HashSet<string> SkipComponentTypes = new HashSet<string>
        {
            "UnityEngine.Transform",
            "UnityEngine.MeshFilter",
            "UnityEngine.MeshRenderer",
            "UnityEngine.CanvasRenderer",
        };

        // SerializedProperty types we can safely capture
        private static readonly HashSet<SerializedPropertyType> SupportedPropTypes = new HashSet<SerializedPropertyType>
        {
            SerializedPropertyType.Float,
            SerializedPropertyType.Integer,
            SerializedPropertyType.Boolean,
            SerializedPropertyType.String,
            SerializedPropertyType.Vector2,
            SerializedPropertyType.Vector3,
            SerializedPropertyType.Vector4,
            SerializedPropertyType.Quaternion,
            SerializedPropertyType.Color,
            SerializedPropertyType.Rect,
            SerializedPropertyType.Bounds,
            SerializedPropertyType.Enum,
            SerializedPropertyType.ObjectReference,
            SerializedPropertyType.LayerMask,
            SerializedPropertyType.AnimationCurve,
        };

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

        // =====================================================================
        // SERIALIZATION
        // =====================================================================
        private static SerializedNode SerializeGameObject(GameObject go)
        {
            SerializedNode node = new SerializedNode();
            node.name = go.name;
            node.tag = go.tag;
            node.layer = go.layer;
            node.isActive = go.activeSelf;
            node.localPosition = go.transform.localPosition;
            node.localRotation = go.transform.localRotation.eulerAngles;
            node.localScale = go.transform.localScale;

            // Prefab detection
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset != null)
                {
                    node.prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                }
            }

            // Save Mesh details
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

                        if (mat.HasProperty("_BaseColor"))
                            sMat.color = mat.GetColor("_BaseColor");
                        else if (mat.HasProperty("_Color"))
                            sMat.color = mat.GetColor("_Color");
                        else
                            sMat.color = Color.white;

                        if (mat.mainTexture != null)
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.mainTexture);
                        else if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.GetTexture("_BaseMap"));
                        else if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                            sMat.mainTexturePath = AssetDatabase.GetAssetPath(mat.GetTexture("_MainTex"));

                        node.materials.Add(sMat);
                    }
                    else
                    {
                        node.materialPaths.Add("");
                        node.materials.Add(null);
                    }
                }
            }

            // =====================================================================
            // GENERIC COMPONENT SERIALIZATION — captures ALL components
            // =====================================================================
            node.components = new List<SerializedComponent>();

            Component[] allComponents = go.GetComponents<Component>();
            foreach (Component comp in allComponents)
            {
                if (comp == null) continue; // missing script
                string typeName = comp.GetType().FullName;

                if (SkipComponentTypes.Contains(typeName)) continue;

                SerializedComponent sc = new SerializedComponent();
                sc.typeName = typeName;
                sc.assemblyQualifiedName = comp.GetType().AssemblyQualifiedName;

                // Capture enabled state
                if (comp is Behaviour b)
                    sc.enabled = b.enabled;
                else if (comp is Collider col)
                    sc.enabled = col.enabled;
                else
                    sc.enabled = true;

                // Use SerializedObject to iterate ALL serialized fields
                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;

                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    // Skip m_Script — we restore via type
                    if (prop.name == "m_Script") continue;
                    // Skip m_Material / m_Materials — handled above
                    if (prop.name == "m_Materials" || prop.name == "m_Material") continue;

                    if (!SupportedPropTypes.Contains(prop.propertyType)) continue;

                    SerializedField sprop = new SerializedField();
                    sprop.name = prop.propertyPath;

                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Float:
                            sprop.type = "float";
                            sprop.value = prop.floatValue.ToString("R");
                            break;
                        case SerializedPropertyType.Integer:
                            sprop.type = "int";
                            sprop.value = prop.intValue.ToString();
                            break;
                        case SerializedPropertyType.Boolean:
                            sprop.type = "bool";
                            sprop.value = prop.boolValue.ToString();
                            break;
                        case SerializedPropertyType.String:
                            sprop.type = "string";
                            sprop.value = prop.stringValue;
                            break;
                        case SerializedPropertyType.Vector2:
                            sprop.type = "Vector2";
                            sprop.value = JsonUtility.ToJson(prop.vector2Value);
                            break;
                        case SerializedPropertyType.Vector3:
                            sprop.type = "Vector3";
                            sprop.value = JsonUtility.ToJson(prop.vector3Value);
                            break;
                        case SerializedPropertyType.Vector4:
                            sprop.type = "Vector4";
                            sprop.value = JsonUtility.ToJson(prop.vector4Value);
                            break;
                        case SerializedPropertyType.Quaternion:
                            sprop.type = "Quaternion";
                            sprop.value = JsonUtility.ToJson(prop.quaternionValue);
                            break;
                        case SerializedPropertyType.Color:
                            sprop.type = "Color";
                            sprop.value = JsonUtility.ToJson(prop.colorValue);
                            break;
                        case SerializedPropertyType.Rect:
                            sprop.type = "Rect";
                            sprop.value = JsonUtility.ToJson(prop.rectValue);
                            break;
                        case SerializedPropertyType.Bounds:
                            sprop.type = "Bounds";
                            sprop.value = JsonUtility.ToJson(prop.boundsValue);
                            break;
                        case SerializedPropertyType.Enum:
                            sprop.type = "enum";
                            sprop.value = prop.enumValueIndex.ToString();
                            break;
                        case SerializedPropertyType.LayerMask:
                            sprop.type = "LayerMask";
                            sprop.value = prop.intValue.ToString();
                            break;
                        case SerializedPropertyType.AnimationCurve:
                            sprop.type = "AnimationCurve";
                            sprop.value = JsonUtility.ToJson(prop.animationCurveValue);
                            break;
                        case SerializedPropertyType.ObjectReference:
                            Object refObj = prop.objectReferenceValue;
                            if (refObj != null)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(refObj);
                                if (!string.IsNullOrEmpty(assetPath))
                                {
                                    // Project asset reference (prefab, material, scriptable object, etc.)
                                    sprop.type = "ObjectRef";
                                    sprop.objectRefPath = assetPath;
                                    sprop.value = refObj.name;
                                }
                                else if (refObj is GameObject sceneGo)
                                {
                                    // Scene object reference — store hierarchy path
                                    sprop.type = "SceneRef";
                                    sprop.value = GetHierarchyPath(sceneGo.transform);
                                }
                                else if (refObj is Component sceneComp)
                                {
                                    sprop.type = "SceneRef";
                                    sprop.value = GetHierarchyPath(sceneComp.transform);
                                }
                                else
                                {
                                    continue; // Can't serialize this reference
                                }
                            }
                            else
                            {
                                continue; // Null ref — skip
                            }
                            break;
                        default:
                            continue;
                    }

                    sc.properties.Add(sprop);
                }

                node.components.Add(sc);
            }

            // Recursively serialize ALL children
            foreach (Transform child in go.transform)
            {
                node.children.Add(SerializeGameObject(child.gameObject));
            }

            return node;
        }

        /// <summary>
        /// Gets a hierarchy path like "Level9/Room1/Hunter" for scene object references.
        /// </summary>
        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        // =====================================================================
        // DESERIALIZATION
        // =====================================================================
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

            // Parent
            if (parent != null && isNew)
            {
                go.transform.SetParent(parent);
            }

            go.name = node.name;
            go.SetActive(node.isActive);

            // Transforms
            go.transform.localPosition = node.localPosition;
            go.transform.localRotation = Quaternion.Euler(node.localRotation);
            go.transform.localScale = node.localScale;

            // Restore Tag
            if (!string.IsNullOrEmpty(node.tag) && node.tag != "Untagged")
            {
                try { go.tag = node.tag; }
                catch { }
            }

            // Restore Layer
            if (node.layer > 0 && node.layer <= 31)
            {
                go.layer = node.layer;
            }

            // Apply Mesh and Materials for newly created GameObjects
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

                            if (!string.IsNullOrEmpty(sMat.assetPath))
                            {
                                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(sMat.assetPath);
                            }

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

            // =====================================================================
            // RECURSIVELY DESERIALIZE ALL CHILDREN
            // =====================================================================
            foreach (SerializedNode childNode in node.children)
            {
                DeserializeGameObject(childNode, go.transform);
            }

            // =====================================================================
            // RESTORE ALL GENERIC COMPONENTS
            // =====================================================================
            if (node.components != null && node.components.Count > 0)
            {
                foreach (var sc in node.components)
                {
                    // Skip Transform, MeshFilter, MeshRenderer — already handled
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

                    // Restore enabled state
                    if (comp is Behaviour b)
                        b.enabled = sc.enabled;
                    else if (comp is Collider col)
                        col.enabled = sc.enabled;

                    // Restore properties via SerializedObject
                    var so = new SerializedObject(comp);
                    foreach (var sprop in sc.properties)
                    {
                        var sp = so.FindProperty(sprop.name);
                        if (sp == null) continue;

                        try
                        {
                            switch (sprop.type)
                            {
                                case "float": sp.floatValue = float.Parse(sprop.value); break;
                                case "int": sp.intValue = int.Parse(sprop.value); break;
                                case "bool": sp.boolValue = bool.Parse(sprop.value); break;
                                case "string": sp.stringValue = sprop.value; break;
                                case "Vector2": sp.vector2Value = JsonUtility.FromJson<Vector2>(sprop.value); break;
                                case "Vector3": sp.vector3Value = JsonUtility.FromJson<Vector3>(sprop.value); break;
                                case "Vector4": sp.vector4Value = JsonUtility.FromJson<Vector4>(sprop.value); break;
                                case "Quaternion": sp.quaternionValue = JsonUtility.FromJson<Quaternion>(sprop.value); break;
                                case "Color": sp.colorValue = JsonUtility.FromJson<Color>(sprop.value); break;
                                case "Rect": sp.rectValue = JsonUtility.FromJson<Rect>(sprop.value); break;
                                case "Bounds": sp.boundsValue = JsonUtility.FromJson<Bounds>(sprop.value); break;
                                case "enum": sp.enumValueIndex = int.Parse(sprop.value); break;
                                case "LayerMask": sp.intValue = int.Parse(sprop.value); break;
                                case "AnimationCurve":
                                    sp.animationCurveValue = JsonUtility.FromJson<AnimationCurve>(sprop.value);
                                    break;
                                case "ObjectRef":
                                    if (!string.IsNullOrEmpty(sprop.objectRefPath))
                                    {
                                        var obj = AssetDatabase.LoadAssetAtPath<Object>(sprop.objectRefPath);
                                        if (obj != null)
                                            sp.objectReferenceValue = obj;
                                    }
                                    break;
                                case "SceneRef":
                                    if (!string.IsNullOrEmpty(sprop.value))
                                    {
                                        GameObject found = GameObject.Find(sprop.value);
                                        if (found != null && sp.propertyType == SerializedPropertyType.ObjectReference)
                                            sp.objectReferenceValue = found;
                                    }
                                    break;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[HouseSerializer] Failed to restore '{sprop.name}' on '{sc.typeName}': {ex.Message}");
                        }
                    }
                    so.ApplyModifiedProperties();
                }
            }

            // Legacy custom component restore (backward compat with old JSONs)
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
    }
}
