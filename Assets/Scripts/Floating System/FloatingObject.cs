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

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        defaultDrag        = rb.linearDamping;
        defaultAngularDrag = rb.angularDamping;
        CacheObjectHeight();
        RefreshSamplePoints();
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        CacheObjectHeight();
        RefreshSamplePoints();
    }

    // Called in both Edit mode (via [ExecuteAlways]) and Play mode
    private void Update()
    {
        // Re-scan children every frame so adding/removing points works live
        RefreshSamplePoints();

        bool inEditMode = !Application.isPlaying;
        if (inEditMode && previewInEditMode)
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
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            foreach (var c in cols) b.Encapsulate(c.bounds);
            objectHalfHeight = Mathf.Max(b.extents.y, 0.05f);
            return;
        }

        // Fall back to renderer bounds
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            objectHalfHeight = Mathf.Max(b.extents.y, 0.05f);
            return;
        }

        objectHalfHeight = 0.5f; // safe default
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Edit-mode preview: move + rotate transform directly.</summary>
    private void EditModeUpdate()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        // Fallback: static Instance is cleared on domain reload; find it manually
        if (water == null) water = FindFirstObjectByType<WaterFloatingSystem>();
        if (water == null || samplePoints.Count == 0) return;

        double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
        float  t          = (float)editorTime;

        // In edit mode: place the object at its natural float depth based on density.
        // sinkFraction = density / waterDensity → how far down the object rests (0=surface, 1=fully under)
        float sinkFraction = Mathf.Clamp01(density / WaterDensity);

        float   totalHeight = 0f;
        Vector3 avgNormal   = Vector3.zero;
        int     sampleCount = 0;

        foreach (var pt in samplePoints)
        {
            if (pt == null) continue;

            WaterFloatingSystem.WaveSample sample = water.SampleWave(pt.transform.position, t);
            float waterY = sample.height + depthOffset;

            pt.lastWaterHeight = waterY;
            pt.hasValidSample  = true;
            pt.isSubmerged     = pt.transform.position.y < waterY;

            totalHeight += waterY;
            avgNormal   += sample.normal;
            sampleCount++;
        }

        if (sampleCount == 0) return;

        float avgWaterY = totalHeight / sampleCount;
        avgNormal       = (avgNormal / sampleCount).normalized;

        // Center Y so the bottom of the object is sinkFraction deep:
        // centerY = waterY + halfHeight - fullHeight * sinkFraction
        //         = waterY + halfHeight * (1 - 2 * sinkFraction)
        float centerY = avgWaterY + objectHalfHeight * (1f - 2f * sinkFraction);

        Vector3    pos       = transform.position;
        pos.y                = centerY;
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, avgNormal) * transform.rotation;

        transform.position = pos;
        transform.rotation = targetRot;

        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>Play-mode: apply Rigidbody buoyancy forces (Archimedes' principle).</summary>
    private void PlayModeFixedUpdate()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        if (water == null || samplePoints.Count == 0) return;

        // ── Archimedes' principle ─────────────────────────────────────────
        // F_buoy = ρ_water × g × V_submerged
        // Object volume derived from mass and user-set density: V = mass / density
        // Submersion is normalised by the object's actual Y extent so a thin plank
        // and a fat barrel of the same mass float at the same *fraction* of their height.
        float g           = Mathf.Abs(Physics.gravity.y);
        float volume      = rb.mass / Mathf.Max(density, 1f);
        float volumePerPt = volume / samplePoints.Count;

        bool  anySubmerged    = false;
        float maxSubFraction  = 0f;

        foreach (var pt in samplePoints)
        {
            if (pt == null) continue;

            WaterFloatingSystem.WaveSample sample = water.SampleWave(pt.transform.position, Time.time);
            float waterY = sample.height + depthOffset;

            pt.lastWaterHeight = waterY;
            pt.hasValidSample  = true;

            float submersion = waterY - pt.transform.position.y;  // positive = under water
            pt.isSubmerged   = submersion > 0f;

            if (pt.isSubmerged)
            {
                anySubmerged = true;

                float subFraction = Mathf.Clamp01(submersion / (2f * objectHalfHeight));
                maxSubFraction    = Mathf.Max(maxSubFraction, subFraction);

                // ── Buoyancy force (non-linear curve) ─────────────────────
                float buoyancyFactor = subFraction * (1f + subFraction);
                float forceMag      = WaterDensity * g * volumePerPt * buoyancyFactor;
                rb.AddForceAtPosition(Vector3.up * forceMag, pt.transform.position, ForceMode.Force);

                // ── Velocity damping (energy absorption) ──────────────────
                if (waterImpactDamping > 0f)
                {
                    Vector3 pointVel     = rb.GetPointVelocity(pt.transform.position);
                    float   dampingScale = waterImpactDamping * subFraction * rb.mass / samplePoints.Count;
                    Vector3 dampingForce = -pointVel * dampingScale;
                    rb.AddForceAtPosition(dampingForce, pt.transform.position, ForceMode.Force);
                }
            }
            else
            {
                // ── Surface capture zone ──────────────────────────────────
                // Extends ABOVE the water surface by one object height.
                // In real water, objects don't just launch out — the surface
                // resists them leaving.  Two effects:
                //
                // 1. Damp upward velocity so the object can't fly out.
                // 2. Pull the object back toward the surface (surface tension).
                //
                // Both fade out the further above the surface the point is.
                float aboveWater     = -submersion;  // positive distance above surface
                float captureRange   = objectHalfHeight * 2f;

                if (aboveWater < captureRange && waterImpactDamping > 0f)
                {
                    // Blend: 1 at water surface → 0 at captureRange above
                    float zoneFactor = 1f - Mathf.Clamp01(aboveWater / captureRange);
                    zoneFactor *= zoneFactor;  // quadratic falloff — strong near surface

                    // Track that we're still in the water's influence
                    maxSubFraction = Mathf.Max(maxSubFraction, zoneFactor * 0.5f);

                    Vector3 pointVel = rb.GetPointVelocity(pt.transform.position);

                    // 1) Damp upward velocity — only resist leaving the water,
                    //    never resist falling back in
                    if (pointVel.y > 0f)
                    {
                        float dampScale    = waterImpactDamping * zoneFactor * rb.mass / samplePoints.Count;
                        Vector3 dampForce  = Vector3.down * (pointVel.y * dampScale);
                        rb.AddForceAtPosition(dampForce, pt.transform.position, ForceMode.Force);
                    }

                    // 2) Surface tension pull — gentle force toward the water
                    //    Stronger for lighter objects (higher buoyancy ratio)
                    float buoyancyRatio = Mathf.Clamp01(1f - density / WaterDensity); // 0.9 for cork, 0 for steel
                    float pullForce     = buoyancyRatio * zoneFactor * rb.mass * g * 0.5f;
                    rb.AddForceAtPosition(Vector3.down * pullForce, pt.transform.position, ForceMode.Force);
                }
            }
        }

        // ── Drag scales with submersion depth ─────────────────────────────
        float massScale   = Mathf.Pow(rb.mass, 1f / 3f);
        rb.linearDamping  = Mathf.Lerp(defaultDrag,        waterDragCoeff        * massScale, maxSubFraction);
        rb.angularDamping = Mathf.Lerp(defaultAngularDrag, waterAngularDragCoeff * massScale, maxSubFraction);
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
