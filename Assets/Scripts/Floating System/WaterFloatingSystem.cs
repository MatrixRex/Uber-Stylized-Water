using UnityEngine;

/// <summary>
/// Place this on your Water Plane object.
/// Automatically reads wave parameters from the water material and exposes
/// SampleWave() so FloatingObject scripts can query the wave height in C#.
/// The Gerstner math here exactly mirrors WaveGenerator.hlsl.
/// </summary>
[AddComponentMenu("Water/Water Floating System")]
[ExecuteAlways]
public class WaterFloatingSystem : MonoBehaviour
{
    public static WaterFloatingSystem Instance { get; private set; }

    // ── Material property names (match your Shader Graph properties) ──────
    private static readonly int ID_WaveSteep     = Shader.PropertyToID("_Wave_Steep");
    private static readonly int ID_WaveLength    = Shader.PropertyToID("_Wave_Length");
    private static readonly int ID_WaveSpeed     = Shader.PropertyToID("_Wave_Speed");
    private static readonly int ID_WaveDetail    = Shader.PropertyToID("_Wave_Detail");
    private static readonly int ID_WaveDirection = Shader.PropertyToID("_Wave_Direction");

    // ── Read-only display (refreshed from material in OnEnable / every frame) ─
    [Header("Wave Parameters (auto-read from material)")]
    [SerializeField, HideInInspector] private float steepness  = 0.5f;
    [SerializeField, HideInInspector] private float wavelength  = 10f;
    [SerializeField, HideInInspector] private float speed       = 1f;
    [SerializeField, HideInInspector] private float iterations  = 4f;
    [SerializeField, HideInInspector] private float direction   = 0f;

    // ── Public accessors for the Editor ───────────────────────────────────
    public float Steepness  => steepness;
    public float Wavelength => wavelength;
    public float Speed      => speed;
    public float Iterations => iterations;
    public float Direction  => direction;

    private Renderer waterRenderer;
    private static readonly float PI = Mathf.PI;
    private static readonly float GoldenAngle = 2.39996323f;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WaterFloatingSystem] Multiple instances found. Keeping the first one.", this);
            enabled = false;
            return;
        }
        Instance = this;
        waterRenderer = GetComponent<Renderer>();
        FetchMaterialParams();
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        if (waterRenderer == null) waterRenderer = GetComponent<Renderer>();
        FetchMaterialParams();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Keep params in sync if material properties change at runtime
        FetchMaterialParams();
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Reads the five wave parameters from the water object's material.
    /// Safe to call in Edit mode (uses sharedMaterial).
    /// </summary>
    public void FetchMaterialParams()
    {
        // Ensure renderer is assigned even if called manually before Awake
        if (waterRenderer == null) waterRenderer = GetComponent<Renderer>();
        if (waterRenderer == null) return;

        // Use sharedMaterial so we don't instantiate a copy in Edit mode
        Material mat = Application.isPlaying ? waterRenderer.material : waterRenderer.sharedMaterial;
        if (mat == null) return;

        if (mat.HasProperty(ID_WaveSteep))     steepness  = mat.GetFloat(ID_WaveSteep);
        if (mat.HasProperty(ID_WaveLength))    wavelength  = mat.GetFloat(ID_WaveLength);
        if (mat.HasProperty(ID_WaveSpeed))     speed       = mat.GetFloat(ID_WaveSpeed);
        if (mat.HasProperty(ID_WaveDetail))    iterations  = mat.GetFloat(ID_WaveDetail);
        if (mat.HasProperty(ID_WaveDirection)) direction   = mat.GetFloat(ID_WaveDirection);
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Result of a wave sample at a world-space position.
    /// </summary>
    public struct WaveSample
    {
        /// <summary>World-space Y height of the wave surface.</summary>
        public float height;
        /// <summary>World-space normal of the wave surface at this point.</summary>
        public Vector3 normal;
        /// <summary>Full 3D displacement so you can see XZ horizontal shift too.</summary>
        public Vector3 offset;
    }

    /// <summary>
    /// Sample the wave at a world-space XZ position.
    /// The water plane's own Y position is used as the base water level.
    /// </summary>
    public WaveSample SampleWave(Vector3 worldPos, float time)
    {
        Vector3 finalOffset   = Vector3.zero;
        Vector3 derivatives   = Vector3.zero;

        float curWavelength = Mathf.Max(wavelength, 0.001f);
        float curSteepness  = steepness;
        float curSpeed      = speed;
        float baseAngle     = direction * (PI / 180f);
        int   iters         = Mathf.Max(1, Mathf.RoundToInt(iterations));

        for (int i = 0; i < iters; i++)
        {
            float angle   = baseAngle + i * GoldenAngle;
            Vector2 dir   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float k       = 2f * PI / curWavelength;
            float f       = Vector2.Dot(dir, new Vector2(worldPos.x, worldPos.z)) * k
                            + time * curSpeed * k;

            float valCos  = Mathf.Cos(f);
            float valSin  = Mathf.Sin(f);
            float amp     = curSteepness / k;

            finalOffset.x += dir.x * (amp * valCos);
            finalOffset.y += amp * valSin;
            finalOffset.z += dir.y * (amp * valCos);

            float wa       = k * amp;
            derivatives.x += dir.x * wa * valSin;
            derivatives.z += dir.y  * wa * valSin;
            derivatives.y += wa * valCos;

            // Next octave (same ratios as the HLSL)
            curWavelength *= 0.618f;
            curSteepness  *= 0.5f;
            curSpeed      *= 1.2f;
        }

        Vector3 worldNormal = new Vector3(
            -derivatives.x,
            1f - derivatives.y,
            -derivatives.z
        ).normalized;

        float waterBaseY = transform.position.y;

        return new WaveSample
        {
            height = waterBaseY + finalOffset.y,
            normal = worldNormal,
            offset = finalOffset
        };
    }

    /// <summary>
    /// Convenience overload using the current application time.
    /// </summary>
    public WaveSample SampleWave(Vector3 worldPos)
        => SampleWave(worldPos, Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup);
}
