using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RiverTools
{
    public enum BedProfileMode
    {
        UShapeCurved,
        Flat,
        VShape,
        CustomCurve
    }

    public enum CarveMode
    {
        CarveDown,
        SetHeight
    }

    /// <summary>
    /// Fast, multithreaded terrain carver for RiverExtrude splines in Unity 6.
    /// Acts as a stateless procedural modifier that registers riverbed parameters 
    /// with the TerrainBaseline component on target Terrains.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RiverExtrude))]
    [AddComponentMenu("Splines/River Terrain Carver")]
    public class RiverTerrainCarver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RiverExtrude m_RiverExtrude;
        [SerializeField] private SplineContainer m_Container;

        [Header("Carving Profile")]
        [Tooltip("Shape profile of the river bed cross-section.")]
        [SerializeField] private BedProfileMode m_BedProfile = BedProfileMode.UShapeCurved;

        [Tooltip("Custom bed depth curve evaluated across normalized width [0, 1] from center to edge.")]
        [SerializeField] private AnimationCurve m_CustomBedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Depth of river bed in world units below spline elevation.")]
        [SerializeField, Min(0f)] private float m_BedDepth = 2f;

        [Tooltip("Ratio of riverbed carve width relative to full river width [0.1, 1.0]. Values < 1.0 ensure the riverbank meets the river mesh edge.")]
        [SerializeField, Range(0.1f, 1.0f)] private float m_BedWidthRatio = 0.8f;

        [Tooltip("Distance in world units to smoothly blend river bank into original terrain height.")]
        [SerializeField, Min(0.1f)] private float m_BankFalloff = 4f;

        [Tooltip("Height offset in world units above spline elevation at the riverbank edge to ensure the terrain rises slightly above water surface [0 = flush with spline].")]
        [SerializeField] private float m_BankEdgeOffset = 1f;

        [Header("Carve Settings")]
        [Tooltip("Carve mode. CarveDown is recommended to avoid terrain clipping above river surface.")]
        [SerializeField] private CarveMode m_CarveMode = CarveMode.CarveDown;

        [Tooltip("Distance between spline sample points for terrain distance calculations (meters). Lower = higher accuracy.")]
        [SerializeField, Min(0.1f)] private float m_SampleSpacing = 1f;

        [Header("Smoothing")]
        [Tooltip("Number of post-carve smoothing passes to soften bank transitions [0 = disabled].")]
        [SerializeField, Range(0, 5)] private int m_SmoothPasses = 1;

        [Tooltip("Strength of the post-carve smoothing filter [0 = no effect, 1 = maximum smooth].")]
        [SerializeField, Range(0f, 1f)] private float m_SmoothStrength = 0.5f;

        [Header("Texture Painting")]
        [Tooltip("Enable dynamic texture painting along the riverbed and banks.")]
        [SerializeField] private bool m_EnableTexturePainting = true;

        [Tooltip("Target TerrainLayer to paint. Auto-checks if layer is on terrain and auto-appends it if missing.")]
        [SerializeField] private TerrainLayer m_TargetTerrainLayer;

        [Tooltip("Maximum opacity/strength of the painted terrain texture at river centerline [0, 1].")]
        [SerializeField, Range(0f, 1f)] private float m_TextureOpacity = 1f;

        [Tooltip("Width ratio of full texture intensity relative to riverbed width [0.1, 1.0].")]
        [SerializeField, Range(0.1f, 1.0f)] private float m_TextureWidthRatio = 1f;

        [Tooltip("Distance in world units past riverbed edge to smoothly fade out painted texture.")]
        [SerializeField, Min(0.1f)] private float m_TextureBankFalloff = 4f;

        [Tooltip("Opacity falloff curve evaluated from river center to outer texture bank falloff edge.")]
        [SerializeField] private AnimationCurve m_TextureBlendCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Target Terrains")]
        [Tooltip("Terrains to carve. If empty, overlapping active scene terrains will be automatically detected.")]
        [SerializeField] private List<Terrain> m_TargetTerrains = new List<Terrain>();

        [Header("Dynamic Layer")]
        [SerializeField] private bool m_EnableDynamicCarve = true;
        [SerializeField] private bool m_AutoRebuildOnSplineChange = true;
        [SerializeField] private bool m_EnableFastMode = true;

        private bool m_IsDirty = true;
        private bool m_IsCarving = false;
        private bool m_IsActivelyEditing = false;
        private float m_LastEditTime = 0f;

        public RiverExtrude RiverExtrude => m_RiverExtrude;
        public SplineContainer Container => m_Container;

        public BedProfileMode BedProfile { get => m_BedProfile; set { m_BedProfile = value; RequestCarve(); } }
        public AnimationCurve CustomBedCurve => m_CustomBedCurve;
        public float BedDepth { get => m_BedDepth; set { m_BedDepth = Mathf.Max(0f, value); RequestCarve(); } }
        public float BedWidthRatio { get => m_BedWidthRatio; set { m_BedWidthRatio = Mathf.Clamp(value, 0.1f, 1.0f); RequestCarve(); } }
        public float BankFalloff { get => m_BankFalloff; set { m_BankFalloff = Mathf.Max(0.1f, value); RequestCarve(); } }
        public float BankEdgeOffset { get => m_BankEdgeOffset; set { m_BankEdgeOffset = value; RequestCarve(); } }
        public CarveMode Mode { get => m_CarveMode; set { m_CarveMode = value; RequestCarve(); } }
        public int SmoothPasses { get => m_SmoothPasses; set { m_SmoothPasses = Mathf.Clamp(value, 0, 5); RequestCarve(); } }
        public float SmoothStrength { get => m_SmoothStrength; set { m_SmoothStrength = Mathf.Clamp01(value); RequestCarve(); } }

        public bool EnableTexturePainting { get => m_EnableTexturePainting; set { m_EnableTexturePainting = value; RequestCarve(); } }
        public TerrainLayer TargetTerrainLayer { get => m_TargetTerrainLayer; set { m_TargetTerrainLayer = value; RequestCarve(); } }
        public float TextureOpacity { get => m_TextureOpacity; set { m_TextureOpacity = Mathf.Clamp01(value); RequestCarve(); } }
        public float TextureWidthRatio { get => m_TextureWidthRatio; set { m_TextureWidthRatio = Mathf.Clamp(value, 0.1f, 1.0f); RequestCarve(); } }
        public float TextureBankFalloff { get => m_TextureBankFalloff; set { m_TextureBankFalloff = Mathf.Max(0.1f, value); RequestCarve(); } }
        public AnimationCurve TextureBlendCurve => m_TextureBlendCurve;

        public bool EnableDynamicCarve
        {
            get => m_EnableDynamicCarve;
            set
            {
                if (m_EnableDynamicCarve != value)
                {
                    m_EnableDynamicCarve = value;
                    if (m_EnableDynamicCarve)
                        RequestCarve();
                    else
                        RestoreAllSnapshots();
                }
            }
        }

        private void OnEnable()
        {
            EnsureReferences();
            Spline.Changed += OnSplineChanged;
            m_IsDirty = true;
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            UnregisterFromAllTerrains();
        }

        private void OnValidate()
        {
            EnsureReferences();
            m_BedDepth = Mathf.Max(0f, m_BedDepth);
            m_BedWidthRatio = Mathf.Clamp(m_BedWidthRatio, 0.1f, 1.0f);
            m_BankFalloff = Mathf.Max(0.1f, m_BankFalloff);
            m_SampleSpacing = Mathf.Max(0.1f, m_SampleSpacing);
            m_SmoothPasses = Mathf.Clamp(m_SmoothPasses, 0, 5);
            m_SmoothStrength = Mathf.Clamp01(m_SmoothStrength);
            m_TextureOpacity = Mathf.Clamp01(m_TextureOpacity);
            m_TextureWidthRatio = Mathf.Clamp(m_TextureWidthRatio, 0.1f, 1.0f);
            m_TextureBankFalloff = Mathf.Max(0.1f, m_TextureBankFalloff);
            m_IsDirty = true;
        }

        private void EnsureReferences()
        {
            if (m_RiverExtrude == null)
                m_RiverExtrude = GetComponent<RiverExtrude>();

            if (m_Container == null)
            {
                if (m_RiverExtrude != null && m_RiverExtrude.Container != null)
                    m_Container = m_RiverExtrude.Container;
                else
                    m_Container = GetComponent<SplineContainer>();
            }
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (!m_AutoRebuildOnSplineChange || m_Container == null)
                return;

            if (spline == m_Container.Spline)
            {
                if (m_EnableFastMode)
                {
                    m_IsActivelyEditing = true;
                    m_LastEditTime = Time.realtimeSinceStartup;
                }
                RequestCarve();
            }
        }

        private void Update()
        {
            if (m_IsActivelyEditing && (Time.realtimeSinceStartup - m_LastEditTime > 0.2f))
            {
                m_IsActivelyEditing = false;
                RequestCarve();
            }

            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                if (m_EnableDynamicCarve && m_AutoRebuildOnSplineChange)
                {
                    if (m_EnableFastMode)
                    {
                        m_IsActivelyEditing = true;
                        m_LastEditTime = Time.realtimeSinceStartup;
                    }
                    RequestCarve();
                }
            }

            if (m_IsDirty && m_EnableDynamicCarve)
            {
                CarveDynamicInternal();
                m_IsDirty = false;
            }
        }

        public void RequestCarve()
        {
            m_IsDirty = true;
        }

        public List<Terrain> GetTargetTerrains()
        {
            if (m_TargetTerrains != null && m_TargetTerrains.Count > 0)
            {
                var valid = new List<Terrain>();
                foreach (var t in m_TargetTerrains)
                {
                    if (t != null && t.terrainData != null) valid.Add(t);
                }
                if (valid.Count > 0) return valid;
            }

            var result = new List<Terrain>();
            Bounds bounds = CalculateSplineWorldBounds(m_BankFalloff + 10f);
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 tPos = terrain.transform.position;
                Vector3 tSize = terrain.terrainData.size;
                Bounds tBounds = new Bounds(tPos + tSize * 0.5f, tSize);
                if (bounds.Intersects(tBounds))
                {
                    result.Add(terrain);
                }
            }
            return result;
        }

        public Bounds CalculateSplineWorldBounds(float margin)
        {
            EnsureReferences();
            if (m_Container == null || m_Container.Spline == null || m_Container.Spline.Count == 0)
                return new Bounds(transform.position, Vector3.one * 10f);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);

            foreach (var knot in m_Container.Spline.Knots)
            {
                float3 worldPos = math.transform(m_Container.transform.localToWorldMatrix, knot.Position);
                min = math.min(min, worldPos);
                max = math.max(max, worldPos);
            }

            Vector3 center = (Vector3)(min + max) * 0.5f;
            Vector3 size = (Vector3)(max - min) + Vector3.one * (margin * 2f);
            return new Bounds(center, size);
        }

        public void CarveDynamic()
        {
            m_EnableDynamicCarve = true;
            CarveDynamicInternal();
        }

        private void CarveDynamicInternal()
        {
            if (m_IsCarving) return;
            EnsureReferences();

            if (m_Container == null || m_Container.Spline == null || m_Container.Spline.Count < 2)
                return;

            m_IsCarving = true;
            try
            {
                List<Terrain> targets = GetTargetTerrains();
                SamplePolyline(out var samples, out float maxHalfWidth, out float maxInfluence);

                string riverID = GetInstanceID().ToString();

                foreach (var terrain in targets)
                {
                    if (terrain == null || terrain.terrainData == null) continue;

                    TerrainBaseline baseline = TerrainBaseline.GetOrCreate(terrain);

                    int targetTexIdx = -1;
                    if (m_EnableTexturePainting && m_TargetTerrainLayer != null)
                    {
                        targetTexIdx = FindOrAddTerrainLayer(terrain.terrainData, m_TargetTerrainLayer);
                    }

                    var modifier = new TerrainBaseline.RiverCarveModifier
                    {
                        SourceID = riverID,
                        Samples = samples,
                        BedDepth = m_BedDepth,
                        BedWidthRatio = m_BedWidthRatio,
                        BankFalloff = m_BankFalloff,
                        BankEdgeOffset = m_BankEdgeOffset,
                        Mode = m_CarveMode,
                        ProfileMode = m_BedProfile,
                        CustomCurve = m_CustomBedCurve,
                        EnableTexturePainting = m_EnableTexturePainting,
                        TargetLayerIndex = targetTexIdx,
                        TextureOpacity = m_TextureOpacity,
                        TextureWidthRatio = m_TextureWidthRatio,
                        TextureBankFalloff = m_TextureBankFalloff,
                        TextureBlendCurve = m_TextureBlendCurve
                    };

                    baseline.RegisterModifier(riverID, modifier);
                }
            }
            finally
            {
                m_IsCarving = false;
            }
        }

        public struct SplinePointSample
        {
            public Vector3 Position;
            public Vector3 Right;
            public float HalfWidth;
        }

        public void SamplePolyline(out List<SplinePointSample> samples, out float maxHalfWidth, out float maxInfluence)
        {
            samples = new List<SplinePointSample>();
            maxHalfWidth = 0f;
            maxInfluence = 0f;

            float length = m_Container.CalculateLength();
            if (length <= 0f) return;

            float spacing = (m_EnableFastMode && m_IsActivelyEditing) ? 15f : m_SampleSpacing;
            int count = Mathf.Max(4, Mathf.CeilToInt(length / spacing));

            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                float3 worldPos, fwd, up, right;
                if (m_RiverExtrude != null)
                {
                    m_RiverExtrude.EvaluateFrame(t, out worldPos, out fwd, out up, out right);
                }
                else
                {
                    m_Container.Evaluate(t, out worldPos, out float3 tangent, out float3 upVec);
                    fwd = math.normalizesafe(tangent, new float3(0, 0, 1));
                    right = math.normalizesafe(math.cross(new float3(0, 1, 0), fwd), new float3(1, 0, 0));
                    up = math.normalizesafe(math.cross(fwd, right), new float3(0, 1, 0));
                }

                float width = 4f;
                if (m_RiverExtrude != null)
                {
                    width = m_RiverExtrude.GetEvaluatedWidthAt(t);
                }
                else
                {
                    width = GetMaxBaseWidth();
                }

                float halfWidth = Mathf.Max(0.1f, width * 0.5f);
                if (halfWidth > maxHalfWidth) maxHalfWidth = halfWidth;

                float influence = (halfWidth * m_BedWidthRatio) + m_BankFalloff;
                if (m_EnableTexturePainting)
                {
                    float texInfluence = (halfWidth * m_TextureWidthRatio) + m_TextureBankFalloff;
                    influence = Mathf.Max(influence, texInfluence);
                }
                if (influence > maxInfluence) maxInfluence = influence;

                samples.Add(new SplinePointSample
                {
                    Position = worldPos,
                    Right = right,
                    HalfWidth = halfWidth
                });
            }
        }

        private float GetMaxBaseWidth()
        {
            if (m_RiverExtrude != null)
            {
                float width = m_RiverExtrude.Width;
                if (m_RiverExtrude.EnableWidthCurve && m_RiverExtrude.WidthCurve != null)
                {
                    float maxCurveVal = 0f;
                    for (int i = 0; i <= 20; i++)
                    {
                        float val = Mathf.Abs(m_RiverExtrude.WidthCurve.Evaluate(i / 20f));
                        if (val > maxCurveVal) maxCurveVal = val;
                    }
                    width *= maxCurveVal;
                }
                return Mathf.Max(0.1f, width);
            }
            return 4f;
        }

        private int FindOrAddTerrainLayer(TerrainData tData, TerrainLayer targetLayer)
        {
            if (tData == null || targetLayer == null) return -1;

            TerrainLayer[] layers = tData.terrainLayers;
            if (layers == null || layers.Length == 0)
            {
#if UNITY_EDITOR
                Undo.RecordObject(tData, "Add Terrain Layer");
#endif
                tData.terrainLayers = new TerrainLayer[] { targetLayer };
#if UNITY_EDITOR
                EditorUtility.SetDirty(tData);
#endif
                return 0;
            }

            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == targetLayer)
                    return i;
            }

