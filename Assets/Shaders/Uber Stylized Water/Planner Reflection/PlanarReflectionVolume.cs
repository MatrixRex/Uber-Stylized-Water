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
    [System.Obsolete("Use reflectionTargets instead")]
    [HideInInspector]
    public GameObject reflectionTarget;

    public List<GameObject> reflectionTargets = new List<GameObject>();
    [Range(-2f, 3f)] public float reflectionPlaneOffset;
    public bool hideReflectionCamera;

    [Header("Volume Settings")]
    public Vector3 volumeSize = new Vector3(10f, 10f, 10f);
    [Min(0)] public float blendDistance = 2f;
    public int priority = 0;

    [HideInInspector]
    public List<Material> targetMaterials = new List<Material>();

    private readonly int _planarReflectionBlendId = Shader.PropertyToID("_PlannerReflectionBlend");

    void OnEnable()
    {
        reflectionLayer = ~(1 << 4);
        UpdateTargetMaterials();
        PlanarReflectionManager.RegisterVolume(this);
    }

    void OnDisable()
    {
        PlanarReflectionManager.UnregisterVolume(this);
        ResetMaterials();
    }

    void OnDestroy()
    {
        PlanarReflectionManager.UnregisterVolume(this);
        ResetMaterials();
    }

    void OnValidate()
    {
        UpdateTargetMaterials();
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

    public GameObject GetPrimaryTarget()
    {
        if (reflectionTargets != null)
        {
            foreach (var target in reflectionTargets)
            {
                if (target != null) return target;
            }
        }
        return null;
    }

    public void UpdateTargetMaterials()
    {
        targetMaterials.Clear();

        #pragma warning disable CS0618
        if (reflectionTarget != null)
        {
            if (!reflectionTargets.Contains(reflectionTarget))
            {
                reflectionTargets.Add(reflectionTarget);
            }
            reflectionTarget = null;
        }
        #pragma warning restore CS0618

        if (reflectionTargets != null)
        {
            foreach (var target in reflectionTargets)
            {
                if (target != null && target.TryGetComponent<Renderer>(out var renderer))
                {
                    if (renderer.sharedMaterial != null && !targetMaterials.Contains(renderer.sharedMaterial))
                    {
                        targetMaterials.Add(renderer.sharedMaterial);
                    }
                }
            }
        }
    }

    public void ResetMaterials()
    {
        if (targetMaterials != null)
        {
            foreach (var mat in targetMaterials)
            {
                if (mat != null)
                {
                    mat.SetFloat(_planarReflectionBlendId, 1f);
                }
            }
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

        // Target validation
        VerifyTargets(volume);

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

    private void VerifyTargets(PlanarReflectionVolume volume)
    {
        if (volume.reflectionTargets == null || volume.reflectionTargets.Count == 0) return;

        bool duplicateMaterials = false;
        bool differentYPlanes = false;
        float? firstY = null;
        var uniqueMaterials = new System.Collections.Generic.HashSet<Material>();

        foreach (var target in volume.reflectionTargets)
        {
            if (target == null) continue;

            // 1. Check Y heights
            float currentY = target.transform.position.y;
            if (firstY == null)
            {
                firstY = currentY;
            }
            else if (Mathf.Abs(firstY.Value - currentY) > 0.001f)
            {
                differentYPlanes = true;
            }

            // 2. Check duplicate materials
            if (target.TryGetComponent<Renderer>(out var r))
            {
                if (r.sharedMaterial != null)
                {
                    if (!uniqueMaterials.Add(r.sharedMaterial))
                    {
                        duplicateMaterials = true;
                    }
                }
            }
        }

        if (duplicateMaterials)
        {
            EditorGUILayout.HelpBox("Some targets share the same material. You only need to assign one target per unique material to apply the reflection settings.", MessageType.Info);
        }

        if (differentYPlanes)
        {
            EditorGUILayout.HelpBox("Targets are at different global Y heights! Planar reflection will not align correctly with all surfaces. The world Y of the first non-null target will be used for the reflection plane calculation.", MessageType.Warning);
        }
    }

    private void DrawInspectorFields(PlanarReflectionVolume volume)
    {
        serializedObject.Update();

        // Draw settings
        EditorGUILayout.PropertyField(serializedObject.FindProperty("renderScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionLayer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectSkybox"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reflectionTargets"));
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
