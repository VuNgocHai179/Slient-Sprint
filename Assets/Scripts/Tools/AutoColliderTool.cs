using UnityEngine;
using UnityEditor;

namespace SilentSprint.Tools
{
    public class AutoColliderTool
    {
        // Creates a menu item on the top menu bar in Unity
        [MenuItem("Tools/Auto Add Mesh Collider")]
        public static void AddMeshColliders()
        {
            // Get the currently selected objects in the Hierarchy
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("No object selected! Please select a Map in the Hierarchy first.");
                return;
            }

            int addedCount = 0;

            foreach (GameObject parentObj in selectedObjects)
            {
                // Find ALL child objects (and the parent itself) that contain a MeshFilter
                MeshFilter[] meshFilters = parentObj.GetComponentsInChildren<MeshFilter>(true);

                foreach (MeshFilter mf in meshFilters)
                {
                    GameObject obj = mf.gameObject;

                    // Check if it already has a Collider; if not, add one
                    if (obj.GetComponent<Collider>() == null)
                    {
                        // Use Undo.AddComponent so the action can be undone with Ctrl+Z
                        Undo.AddComponent<MeshCollider>(obj);
                        addedCount++;
                    }
                }
            }

            Debug.Log($"Done! Automatically added {addedCount} Mesh Colliders to the Map.");
        }
    }
}