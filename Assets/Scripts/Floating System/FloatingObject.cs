using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to any GameObject with a Rigidbody that should float on the water.
///
/// Setup:
///   1. Add this component (a Rigidbody will be required automatically).
///   2. Create empty child GameObjects and add FloatingSamplePoint to each.
///      Place them at the corners / key points of your object (bow, stern, etc.)
///   3. Enable "Preview In Edit Mode" to see floating in the Scene view without entering Play mode.
/// </summary>
[AddComponentMenu("Water/Floating Object")]
[RequireComponent(typeof(Rigidbody))]
[ExecuteAlways]
public class FloatingObject : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Edit Mode")]
    [Tooltip("Preview floating in the Scene view without entering Play mode.")]
    public bool previewInEditMode = false;

    [Header("Buoyancy")]
    [Tooltip("Material density in kg/m³. Determines how high the object floats.\n" +
             "Water = 1000  →  denser sinks, lighter floats.\n\n" +
             "Presets:\n" +
             "  Foam / Cork   ~  100 – 250\n" +
             "  Wood (pine)   ~  500\n" +
             "  Wood (oak)    ~  720\n" +
             "  Plastic       ~  950\n" +
             "  Concrete      ~ 2000\n" +
             "  Steel         ~ 7800")]
    [Min(1f)]
    public float density = 600f;   // kg/m³ — wood default (floats)

    [Tooltip("Shift the waterline up / down. Negative = floats higher, positive = rides lower.")]
    public float depthOffset = 0f;

    [Header("Water Drag")]
    [Tooltip("Linear drag coefficient. Final drag = this × mass^(1/3).\n" +
             "Heavier objects automatically get more resistance.")]
    public float waterDragCoeff = 0.8f;

    [Tooltip("Angular drag coefficient. Final angular drag = this × mass^(1/3).")]
    public float waterAngularDragCoeff = 0.4f;

    [Tooltip("How strongly the water absorbs kinetic energy on impact.\n" +
             "Higher = object decelerates faster when hitting the water.\n" +
             "0 = no damping (springy), 5+ = very heavy water feel.")]
    [Range(0f, 10f)]
    public float waterImpactDamping = 2f;

    // ── Internal ──────────────────────────────────────────────────────────
    private Rigidbody rb;
    private float defaultDrag;
    private float defaultAngularDrag;

    // Cached object half-height used to normalize submersion depth
    private float objectHalfHeight = 0.5f;

    // Cache of sample points; refreshed when structure changes
    private readonly List<FloatingSamplePoint> samplePoints = new List<FloatingSamplePoint>();

    private const float WaterDensity = 1000f; // kg/m³

    // Helper properties for buoyancy calculations
    private float DensityRatio => Mathf.Clamp01(density / WaterDensity);
    private float BuoyancyRatio => Mathf.Clamp01(1f - DensityRatio);
    private float CaptureRange => objectHalfHeight * 2f;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake() => Initialize();

    private void OnEnable() => Initialize();

    private void Initialize()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            defaultDrag = rb.linearDamping;
            defaultAngularDrag = rb.angularDamping;
        }
        CacheObjectHeight();
        RefreshSamplePoints();
    }

    // Called in both Edit mode (via [ExecuteAlways]) and Play mode
    private void Update()
    {
        // Re-scan children every frame so adding/removing points works live
        RefreshSamplePoints();

        if (!Application.isPlaying && previewInEditMode)
        {
            EditModeUpdate();
        }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying) return;
        PlayModeFixedUpdate();
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Calculates the object's half-height from its Colliders or Renderer bounds.
    /// Used to normalise submersion depth so shape actually matters.
    /// </summary>
    private void CacheObjectHeight()
    {
        // Try colliders first (more accurate for physics)
        var colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            objectHalfHeight = Mathf.Max(GetBounds(colliders).extents.y, 0.05f);
            return;
        }

        // Fall back to renderer bounds
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            objectHalfHeight = Mathf.Max(GetBounds(renderers).extents.y, 0.05f);
            return;
        }

        objectHalfHeight = 0.5f; // safe default
    }

    private Bounds GetBounds<T>(T[] components) where T : Component
    {
        Bounds bounds = default;
        bool initialized = false;

        foreach (var comp in components)
        {
            Bounds b = comp switch
            {
                Collider c => c.bounds,
                Renderer r => r.bounds,
                _ => default
            };

            if (!initialized)
            {
                bounds = b;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(b);
            }
        }
        return bounds;
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Edit-mode preview: move + rotate transform directly.</summary>
    private void EditModeUpdate()
    {
        WaterFloatingSystem water = GetWaterSystem();
        if (water == null || samplePoints.Count == 0) return;

        float time = (float)UnityEditor.EditorApplication.timeSinceStartup;
        
        CalculateAverageWaterSurface(water, time, out float avgWaterY, out Vector3 avgNormal);
        UpdateTransformForEditMode(avgWaterY, avgNormal);

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private WaterFloatingSystem GetWaterSystem()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        return water != null ? water : FindFirstObjectByType<WaterFloatingSystem>();
    }

    private void CalculateAverageWaterSurface(WaterFloatingSystem water, float time, out float avgWaterY, out Vector3 avgNormal)
    {
        float totalHeight = 0f;
        Vector3 combinedNormal = Vector3.zero;
        int count = 0;

        foreach (var pt in samplePoints)
        {
            if (pt == null) continue;

            var sample = water.SampleWave(pt.transform.position, time);
            float waterY = sample.height + depthOffset;

            pt.lastWaterHeight = waterY;
            pt.hasValidSample = true;
            pt.isSubmerged = pt.transform.position.y < waterY;

            totalHeight += waterY;
            combinedNormal += sample.normal;
            count++;
        }

        avgWaterY = count > 0 ? totalHeight / count : transform.position.y;
        avgNormal = count > 0 ? (combinedNormal / count).normalized : Vector3.up;
    }

    private void UpdateTransformForEditMode(float avgWaterY, Vector3 avgNormal)
    {
        // sinkFraction = density / waterDensity → how far down the object rests (0=surface, 1=fully under)
        // centerY = waterY + halfHeight * (1 - 2 * sinkFraction)
        float centerY = avgWaterY + objectHalfHeight * (1f - 2f * DensityRatio);

        Vector3 pos = transform.position;
        pos.y = centerY;
        transform.position = pos;

        transform.rotation = Quaternion.FromToRotation(transform.up, avgNormal) * transform.rotation;
    }

    /// <summary>Play-mode: apply Rigidbody buoyancy forces (Archimedes' principle).</summary>
    private void PlayModeFixedUpdate()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        if (water == null || samplePoints.Count == 0) return;

        float volumePerPt = (rb.mass / Mathf.Max(density, 1f)) / samplePoints.Count;
        float gravity = Mathf.Abs(Physics.gravity.y);

        Vector3 combinedNormal = Vector3.zero;
        float maxSubFraction = 0f;
        int normalCount = 0;

        foreach (var pt in samplePoints)
        {
            if (pt == null) continue;

            var sample = water.SampleWave(pt.transform.position, Time.time);
            float waterY = sample.height + depthOffset;

            // Cache for alignment and general state
            combinedNormal += sample.normal;
            normalCount++;

            pt.lastWaterHeight = waterY;
            pt.hasValidSample = true;

            float submersion = waterY - pt.transform.position.y; // positive = under water
            pt.isSubmerged = submersion > 0f;

            if (pt.isSubmerged)
            {
                float subFraction = Mathf.Clamp01(submersion / CaptureRange);
                maxSubFraction = Mathf.Max(maxSubFraction, subFraction);

                ApplyBuoyancyForce(pt, subFraction, volumePerPt, gravity);
                ApplyImpactDamping(pt, subFraction);
            }
            else
            {
                float zoneFactor = ApplySurfaceCaptureZone(pt, submersion, gravity);
                maxSubFraction = Mathf.Max(maxSubFraction, zoneFactor * 0.5f);
            }
        }

        if (normalCount > 0 && maxSubFraction > 0f)
        {
            Vector3 avgNormal = (combinedNormal / normalCount).normalized;
            ApplyAlignmentTorque(avgNormal, maxSubFraction, gravity);
        }

        UpdateDragProperties(maxSubFraction);
    }

    private void ApplyBuoyancyForce(FloatingSamplePoint pt, float subFraction, float volumePerPt, float gravity)
    {
        // F_buoy = ρ_water × g × V_submerged
        // Buoyancy force (non-linear curve)
        float buoyancyFactor = subFraction * (1f + subFraction);
        float forceMag = WaterDensity * gravity * volumePerPt * buoyancyFactor;
        rb.AddForceAtPosition(Vector3.up * forceMag, pt.transform.position, ForceMode.Force);
    }

    private void ApplyImpactDamping(FloatingSamplePoint pt, float subFraction)
    {
        if (waterImpactDamping <= 0f) return;

        Vector3 pointVel = rb.GetPointVelocity(pt.transform.position);
        float dampingScale = waterImpactDamping * subFraction * rb.mass / samplePoints.Count;
        rb.AddForceAtPosition(-pointVel * dampingScale, pt.transform.position, ForceMode.Force);
    }

    private float ApplySurfaceCaptureZone(FloatingSamplePoint pt, float submersion, float gravity)
    {
        float aboveWater = -submersion; // positive distance above surface
        if (aboveWater >= CaptureRange || waterImpactDamping <= 0f) return 0f;

        // Blend: 1 at water surface → 0 at captureRange above
        float zoneFactor = 1f - Mathf.Clamp01(aboveWater / CaptureRange);
        zoneFactor *= zoneFactor; // quadratic falloff — strong near surface

        Vector3 pointVel = rb.GetPointVelocity(pt.transform.position);

        // 1) Damp upward velocity — only resist leaving the water
        if (pointVel.y > 0f)
        {
            float dampScale = waterImpactDamping * zoneFactor * rb.mass / samplePoints.Count;
            rb.AddForceAtPosition(Vector3.down * (pointVel.y * dampScale), pt.transform.position, ForceMode.Force);
        }

        // 2) Surface tension pull — gentle force toward the water
        float pullForce = BuoyancyRatio * zoneFactor * rb.mass * gravity * 0.5f;
        rb.AddForceAtPosition(Vector3.down * pullForce, pt.transform.position, ForceMode.Force);

        return zoneFactor;
    }

    private void ApplyAlignmentTorque(Vector3 avgNormal, float maxSubFraction, float gravity)
    {
        // Cross product gives the rotation axis + magnitude of misalignment
        Vector3 alignAxis = Vector3.Cross(transform.up, avgNormal);

        // Transition from Acceleration to Force to allow mass to influence stability
        // Heavier objects (higher density * mass) will resist alignment more naturally
        float buoyancyRatio = BuoyancyRatio;
        
        // Scale torque by mass to maintain consistent rotational acceleration across different weights
        // but adjusted by density to favor light floaters
        float torqueMag = buoyancyRatio * maxSubFraction * gravity * rb.mass * 2f;
        rb.AddTorque(alignAxis * torqueMag, ForceMode.Force);
    }

    private void UpdateDragProperties(float maxSubFraction)
    {
        // Drag should scale with the object's displacement/volume (Effective Radius)
        // volume = mass / density
        // volume^(1/3) gives a linear scale of the object's size in water
        float volume = rb.mass / Mathf.Max(density, 1f);
        float volumeScale = Mathf.Pow(volume, 1f / 3f);

        rb.linearDamping = Mathf.Lerp(defaultDrag, waterDragCoeff * volumeScale, maxSubFraction);
        rb.angularDamping = Mathf.Lerp(defaultAngularDrag, waterAngularDragCoeff * volumeScale, maxSubFraction);
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Scans all children for FloatingSamplePoint components.</summary>
    public void RefreshSamplePoints()
    {
        samplePoints.Clear();
        GetComponentsInChildren(true, samplePoints);
    }

    /// <summary>Expose sample points for the Editor.</summary>
    public IReadOnlyList<FloatingSamplePoint> SamplePoints => samplePoints;

    /// <summary>Cached object half-height (exposed for Editor display).</summary>
    public float ObjectHalfHeight => objectHalfHeight;
}
