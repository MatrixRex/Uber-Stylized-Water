using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Effects/Planar Reflection Volume")]
public class PlanarReflectionVolume : MonoBehaviour
{
    public bool isGlobal = false;

    [Range(0.01f, 1f)] public float renderScale = 1f;
    public LayerMask reflectionLayer = -1;
    public bool reflectSkybox;
    public GameObject reflectionTarget;
    [Range(-2f, 3f)] public float reflectionPlaneOffset;
    public bool hideReflectionCamera;

    [Header("Volume Settings")]
    public Vector3 volumeSize = new Vector3(10f, 10f, 10f);
    [Min(0)] public float blendDistance = 2f;
    public int priority = 0;

    [HideInInspector]
    public Material targetMaterial;

    private readonly int _planarReflectionBlendId = Shader.PropertyToID("_PlannerReflectionBlend");

    void OnEnable()
    {
        reflectionLayer = ~(1 << 4);
        UpdateTargetMaterial();
        PlanarReflectionManager.RegisterVolume(this);
    }

    void OnDisable()
    {
        PlanarReflectionManager.UnregisterVolume(this);
        ResetMaterial();
    }

    void OnDestroy()
    {
        PlanarReflectionManager.UnregisterVolume(this);
        ResetMaterial();
    }

    void OnValidate()
    {
        UpdateTargetMaterial();
        if (isGlobal)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += DisableOtherGlobals;
            #else
            DisableOtherGlobals();
            #endif
        }
    }

    private void DisableOtherGlobals()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DisableOtherGlobals;
        #endif

        if (this == null || !isGlobal) return;

        var volumes = GameObject.FindObjectsByType<PlanarReflectionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vol in volumes)
        {
            if (vol != this && vol.isGlobal)
            {
                vol.isGlobal = false;
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(vol);
                #endif
            }
        }
    }

    public void UpdateTargetMaterial()
    {
        if (reflectionTarget != null)
        {
            var renderer = reflectionTarget.GetComponent<Renderer>();
            if (renderer != null)
            {
                targetMaterial = renderer.sharedMaterial;
            }
        }
    }

    public void ResetMaterial()
    {
        if (targetMaterial != null)
        {
            targetMaterial.SetFloat(_planarReflectionBlendId, 1f);
        }
    }

    public float GetBlendFactor(Camera camera)
    {
        if (isGlobal) return 0f;
        if (blendDistance <= 0) return IsCameraInVolume(camera) ? 0f : 1f;

        // Transform camera position to local space
        Vector3 cameraLocalPos = transform.InverseTransformPoint(camera.transform.position);
        Vector3 halfSize = volumeSize * 0.5f;

        // Calculate distance from each boundary
        float distanceX = Mathf.Max(0, Mathf.Abs(cameraLocalPos.x) - halfSize.x);
        float distanceY = Mathf.Max(0, Mathf.Abs(cameraLocalPos.y) - halfSize.y);
        float distanceZ = Mathf.Max(0, Mathf.Abs(cameraLocalPos.z) - halfSize.z);

        // Get the maximum distance from any boundary
        float maxDistance = Mathf.Max(distanceX, Mathf.Max(distanceY, distanceZ));

        // If inside volume
        if (maxDistance <= 0) return 0f;

        // Calculate blend factor
        return Mathf.Clamp01(maxDistance / blendDistance);
    }

    public bool IsCameraInRange(Camera camera)
    {
        if (isGlobal) return true;

        Vector3 cameraLocalPos = transform.InverseTransformPoint(camera.transform.position);
        Vector3 halfSize = volumeSize * 0.5f + new Vector3(blendDistance, blendDistance, blendDistance);

        return Mathf.Abs(cameraLocalPos.x) <= halfSize.x &&
               Mathf.Abs(cameraLocalPos.y) <= halfSize.y &&
               Mathf.Abs(cameraLocalPos.z) <= halfSize.z;
    }

    private bool IsCameraInVolume(Camera camera)
    {
        Vector3 cameraLocalPos = transform.InverseTransformPoint(camera.transform.position);
        Vector3 halfSize = volumeSize * 0.5f;

        return Mathf.Abs(cameraLocalPos.x) <= halfSize.x &&
               Mathf.Abs(cameraLocalPos.y) <= halfSize.y &&
               Mathf.Abs(cameraLocalPos.z) <= halfSize.z;
    }

    private void OnDrawGizmos()
    {
        if (isGlobal) return;

        if (!Application.isPlaying)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
            #endif
        }

        // Draw inner volume
        Gizmos.color = new Color(0, 1, 1, 0.0f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, volumeSize);

        // Draw inner volume wireframe
        Gizmos.color = new Color(0, 1, 1, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, volumeSize);

        // Draw blend volume wireframe
        if (blendDistance > 0)
        {
            Gizmos.color = new Color(0, 0.5f, 0.5f, 0.5f);
            Vector3 blendSize = volumeSize + new Vector3(blendDistance * 2, blendDistance * 2, blendDistance * 2);
            Gizmos.DrawWireCube(Vector3.zero, blendSize);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PlanarReflectionVolume))]
