using UnityEngine;
using UnityEditor;

namespace RiverTools
{
    [CustomEditor(typeof(RiverTerrainCarver))]
    public class RiverTerrainCarverEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RiverTerrainCarver carver = (RiverTerrainCarver)target;
            serializedObject.Update();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("River Spline Terrain Carver", EditorStyles.boldLabel);

            // Dynamic Enable Toggle Header Box
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty enableDynamicProp = serializedObject.FindProperty("m_EnableDynamicCarve");
                EditorGUI.BeginChangeCheck();
                bool enableDynamic = EditorGUILayout.ToggleLeft("Enable Dynamic Terrain Carve Layer", enableDynamicProp.boolValue, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck())
                {
                    enableDynamicProp.boolValue = enableDynamic;
                    serializedObject.ApplyModifiedProperties();
                    carver.EnableDynamicCarve = enableDynamic;
                }

                if (enableDynamic)
                {
                    EditorGUILayout.HelpBox("Dynamic layer is ACTIVE. The river is composited onto the terrain via TerrainBaseline in real-time.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Dynamic layer is DISABLED. Terrain is in pure baseline ground state.", MessageType.Warning);
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_AutoRebuildOnSplineChange"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_EnableFastMode"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // Manual Carve & Revert Buttons
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Recalculate Carve Now", GUILayout.Height(30)))
                {
                    carver.CarveDynamic();
                    EditorUtility.SetDirty(carver);
                }

                if (GUILayout.Button("Revert to Base Ground", GUILayout.Height(30)))
                {
                    carver.RestoreAllSnapshots();
                    carver.EnableDynamicCarve = false;
                    EditorUtility.SetDirty(carver);
                }
            }

            EditorGUILayout.Space(8);

            // Permanent Baking Action Button
            using (new EditorGUI.DisabledGroupScope(!carver.EnableDynamicCarve))
            {
                if (GUILayout.Button("Bake Into Terrain (Stamp Permanently)", GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog("Bake Into Terrain",
                        "This will permanently stamp the carved riverbed heightmap and painted textures into the terrain baseline so you can use standard Unity Terrain sculpting and painting brushes directly on it.\n\nProceed?",
                        "Bake", "Cancel"))
                    {
                        carver.BakeIntoTerrain();
                        EditorUtility.SetDirty(carver);
                    }
                }
            }

            EditorGUILayout.Space(8);

            // 1. Carving Profile & Geometry Group
            EditorGUILayout.LabelField("River Bed & Bank Geometry", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty profileProp = serializedObject.FindProperty("m_BedProfile");
                EditorGUILayout.PropertyField(profileProp);
                if (profileProp.enumValueIndex == (int)BedProfileMode.CustomCurve)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CustomBedCurve"));
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BedDepth"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BedWidthRatio"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BankEdgeOffset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_BankFalloff"));
            }

            EditorGUILayout.Space(8);

            // 2. Terrain Texture Painting Group
            SerializedProperty enableTexPaintProp = serializedObject.FindProperty("m_EnableTexturePainting");
            EditorGUILayout.LabelField("Terrain Texture Painting", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(enableTexPaintProp);
                if (enableTexPaintProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TargetTerrainLayer"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TextureOpacity"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TextureWidthRatio"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TextureBankFalloff"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TextureBlendCurve"));
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(8);

            // 3. Settings Group
            EditorGUILayout.LabelField("Carve Quality & Sampling", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CarveMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SampleSpacing"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SmoothPasses"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SmoothStrength"));
            }

            EditorGUILayout.Space(8);

            // 4. Target Terrains Group
            EditorGUILayout.LabelField("Target Terrains", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TargetTerrains"), true);
                if (serializedObject.FindProperty("m_TargetTerrains").arraySize == 0)
                {
                    EditorGUILayout.HelpBox("Target terrains list is empty. Overlapping active scene terrains will be automatically detected.", MessageType.Info);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
