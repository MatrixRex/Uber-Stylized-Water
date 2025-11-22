using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
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

    private static Camera _reflectionCamera;
    private Camera _activeCamera;
    private UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _cameraData;
    private static RenderTexture _reflectionTexture;
    private RenderTextureDescriptor _previousDescriptor;
    private readonly int _planarReflectionTextureId = Shader.PropertyToID("_PlanarReflectionTexture");
    private readonly int _planarReflectionBlendId = Shader.PropertyToID("_PlannerReflectionBlend");
    private Material _targetMaterial;
    public static event Action<ScriptableRenderContext, Camera> BeginPlanarReflections;

    private Bounds _volumeBounds;

    void OnEnable()
    {
        // FIX: Removed the RenderPipelineManager subscription to avoid recursion
        // RenderPipelineManager.beginCameraRendering += DoPlanarReflections;

        reflectionLayer = ~(1 << 4);
        UpdateBounds();

        // Get the material from the reflection target
        UpdateTargetMaterial();
    }

    void OnDisable()
    {
        CleanUp();
        // FIX: Removed unsubscription since we don't subscribe anymore
        // RenderPipelineManager.beginCameraRendering -= DoPlanarReflections;

        // Reset blend parameter on the specific material when disabled
        if (_targetMaterial != null)
        {
            _targetMaterial.SetFloat(_planarReflectionBlendId, 1f);
        }
    }

    void OnDestroy()
    {
        CleanUp();
        // FIX: Removed unsubscription
        // RenderPipelineManager.beginCameraRendering -= DoPlanarReflections;

        // Reset blend parameter on the specific material when destroyed
        if (_targetMaterial != null)
        {
            _targetMaterial.SetFloat(_planarReflectionBlendId, 1f);
        }
    }

    // FIX: Added LateUpdate to trigger the render safely outside the pipeline loop
    void LateUpdate()
    {
        // 1. Handle Game View Camera
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            DoPlanarReflections(default, mainCam);
        }

#if UNITY_EDITOR
        // 2. Handle Scene View Camera (Optional support for editing)
        // We check if we are currently drawing the scene view to update reflections there too
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        if (sceneView != null && sceneView.camera != null && sceneView.camera != mainCam)
        {
            DoPlanarReflections(default, sceneView.camera);
        }
