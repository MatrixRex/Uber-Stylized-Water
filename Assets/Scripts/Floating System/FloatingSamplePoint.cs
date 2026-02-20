using UnityEngine;

/// <summary>
/// Attach this to an empty child GameObject on your floating object.
/// Its world-space position is used as a sample point by FloatingObject.
/// No manual configuration needed — just place the child where you want
/// to probe the water surface (e.g. bow, stern, port, starboard).
/// </summary>
[AddComponentMenu("Water/Floating Sample Point")]
[ExecuteAlways]
public class FloatingSamplePoint : MonoBehaviour
{
    // ── Set internally by FloatingObject each frame ───────────────────────
    [HideInInspector] public float  lastWaterHeight;
    [HideInInspector] public bool   isSubmerged;
    [HideInInspector] public bool   hasValidSample;

    // ── Gizmo colours ─────────────────────────────────────────────────────
    private static readonly Color SampleColor = new Color(0.2f, 0.5f, 1f, 0.9f);   // blue
    private static readonly Color WaterColor  = new Color(0.1f, 0.9f, 0.4f, 0.9f); // green
    private static readonly Color LineColor   = new Color(1f,   1f,   1f, 0.6f);   // white

    private void OnDrawGizmosSelected() => DrawGizmos();
    private void OnDrawGizmos()         => DrawGizmos(); // always visible

    private void DrawGizmos()
    {
        Vector3 samplePos = transform.position;

        // ── Sample Point ─────────────────────────────────────────────────
        Gizmos.color = SampleColor;
        Gizmos.DrawSphere(samplePos, 0.07f);

        if (!hasValidSample) return;

        // ── Water Surface Point ───────────────────────────────────────────
        Vector3 waterPos = new Vector3(samplePos.x, lastWaterHeight, samplePos.z);
        Gizmos.color = WaterColor;
        Gizmos.DrawSphere(waterPos, 0.07f);

        // ── Connecting Line ───────────────────────────────────────────────
        Gizmos.color = LineColor;
        Gizmos.DrawLine(samplePos, waterPos);
    }
}
