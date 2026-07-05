using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GeneratorEditor : MonoBehaviour
{
    [CustomEditor(typeof(Generator))]
    class DecalMeshHelperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if(!Application.isPlaying)
            {
                GUILayout.Space(20);
                if (GUILayout.Button("Generate", GUILayout.Height(50)))
                    ((Generator)target).RunProgram();

                GUILayout.Space(10);
                if (GUILayout.Button("Save Layout Snapshot", GUILayout.Height(30)))
                {
                    Generator generator = (Generator)target;
                    SavedDungeonLayout snapshot = generator.CreateLayoutSnapshot();
                    if (snapshot != null)
                    {
                        string path = EditorUtility.SaveFilePanelInProject("Save Dungeon Layout", "NewDungeonLayout", "asset", "Please enter a file name to save the layout to");
                        if (!string.IsNullOrEmpty(path))
                        {
                            AssetDatabase.CreateAsset(snapshot, path);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            Debug.Log("Successfully saved Dungeon Layout to: " + path);
                        }
                    }
                }
            }
        }
    }
}
