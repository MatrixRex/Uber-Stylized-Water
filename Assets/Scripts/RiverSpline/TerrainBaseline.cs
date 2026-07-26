using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RiverTools
{
    public static class DeflateCompressor
    {
        public static byte[] CompressFloatArray(float[] data)
        {
            if (data == null || data.Length == 0) return null;
            byte[] raw = new byte[data.Length * sizeof(float)];
            System.Buffer.BlockCopy(data, 0, raw, 0, raw.Length);

            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest))
                {
                    deflate.Write(raw, 0, raw.Length);
                }
                return ms.ToArray();
            }
        }

        public static float[] DecompressFloatArray(byte[] compressed, int targetFloatCount)
        {
            if (compressed == null || compressed.Length == 0 || targetFloatCount <= 0) return null;
            byte[] raw = new byte[targetFloatCount * sizeof(float)];

            using (var ms = new MemoryStream(compressed))
            {
                using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    int read = 0;
                    while (read < raw.Length)
                    {
                        int n = deflate.Read(raw, read, raw.Length - read);
                        if (n == 0) break;
                        read += n;
                    }
                }
            }

            float[] floats = new float[targetFloatCount];
            System.Buffer.BlockCopy(raw, 0, floats, 0, raw.Length);
            return floats;
        }
    }

    /// <summary>
    /// Lightweight component attached to Terrain GameObjects that maintains the pure user ground baseline 
    /// (heights & alphamaps) and acts as the SINGLE WRITER to TerrainData for all procedural river carvers.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Terrain))]
    [AddComponentMenu("Splines/Terrain Baseline")]
    public class TerrainBaseline : MonoBehaviour
    {
        [System.Serializable]
        public class RiverCarveModifier
        {
            public string SourceID;
            public List<RiverTerrainCarver.SplinePointSample> Samples;
            public float BedDepth;
            public float BedWidthRatio;
            public float BankFalloff;
            public float BankEdgeOffset;
            public CarveMode Mode;
            public BedProfileMode ProfileMode;
            public AnimationCurve CustomCurve;

            // Texture painting
            public bool EnableTexturePainting;
            public int TargetLayerIndex;
            public float TextureOpacity;
            public float TextureWidthRatio;
            public float TextureBankFalloff;
            public AnimationCurve TextureBlendCurve;
        }

        [HideInInspector] [SerializeField] private byte[] m_CompressedUserHeights;
        [HideInInspector] [SerializeField] private int m_HeightResolution;
        [HideInInspector] [SerializeField] private byte[] m_CompressedUserAlphamaps;
        [HideInInspector] [SerializeField] private int m_AlphaWidth;
        [HideInInspector] [SerializeField] private int m_AlphaHeight;
        [HideInInspector] [SerializeField] private int m_AlphaLayers;

        private Terrain m_Terrain;
        private float[,] m_UserHeights;
        private float[,,] m_UserAlphamaps;
        private bool m_IsLoaded = false;
        private bool m_IsApplyingComposite = false;

        private readonly Dictionary<string, RiverCarveModifier> m_Modifiers = new Dictionary<string, RiverCarveModifier>();

        public Terrain Terrain
        {
            get
            {
                if (m_Terrain == null) m_Terrain = GetComponent<Terrain>();
                return m_Terrain;
            }
        }

        public static TerrainBaseline GetOrCreate(Terrain terrain)
        {
            if (terrain == null) return null;
            var baseline = terrain.GetComponent<TerrainBaseline>();
            if (baseline == null)
            {
                baseline = terrain.gameObject.AddComponent<TerrainBaseline>();
            }
            return baseline;
        }

        private void OnEnable()
        {
            EnsureLoaded();
#if UNITY_EDITOR
            TerrainCallbacks.heightmapChanged += OnTerrainHeightmapChanged;
            TerrainCallbacks.textureChanged += OnTerrainTextureChanged;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            TerrainCallbacks.heightmapChanged -= OnTerrainHeightmapChanged;
            TerrainCallbacks.textureChanged -= OnTerrainTextureChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnTerrainHeightmapChanged(Terrain terrain, RectInt region, bool synched)
        {
            if (terrain != Terrain || m_IsApplyingComposite) return;
            UpdateUserHeightsFromTerrain(region);
        }

        private void OnTerrainTextureChanged(Terrain terrain, string textureName, RectInt region, bool synched)
        {
            if (terrain != Terrain || m_IsApplyingComposite) return;
            UpdateUserAlphamapsFromTerrain(region);
        }
#endif

        public void EnsureLoaded()
        {
            if (m_IsLoaded && m_UserHeights != null) return;
            Terrain t = Terrain;
            if (t == null || t.terrainData == null) return;

            TerrainData tData = t.terrainData;
            int hRes = tData.heightmapResolution;
            int aW = tData.alphamapWidth;
            int aH = tData.alphamapHeight;
            int aL = tData.alphamapLayers;

            bool heightsDecompressed = false;
            if (m_CompressedUserHeights != null && m_CompressedUserHeights.Length > 0 && m_HeightResolution == hRes)
            {
                float[] flatH = DeflateCompressor.DecompressFloatArray(m_CompressedUserHeights, hRes * hRes);
                if (flatH != null && flatH.Length == hRes * hRes)
                {
                    m_UserHeights = new float[hRes, hRes];
                    int idx = 0;
                    for (int z = 0; z < hRes; z++)
                    {
                        for (int x = 0; x < hRes; x++)
                        {
                            m_UserHeights[z, x] = flatH[idx++];
                        }
                    }
                    heightsDecompressed = true;
                }
            }

            if (!heightsDecompressed)
            {
                m_UserHeights = tData.GetHeights(0, 0, hRes, hRes);
                SaveUserHeightsToScene(hRes);
            }

            bool alphaDecompressed = false;
            if (m_CompressedUserAlphamaps != null && m_CompressedUserAlphamaps.Length > 0 && m_AlphaWidth == aW && m_AlphaHeight == aH && m_AlphaLayers == aL)
            {
                float[] flatA = DeflateCompressor.DecompressFloatArray(m_CompressedUserAlphamaps, aH * aW * aL);
                if (flatA != null && flatA.Length == aH * aW * aL)
                {
                    m_UserAlphamaps = new float[aH, aW, aL];
                    int idx = 0;
                    for (int z = 0; z < aH; z++)
                    {
                        for (int x = 0; x < aW; x++)
                        {
                            for (int k = 0; k < aL; k++)
                            {
                                m_UserAlphamaps[z, x, k] = flatA[idx++];
                            }
                        }
                    }
                    alphaDecompressed = true;
                }
            }

            if (!alphaDecompressed)
            {
                m_UserAlphamaps = tData.GetAlphamaps(0, 0, aW, aH);
                SaveUserAlphamapsToScene(aW, aH, aL);
            }

            m_IsLoaded = true;
        }

        private void SaveUserHeightsToScene(int hRes)
        {
            if (m_UserHeights == null) return;
            m_HeightResolution = hRes;
            float[] flat = new float[hRes * hRes];
            int idx = 0;
            for (int z = 0; z < hRes; z++)
            {
                for (int x = 0; x < hRes; x++)
                {
                    flat[idx++] = m_UserHeights[z, x];
                }
            }
            m_CompressedUserHeights = DeflateCompressor.CompressFloatArray(flat);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private void SaveUserAlphamapsToScene(int aW, int aH, int aL)
        {
            if (m_UserAlphamaps == null) return;
            m_AlphaWidth = aW;
            m_AlphaHeight = aH;
            m_AlphaLayers = aL;
            float[] flat = new float[aH * aW * aL];
            int idx = 0;
            for (int z = 0; z < aH; z++)
            {
                for (int x = 0; x < aW; x++)
                {
                    for (int k = 0; k < aL; k++)
                    {
                        flat[idx++] = m_UserAlphamaps[z, x, k];
                    }
                }
            }
            m_CompressedUserAlphamaps = DeflateCompressor.CompressFloatArray(flat);
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private void UpdateUserHeightsFromTerrain(RectInt region)
        {
            EnsureLoaded();
            Terrain t = Terrain;
            if (t == null || t.terrainData == null || m_UserHeights == null) return;
            TerrainData tData = t.terrainData;
            int hRes = tData.heightmapResolution;

            float[,] currentH = tData.GetHeights(region.x, region.y, region.width, region.height);
            Vector3 tPos = t.transform.position;
            Vector3 tSize = tData.size;

            for (int r = 0; r < region.height; r++)
            {
                int gz = region.y + r;
                float worldZ = tPos.z + (gz / (float)(hRes - 1)) * tSize.z;

                for (int c = 0; c < region.width; c++)
                {
                    int gx = region.x + c;
                    float worldX = tPos.x + (gx / (float)(hRes - 1)) * tSize.x;
                    Vector3 cellWorld = new Vector3(worldX, 0f, worldZ);

                    float hLive = currentH[r, c];
                    float riverOffset = GetTotalRiverHeightOffsetAt(cellWorld);

                    // Reconstitute base user height by subtracting river carving offset
                    m_UserHeights[gz, gx] = Mathf.Max(0f, hLive + riverOffset);
                }
            }

            SaveUserHeightsToScene(hRes);
        }

        private void UpdateUserAlphamapsFromTerrain(RectInt region)
        {
            EnsureLoaded();
            Terrain t = Terrain;
            if (t == null || t.terrainData == null || m_UserAlphamaps == null) return;
            TerrainData tData = t.terrainData;
            int aW = tData.alphamapWidth;
            int aH = tData.alphamapHeight;
            int aL = tData.alphamapLayers;

            float[,,] currentA = tData.GetAlphamaps(region.x, region.y, region.width, region.height);

            for (int r = 0; r < region.height; r++)
            {
                int gz = region.y + r;
                for (int c = 0; c < region.width; c++)
                {
                    int gx = region.x + c;
                    for (int k = 0; k < aL; k++)
                    {
                        m_UserAlphamaps[gz, gx, k] = currentA[r, c, k];
                    }
                }
            }

            SaveUserAlphamapsToScene(aW, aH, aL);
        }

        private float GetTotalRiverHeightOffsetAt(Vector3 cellWorld)
        {
            float maxCarveOffset = 0f;
            foreach (var mod in m_Modifiers.Values)
            {
                if (mod == null || mod.Samples == null || mod.Samples.Count < 2) continue;
                var segments = RiverTerrainCarver.PrepareSplineSegments(mod.Samples, mod.BankFalloff + 2f);
                RiverTerrainCarver.FindClosestSplinePoint(cellWorld, segments, out _, out float dist, out float halfW);

                float bedHalfWidth = halfW * mod.BedWidthRatio;
                float totalInfluence = bedHalfWidth + mod.BankFalloff;

                if (dist <= totalInfluence && bedHalfWidth > 0f)
                {
                    float u = dist / bedHalfWidth;
                    float depthMult = RiverTerrainCarver.EvaluateBedProfile(mod.ProfileMode, u, mod.CustomCurve);
                    float offset = mod.BedDepth * depthMult;
                    if (offset > maxCarveOffset) maxCarveOffset = offset;
                }
            }
            return maxCarveOffset / (Terrain != null && Terrain.terrainData != null ? Terrain.terrainData.size.y : 1000f);
        }

        public void RegisterModifier(string sourceID, RiverCarveModifier modifier)
        {
            if (string.IsNullOrEmpty(sourceID) || modifier == null) return;
            m_Modifiers[sourceID] = modifier;
            RebuildComposite();
        }

        public void UnregisterModifier(string sourceID)
        {
            if (string.IsNullOrEmpty(sourceID)) return;
            if (m_Modifiers.Remove(sourceID))
            {
                RebuildComposite();
            }
        }

        public void RebuildComposite()
        {
            EnsureLoaded();
            Terrain t = Terrain;
            if (t == null || t.terrainData == null || m_UserHeights == null) return;

            TerrainData tData = t.terrainData;
            Vector3 tPos = t.transform.position;
            Vector3 tSize = tData.size;
            int hRes = tData.heightmapResolution;
            int aW = tData.alphamapWidth;
            int aH = tData.alphamapHeight;
            int aL = tData.alphamapLayers;

            m_IsApplyingComposite = true;
            try
            {
                // Calculate composite heights
                float[,] finalH = new float[hRes, hRes];

                System.Threading.Tasks.Parallel.For(0, hRes, z =>
                {
                    float worldZ = tPos.z + (z / (float)(hRes - 1)) * tSize.z;
                    for (int x = 0; x < hRes; x++)
                    {
                        float worldX = tPos.x + (x / (float)(hRes - 1)) * tSize.x;
                        Vector3 cellWorld = new Vector3(worldX, 0f, worldZ);

                        float baseH = m_UserHeights[z, x];
                        float baseWorldY = tPos.y + baseH * tSize.y;
                        float finalWorldY = baseWorldY;

                        foreach (var mod in m_Modifiers.Values)
                        {
                            if (mod == null || mod.Samples == null || mod.Samples.Count < 2) continue;
                            var segments = RiverTerrainCarver.PrepareSplineSegments(mod.Samples, mod.BankFalloff + 2f);
                            RiverTerrainCarver.FindClosestSplinePoint(cellWorld, segments, out Vector3 closestSplinePos, out float dist, out float halfW);

                            float bedHalfWidth = halfW * mod.BedWidthRatio;
                            float totalInfluence = bedHalfWidth + mod.BankFalloff;

                            if (dist <= totalInfluence)
                            {
                                float splineWorldY = closestSplinePos.y;
                                if (dist <= bedHalfWidth)
                                {
                                    float u = dist / bedHalfWidth;
                                    float depthMult = RiverTerrainCarver.EvaluateBedProfile(mod.ProfileMode, u, mod.CustomCurve);
                                    float targetBedY = splineWorldY + (mod.BankEdgeOffset * u) - (mod.BedDepth * depthMult);

                                    if (mod.Mode == CarveMode.CarveDown)
                                    {
                                        float upperLimitY = (mod.BankEdgeOffset > 0f) ? Mathf.Max(baseWorldY, splineWorldY + mod.BankEdgeOffset * u) : baseWorldY;
                                        finalWorldY = Mathf.Min(finalWorldY, Mathf.Min(upperLimitY, targetBedY));
                                    }
                                    else
                                    {
                                        finalWorldY = targetBedY;
                                    }
                                }
                                else
                                {
                                    float bankFrac = (dist - bedHalfWidth) / mod.BankFalloff;
                                    float tBlend = Mathf.SmoothStep(0f, 1f, bankFrac);
                                    float bedEdgeY = splineWorldY + mod.BankEdgeOffset;
                                    float bankTargetY = Mathf.Lerp(bedEdgeY, baseWorldY, tBlend);
                                    if (mod.Mode == CarveMode.CarveDown)
                                    {
                                        finalWorldY = Mathf.Min(finalWorldY, bankTargetY);
                                    }
                                    else
                                    {
                                        finalWorldY = bankTargetY;
                                    }
                                }
                            }
                        }

                        finalH[z, x] = Mathf.Clamp01((finalWorldY - tPos.y) / tSize.y);
                    }
                });

                tData.SetHeightsDelayLOD(0, 0, finalH);
                tData.SyncHeightmap();

                // Calculate composite alphamaps
                if (m_UserAlphamaps != null)
                {
                    float[,,] finalA = new float[aH, aW, aL];

                    System.Threading.Tasks.Parallel.For(0, aH, z =>
                    {
                        float worldZ = tPos.z + (z / (float)(aH - 1)) * tSize.z;
                        for (int x = 0; x < aW; x++)
                        {
                            float worldX = tPos.x + (x / (float)(aW - 1)) * tSize.x;
                            Vector3 cellWorld = new Vector3(worldX, 0f, worldZ);

                            for (int k = 0; k < aL; k++)
                            {
                                finalA[z, x, k] = m_UserAlphamaps[z, x, k];
                            }

                            foreach (var mod in m_Modifiers.Values)
                            {
                                if (mod == null || !mod.EnableTexturePainting || mod.TargetLayerIndex < 0 || mod.TargetLayerIndex >= aL) continue;
                                if (mod.Samples == null || mod.Samples.Count < 2) continue;

                                var texSegments = RiverTerrainCarver.PrepareSplineSegments(mod.Samples, mod.TextureBankFalloff + 2f);
                                RiverTerrainCarver.FindClosestSplinePoint(cellWorld, texSegments, out _, out float dist, out float halfW);

                                float texBedHalfWidth = halfW * mod.TextureWidthRatio;
                                float totalTexInfluence = texBedHalfWidth + mod.TextureBankFalloff;

                                if (dist <= totalTexInfluence)
                                {
                                    float blendRatio = 0f;
                                    if (dist <= texBedHalfWidth)
                                    {
                                        blendRatio = mod.TextureOpacity;
                                    }
                                    else
                                    {
                                        float bankFrac = (dist - texBedHalfWidth) / mod.TextureBankFalloff;
                                        float curveVal = Mathf.Clamp01(mod.TextureBlendCurve != null ? mod.TextureBlendCurve.Evaluate(bankFrac) : (1f - bankFrac));
                                        blendRatio = mod.TextureOpacity * curveVal;
                                    }

                                    if (blendRatio > 0.0001f)
                                    {
                                        int targetLayerIdx = mod.TargetLayerIndex;
                                        for (int k = 0; k < aL; k++)
                                        {
                                            float baseW = finalA[z, x, k];
                                            if (k == targetLayerIdx)
                                            {
                                                finalA[z, x, k] = baseW + (1f - baseW) * blendRatio;
                                            }
                                            else
                                            {
                                                finalA[z, x, k] = baseW * (1f - blendRatio);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });

                    tData.SetAlphamaps(0, 0, finalA);
                }
            }
            finally
            {
                m_IsApplyingComposite = false;
            }
        }

        public void RevertToBaselineGround()
        {
            EnsureLoaded();
            Terrain t = Terrain;
            if (t == null || t.terrainData == null || m_UserHeights == null) return;
            TerrainData tData = t.terrainData;
            m_Modifiers.Clear();

            m_IsApplyingComposite = true;
            try
            {
                tData.SetHeightsDelayLOD(0, 0, m_UserHeights);
                tData.SyncHeightmap();
                if (m_UserAlphamaps != null)
                {
                    tData.SetAlphamaps(0, 0, m_UserAlphamaps);
                }
            }
            finally
            {
                m_IsApplyingComposite = false;
            }
        }
    }
}
