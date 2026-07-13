using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

public static class RendererSetupEditor
{
    [MenuItem("UWa/Setup Renderer")]
    public static void SetupRenderer()
    {
        // 1. Find active UniversalRenderPipelineAsset
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
        {
            urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }

        if (urpAsset == null)
        {
            EditorUtility.DisplayDialog("UWa Setup Renderer", "No active Universal Render Pipeline (URP) asset found. Please make sure URP is set up in Graphics Settings.", "OK");
            return;
        }

        // 2. Retrieve renderer data list via reflection
        FieldInfo rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rendererDataListField == null)
        {
            EditorUtility.DisplayDialog("UWa Setup Renderer", "Could not find 'm_RendererDataList' in UniversalRenderPipelineAsset via reflection.", "OK");
            return;
        }

        ScriptableRendererData[] rendererDataList = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];
        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            EditorUtility.DisplayDialog("UWa Setup Renderer", "The URP asset has no renderers configured.", "OK");
            return;
        }

        // 3. Determine the active/current renderer index in use
        int activeRendererIndex = GetActiveRendererIndex(urpAsset);

        if (activeRendererIndex < 0 || activeRendererIndex >= rendererDataList.Length)
        {
            activeRendererIndex = 0;
        }

        ScriptableRendererData rendererData = rendererDataList[activeRendererIndex];
        if (rendererData == null)
        {
            EditorUtility.DisplayDialog("UWa Setup Renderer", "Active/current renderer data is null.", "OK");
            return;
        }

        // 4. Check if feature already exists
        string featureName = "WaterStencilCullFeature";
        ScriptableRendererFeature existingFeature = null;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null && feature.name == featureName)
            {
                existingFeature = feature;
                break;
            }
        }

        if (existingFeature != null)
        {
            EditorUtility.DisplayDialog("UWa Setup Renderer", $"Renderer feature '{featureName}' already exists on the renderer '{rendererData.name}'.", "OK");
            return;
        }

        // 5. Create and configure RenderObjects feature
        Undo.RegisterCompleteObjectUndo(rendererData, "Add Water Stencil Cull Feature");

        RenderObjects newFeature = ScriptableObject.CreateInstance<RenderObjects>();
        newFeature.name = featureName;

        // Configure standard RenderObjects settings
        newFeature.settings.Event = RenderPassEvent.AfterRenderingTransparents;
        
        // Filter Settings
        newFeature.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
        int waterLayer = LayerMask.NameToLayer("Water");
        if (waterLayer < 0 || waterLayer > 31)
        {
            waterLayer = 4; // Unity's default Water layer
        }
        newFeature.settings.filterSettings.LayerMask = 1 << waterLayer;
        newFeature.settings.filterSettings.PassNames = null;

        // Overrides
        newFeature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
        newFeature.settings.overrideDepthState = false;

        // Stencil Settings
        newFeature.settings.stencilSettings.overrideStencilState = true;
        newFeature.settings.stencilSettings.stencilReference = 1;
        newFeature.settings.stencilSettings.stencilCompareFunction = CompareFunction.NotEqual;
        newFeature.settings.stencilSettings.passOperation = StencilOp.Keep;
        newFeature.settings.stencilSettings.failOperation = StencilOp.Keep;
        newFeature.settings.stencilSettings.zFailOperation = StencilOp.Keep;

        // Add to AssetDatabase as sub-asset
        AssetDatabase.AddObjectToAsset(newFeature, rendererData);
        rendererData.rendererFeatures.Add(newFeature);

        // Mark dirty
        EditorUtility.SetDirty(rendererData);
        rendererData.SetDirty();

        // Invoke URP's internal validation / cache update via reflection
        MethodInfo onValidateMethod = rendererData.GetType().GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (onValidateMethod != null)
        {
            onValidateMethod.Invoke(rendererData, null);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("UWa Setup Renderer", $"Successfully added '{featureName}' feature to the renderer '{rendererData.name}'.", "OK");
        Debug.Log($"[UWa] Added RenderObjects feature '{featureName}' to renderer '{rendererData.name}'");
    }

    private static int GetActiveRendererIndex(UniversalRenderPipelineAsset urpAsset)
    {
        // 1. Check if a Camera is selected in the Hierarchy
        if (Selection.activeGameObject != null)
        {
            var camera = Selection.activeGameObject.GetComponent<Camera>();
            if (camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
            {
                int index = GetCameraRendererIndex(additionalCameraData);
                if (index >= 0) return index;
            }
        }

        // 2. Check the Main Camera in the scene
        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent<UniversalAdditionalCameraData>(out var mainCamData))
        {
            int index = GetCameraRendererIndex(mainCamData);
            if (index >= 0) return index;
        }

        // 3. Fallback: retrieve m_DefaultRendererIndex from the URP Asset via reflection
        int defaultIndex = 0;
        FieldInfo defaultRendererIndexField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        if (defaultRendererIndexField != null)
        {
            defaultIndex = (int)defaultRendererIndexField.GetValue(urpAsset);
        }
        return defaultIndex;
    }

    private static int GetCameraRendererIndex(UniversalAdditionalCameraData cameraData)
    {
        if (cameraData == null) return -1;

        // Try to get rendererIndex property (sometimes public, sometimes internal/private)
        var prop = typeof(UniversalAdditionalCameraData).GetProperty("rendererIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
        {
            try
            {
                return (int)prop.GetValue(cameraData);
            }
            catch {}
        }
        
        // Try to get m_RendererIndex field
        var field = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            try
            {
                return (int)field.GetValue(cameraData);
            }
            catch {}
        }
        
        return -1;
    }
}