public class PlanarReflectionVolumeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PlanarReflectionVolume volume = (PlanarReflectionVolume)target;

        // Check for global conflicts
        var volumes = GameObject.FindObjectsByType<PlanarReflectionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool hasOtherGlobal = false;
        PlanarReflectionVolume activeGlobal = null;

        foreach (var vol in volumes)
        {
            if (vol != volume && vol.isActiveAndEnabled)
            {
                if (vol.isGlobal)
                {
                    activeGlobal = vol;
                    if (volume.isGlobal)
                    {
                        hasOtherGlobal = true;
                    }
                }
            }
        }

        // Draw isGlobal field first (disabled if another volume is already global)
        serializedObject.Update();
        SerializedProperty isGlobalProp = serializedObject.FindProperty("isGlobal");
        
        bool disableGlobalToggle = !volume.isGlobal && activeGlobal != null;
        EditorGUI.BeginDisabledGroup(disableGlobalToggle);
        EditorGUILayout.PropertyField(isGlobalProp);
        EditorGUI.EndDisabledGroup();
        
        serializedObject.ApplyModifiedProperties();

        // If not global, draw volume settings, otherwise skip volume boundaries settings
        DrawInspectorFields(volume);

        // Check 1: Check if PlanarReflectionManager exists in the scene
        var manager = GameObject.FindAnyObjectByType<PlanarReflectionManager>();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Planar Reflection Manager is missing from the scene. It will be created automatically at runtime, but you can create it now to customize global settings.", MessageType.Warning);
            if (GUILayout.Button("Create Planar Reflection Manager"))
            {
                var go = new GameObject("Planar Reflection Manager");
                go.AddComponent<PlanarReflectionManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create Planar Reflection Manager");
            }
        }

        if (volume.isGlobal && hasOtherGlobal)
        {
            EditorGUILayout.HelpBox("Multiple global Planar Reflection Volumes are active. This will cause priority/rendering conflicts.", MessageType.Error);
        }
        else if (!volume.isGlobal && activeGlobal != null)
        {
            if (volume.priority <= activeGlobal.priority)
            {
                EditorGUILayout.HelpBox($"A global Planar Reflection Volume ('{activeGlobal.name}', Priority: {activeGlobal.priority}) is active with the same or higher priority. This local volume will be overridden and will not work.", MessageType.Warning);
            }
        }
    }

    private void DrawInspectorFields(PlanarReflectionVolume volume)
    {
        serializedObject.Update();

        // Draw settings
        EditorGUILayout.PropertyField(serializedObject.FindProperty("renderScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionLayer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectSkybox"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionTarget"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionPlaneOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hideReflectionCamera"));

        if (!volume.isGlobal)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Volume Bounds Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("volumeSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blendDistance"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("priority"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif