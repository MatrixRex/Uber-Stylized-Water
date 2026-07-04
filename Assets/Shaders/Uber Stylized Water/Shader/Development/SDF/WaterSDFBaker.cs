// Editor/WaterSDFBaker.cs
// Bakes a shoreline SDF + water depth map for a region of the scene.
//
// Requirements:
//  - Water surface meshes on their own layer (e.g. "Water")
//  - Terrain / obstacles on layers included in Terrain Mask
//  - WaterSDFJumpFlood.compute and Hidden/WaterSDF/HeightBake shader in project
//
// Output EXR channels:
//  R = signed distance to shoreline (meters, + in water, - on land)
//  G = water depth (meters)

using System.IO;
using UnityEditor;
using UnityEngine;

public class WaterSDFBaker : EditorWindow
{
    // Region ------------------------------------------------------------
    Vector3 center = Vector3.zero;
    float   size   = 200f;          // square region, world units
    float   captureCeiling = 200f;  // how far above 'center.y' the camera sits
    float   captureDepth   = 500f;  // how far below the camera the far plane reaches

    // Layers ------------------------------------------------------------
    LayerMask terrainMask = ~0;
    LayerMask waterMask   = 0;

    // Quality -----------------------------------------------------------
    int   resolution  = 1024;
    float maxDistance = 50f;        // SDF clamp in meters

    // Assets ------------------------------------------------------------
    ComputeShader jfaCompute;
    string outputPath = "Assets/WaterSDF.exr";

    const float NO_DATA = -100000f;

