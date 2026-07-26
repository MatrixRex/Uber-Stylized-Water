using UnityEngine;
using UnityEditor;

namespace RiverTools
{
    [CustomEditor(typeof(TerrainBaseline))]
    public class TerrainBaselineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            TerrainBaseline baseline = (TerrainBaseline)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Terrain Ground Baseline Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    "This TerrainBaseline component stores pure user ground data in scene memory and acts as the central compositor for all procedural river carvers.\n\n" +
                    "Zero disk asset re-imports, zero editor freezing, and zero carving trails when moving rivers!",
                    MessageType.Info
                );

                if (GUILayout.Button("Revert Terrain to User Ground", GUILayout.Height(30)))
                {
                    if (EditorUtility.DisplayDialog("Revert Terrain",
                        "This will un-carve all procedural rivers on this terrain and restore pure user ground. Proceed?",
                        "Revert", "Cancel"))
                    {
                        baseline.RevertToBaselineGround();
                        EditorUtility.SetDirty(baseline.Terrain.terrainData);
                    }
                }
            }

            EditorGUILayout.Space(8);
            DrawDefaultInspector();
        }
    }
}
