using UnityEngine;
using UnityEditor;

namespace SilentSprint.Tools
{
    public class DarkenWindowsTool : EditorWindow
    {
        [MenuItem("Tools/Silent Sprint/Fix Glowing Windows")]
        public static void ShowWindow()
        {
            GetWindow<DarkenWindowsTool>("Darken Windows");
        }

        private Material darkMaterial;

        void OnGUI()
        {
            GUILayout.Label("GLTF MATERIAL REPLACEMENT TOOL", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Definitive solution for locked glTF files.\nReplaces everything with a single dark Material to maximize performance!", MessageType.Info);

            darkMaterial = (Material)EditorGUILayout.ObjectField("Drag Mat_DarkCity here:", darkMaterial, typeof(Material), false);

            GUILayout.Space(20);

            if (GUILayout.Button("APPLY TO ALL SELECTED OBJECTS", GUILayout.Height(50)))
            {
                ApplyMaterial();
            }
        }

        void ApplyMaterial()
        {
            if (darkMaterial == null)
            {
                EditorUtility.DisplayDialog("Error", "You haven't assigned the Mat_DarkCity Material!", "OK");
                return;
            }

            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Please select buildings in the Hierarchy first!", "OK");
                return;
            }

            int count = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    Undo.RecordObject(r, "Apply Dark Material");

                    Material[] newMats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++)
                    {
                        newMats[i] = darkMaterial;
                        count++;
                    }
                    r.sharedMaterials = newMats;
                    EditorUtility.SetDirty(r);
                }
            }

            EditorUtility.DisplayDialog("Success", $"Successfully replaced {count} surfaces with the dark material!", "OK");
        }
    }
}