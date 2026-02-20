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
    [Tooltip("Upward force applied per submerged sample point each FixedUpdate.")]
    public float buoyancyForce = 15f;

    [Tooltip("Downward offset applied to the sample-point water level. " +
             "Positive = object sinks deeper, negative = object rides higher.")]
    public float depthOffset = 0f;

    [Header("Water Drag (applied while any point is submerged)")]
    public float waterDrag        = 3f;
    public float waterAngularDrag = 1f;

    // ── Internal ──────────────────────────────────────────────────────────
    private Rigidbody rb;
    private float defaultDrag;
    private float defaultAngularDrag;

    // Cache of sample points; refreshed when structure changes
    private readonly List<FloatingSamplePoint> samplePoints = new List<FloatingSamplePoint>();

    // Edit-mode: store where we moved the object so we don't fight the transform
    private Vector3 editModeTargetPosition;
    private Quaternion editModeTargetRotation;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        defaultDrag        = rb.linearDamping;
        defaultAngularDrag = rb.angularDamping;
        RefreshSamplePoints();
    }

    private void OnEnable()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
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
    /// <summary>Edit-mode preview: move + rotate transform directly.</summary>
    private void EditModeUpdate()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        // Fallback: static Instance is cleared on domain reload; find it manually
        if (water == null) water = FindObjectOfType<WaterFloatingSystem>();
        if (water == null || samplePoints.Count == 0) return;

        double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
        float  t          = (float)editorTime;

        float  totalHeight   = 0f;
        Vector3 avgNormal    = Vector3.zero;
        int    submergedCount = 0;

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
            submergedCount++;
        }

        if (submergedCount == 0) return;

        float avgY       = totalHeight / submergedCount;
        avgNormal        = (avgNormal / submergedCount).normalized;

        // Position: keep XZ, set Y to average water height
        Vector3 pos      = transform.position;
        pos.y            = avgY;

        // Rotation: align up-axis to average wave normal
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, avgNormal) * transform.rotation;

        transform.position = pos;
        transform.rotation = targetRot;

        // Keep the scene repainted
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>Play-mode: apply Rigidbody forces.</summary>
    private void PlayModeFixedUpdate()
    {
        WaterFloatingSystem water = WaterFloatingSystem.Instance;
        if (water == null || samplePoints.Count == 0) return;

        bool anySubmerged  = false;

        foreach (var pt in samplePoints)
        {
            if (pt == null) continue;

            WaterFloatingSystem.WaveSample sample = water.SampleWave(pt.transform.position, Time.time);
            float waterY = sample.height + depthOffset;

            pt.lastWaterHeight = waterY;
            pt.hasValidSample  = true;

            float submersion = waterY - pt.transform.position.y;
            pt.isSubmerged   = submersion > 0f;

            if (pt.isSubmerged)
            {
                anySubmerged = true;
                // Clamp submersion so we don't get crazy forces on large waves
                float clampedSub = Mathf.Clamp01(submersion);
                Vector3 force    = Vector3.up * buoyancyForce * clampedSub;
                rb.AddForceAtPosition(force, pt.transform.position, ForceMode.Force);
            }
        }

        // Apply water drag when at least one point is submerged
        rb.linearDamping        = anySubmerged ? waterDrag        : defaultDrag;
        rb.angularDamping = anySubmerged ? waterAngularDrag : defaultAngularDrag;
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
}