#if UNITY_EDITOR
            Undo.RecordObject(tData, "Auto Add Terrain Layer");
#endif
            TerrainLayer[] newLayers = new TerrainLayer[layers.Length + 1];
            System.Array.Copy(layers, newLayers, layers.Length);
            newLayers[layers.Length] = targetLayer;
            tData.terrainLayers = newLayers;
#if UNITY_EDITOR
            EditorUtility.SetDirty(tData);
#endif
            return layers.Length;
        }

        public struct SegmentData
        {
            public Vector3 P0;
            public Vector3 P1;
            public float HalfWidth0;
            public float HalfWidth1;
            public Bounds BoundingBox;
        }

        public static List<SegmentData> PrepareSplineSegments(List<SplinePointSample> samples, float maxInfluenceMargin)
        {
            var segments = new List<SegmentData>();
            if (samples == null || samples.Count < 2) return segments;

            for (int i = 0; i < samples.Count - 1; i++)
            {
                var s0 = samples[i];
                var s1 = samples[i + 1];

                Vector3 min = Vector3.Min(s0.Position, s1.Position) - Vector3.one * maxInfluenceMargin;
                Vector3 max = Vector3.Max(s0.Position, s1.Position) + Vector3.one * maxInfluenceMargin;

                segments.Add(new SegmentData
                {
                    P0 = s0.Position,
                    P1 = s1.Position,
                    HalfWidth0 = s0.HalfWidth,
                    HalfWidth1 = s1.HalfWidth,
                    BoundingBox = new Bounds((min + max) * 0.5f, max - min)
                });
            }

            return segments;
        }

        public static void FindClosestSplinePoint(Vector3 worldPos, List<SegmentData> segments, out Vector3 closestPoint, out float distanceToCenterline, out float localHalfWidth)
        {
            closestPoint = Vector3.zero;
            distanceToCenterline = float.MaxValue;
            localHalfWidth = 1f;

            if (segments == null || segments.Count == 0) return;

            float minSqDist = float.MaxValue;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];

                if (!seg.BoundingBox.Contains(new Vector3(worldPos.x, seg.BoundingBox.center.y, worldPos.z)))
                    continue;

                Vector3 v = seg.P1 - seg.P0;
                Vector3 w = worldPos - seg.P0;

                float c1 = Vector3.Dot(w, v);
                float c2 = Vector3.Dot(v, v);
                float t = (c2 <= 0.00001f) ? 0f : Mathf.Clamp01(c1 / c2);

                Vector3 proj = seg.P0 + t * v;

                float dx = worldPos.x - proj.x;
                float dz = worldPos.z - proj.z;
                float sqDist = dx * dx + dz * dz;

                if (sqDist < minSqDist)
                {
                    minSqDist = sqDist;
                    closestPoint = proj;
                    localHalfWidth = Mathf.Lerp(seg.HalfWidth0, seg.HalfWidth1, t);
                }
            }

            if (minSqDist == float.MaxValue)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    var seg = segments[i];
                    Vector3 v = seg.P1 - seg.P0;
                    Vector3 w = worldPos - seg.P0;
                    float c1 = Vector3.Dot(w, v);
                    float c2 = Vector3.Dot(v, v);
                    float t = (c2 <= 0.00001f) ? 0f : Mathf.Clamp01(c1 / c2);
                    Vector3 proj = seg.P0 + t * v;

                    float dx = worldPos.x - proj.x;
                    float dz = worldPos.z - proj.z;
                    float sqDist = dx * dx + dz * dz;

                    if (sqDist < minSqDist)
                    {
                        minSqDist = sqDist;
                        closestPoint = proj;
                        localHalfWidth = Mathf.Lerp(seg.HalfWidth0, seg.HalfWidth1, t);
                    }
                }
            }

            distanceToCenterline = Mathf.Sqrt(minSqDist);
        }

        public static float EvaluateBedProfile(BedProfileMode profileMode, float u, AnimationCurve customCurve)
        {
            u = Mathf.Clamp01(u);
            switch (profileMode)
            {
                case BedProfileMode.UShapeCurved:
                    return Mathf.Cos(u * Mathf.PI * 0.5f);
                case BedProfileMode.Flat:
                    return 1.0f;
                case BedProfileMode.VShape:
                    return 1.0f - u;
                case BedProfileMode.CustomCurve:
                    return customCurve != null ? Mathf.Clamp01(customCurve.Evaluate(u)) : (1.0f - u);
                default:
                    return Mathf.Cos(u * Mathf.PI * 0.5f);
            }
        }

        public void RestoreAllSnapshots()
        {
            UnregisterFromAllTerrains();
            m_EnableDynamicCarve = false;
        }

        private void UnregisterFromAllTerrains()
        {
            string riverID = GetInstanceID().ToString();
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null) continue;
                var baseline = terrain.GetComponent<TerrainBaseline>();
                if (baseline != null)
                {
                    baseline.UnregisterModifier(riverID);
                }
            }
        }

        public void BakeIntoTerrain()
        {
            CarveDynamicInternal();

            string riverID = GetInstanceID().ToString();
            List<Terrain> targets = GetTargetTerrains();

            foreach (var terrain in targets)
            {
                if (terrain == null) continue;
                var baseline = terrain.GetComponent<TerrainBaseline>();
                if (baseline != null)
                {
                    baseline.UnregisterModifier(riverID);
                    baseline.EnsureLoaded();
                }
            }

            m_EnableDynamicCarve = false;
        }
    }
}
