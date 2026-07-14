using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace RiverTools
{
    /// <summary>
    /// Drop-in replacement for SplineExtrude, purpose-built for flat, variable-width
    /// river ribbons. Unlike SplineExtrude / IExtrudeShape, U is derived from actual
    /// world-space width at each ring, so a tiling texture never stretches as the
    /// river gets wider or narrower - only TexelsPerUnit controls tile density.
    ///
    /// Tested against com.unity.splines 2.8.4.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("Splines/River Extrude")]
    public class RiverExtrude : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] SplineContainer m_Container;

        [Header("Shape")]
        [Tooltip("Flat width in world units, used everywhere a knot's multiplier is 1.")]
        [SerializeField, Min(0f)] float m_BaseWidth = 4f;

        [Tooltip("One multiplier per knot, in knot order (auto-resized to match the spline). 1 = BaseWidth at that knot, 0.5 = half width, 2 = double width. Values are linearly interpolated between knots.")]
        [SerializeField] List<float> m_KnotWidthMultipliers = new List<float>();

        [Tooltip("Vertices across the width of the river. 2 = flat ribbon, more = smoother bank falloff / room for vertex colors.")]
        [SerializeField, Range(2, 32)] int m_CrossSections = 2;

        [Header("Mesh Density")]
        [Tooltip("How many edge loops per unit of spline length.")]
        [SerializeField, Min(0.01f)] float m_SegmentsPerUnit = 2f;

        [Header("UVs / Tiling")]
        [Tooltip("World units covered by one texture tile across the width (U axis). Width-correct: no stretching as river width changes.")]
        [SerializeField, Min(0.01f)] float m_UTexelSize = 1f;

        [Tooltip("World units covered by one texture tile along the flow direction (V axis).")]
        [SerializeField, Min(0.01f)] float m_VTexelSize = 1f;

        [Header("Update")]
        [SerializeField] bool m_RebuildOnSplineChange = true;
        [SerializeField] bool m_RebuildEveryFrame = false;

        [Header("Collider (optional)")]
        [SerializeField] bool m_UpdateMeshCollider = false;

        [HideInInspector] [SerializeField] bool m_IsBaked = false;
        [HideInInspector] [SerializeField] Mesh m_BakedMesh = null;

        Mesh m_Mesh;
        bool m_RebuildRequested = true;

        public Mesh GeneratedMesh => m_Mesh;

        public bool IsBaked
        {
            get => m_IsBaked;
            set => m_IsBaked = value;
        }

        public Mesh BakedMesh
        {
            get => m_BakedMesh;
            set => m_BakedMesh = value;
        }

        public bool UpdateMeshCollider
        {
            get => m_UpdateMeshCollider;
            set => m_UpdateMeshCollider = value;
        }

        public SplineContainer Container
        {
            get => m_Container;
            set { m_Container = value; m_RebuildRequested = true; }
        }

        public float BaseWidth
        {
            get => m_BaseWidth;
            set { m_BaseWidth = Mathf.Max(0f, value); m_RebuildRequested = true; }
        }

        /// <summary>
        /// Sets the width multiplier for a specific knot (0 = first knot). Resizes the
        /// list if needed. 1 = BaseWidth, 0.5 = half width at that knot, etc.
        /// </summary>
        public void SetKnotWidthMultiplier(int knotIndex, float multiplier)
        {
            EnsureKnotWidthListSize(knotIndex + 1);
            m_KnotWidthMultipliers[knotIndex] = multiplier;
            m_RebuildRequested = true;
        }

        void EnsureKnotWidthListSize(int count)
        {
            while (m_KnotWidthMultipliers.Count < count)
                m_KnotWidthMultipliers.Add(1f);
        }

        public int CrossSections
        {
            get => m_CrossSections;
            set { m_CrossSections = Mathf.Max(2, value); m_RebuildRequested = true; }
        }

        public float SegmentsPerUnit
        {
            get => m_SegmentsPerUnit;
            set { m_SegmentsPerUnit = Mathf.Max(0.01f, value); m_RebuildRequested = true; }
        }

        public float UTexelSize
        {
            get => m_UTexelSize;
            set { m_UTexelSize = Mathf.Max(0.01f, value); m_RebuildRequested = true; }
        }

        public float VTexelSize
        {
            get => m_VTexelSize;
            set { m_VTexelSize = Mathf.Max(0.01f, value); m_RebuildRequested = true; }
        }

        void OnEnable()
        {
            EnsureMeshExists();
            Spline.Changed += OnSplineChanged;
            m_RebuildRequested = true;
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        void OnValidate()
        {
            m_BaseWidth = Mathf.Max(0f, m_BaseWidth);
            for (int i = 0; i < m_KnotWidthMultipliers.Count; i++)
                m_KnotWidthMultipliers[i] = Mathf.Max(0f, m_KnotWidthMultipliers[i]);
            m_CrossSections = Mathf.Max(2, m_CrossSections);
            m_SegmentsPerUnit = Mathf.Max(0.01f, m_SegmentsPerUnit);
            m_UTexelSize = Mathf.Max(0.01f, m_UTexelSize);
            m_VTexelSize = Mathf.Max(0.01f, m_VTexelSize);
            m_RebuildRequested = true;
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (!m_RebuildOnSplineChange || m_Container == null)
                return;

            // Rebuild if the modified spline belongs to our container.
            if (spline == m_Container.Spline)
                m_RebuildRequested = true;
        }

        void Update()
        {
            if (m_RebuildEveryFrame)
                m_RebuildRequested = true;

            if (m_RebuildRequested)
            {
                Rebuild();
                m_RebuildRequested = false;
            }
        }

        /// <summary>
        /// Converts a normalized spline t into a fractional knot index, then linearly
        /// interpolates between the two surrounding entries in m_KnotWidthMultipliers.
        /// </summary>
        float EvaluateKnotWidthMultiplier(Spline spline, float normalizedT, int knotCount)
        {
            if (knotCount <= 0)
                return 1f;
            if (knotCount == 1)
                return m_KnotWidthMultipliers[0];

            float knotT = spline.ConvertIndexUnit(normalizedT, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            int i0 = Mathf.FloorToInt(knotT);
            float frac = knotT - i0;
            int i1 = i0 + 1;

            if (spline.Closed)
            {
                i0 = ((i0 % knotCount) + knotCount) % knotCount;
                i1 = ((i1 % knotCount) + knotCount) % knotCount;
            }
            else
            {
                i0 = Mathf.Clamp(i0, 0, knotCount - 1);
                i1 = Mathf.Clamp(i1, 0, knotCount - 1);
            }

            return Mathf.Lerp(m_KnotWidthMultipliers[i0], m_KnotWidthMultipliers[i1], frac);
        }

        void EnsureMeshExists()
        {
            var filter = GetComponent<MeshFilter>();
            if (m_IsBaked)
            {
                if (m_BakedMesh != null && filter != null)
                {
                    if (filter.sharedMesh != m_BakedMesh)
                        filter.sharedMesh = m_BakedMesh;
                }
                return;
            }

            if (m_Mesh == null)
            {
                m_Mesh = new Mesh { name = "River Mesh" };
                m_Mesh.hideFlags = HideFlags.DontSave;
            }

            if (filter != null && filter.sharedMesh != m_Mesh)
                filter.sharedMesh = m_Mesh;
        }

        /// <summary>
        /// Rebuild the river mesh immediately. Safe to call manually (e.g. from an
        /// editor tool or after procedurally changing the spline/width curve).
        /// </summary>
        public void Rebuild()
        {
            if (m_IsBaked)
            {
                EnsureMeshExists();
                if (m_UpdateMeshCollider && TryGetComponent<MeshCollider>(out var bakedCollider) && m_BakedMesh != null)
                {
                    if (bakedCollider.sharedMesh != m_BakedMesh)
                        bakedCollider.sharedMesh = m_BakedMesh;
                }
                return;
            }

            if (m_Container == null || m_Container.Spline == null || m_Container.Spline.Count < 2)
                return;

            EnsureMeshExists();

            var spline = m_Container.Spline;

            // World-space length (accounts for this transform's scale/rotation).
            float length = m_Container.CalculateLength();
            if (length <= 0f)
                return;

            int steps = Mathf.Max(1, Mathf.CeilToInt(length * m_SegmentsPerUnit));
            int cols = m_CrossSections;
            int knotCount = spline.Count;

            EnsureKnotWidthListSize(knotCount);

            int vertCount = (steps + 1) * cols;
            var vertices = new List<Vector3>(vertCount);
            var normals = new List<Vector3>(vertCount);
            var uvs = new List<Vector2>(vertCount);
            var tris = new List<int>(steps * (cols - 1) * 6);

            float3 prevWorldPos = default;
            float distanceSoFar = 0f;

            float vTexelSize = m_VTexelSize;
            if (spline.Closed)
            {
                float totalV = length / m_VTexelSize;
                float roundedV = Mathf.Round(totalV);
                if (roundedV < 1f) roundedV = 1f;
                vTexelSize = length / roundedV;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;

                m_Container.Evaluate(t, out float3 worldPos, out float3 tangent, out float3 upVec);

                float3 fwd = math.normalizesafe(tangent, new float3(0, 0, 1));
                float3 up = math.normalizesafe(upVec, new float3(0, 1, 0));
                float3 right = math.normalizesafe(math.cross(up, fwd), new float3(1, 0, 0));

                if (i > 0)
                    distanceSoFar += math.distance(worldPos, prevWorldPos);
                prevWorldPos = worldPos;

                // Interpolate the width multiplier between the two nearest knots.
                float widthMultiplier = EvaluateKnotWidthMultiplier(spline, t, knotCount);
                float width = Mathf.Max(0f, m_BaseWidth * widthMultiplier);
                float v = distanceSoFar / vTexelSize;

                for (int c = 0; c < cols; c++)
                {
                    float frac = cols == 1 ? 0.5f : c / (float)(cols - 1); // 0 = left edge, 1 = right edge
                    float lateralOffset = (frac - 0.5f) * width;

                    float3 worldVert = worldPos + right * lateralOffset;

                    // Convert back to local space since the mesh lives under this transform.
                    Vector3 localVert = transform.InverseTransformPoint(worldVert);
                    vertices.Add(localVert);

                    Vector3 localNormal = transform.InverseTransformDirection(up);
                    normals.Add(localNormal.normalized);

                    // U is driven by actual world distance from the left edge, so texel
                    // density stays constant regardless of how wide the river gets.
                    float u = (frac * width) / m_UTexelSize;
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int i = 0; i < steps; i++)
            {
                int rowA = i * cols;
                int rowB = (i + 1) * cols;

                for (int c = 0; c < cols - 1; c++)
                {
                    int a = rowA + c;
                    int b = rowA + c + 1;
                    int cIdx = rowB + c;
                    int d = rowB + c + 1;

                    // Wind so the face points along +up (matches upVector from the spline).
                    tris.Add(a); tris.Add(cIdx); tris.Add(b);
                    tris.Add(b); tris.Add(cIdx); tris.Add(d);
                }
            }

            m_Mesh.Clear();
            m_Mesh.SetVertices(vertices);
            m_Mesh.SetNormals(normals);
            m_Mesh.SetUVs(0, uvs);

            // Initialize vertex colors to clear black with 1.0 alpha (no overlay)
            Color[] colors = new Color[vertices.Count];
            for (int k = 0; k < colors.Length; k++)
            {
                colors[k] = new Color(0f, 0f, 0f, 1f);
            }
            m_Mesh.colors = colors;

            // Initialize UV2 (channel 1) with Vector4 values (1, 1, 1, 0)
            List<Vector4> uv2 = new List<Vector4>(vertices.Count);
            for (int k = 0; k < vertices.Count; k++)
            {
                uv2.Add(new Vector4(1f, 1f, 1f, 0f));
            }
            m_Mesh.SetUVs(1, uv2);

            m_Mesh.SetTriangles(tris, 0);
            m_Mesh.RecalculateBounds();
            m_Mesh.RecalculateTangents();

            if (m_UpdateMeshCollider && TryGetComponent<MeshCollider>(out var collider))
                collider.sharedMesh = m_Mesh;
        }
    }
}