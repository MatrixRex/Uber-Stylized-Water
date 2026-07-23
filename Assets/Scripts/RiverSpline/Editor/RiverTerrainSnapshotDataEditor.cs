using UnityEngine;
using UnityEditor;

namespace RiverTools
{
    /// <summary>
    /// Custom editor for RiverTerrainSnapshotData assets to display summary metrics 
    /// without freezing Unity trying to draw millions of raw byte array element GUI slots.
    /// </summary>
    [CustomEditor(typeof(RiverTerrainSnapshotData))]
    public class RiverTerrainSnapshotDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RiverTerrainSnapshotData data = (RiverTerrainSnapshotData)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("River Terrain Snapshot Asset", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This asset stores binary baseline heightmaps and texture alphamaps for RiverTerrainCarver.\nDo not modify raw data manually.", MessageType.Info);

            EditorGUILayout.Space(5);
            if (data.Entries == null || data.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Snapshot status: Empty (no terrain baseline captured yet).", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"Captured Terrain Baselines ({data.Entries.Count}):", EditorStyles.boldLabel);
                foreach (var entry in data.Entries)
                {
                    if (entry == null) continue;
                    float heightMB = (entry.HeightDataBytes != null) ? entry.HeightDataBytes.Length / (1024f * 1024f) : 0f;
                    float alphaMB = (entry.AlphaDataBytes != null) ? entry.AlphaDataBytes.Length / (1024f * 1024f) : 0f;

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"Terrain: {entry.TerrainName}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Heightmap Resolution: {entry.HeightResolution}x{entry.HeightResolution} ({heightMB:F2} MB)", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"Alphamaps Resolution: {entry.AlphamapWidth}x{entry.AlphamapHeight}x{entry.AlphamapLayers} ({alphaMB:F2} MB)", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.Space(2);
                }
            }
        }
    }
}
