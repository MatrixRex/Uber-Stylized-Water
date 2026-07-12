# Planar Reflection Probe Editor Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modify the Planar Reflection system to split the logic into `PlanarReflectionVolume` (data configuration) and `PlanarReflectionManager` (shared rendering & lifecycle coordination) to support multiple probes in the editor without conflicts.

**Architecture:** We will implement the `PlanarReflectionManager` as an editor-safe singleton MonoBehaviour which is auto-created dynamically when any volume registers. The manager will evaluate the active volume for each camera using sorting priorities and blend factors, and render a single shared reflection texture, updating all volume materials' blend parameters accordingly.

**Tech Stack:** Unity 2021+, Universal Render Pipeline (URP), C# Scripting.

---

### Task 1: Rewrite PlanarReflection.cs with Coordinated Manager and Volumes

**Files:**
- Modify: `Assets/Shaders/Uber Stylized Water/Planner Reflection/PlanarReflection.cs`

- [ ] **Step 1: Replace PlanarReflection.cs content with the coordinated Manager/Volume implementation**

Replace the code in [PlanarReflection.cs](file:///g:/Personal/00 Unity/Uber-Stylized-Water/Assets/Shaders/Uber Stylized Water/Planner Reflection/PlanarReflection.cs) with the following implementation:

```csharp
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

[ExecuteAlways]
public class PlanarReflectionManager : MonoBehaviour
{
    public bool runOnEditMode = true;

    private static PlanarReflectionManager _instance;
    public static PlanarReflectionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = GameObject.FindAnyObjectByType<PlanarReflectionManager>();
                if (_instance == null)
                {
                    var go = new GameObject("Planar Reflection Manager");
                    _instance = go.AddComponent<PlanarReflectionManager>();
                }
            }
            return _instance;
        }
    }

    private static readonly List<PlanarReflectionVolume> _volumes = new List<PlanarReflectionVolume>();
    private Camera _reflectionCamera;
    private RenderTexture _reflectionTexture;
    private RenderTextureDescriptor _previousDescriptor;
    
    private readonly int _planarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTexture");
    private readonly int _planarReflectionBlendId = Shader.PropertyToID("_PlannerReflectionBlend");

    public static event Action<ScriptableRenderContext, Camera> BeginPlanarReflections;

    private bool _hasLoggedCameraWarning = false;

    public static void RegisterVolume(PlanarReflectionVolume volume)
    {
        if (!_volumes.Contains(volume))
        {
            _volumes.Add(volume);
        }
        // Force instantiation of manager
        var mgr = Instance;
    }

    public static void UnregisterVolume(PlanarReflectionVolume volume)
    {
        _volumes.Remove(volume);
    }

    void OnEnable()
    {
        if (_instance == null) _instance = this;
    }

    void OnDisable()
    {
        CleanUp();
    }

    void OnDestroy()
    {
        CleanUp();
    }

    void LateUpdate()
    {
        Camera targetCamera = null;

        if (Application.isPlaying)
        {
            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                DoPlanarReflections(default, targetCamera);
            }
        }
        else
        {
            // In Edit Mode
            if (runOnEditMode)
            {
                #if UNITY_EDITOR
                targetCamera = SceneView.lastActiveSceneView?.camera;
                #endif
            }
            else
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    LogMissingGameCameraWarning();
                    #if UNITY_EDITOR
                    targetCamera = SceneView.lastActiveSceneView?.camera;
                    #endif
                }
                else
                {
                    _hasLoggedCameraWarning = false;
                }
            }

            if (targetCamera != null)
            {
                DoPlanarReflections(default, targetCamera);
            }
        }
    }

    private void LogMissingGameCameraWarning()
    {
        if (!_hasLoggedCameraWarning)
        {
            Debug.LogWarning("[PlanarReflectionManager] Game camera (Camera.main) not found. Falling back to Scene View camera.");
            _hasLoggedCameraWarning = true;
        }
    }

    private PlanarReflectionVolume FindActiveVolume(Camera camera, out float blendFactor)
    {
        PlanarReflectionVolume activeVolume = null;
        float minBlend = 1f;
        int maxPriority = int.MinValue;

        // Cleanup null entries
        for (int i = _volumes.Count - 1; i >= 0; i--)
        {
            if (_volumes[i] == null)
            {
                _volumes.RemoveAt(i);
            }
        }

        foreach (var volume in _volumes)
        {
            if (!volume.gameObject.activeInHierarchy || !volume.enabled) continue;
            if (volume.reflectionTarget == null) continue;

            float blend = volume.GetBlendFactor(camera);
            if (blend < 1f)
            {
                if (volume.priority > maxPriority)
                {
                    maxPriority = volume.priority;
                    minBlend = blend;
                    activeVolume = volume;
                }
                else if (volume.priority == maxPriority && blend < minBlend)
                {
                    minBlend = blend;
                    activeVolume = volume;
                }
            }
        }

        blendFactor = minBlend;
        return activeVolume;
    }

    private void DoPlanarReflections(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return;

        float activeBlend;
        PlanarReflectionVolume activeVolume = FindActiveVolume(camera, out activeBlend);

        // Update all volumes' target materials
        foreach (var volume in _volumes)
        {
            if (volume == null) continue;
            volume.UpdateTargetMaterial();
            if (volume.targetMaterial == null) continue;

            if (activeVolume == volume)
            {
                volume.targetMaterial.SetFloat(_planarReflectionBlendId, activeBlend);
            }
            else
            {
                if (activeVolume == null || activeVolume.targetMaterial != volume.targetMaterial)
                {
                    volume.targetMaterial.SetFloat(_planarReflectionBlendId, 1f);
                }
            }
        }

        if (activeVolume == null || activeBlend >= 1f) return;

        UpdateReflectionCamera(camera, activeVolume);
        CreateReflectionTexture(camera, activeVolume);

        var data = new PlanarReflectionSettingData();
        data.Set();

        BeginPlanarReflections?.Invoke(context, _reflectionCamera);

        if (_reflectionCamera.WorldToViewportPoint(activeVolume.reflectionTarget.transform.position).z < 100000)
        {
            RenderPipeline.SubmitRenderRequest(_reflectionCamera, new UniversalRenderPipeline.SingleCameraRequest());
        }

        data.Restore();
        Shader.SetGlobalTexture(_planarReflectionTextureId, _reflectionTexture);
    }

    private void UpdateReflectionCamera(Camera realCamera, PlanarReflectionVolume volume)
    {
        if (_reflectionCamera == null)
        {
            _reflectionCamera = FindReflectionCamera();
            if (_reflectionCamera == null)
            {
                _reflectionCamera = InitializeReflectionCamera(volume);
            }
        }

        _reflectionCamera.gameObject.hideFlags = HideFlags.DontSave;
        if (volume.hideReflectionCamera)
        {
            _reflectionCamera.gameObject.hideFlags |= HideFlags.HideInHierarchy;
        }
        else
        {
            _reflectionCamera.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
        }

        Vector3 pos = Vector3.zero;
        Vector3 normal = Vector3.up;

        if (volume.reflectionTarget != null)
        {
            pos = volume.reflectionTarget.transform.position + Vector3.up * volume.reflectionPlaneOffset;
            normal = volume.reflectionTarget.transform.up;
        }

        UpdateCamera(realCamera, _reflectionCamera, volume);

        float d = -Vector3.Dot(normal, pos);
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflection = Matrix4x4.zero;
        CalculateReflectionMatrix(ref reflection, reflectionPlane);

        Vector3 oldPos = realCamera.transform.position;
        float distFromPlane = Vector3.Dot(normal, oldPos) + d;
        Vector3 newPos = oldPos - (2 * distFromPlane * normal);

        Vector3 oldForward = realCamera.transform.forward;
        Vector3 newForward = oldForward - (2 * Vector3.Dot(oldForward, normal) * normal);

        _reflectionCamera.transform.position = newPos;
        _reflectionCamera.transform.forward = newForward;

        _reflectionCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix * reflection;

        var clipPlane = CameraSpacePlane(_reflectionCamera, pos, normal, 1.0f);
        if (realCamera.orthographic)
        {
            var projection = realCamera.projectionMatrix;
            CalculateObliqueMatrixOrtho(ref projection, clipPlane);
            _reflectionCamera.projectionMatrix = projection;
        }
        else
        {
            var projection = _reflectionCamera.CalculateObliqueMatrix(clipPlane);
            _reflectionCamera.projectionMatrix = projection;
        }

        _reflectionCamera.cullingMask = volume.reflectionLayer;
    }

    private void UpdateCamera(Camera src, Camera dest, PlanarReflectionVolume volume)
    {
        if (dest == null) return;

        dest.CopyFrom(src);
        dest.useOcclusionCulling = false;

        if (dest.gameObject.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData camData))
        {
            camData.renderShadows = false;
            if (volume.reflectSkybox) dest.clearFlags = CameraClearFlags.Skybox;
            else
            {
                dest.clearFlags = CameraClearFlags.SolidColor;
                dest.backgroundColor = Color.black;
            }
        }
    }

    private Camera InitializeReflectionCamera(PlanarReflectionVolume volume)
    {
        var go = new GameObject("", typeof(Camera));
        go.name = "Reflection Camera [" + go.GetInstanceID() + "]";
        go.hideFlags = HideFlags.DontSave;
        
        var camData = go.AddComponent(typeof(UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)) as UnityEngine.Rendering.Universal.UniversalAdditionalCameraData;

        camData.requiresColorOption = CameraOverrideOption.Off;
        camData.requiresDepthOption = CameraOverrideOption.Off;
        camData.SetRenderer(0);

        var t = transform;
        var reflectionCamera = go.GetComponent<Camera>();
        reflectionCamera.transform.SetPositionAndRotation(t.position, t.rotation);
        reflectionCamera.depth = -10;
        reflectionCamera.enabled = false;

        return reflectionCamera;
    }

    private Camera FindReflectionCamera()
    {
        Camera[] cameras = GameObject.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam.name.Contains("Reflection Camera ["))
            {
                return cam;
            }
        }
        return null;
    }

    private void CreateReflectionTexture(Camera camera, PlanarReflectionVolume volume)
    {
        var descriptor = GetDescriptor(camera, UniversalRenderPipeline.asset.renderScale, volume.renderScale);

        if (_reflectionTexture == null)
        {
            _reflectionTexture = RenderTexture.GetTemporary(descriptor);
            _previousDescriptor = descriptor;
        }
        else if (!descriptor.Equals(_previousDescriptor))
        {
            if (_reflectionTexture) RenderTexture.ReleaseTemporary(_reflectionTexture);

            _reflectionTexture = RenderTexture.GetTemporary(descriptor);
            _previousDescriptor = descriptor;
        }
        _reflectionCamera.targetTexture = _reflectionTexture;
    }

    RenderTextureDescriptor GetDescriptor(Camera camera, float pipelineRenderScale, float renderScale)
    {
        var width = (int)Mathf.Max(camera.pixelWidth * pipelineRenderScale * renderScale);
        var height = (int)Mathf.Max(camera.pixelHeight * pipelineRenderScale * renderScale);
        var hdr = camera.allowHDR;
        var renderTextureFormat = hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        return new RenderTextureDescriptor(width, height, renderTextureFormat, 16)
        {
            autoGenerateMips = true,
            useMipMap = true
        };
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        var m = cam.worldToCameraMatrix;
        var cameraPosition = m.MultiplyPoint(pos);
        var cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    private static void CalculateObliqueMatrixOrtho(ref Matrix4x4 projection, Vector4 clipPlane)
    {
        Vector4 q = projection.inverse * new Vector4(
            Mathf.Sign(clipPlane.x),
            Mathf.Sign(clipPlane.y),
            1.0f,
            1.0f
        );
        Vector4 c = clipPlane * (2.0f / Vector4.Dot(clipPlane, q));
        projection[2, 0] = c.x;
        projection[2, 1] = c.y;
        projection[2, 2] = c.z;
        projection[2, 3] = c.w - 1.0f;
    }

    public static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMatrix, Vector4 plane)
    {
        reflectionMatrix.m00 = (1F - 2F * plane[0] * plane[0]);
        reflectionMatrix.m01 = (-2F * plane[0] * plane[1]);
        reflectionMatrix.m02 = (-2F * plane[0] * plane[2]);
        reflectionMatrix.m03 = (-2F * plane[3] * plane[0]);

        reflectionMatrix.m10 = (-2F * plane[1] * plane[0]);
        reflectionMatrix.m11 = (1F - 2F * plane[1] * plane[1]);
        reflectionMatrix.m12 = (-2F * plane[1] * plane[2]);
        reflectionMatrix.m13 = (-2F * plane[3] * plane[1]);

        reflectionMatrix.m20 = (-2F * plane[2] * plane[0]);
        reflectionMatrix.m21 = (-2F * plane[2] * plane[1]);
        reflectionMatrix.m22 = (1F - 2F * plane[2] * plane[2]);
        reflectionMatrix.m23 = (-2F * plane[3] * plane[2]);

        reflectionMatrix.m30 = 0F;
        reflectionMatrix.m31 = 0F;
        reflectionMatrix.m32 = 0F;
        reflectionMatrix.m33 = 1F;
    }

    void CleanUp()
    {
        if (_reflectionCamera)
        {
            _reflectionCamera.targetTexture = null;
            SafeDestroyObject(_reflectionCamera.gameObject);
            _reflectionCamera = null;
        }

        if (_reflectionTexture)
        {
            RenderTexture.ReleaseTemporary(_reflectionTexture);
            _reflectionTexture = null;
        }
    }

    void SafeDestroyObject(UnityEngine.Object obj)
    {
        if (Application.isEditor) DestroyImmediate(obj);
        else Destroy(obj);
    }

    class PlanarReflectionSettingData
    {
        private readonly bool fog;
        private readonly int maximumLODLevel;
        private readonly float lodBias;

        public PlanarReflectionSettingData()
        {
            fog = RenderSettings.fog;
            maximumLODLevel = QualitySettings.maximumLODLevel;
            lodBias = QualitySettings.lodBias;
        }

        public void Set()
        {
            GL.invertCulling = true;
            RenderSettings.fog = false;
            QualitySettings.maximumLODLevel = 1;
            QualitySettings.lodBias = lodBias * 0.5f;
        }

        public void Restore()
        {
            GL.invertCulling = false;
            RenderSettings.fog = fog;
            QualitySettings.maximumLODLevel = maximumLODLevel;
            QualitySettings.lodBias = lodBias;
        }
    }
}
```

- [ ] **Step 2: Save and verify compilation in Unity Editor**

Ensure the code compiles without errors or warnings.
