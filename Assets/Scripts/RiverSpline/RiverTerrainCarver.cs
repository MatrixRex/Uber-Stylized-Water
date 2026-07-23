using System.Collections.Generic;
using System.Threading.Tasks;
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
        CarveDownOnly,
        ForceHeight
    }

    /// <summary>
    /// Fast, multithreaded terrain carver for RiverExtrude splines in Unity 6.
    /// Operates as a dynamic non-destructive layer on top of Unity Terrains with
    /// one-click "Bake Into Terrain" for standard sculpting brush editing.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
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
        [Tooltip("Carve mode. CarveDownOnly is recommended to avoid terrain clipping above river surface.")]
        [SerializeField] private CarveMode m_CarveMode = CarveMode.CarveDownOnly;

        [Tooltip("Distance between spline sample points for terrain distance calculations (meters). Lower = higher accuracy.")]
        [SerializeField, Min(0.1f)] private float m_SampleSpacing = 1f;

        [Header("Smoothing")]
        [Tooltip("Number of post-carve smoothing passes to soften bank transitions and remove heightmap grid stepping [0 = disabled].")]
        [SerializeField, Range(0, 5)] private int m_SmoothPasses = 1;

        [Tooltip("Strength of the post-carve smoothing filter [0 = no effect, 1 = maximum smooth].")]
        [SerializeField, Range(0f, 1f)] private float m_SmoothStrength = 0.5f;

        [Header("Texture Painting")]
        [Tooltip("Enable dynamic texture painting along the riverbed and banks.")]
        [SerializeField] private bool m_EnableTexturePainting = true;

        [Tooltip("Target TerrainLayer to paint. If assigned, auto-detects or auto-adds to target terrains.")]
        [SerializeField] private TerrainLayer m_TargetTerrainLayer;

        [Tooltip("Fallback target terrain layer index if TerrainLayer asset is not assigned [0 = first layer].")]
        [SerializeField, Min(0)] private int m_TargetLayerIndex = 0;

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

        [Tooltip("Persistent snapshot ScriptableObject asset to store baseline terrain heightmaps and alphamaps across Unity restarts.")]
        [SerializeField] private RiverTerrainSnapshotData m_SnapshotAsset;

        // Internal snapshot structure for non-destructive dynamic layer
        private class TerrainSnapshot
        {
            public Terrain Terrain;
            public int Resolution;
            public float[,] FullOriginalHeights;
            public bool HasLastBounds;
            public int LastX0, LastX1, LastZ0, LastZ1;

            // Alphamap / splatmap snapshot
            public int AlphamapWidth;
            public int AlphamapHeight;
            public int AlphamapLayers;
            public float[,,] FullOriginalAlphamaps;
            public bool HasLastAlphaBounds;
            public int LastAlphaX0, LastAlphaX1, LastAlphaZ0, LastAlphaZ1;

            public bool IsModified;
        }

        private readonly Dictionary<Terrain, TerrainSnapshot> m_Snapshots = new Dictionary<Terrain, TerrainSnapshot>();
        private bool m_IsDirty = true;
        private bool m_IsCarving = false;
        private bool m_IsActivelyEditing = false;

        public RiverExtrude RiverExtrude => m_RiverExtrude;
        public SplineContainer Container => m_Container;
        public RiverTerrainSnapshotData SnapshotAsset
        {
            get => m_SnapshotAsset;
            set { m_SnapshotAsset = value; RequestCarve(); }
        }