    [MenuItem("Tools/Water SDF Baker")]
    static void Open() => GetWindow<WaterSDFBaker>("Water SDF Baker");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Region", EditorStyles.boldLabel);
        center         = EditorGUILayout.Vector3Field("Center", center);
        size           = EditorGUILayout.FloatField("Size (square, m)", size);
        captureCeiling = EditorGUILayout.FloatField("Capture Ceiling", captureCeiling);
        captureDepth   = EditorGUILayout.FloatField("Capture Depth", captureDepth);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
        terrainMask = LayerMaskField("Terrain / Obstacles", terrainMask);
        waterMask   = LayerMaskField("Water Surface", waterMask);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quality", EditorStyles.boldLabel);
        resolution  = EditorGUILayout.IntPopup("Resolution", resolution,
            new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
        maxDistance = EditorGUILayout.FloatField("Max Distance (m)", maxDistance);
        EditorGUILayout.HelpBox(
            $"Texel size: {size / resolution:F3} m — this is your shoreline precision.",
            MessageType.Info);

        EditorGUILayout.Space();
        jfaCompute = (ComputeShader)EditorGUILayout.ObjectField(
            "JFA Compute", jfaCompute, typeof(ComputeShader), false);
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(jfaCompute == null || waterMask == 0))
        {
            if (GUILayout.Button("Bake", GUILayout.Height(32)))
                Bake();
        }
    }

    void Bake()
    {
        Shader heightShader = Shader.Find("Hidden/WaterSDF/HeightBake");
        if (heightShader == null)
        {
            Debug.LogError("Hidden/WaterSDF/HeightBake shader not found.");
            return;
        }

        // --- 1. Top-down height renders --------------------------------
        RenderTexture terrainH = NewFloatRT(resolution, RenderTextureFormat.RFloat, false);
        RenderTexture waterH   = NewFloatRT(resolution, RenderTextureFormat.RFloat, false);

        var camGO = new GameObject("~WaterSDFCam") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var cam = camGO.AddComponent<Camera>();
            cam.enabled          = false;
            cam.orthographic     = true;
            cam.orthographicSize = size * 0.5f;
            cam.aspect           = 1f;
            cam.transform.SetPositionAndRotation(
                center + Vector3.up * captureCeiling,
                Quaternion.LookRotation(Vector3.down, Vector3.forward));
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane  = captureCeiling + captureDepth;
            cam.clearFlags    = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(NO_DATA, 0, 0, 0);
            cam.allowMSAA     = false;

            cam.cullingMask   = terrainMask;
            cam.targetTexture = terrainH;
            cam.RenderWithShader(heightShader, "");   // empty tag = replace everything

            cam.cullingMask   = waterMask;
            cam.targetTexture = waterH;
            cam.RenderWithShader(heightShader, "");

            cam.targetTexture = null;
        }
        finally { DestroyImmediate(camGO); }

        // --- 2. JFA on the GPU ------------------------------------------
        RenderTexture mask   = NewFloatRT(resolution, RenderTextureFormat.RFloat, true);
        RenderTexture seedsA = NewFloatRT(resolution, RenderTextureFormat.RGFloat, true);
        RenderTexture seedsB = NewFloatRT(resolution, RenderTextureFormat.RGFloat, true);
        RenderTexture result = NewFloatRT(resolution, RenderTextureFormat.ARGBFloat, true);
        RenderTexture final  = NewFloatRT(resolution, RenderTextureFormat.ARGBFloat, true);

        int groups = Mathf.CeilToInt(resolution / 8f);
        float texelWorldSize = size / resolution;

        jfaCompute.SetInt("_Resolution", resolution);
        jfaCompute.SetFloat("_TexelWorldSize", texelWorldSize);
        jfaCompute.SetFloat("_MaxDistance", maxDistance);
        jfaCompute.SetFloat("_NoDataHeight", NO_DATA);

        int kMask = jfaCompute.FindKernel("BuildMask");
        jfaCompute.SetTexture(kMask, "_TerrainHeight", terrainH);
        jfaCompute.SetTexture(kMask, "_WaterHeight", waterH);
        jfaCompute.SetTexture(kMask, "_Mask", mask);
        jfaCompute.Dispatch(kMask, groups, groups, 1);

        int kInit = jfaCompute.FindKernel("InitSeeds");
        jfaCompute.SetTexture(kInit, "_Mask", mask);
        jfaCompute.SetTexture(kInit, "_DstSeeds", seedsA);
        jfaCompute.Dispatch(kInit, groups, groups, 1);

        int kJump = jfaCompute.FindKernel("JumpFlood");
        RenderTexture src = seedsA, dst = seedsB;
        for (int jump = resolution / 2; jump >= 1; jump /= 2)
        {
            jfaCompute.SetInt("_JumpSize", jump);
            jfaCompute.SetTexture(kJump, "_SrcSeeds", src);
            jfaCompute.SetTexture(kJump, "_DstSeeds", dst);
            jfaCompute.Dispatch(kJump, groups, groups, 1);
            (src, dst) = (dst, src);
        }
        // extra jump=1 pass improves accuracy (JFA+1)
        jfaCompute.SetInt("_JumpSize", 1);
        jfaCompute.SetTexture(kJump, "_SrcSeeds", src);
        jfaCompute.SetTexture(kJump, "_DstSeeds", dst);
        jfaCompute.Dispatch(kJump, groups, groups, 1);
        (src, dst) = (dst, src);

        int kRes = jfaCompute.FindKernel("Resolve");
        jfaCompute.SetTexture(kRes, "_SrcSeeds", src);
        jfaCompute.SetTexture(kRes, "_Mask", mask);
        jfaCompute.SetTexture(kRes, "_TerrainHeight", terrainH);
        jfaCompute.SetTexture(kRes, "_WaterHeight", waterH);
        jfaCompute.SetTexture(kRes, "_Result", result);
        jfaCompute.Dispatch(kRes, groups, groups, 1);

        int kGrad = jfaCompute.FindKernel("ComputeGradient");
        jfaCompute.SetTexture(kGrad, "_ResolveSrc", result);
        jfaCompute.SetTexture(kGrad, "_Final", final);
        jfaCompute.Dispatch(kGrad, groups, groups, 1);

        // --- 3. Read back & save as EXR ----------------------------------
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
        RenderTexture.active = final;
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        File.WriteAllBytes(outputPath, tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        DestroyImmediate(tex);

        foreach (var rt in new[] { terrainH, waterH, mask, seedsA, seedsB, result, final })
            rt.Release();

        AssetDatabase.ImportAsset(outputPath);
        if (AssetImporter.GetAtPath(outputPath) is TextureImporter imp)
        {
            imp.sRGBTexture        = false;
            imp.mipmapEnabled      = false;
            imp.wrapMode           = TextureWrapMode.Clamp;
            imp.filterMode         = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        Vector2 boundsMin = new Vector2(center.x - size * 0.5f, center.z - size * 0.5f);
        Debug.Log($"Water SDF baked to {outputPath}\n" +
                  $"Bounds min XZ: {boundsMin}, size: {size}. " +
                  $"Assign these to your WaterSDFBinder.");
    }

    static RenderTexture NewFloatRT(int res, RenderTextureFormat fmt, bool randomWrite)
    {
        var rt = new RenderTexture(res, res, randomWrite ? 0 : 24, fmt,
                                   RenderTextureReadWrite.Linear)
        {
            enableRandomWrite = randomWrite,
            filterMode = FilterMode.Point
        };
        rt.Create();
        return rt;
    }

    static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        int concat = UnityEditorInternal.InternalEditorUtility
                        .LayerMaskToConcatenatedLayersMask(mask);
        concat = EditorGUILayout.MaskField(label, concat, layers);
        return UnityEditorInternal.InternalEditorUtility
                        .ConcatenatedLayersMaskToLayerMask(concat);
    }
}
