using UnityEngine;
using System.Collections.Generic;

namespace RiverTools
{
    [System.Serializable]
    public class TerrainSnapshotEntry
    {
        public string TerrainName;
        public int HeightResolution;
        [HideInInspector] public byte[] HeightDataBytes;

        public int AlphamapWidth;
        public int AlphamapHeight;
        public int AlphamapLayers;
        [HideInInspector] public byte[] AlphaDataBytes;
    }

    /// <summary>
    /// ScriptableObject asset that stores baseline terrain heightmap and alphamap snapshots persistently
    /// so dynamic carving state survives Unity restarts and domain reloads.
    /// </summary>
    [CreateAssetMenu(fileName = "RiverTerrainSnapshot", menuName = "Splines/River Terrain Snapshot Data")]
    public class RiverTerrainSnapshotData : ScriptableObject
    {
        [SerializeField] private List<TerrainSnapshotEntry> m_Entries = new List<TerrainSnapshotEntry>();

        public List<TerrainSnapshotEntry> Entries => m_Entries;

        public void Clear()
        {
            m_Entries.Clear();
        }

        public TerrainSnapshotEntry GetEntry(Terrain terrain)
        {
            if (terrain == null) return null;
            string tName = terrain.name;
            foreach (var entry in m_Entries)
            {
                if (entry != null && entry.TerrainName == tName) return entry;
            }
            return null;
        }

        public void SetSnapshot(Terrain terrain, float[,] heights, float[,,] alphamaps)
        {
            if (terrain == null) return;
            string tName = terrain.name;

            TerrainSnapshotEntry entry = GetEntry(terrain);
            if (entry == null)
            {
                entry = new TerrainSnapshotEntry { TerrainName = tName };
                m_Entries.Add(entry);
            }

            if (heights != null)
            {
                int hRes = heights.GetLength(0);
                entry.HeightResolution = hRes;
                int count = hRes * hRes;
                float[] flatHeights = new float[count];
                int idx = 0;
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        flatHeights[idx++] = heights[z, x];
                    }
                }
                entry.HeightDataBytes = new byte[count * sizeof(float)];
                System.Buffer.BlockCopy(flatHeights, 0, entry.HeightDataBytes, 0, entry.HeightDataBytes.Length);
            }

            if (alphamaps != null)
            {
                int aH = alphamaps.GetLength(0);
                int aW = alphamaps.GetLength(1);
                int aL = alphamaps.GetLength(2);
                entry.AlphamapHeight = aH;
                entry.AlphamapWidth = aW;
                entry.AlphamapLayers = aL;

                int count = aH * aW * aL;
                float[] flatAlphamaps = new float[count];
                int idx = 0;
                for (int z = 0; z < aH; z++)
                {
                    for (int x = 0; x < aW; x++)
                    {
                        for (int k = 0; k < aL; k++)
                        {
                            flatAlphamaps[idx++] = alphamaps[z, x, k];
                        }
                    }
                }
                entry.AlphaDataBytes = new byte[count * sizeof(float)];
                System.Buffer.BlockCopy(flatAlphamaps, 0, entry.AlphaDataBytes, 0, entry.AlphaDataBytes.Length);
            }
        }

        public bool TryGetSnapshot(Terrain terrain, out float[,] heights, out float[,,] alphamaps, out int hRes, out int aWidth, out int aHeight, out int aLayers)
        {
            heights = null;
            alphamaps = null;
            hRes = 0;
            aWidth = 0;
            aHeight = 0;
            aLayers = 0;

            if (terrain == null) return false;
            TerrainSnapshotEntry entry = GetEntry(terrain);
            if (entry == null) return false;

            if (entry.HeightDataBytes != null && entry.HeightDataBytes.Length > 0 && entry.HeightResolution > 0)
            {
                hRes = entry.HeightResolution;
                int count = hRes * hRes;
                if (entry.HeightDataBytes.Length == count * sizeof(float))
                {
                    float[] flatHeights = new float[count];
                    System.Buffer.BlockCopy(entry.HeightDataBytes, 0, flatHeights, 0, entry.HeightDataBytes.Length);
                    heights = new float[hRes, hRes];
                    int idx = 0;
                    for (int z = 0; z < hRes; z++)
                    {
                        for (int x = 0; x < hRes; x++)
                        {
                            heights[z, x] = flatHeights[idx++];
                        }
                    }
                }
            }

            if (entry.AlphaDataBytes != null && entry.AlphaDataBytes.Length > 0 && entry.AlphamapWidth > 0 && entry.AlphamapHeight > 0 && entry.AlphamapLayers > 0)
            {
                aWidth = entry.AlphamapWidth;
                aHeight = entry.AlphamapHeight;
                aLayers = entry.AlphamapLayers;
                int count = aHeight * aWidth * aLayers;
                if (entry.AlphaDataBytes.Length == count * sizeof(float))
                {
                    float[] flatAlphamaps = new float[count];
                    System.Buffer.BlockCopy(entry.AlphaDataBytes, 0, flatAlphamaps, 0, entry.AlphaDataBytes.Length);
                    alphamaps = new float[aHeight, aWidth, aLayers];
                    int idx = 0;
                    for (int z = 0; z < aHeight; z++)
                    {
                        for (int x = 0; x < aWidth; x++)
                        {
                            for (int k = 0; k < aLayers; k++)
                            {
                                alphamaps[z, x, k] = flatAlphamaps[idx++];
                            }
                        }
                    }
                }
            }

            return heights != null;
        }
    }
}
