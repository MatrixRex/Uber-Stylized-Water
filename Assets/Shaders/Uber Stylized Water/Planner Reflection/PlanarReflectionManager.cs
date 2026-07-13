using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            if (volume.GetPrimaryTarget() == null) continue;

            float blend = volume.GetBlendFactor(camera);
            if (blend < 1f)
            {
                if (volume.priority > maxPriority)
                {
                    maxPriority = volume.priority;
                    minBlend = blend;
                    activeVolume = volume;
                }
                else if (volume.priority == maxPriority)
                {
                    if (volume.isGlobal && (activeVolume == null || !activeVolume.isGlobal))
                    {
                        minBlend = blend;
                        activeVolume = volume;
                    }
                    else if (volume.isGlobal == (activeVolume != null && activeVolume.isGlobal) && blend < minBlend)
                    {
                        minBlend = blend;
                        activeVolume = volume;
                    }
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
            volume.UpdateTargetMaterials();
            if (volume.targetMaterials == null || volume.targetMaterials.Count == 0) continue;

            if (activeVolume == volume)
            {
                foreach (var mat in volume.targetMaterials)
                {
                    if (mat != null) mat.SetFloat(_planarReflectionBlendId, activeBlend);
                }
            }
            else
            {
                foreach (var mat in volume.targetMaterials)
                {
                    if (mat != null)
                    {
                        if (activeVolume == null || !activeVolume.targetMaterials.Contains(mat))
                        {
                            mat.SetFloat(_planarReflectionBlendId, 1f);
                        }
                    }
                }
            }
        }

        if (activeVolume == null || activeBlend >= 1f) return;

        UpdateReflectionCamera(camera, activeVolume);
        CreateReflectionTexture(camera, activeVolume);

        var data = new PlanarReflectionSettingData();
        data.Set();

        BeginPlanarReflections?.Invoke(context, _reflectionCamera);

        var activePrimaryTarget = activeVolume.GetPrimaryTarget();
        if (activePrimaryTarget != null && _reflectionCamera.WorldToViewportPoint(activePrimaryTarget.transform.position).z < 100000)
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

        var primaryTarget = volume.GetPrimaryTarget();
        if (primaryTarget != null)
        {
            pos = primaryTarget.transform.position + Vector3.up * volume.reflectionPlaneOffset;
            normal = primaryTarget.transform.up;
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

#if UNITY_EDITOR
[CustomEditor(typeof(PlanarReflectionManager))]
public class PlanarReflectionManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PlanarReflectionManager manager = (PlanarReflectionManager)target;

        // Draw default fields (e.g. runOnEditMode)
        DrawDefaultInspector();

        EditorGUILayout.Space();
        

        // Scan the scene for an active global volume
        PlanarReflectionVolume globalVolume = null;
        var volumes = GameObject.FindObjectsByType<PlanarReflectionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vol in volumes)
        {
            if (vol.isActiveAndEnabled && vol.isGlobal)
            {
                globalVolume = vol;
                break;
            }
        }

        if (globalVolume != null)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Active Global Volume", globalVolume, typeof(PlanarReflectionVolume), true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox($"Planar reflections are enabled globally by '{globalVolume.name}'. Click the reference above to find it in the Hierarchy.", MessageType.Info);
        }
       
    }
}
#endif