#if UNITY_EDITOR
        public RiverTerrainSnapshotData GetOrCreateSnapshotAsset()
        {
            if (m_SnapshotAsset != null) return m_SnapshotAsset;

            string dir = "Assets/RiverSnapshots";
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                UnityEditor.AssetDatabase.Refresh();
            }
            string path = $"{dir}/RiverSnapshot_{gameObject.name}.asset";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);

            m_SnapshotAsset = ScriptableObject.CreateInstance<RiverTerrainSnapshotData>();
            UnityEditor.AssetDatabase.CreateAsset(m_SnapshotAsset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.EditorUtility.SetDirty(this);
            return m_SnapshotAsset;
        }

        private void SaveSnapshotToAsset(Terrain terrain, TerrainSnapshot snapshot)
        {
            if (snapshot == null || terrain == null || snapshot.FullOriginalHeights == null) return;
            RiverTerrainSnapshotData asset = GetOrCreateSnapshotAsset();
            if (asset != null)
            {
                asset.SetSnapshot(terrain, snapshot.FullOriginalHeights, snapshot.FullOriginalAlphamaps);
                UnityEditor.EditorUtility.SetDirty(asset);
            }
        }

        public void SaveAllSnapshotsToAsset()
        {
            if (m_Snapshots == null || m_Snapshots.Count == 0) return;
            RiverTerrainSnapshotData asset = GetOrCreateSnapshotAsset();
            if (asset != null)
            {
                foreach (var kvp in m_Snapshots)
                {
                    if (kvp.Key != null && kvp.Value != null && kvp.Value.FullOriginalHeights != null)
                    {
                        asset.SetSnapshot(kvp.Key, kvp.Value.FullOriginalHeights, kvp.Value.FullOriginalAlphamaps);
                    }
                }
                UnityEditor.EditorUtility.SetDirty(asset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
        }
#endif

        public void CaptureFreshBaseline()
        {
            EnsureReferences();
            List<Terrain> targets = GetTargetTerrains();
            foreach (var terrain in targets)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                TerrainData tData = terrain.terrainData;
                int hRes = tData.heightmapResolution;
                int aW = tData.alphamapWidth;
                int aH = tData.alphamapHeight;
                int aL = tData.alphamapLayers;

                var snap = new TerrainSnapshot
                {
                    Terrain = terrain,
                    Resolution = hRes,
                    FullOriginalHeights = tData.GetHeights(0, 0, hRes, hRes),
                    HasLastBounds = false,
                    AlphamapWidth = aW,
                    AlphamapHeight = aH,
                    AlphamapLayers = aL,
                    FullOriginalAlphamaps = tData.GetAlphamaps(0, 0, aW, aH),
                    HasLastAlphaBounds = false
                };
                m_Snapshots[terrain] = snap;

#if UNITY_EDITOR
                SaveSnapshotToAsset(terrain, snap);
#endif
            }

#if UNITY_EDITOR
            if (m_SnapshotAsset != null)
            {
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif

            RequestCarve();
        }
        public bool EnableDynamicCarve
        {
            get => m_EnableDynamicCarve;
            set
            {
                if (m_EnableDynamicCarve != value)
                {
                    m_EnableDynamicCarve = value;
                    if (!m_EnableDynamicCarve)
                        RestoreAllSnapshots();
                    else
                        RequestCarve();
                }
            }
        }

        public bool AutoRebuildOnSplineChange
        {
            get => m_AutoRebuildOnSplineChange;
            set => m_AutoRebuildOnSplineChange = value;
        }

        public bool EnableFastMode
        {
            get => m_EnableFastMode;
            set => m_EnableFastMode = value;
        }

        public bool IsActivelyEditing
        {
            get => m_IsActivelyEditing;
            set
            {
                if (m_IsActivelyEditing != value)
                {
                    m_IsActivelyEditing = value;
                    if (!m_IsActivelyEditing)
                    {
                        // Editing completed: request high-quality final pass
                        RequestCarve();
                    }
                }
            }
        }

        public BedProfileMode BedProfile { get => m_BedProfile; set { m_BedProfile = value; RequestCarve(); } }
        public float BedDepth { get => m_BedDepth; set { m_BedDepth = Mathf.Max(0f, value); RequestCarve(); } }
        public float BedWidthRatio { get => m_BedWidthRatio; set { m_BedWidthRatio = Mathf.Clamp(value, 0.1f, 1.0f); RequestCarve(); } }
        public float BankFalloff { get => m_BankFalloff; set { m_BankFalloff = Mathf.Max(0.1f, value); RequestCarve(); } }
        public float BankEdgeOffset { get => m_BankEdgeOffset; set { m_BankEdgeOffset = value; RequestCarve(); } }
        public CarveMode Mode { get => m_CarveMode; set { m_CarveMode = value; RequestCarve(); } }
        public int SmoothPasses { get => m_SmoothPasses; set { m_SmoothPasses = Mathf.Clamp(value, 0, 5); RequestCarve(); } }
        public float SmoothStrength { get => m_SmoothStrength; set { m_SmoothStrength = Mathf.Clamp01(value); RequestCarve(); } }

        public bool EnableTexturePainting { get => m_EnableTexturePainting; set { m_EnableTexturePainting = value; RequestCarve(); } }
        public TerrainLayer TargetTerrainLayer { get => m_TargetTerrainLayer; set { m_TargetTerrainLayer = value; RequestCarve(); } }
        public int TargetLayerIndex { get => m_TargetLayerIndex; set { m_TargetLayerIndex = Mathf.Max(0, value); RequestCarve(); } }
        public float TextureOpacity { get => m_TextureOpacity; set { m_TextureOpacity = Mathf.Clamp01(value); RequestCarve(); } }
        public float TextureWidthRatio { get => m_TextureWidthRatio; set { m_TextureWidthRatio = Mathf.Clamp(value, 0.1f, 1.0f); RequestCarve(); } }
        public float TextureBankFalloff { get => m_TextureBankFalloff; set { m_TextureBankFalloff = Mathf.Max(0.1f, value); RequestCarve(); } }
        public AnimationCurve TextureBlendCurve => m_TextureBlendCurve;

        private void OnEnable()
        {
            EnsureReferences();
            Spline.Changed += OnSplineChanged;
            m_IsDirty = true;
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
            RestoreAllSnapshots();
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
            m_TargetLayerIndex = Mathf.Max(0, m_TargetLayerIndex);
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
                RequestCarve();
        }

        private void Update()
        {
            if (transform.hasChanged)
            {
                transform.hasChanged = false;
                if (m_EnableDynamicCarve && m_AutoRebuildOnSplineChange)
                {
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
            return FindOverlappingTerrains();
        }

        public List<Terrain> FindOverlappingTerrains()
        {
            var result = new List<Terrain>();
            EnsureReferences();

            if (m_Container == null || m_Container.Spline == null || m_Container.Spline.Count < 2)
                return result;

            Bounds splineBounds = CalculateSplineWorldBounds(m_BankFalloff + GetMaxBaseWidth());

            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 terrainPos = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                Bounds terrainBounds = new Bounds(terrainPos + terrainSize * 0.5f, terrainSize);

                if (splineBounds.Intersects(terrainBounds))
                {
                    result.Add(terrain);
                }
            }
            return result;
        }

        private float GetMaxBaseWidth()
        {
            if (m_RiverExtrude != null)
                return m_RiverExtrude.BaseWidth * 2f;
            return 8f;
        }

        private Bounds CalculateSplineWorldBounds(float padding)
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            if (m_Container == null || m_Container.Spline == null) return b;

            float length = m_Container.CalculateLength();
            int samples = Mathf.Max(10, Mathf.CeilToInt(length / 2f));

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                m_Container.Evaluate(t, out float3 worldP, out float3 tangent, out float3 up);
                if (i == 0) b = new Bounds(worldP, Vector3.zero);
                else b.Encapsulate(worldP);
            }

            b.Expand(padding * 2f);
            return b;
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

                foreach (var terrain in targets)
                {
                    CarveSingleTerrain(terrain, samples, maxInfluence);
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

        private int FindOrAddTerrainLayer(TerrainData tData, TerrainLayer targetLayer, int fallbackIndex)
        {
            if (tData == null) return fallbackIndex;

            TerrainLayer[] layers = tData.terrainLayers;
            if (layers == null || layers.Length == 0)
            {
                if (targetLayer != null)
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
                return 0;
            }

            if (targetLayer != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] == targetLayer)
                        return i;
                }

                // Layer not present on terrain: auto-add it!
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

            return Mathf.Clamp(fallbackIndex, 0, layers.Length - 1);
        }

        private void CarveSingleTerrain(Terrain terrain, List<SplinePointSample> samples, float maxInfluenceRadius)
        {
            TerrainData tData = terrain.terrainData;
            if (tData == null) return;

            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = tData.size;
            int hRes = tData.heightmapResolution;

            int aWidth = tData.alphamapWidth;
            int aHeight = tData.alphamapHeight;
            int aLayers = tData.alphamapLayers;

            // Manage base height snapshot for non-destructive dynamic editing (full heightmap + alphamap)
            if (!m_Snapshots.TryGetValue(terrain, out var snapshot) || snapshot.FullOriginalHeights == null || snapshot.Resolution != hRes ||
                snapshot.FullOriginalAlphamaps == null || snapshot.AlphamapWidth != aWidth || snapshot.AlphamapHeight != aHeight || snapshot.AlphamapLayers != aLayers)
            {
                bool loadedFromAsset = false;
                if (m_SnapshotAsset != null && m_SnapshotAsset.TryGetSnapshot(terrain, out float[,] loadedH, out float[,,] loadedA, out int lHRes, out int lAW, out int lAH, out int lAL))
                {
                    if (lHRes == hRes && lAW == aWidth && lAH == aHeight && lAL == aLayers)
                    {
                        snapshot = new TerrainSnapshot
                        {
                            Terrain = terrain,
                            Resolution = hRes,
                            FullOriginalHeights = loadedH,
                            HasLastBounds = false,
                            AlphamapWidth = aWidth,
                            AlphamapHeight = aHeight,
                            AlphamapLayers = aLayers,
                            FullOriginalAlphamaps = loadedA,
                            HasLastAlphaBounds = false
                        };
                        m_Snapshots[terrain] = snapshot;
                        loadedFromAsset = true;
                    }
                }

                if (!loadedFromAsset)
                {
                    snapshot = new TerrainSnapshot
                    {
                        Terrain = terrain,
                        Resolution = hRes,
                        FullOriginalHeights = tData.GetHeights(0, 0, hRes, hRes),
                        HasLastBounds = false,
                        AlphamapWidth = aWidth,
                        AlphamapHeight = aHeight,
                        AlphamapLayers = aLayers,
                        FullOriginalAlphamaps = tData.GetAlphamaps(0, 0, aWidth, aHeight),
                        HasLastAlphaBounds = false
                    };
                    m_Snapshots[terrain] = snapshot;
#if UNITY_EDITOR
                    SaveSnapshotToAsset(terrain, snapshot);
#endif
                }
            }

            Bounds splineBounds = CalculateSplineWorldBounds(maxInfluenceRadius);
            Bounds terrainBounds = new Bounds(tPos + tSize * 0.5f, tSize);

            int currX0 = 0, currX1 = 0, currZ0 = 0, currZ1 = 0;
            bool currentIntersects = splineBounds.Intersects(terrainBounds);

            if (currentIntersects)
            {
                Vector3 minLocal = splineBounds.min - tPos;
                Vector3 maxLocal = splineBounds.max - tPos;

                currX0 = Mathf.Clamp(Mathf.FloorToInt((minLocal.x / tSize.x) * (hRes - 1)), 0, hRes - 1);
                currX1 = Mathf.Clamp(Mathf.CeilToInt((maxLocal.x / tSize.x) * (hRes - 1)), 0, hRes - 1);
                currZ0 = Mathf.Clamp(Mathf.FloorToInt((minLocal.z / tSize.z) * (hRes - 1)), 0, hRes - 1);
                currZ1 = Mathf.Clamp(Mathf.CeilToInt((maxLocal.z / tSize.z) * (hRes - 1)), 0, hRes - 1);
            }

            if (currentIntersects || snapshot.HasLastBounds)
            {
                int unionX0 = currX0;
                int unionX1 = currX1;
                int unionZ0 = currZ0;
                int unionZ1 = currZ1;

                if (snapshot.HasLastBounds)
                {
                    if (!currentIntersects)
                    {
                        unionX0 = snapshot.LastX0;
                        unionX1 = snapshot.LastX1;
                        unionZ0 = snapshot.LastZ0;
                        unionZ1 = snapshot.LastZ1;
                    }
                    else
                    {
                        unionX0 = Mathf.Min(currX0, snapshot.LastX0);
                        unionX1 = Mathf.Max(currX1, snapshot.LastX1);
                        unionZ0 = Mathf.Min(currZ0, snapshot.LastZ0);
                        unionZ1 = Mathf.Max(currZ1, snapshot.LastZ1);
                    }
                }

                int width = unionX1 - unionX0 + 1;
                int height = unionZ1 - unionZ0 + 1;

                if (width > 0 && height > 0)
                {
                    float[,] fullOrig = snapshot.FullOriginalHeights;
                    float[,] newHeights = new float[height, width];

                    float bankFalloff = m_BankFalloff;
                    float bedDepth = m_BedDepth;
                    float bedWidthRatio = m_BedWidthRatio;
                    float bankEdgeOffset = m_BankEdgeOffset;
                    CarveMode mode = m_CarveMode;
                    BedProfileMode profile = m_BedProfile;

                    // Multithreaded height calculation over rows
                    Parallel.For(0, height, r =>
                    {
                        int gz = unionZ0 + r;
                        float worldZ = tPos.z + (gz / (float)(hRes - 1)) * tSize.z;

                        for (int c = 0; c < width; c++)
                        {
                            int gx = unionX0 + c;
                            float worldX = tPos.x + (gx / (float)(hRes - 1)) * tSize.x;

                            float origNorm = fullOrig[gz, gx];

                            if (!currentIntersects)
                            {
                                newHeights[r, c] = origNorm;
                                continue;
                            }

                            Vector3 cellWorld = new Vector3(worldX, 0f, worldZ);
                            float origWorldY = tPos.y + (origNorm * tSize.y);

                            FindClosestSplinePoint(cellWorld, samples, out Vector3 closestSplinePos, out float distToCenterline, out float localHalfWidth);

                            float bedHalfWidth = localHalfWidth * bedWidthRatio;
                            float totalInfluence = bedHalfWidth + bankFalloff;

                            if (distToCenterline > totalInfluence)
                            {
                                newHeights[r, c] = origNorm;
                                continue;
                            }

                            float splineWorldY = closestSplinePos.y;
                            float finalWorldY;

                            if (distToCenterline <= bedHalfWidth)
                            {
                                float u = distToCenterline / bedHalfWidth;
                                float depthMult = EvaluateBedProfile(profile, u);
                                float targetBedY = splineWorldY + (bankEdgeOffset * u) - (bedDepth * depthMult);

                                if (mode == CarveMode.CarveDownOnly)
                                {
                                    float upperLimitY = (bankEdgeOffset > 0f) ? Mathf.Max(origWorldY, splineWorldY + bankEdgeOffset * u) : origWorldY;
                                    finalWorldY = Mathf.Min(upperLimitY, targetBedY);
                                }
                                else
                                {
                                    finalWorldY = targetBedY;
                                }
                            }
                            else
                            {
                                float bankFrac = (distToCenterline - bedHalfWidth) / bankFalloff;
                                float smoothBank = SmoothStep(0f, 1f, bankFrac);

                                float edgeDepthMult = EvaluateBedProfile(profile, 1f);
                                float bedEdgeY = splineWorldY + bankEdgeOffset - (bedDepth * edgeDepthMult);

                                float blendBankY = Mathf.Lerp(bedEdgeY, origWorldY, smoothBank);

                                if (mode == CarveMode.CarveDownOnly)
                                {
                                    float targetLipY = Mathf.Lerp(splineWorldY + bankEdgeOffset, origWorldY, smoothBank);
                                    float upperLimitY = (bankEdgeOffset > 0f) ? Mathf.Max(origWorldY, targetLipY) : origWorldY;
                                    finalWorldY = Mathf.Min(upperLimitY, blendBankY);
                                }
                                else
                                {
                                    finalWorldY = blendBankY;
                                }
                            }

                            newHeights[r, c] = Mathf.Clamp01((finalWorldY - tPos.y) / tSize.y);
                        }
                    });

                    int effectiveSmoothPasses = (m_EnableFastMode && m_IsActivelyEditing) ? 0 : m_SmoothPasses;

                    if (effectiveSmoothPasses > 0 && m_SmoothStrength > 0f && height > 2 && width > 2)
                    {
                        float[,] tempHeights = new float[height, width];
                        float strength = m_SmoothStrength;

                        for (int pass = 0; pass < effectiveSmoothPasses; pass++)
                        {
                            System.Array.Copy(newHeights, tempHeights, newHeights.Length);

                            Parallel.For(1, height - 1, r =>
                            {
                                for (int c = 1; c < width - 1; c++)
                                {
                                    float center = tempHeights[r, c];
                                    float sum = tempHeights[r - 1, c - 1] * 1f + tempHeights[r - 1, c] * 2f + tempHeights[r - 1, c + 1] * 1f +
                                                tempHeights[r,     c - 1] * 2f + tempHeights[r,     c] * 4f + tempHeights[r,     c + 1] * 2f +
                                                tempHeights[r + 1, c - 1] * 1f + tempHeights[r + 1, c] * 2f + tempHeights[r + 1, c + 1] * 1f;

                                    float smoothed = sum / 16f;
                                    newHeights[r, c] = Mathf.Lerp(center, smoothed, strength);
                                }
                            });
                        }
                    }

                    tData.SetHeightsDelayLOD(unionX0, unionZ0, newHeights);
                    tData.SyncHeightmap();

                    if (currentIntersects)
                    {
                        snapshot.HasLastBounds = true;
                        snapshot.LastX0 = currX0;
                        snapshot.LastX1 = currX1;
                        snapshot.LastZ0 = currZ0;
                        snapshot.LastZ1 = currZ1;
                    }
                    else
                    {
                        snapshot.HasLastBounds = false;
                    }
                }
            }

            // Texture painting pass
            if (m_EnableTexturePainting)
            {
                PaintSingleTerrainAlphamaps(terrain, snapshot, samples, splineBounds, currentIntersects);
            }
            else if (snapshot.HasLastAlphaBounds && snapshot.FullOriginalAlphamaps != null)
            {
                // Texture painting was toggled OFF: revert alphamap snapshot
                tData.SetAlphamaps(0, 0, snapshot.FullOriginalAlphamaps);
                snapshot.HasLastAlphaBounds = false;
            }
        }

        private void PaintSingleTerrainAlphamaps(Terrain terrain, TerrainSnapshot snapshot, List<SplinePointSample> samples, Bounds splineBounds, bool currentIntersects)
        {
            TerrainData tData = terrain.terrainData;
            if (tData == null) return;

            int targetLayerIdx = FindOrAddTerrainLayer(tData, m_TargetTerrainLayer, m_TargetLayerIndex);

            int aWidth = tData.alphamapWidth;
            int aHeight = tData.alphamapHeight;
            int aLayers = tData.alphamapLayers;

            if (snapshot.FullOriginalAlphamaps == null || snapshot.AlphamapWidth != aWidth || snapshot.AlphamapHeight != aHeight || snapshot.AlphamapLayers != aLayers)
            {
                snapshot.AlphamapWidth = aWidth;
                snapshot.AlphamapHeight = aHeight;
                snapshot.AlphamapLayers = aLayers;
                snapshot.FullOriginalAlphamaps = tData.GetAlphamaps(0, 0, aWidth, aHeight);
                snapshot.HasLastAlphaBounds = false;
            }

            if (targetLayerIdx < 0 || targetLayerIdx >= snapshot.AlphamapLayers) return;

            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = tData.size;

            int currAX0 = 0, currAX1 = 0, currAZ0 = 0, currAZ1 = 0;

            if (currentIntersects)
            {
                Vector3 minLocal = splineBounds.min - tPos;
                Vector3 maxLocal = splineBounds.max - tPos;

                currAX0 = Mathf.Clamp(Mathf.FloorToInt((minLocal.x / tSize.x) * (aWidth - 1)), 0, aWidth - 1);
                currAX1 = Mathf.Clamp(Mathf.CeilToInt((maxLocal.x / tSize.x) * (aWidth - 1)), 0, aWidth - 1);
                currAZ0 = Mathf.Clamp(Mathf.FloorToInt((minLocal.z / tSize.z) * (aHeight - 1)), 0, aHeight - 1);
                currAZ1 = Mathf.Clamp(Mathf.CeilToInt((maxLocal.z / tSize.z) * (aHeight - 1)), 0, aHeight - 1);
            }

            if (!currentIntersects && !snapshot.HasLastAlphaBounds)
                return;

            int unionAX0 = currAX0;
            int unionAX1 = currAX1;
            int unionAZ0 = currAZ0;
            int unionAZ1 = currAZ1;

            if (snapshot.HasLastAlphaBounds)
            {
                if (!currentIntersects)
                {
                    unionAX0 = snapshot.LastAlphaX0;
                    unionAX1 = snapshot.LastAlphaX1;
                    unionAZ0 = snapshot.LastAlphaZ0;
                    unionAZ1 = snapshot.LastAlphaZ1;
                }
                else
                {
                    unionAX0 = Mathf.Min(currAX0, snapshot.LastAlphaX0);
                    unionAX1 = Mathf.Max(currAX1, snapshot.LastAlphaX1);
                    unionAZ0 = Mathf.Min(currAZ0, snapshot.LastAlphaZ0);
                    unionAZ1 = Mathf.Max(currAZ1, snapshot.LastAlphaZ1);
                }
            }

            int regionWidth = unionAX1 - unionAX0 + 1;
            int regionHeight = unionAZ1 - unionAZ0 + 1;

            if (regionWidth <= 0 || regionHeight <= 0) return;

            float[,,] origAlpha = snapshot.FullOriginalAlphamaps;
            int numLayers = snapshot.AlphamapLayers;
            float[,,] newAlphamaps = new float[regionHeight, regionWidth, numLayers];

            float texWidthRatio = m_TextureWidthRatio;
            float texBankFalloff = m_TextureBankFalloff;
            float texOpacity = m_TextureOpacity;

            Parallel.For(0, regionHeight, r =>
            {
                int gz = unionAZ0 + r;
                float worldZ = tPos.z + (gz / (float)(aHeight - 1)) * tSize.z;

                for (int c = 0; c < regionWidth; c++)
                {
                    int gx = unionAX0 + c;
                    float worldX = tPos.x + (gx / (float)(aWidth - 1)) * tSize.x;

                    if (!currentIntersects)
                    {
                        for (int k = 0; k < numLayers; k++)
                        {
                            newAlphamaps[r, c, k] = origAlpha[gz, gx, k];
                        }
                        continue;
                    }

                    Vector3 cellWorld = new Vector3(worldX, 0f, worldZ);
                    FindClosestSplinePoint(cellWorld, samples, out Vector3 closestSplinePos, out float distToCenterline, out float localHalfWidth);

                    float texBedHalfWidth = localHalfWidth * texWidthRatio;
                    float totalTexInfluence = texBedHalfWidth + texBankFalloff;

                    if (distToCenterline > totalTexInfluence)
                    {
                        for (int k = 0; k < numLayers; k++)
                        {
                            newAlphamaps[r, c, k] = origAlpha[gz, gx, k];
                        }
                        continue;
                    }

                    float blendRatio = 0f;
                    if (distToCenterline <= texBedHalfWidth)
                    {
                        blendRatio = texOpacity;
                    }
                    else
                    {
                        float bankFrac = (distToCenterline - texBedHalfWidth) / texBankFalloff;
                        float curveVal = Mathf.Clamp01(m_TextureBlendCurve.Evaluate(bankFrac));
                        blendRatio = texOpacity * curveVal;
                    }

                    if (blendRatio <= 0.0001f)
                    {
                        for (int k = 0; k < numLayers; k++)
                        {
                            newAlphamaps[r, c, k] = origAlpha[gz, gx, k];
                        }
                        continue;
                    }

                    for (int k = 0; k < numLayers; k++)
                    {
                        float origW = origAlpha[gz, gx, k];
                        if (k == targetLayerIdx)
                        {
                            newAlphamaps[r, c, k] = origW + (1f - origW) * blendRatio;
                        }
                        else
                        {
                            newAlphamaps[r, c, k] = origW * (1f - blendRatio);
                        }
                    }
                }
            });

            tData.SetAlphamaps(unionAX0, unionAZ0, newAlphamaps);

            if (currentIntersects)
            {
                snapshot.HasLastAlphaBounds = true;
                snapshot.LastAlphaX0 = currAX0;
                snapshot.LastAlphaX1 = currAX1;
                snapshot.LastAlphaZ0 = currAZ0;
                snapshot.LastAlphaZ1 = currAZ1;
            }
            else
            {
                snapshot.HasLastAlphaBounds = false;
            }
        }

        private static void FindClosestSplinePoint(Vector3 cellWorld, List<SplinePointSample> samples, out Vector3 closestPos, out float minDist, out float halfWidth)
        {
            closestPos = Vector3.zero;
            minDist = float.MaxValue;
            halfWidth = 2f;

            if (samples == null || samples.Count < 2) return;

            Vector2 cell2D = new Vector2(cellWorld.x, cellWorld.z);

            for (int i = 0; i < samples.Count - 1; i++)
            {
                var s0 = samples[i];
                var s1 = samples[i + 1];

                Vector2 p0 = new Vector2(s0.Position.x, s0.Position.z);
                Vector2 p1 = new Vector2(s1.Position.x, s1.Position.z);

                Vector2 v = p1 - p0;
                float sqrLen = v.sqrMagnitude;

                float t = 0f;
                if (sqrLen > 0.0001f)
                {
                    t = Mathf.Clamp01(Vector2.Dot(cell2D - p0, v) / sqrLen);
                }

                Vector2 proj2D = p0 + v * t;
                float dist = Vector2.Distance(cell2D, proj2D);

                if (dist < minDist)
                {
                    minDist = dist;
                    closestPos = Vector3.Lerp(s0.Position, s1.Position, t);
                    halfWidth = Mathf.Lerp(s0.HalfWidth, s1.HalfWidth, t);
                }
            }
        }

        private float EvaluateBedProfile(BedProfileMode mode, float u)
        {
            u = Mathf.Clamp01(u);
            switch (mode)
            {
                case BedProfileMode.UShapeCurved:
                    return 0.5f * (1f + Mathf.Cos(u * Mathf.PI));
                case BedProfileMode.Flat:
                    return 1f;
                case BedProfileMode.VShape:
                    return 1f - u;
                case BedProfileMode.CustomCurve:
                    return Mathf.Clamp01(m_CustomBedCurve.Evaluate(u));
                default:
                    return 1f - u * u;
            }
        }

        private static float SmoothStep(float from, float to, float t)
        {
            t = Mathf.Clamp01((t - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        public void RestoreAllSnapshots()
        {
            List<Terrain> targets = GetTargetTerrains();
            foreach (var terrain in targets)
            {
                if (terrain != null && terrain.terrainData != null)
                {
                    float[,] heightsToRestore = null;
                    float[,,] alphaToRestore = null;

                    if (m_Snapshots.TryGetValue(terrain, out var snap) && snap.FullOriginalHeights != null)
                    {
                        heightsToRestore = snap.FullOriginalHeights;
                        alphaToRestore = snap.FullOriginalAlphamaps;
                    }
                    else if (m_SnapshotAsset != null && m_SnapshotAsset.TryGetSnapshot(terrain, out float[,] loadedH, out float[,,] loadedA, out _, out _, out _, out _))
                    {
                        heightsToRestore = loadedH;
                        alphaToRestore = loadedA;
                    }

                    if (heightsToRestore != null)
                    {
                        terrain.terrainData.SetHeightsDelayLOD(0, 0, heightsToRestore);
                        terrain.terrainData.SyncHeightmap();
                    }
                    if (alphaToRestore != null)
                    {
                        terrain.terrainData.SetAlphamaps(0, 0, alphaToRestore);
                    }
                }
            }
            m_Snapshots.Clear();
        }

        /// <summary>
        /// Permanently bakes the dynamic carve layer into the terrain heightmaps and alphamaps.
        /// After baking, standard Unity Terrain sculpting and painting brushes can edit the riverbed directly.
        /// </summary>
        public void BakeIntoTerrain()
        {
            CarveDynamicInternal();

#if UNITY_EDITOR
            foreach (var kvp in m_Snapshots)
            {
                var terrain = kvp.Key;
                if (terrain != null && terrain.terrainData != null)
                {
                    Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Bake River Spline Terrain Carving & Texturing");
                    EditorUtility.SetDirty(terrain.terrainData);
                }
            }

            if (m_SnapshotAsset != null)
            {
                m_SnapshotAsset.Clear();
                EditorUtility.SetDirty(m_SnapshotAsset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif

            // Clear snapshots so the baked state becomes the new base terrain state
            m_Snapshots.Clear();
            m_EnableDynamicCarve = false;
        }
    }
}