#endif
    }

    private void UpdateTargetMaterial()
    {
        if (reflectionTarget != null && _targetMaterial == null)
        {
            var renderer = reflectionTarget.GetComponent<Renderer>();
            if (renderer != null)
            {
                _targetMaterial = renderer.sharedMaterial;
            }
        }
    }

    private void UpdateBounds()
    {
        _volumeBounds = new Bounds(transform.position, volumeSize);
    }

    void OnValidate()
    {
        UpdateBounds();
    }

    private float GetBlendFactor(Camera camera)
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


    private bool IsCameraInRange(Camera camera)
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

    void CleanUp()
    {
        if (_reflectionCamera)
        {
            _reflectionCamera.targetTexture = null;
            SafeDestroyObject(_reflectionCamera.gameObject);
        }

        if (_reflectionTexture)
        {
            RenderTexture.ReleaseTemporary(_reflectionTexture);
        }
    }

    void SafeDestroyObject(UnityEngine.Object obj)
    {
        if (Application.isEditor) DestroyImmediate(obj);
        else Destroy(obj);
    }

    private void UpdateReflectionCamera(Camera realCamera)
    {
        if (_reflectionCamera == null)
        {
            _reflectionCamera = InitializeReflectionCamera();
        }
        else if (_reflectionCamera.gameObject.hideFlags != (hideReflectionCamera ? HideFlags.HideAndDontSave : HideFlags.DontSave))
        {
            // Update hide flags if they've changed
            _reflectionCamera.gameObject.hideFlags = hideReflectionCamera ? HideFlags.HideAndDontSave : HideFlags.DontSave;
#if UNITY_EDITOR
            // Force update the hierarchy in editor
            UnityEditor.EditorApplication.DirtyHierarchyWindowSorting();
#endif
        }

        Vector3 pos = Vector3.zero;
        Vector3 normal = Vector3.up;

        if (reflectionTarget != null)
        {
            pos = reflectionTarget.transform.position + Vector3.up * reflectionPlaneOffset;
            normal = reflectionTarget.transform.up;
        }

        UpdateCamera(realCamera, _reflectionCamera);
        _reflectionCamera.gameObject.hideFlags = (hideReflectionCamera) ? HideFlags.HideAndDontSave : HideFlags.DontSave;

        var d = -Vector3.Dot(normal, pos);
        var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        var reflection = Matrix4x4.identity;
        reflection *= Matrix4x4.Scale(new Vector3(1, -1, 1));

        CalculateReflectionMatrix(ref reflection, reflectionPlane);
        var oldPosition = realCamera.transform.position - new Vector3(0, pos.y * 2, 0);
        var newPosition = ReflectPosition(oldPosition);
        _reflectionCamera.transform.forward = Vector3.Scale(realCamera.transform.forward, new Vector3(1, -1, 1));
        _reflectionCamera.worldToCameraMatrix = realCamera.worldToCameraMatrix * reflection;

        var clipPlane = CameraSpacePlane(_reflectionCamera, pos - Vector3.up * 0.1f, normal, 1.0f);
        var projection = realCamera.CalculateObliqueMatrix(clipPlane);
        _reflectionCamera.projectionMatrix = projection;
        _reflectionCamera.cullingMask = reflectionLayer;
        _reflectionCamera.transform.position = newPosition;
    }

    private void UpdateCamera(Camera src, Camera dest)
    {
        if (dest == null) return;

        dest.CopyFrom(src);
        dest.useOcclusionCulling = false;

        if (dest.gameObject.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData camData))
        {
            camData.renderShadows = false;
            if (reflectSkybox) dest.clearFlags = CameraClearFlags.Skybox;
            else
            {
                dest.clearFlags = CameraClearFlags.SolidColor;
                dest.backgroundColor = Color.black;
            }
        }
    }

    private Camera InitializeReflectionCamera()
    {
        var go = new GameObject("", typeof(Camera));
        go.name = "Reflection Camera [" + go.GetInstanceID() + "]";
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

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        var m = cam.worldToCameraMatrix;
        var cameraPosition = m.MultiplyPoint(pos);
        var cameraNormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
    }

    RenderTextureDescriptor GetDescriptor(Camera camera, float pipelineRenderScale)
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

    private void CreateReflectionTexture(Camera camera)
    {
        var descriptor = GetDescriptor(camera, UniversalRenderPipeline.asset.renderScale);

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

    private void DoPlanarReflections(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return;
        if (!reflectionTarget) return;

        // Update material reference if needed
        UpdateTargetMaterial();
        if (_targetMaterial == null) return;

        // Check if camera is in range (volume + blend distance)
        if (!IsCameraInRange(camera))
        {
            _targetMaterial.SetFloat(_planarReflectionBlendId, 1f);
            return;
        }

        // Calculate and set blend factor on the specific material
        float blendFactor = GetBlendFactor(camera);
        _targetMaterial.SetFloat(_planarReflectionBlendId, blendFactor);

        // If fully blended to probe reflections, don't render planar reflection
        if (blendFactor >= 1f) return;

        UpdateReflectionCamera(camera);
        CreateReflectionTexture(camera);

        var data = new PlanarReflectionSettingData();
        data.Set();

        // Invoke event (Context is passed as default since we are in LateUpdate)
        BeginPlanarReflections?.Invoke(context, _reflectionCamera);

        if (_reflectionCamera.WorldToViewportPoint(reflectionTarget.transform.position).z < 100000)
        {
            // FIX: Updated API call to avoid Obsolete warning and Recursion error
            RenderPipeline.SubmitRenderRequest(_reflectionCamera, new UniversalRenderPipeline.SingleCameraRequest());
        }

        data.Restore();
        Shader.SetGlobalTexture(_planarReflectionTextureId, _reflectionTexture);
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

    public static Vector3 ReflectPosition(Vector3 pos)
    {
        var newPos = new Vector3(pos.x, -pos.y, pos.z);
        return newPos;
    }

    private void OnDrawGizmos()
    {
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