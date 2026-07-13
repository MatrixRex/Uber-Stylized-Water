using UnityEngine;
using UnityEditor;

public static class PlanarReflectionEditor
{
    [MenuItem("UWa/Setup Planar Reflection")]
    public static void SetupPlanarReflection()
    {
        // 1. Setup Planar Reflection Manager
        PlanarReflectionManager manager = GameObject.FindAnyObjectByType<PlanarReflectionManager>();
        if (manager == null)
        {
            GameObject managerGo = new GameObject("Planar Reflection Manager");
            manager = managerGo.AddComponent<PlanarReflectionManager>();
            Undo.RegisterCreatedObjectUndo(managerGo, "Create Planar Reflection Manager");
            Debug.Log("[UWa] Created Planar Reflection Manager in scene.");
        }
        else
        {
            Debug.Log("[UWa] Found existing Planar Reflection Manager in scene.");
        }

        // 2. Setup Planar Reflection Volume
        GameObject volumeGo = new GameObject("Planar Reflection Volume");
        var volume = volumeGo.AddComponent<PlanarReflectionVolume>();
        Undo.RegisterCreatedObjectUndo(volumeGo, "Create Planar Reflection Volume");
        Debug.Log("[UWa] Created Planar Reflection Volume in scene.");

        // Try to automatically find a default target
        Renderer[] renderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r.gameObject.name.ToLower().Contains("water") || 
                (r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.ToLower().Contains("water")))
            {
                volume.reflectionTarget = r.gameObject;
                volume.UpdateTargetMaterial();
                EditorUtility.SetDirty(volume);
                Debug.Log($"[UWa] Automatically assigned reflection target: '{r.gameObject.name}'");
                break;
            }
        }

        // Select the volume in the hierarchy
        Selection.activeGameObject = volumeGo;

        // Mark the active scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
