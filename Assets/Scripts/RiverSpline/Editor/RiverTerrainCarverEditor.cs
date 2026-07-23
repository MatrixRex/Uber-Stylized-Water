using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace RiverTools
{
    [CustomEditor(typeof(RiverTerrainCarver))]
    public class RiverTerrainCarverEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RiverTerrainCarver carver = (RiverTerrainCarver)target;

            serializedObject.Update();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("River Terrain Carver Controls", EditorStyles.boldLabel);

            // 1. Prominent Dynamic Layer Toggle Button (ON = Carves, OFF = Reverts)
            bool isDynamic = carver.EnableDynamicCarve;
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = isDynamic ? new Color(0.25f, 0.85f, 0.35f) : new Color(0.85f, 0.3f, 0.3f);
            if (GUILayout.Button(isDynamic ? "DYNAMIC LIVE CARVE LAYER: ACTIVE [ON]" : "DYNAMIC LIVE CARVE LAYER: INACTIVE [OFF]", GUILayout.Height(36)))
            {
                Undo.RecordObject(carver, "Toggle Dynamic Terrain Carve");
                carver.EnableDynamicCarve = !isDynamic;
                EditorUtility.SetDirty(carver);
            }
            GUI.backgroundColor = originalBg;

            EditorGUILayout.Space(6);

            // 2. Control Toggle Buttons Row (Realtime Update & Fast Edit Mode)
            using (new GUILayout.HorizontalScope())
            {
                bool isRealtime = carver.AutoRebuildOnSplineChange;
                GUI.backgroundColor = isRealtime ? new Color(0.2f, 0.7f, 1f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button(isRealtime ? "Realtime Update: ON" : "Realtime Update: OFF", GUILayout.Height(28)))
                {
                    Undo.RecordObject(carver, "Toggle Realtime Update");
                    carver.AutoRebuildOnSplineChange = !isRealtime;
                    EditorUtility.SetDirty(carver);
                }

                bool isFastMode = carver.EnableFastMode;
                GUI.backgroundColor = isFastMode ? new Color(1f, 0.75f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUILayout.Button(isFastMode ? "Fast Edit Mode: ON" : "Fast Edit Mode: OFF", GUILayout.Height(28)))
                {
                    Undo.RecordObject(carver, "Toggle Fast Edit Mode");
                    carver.EnableFastMode = !isFastMode;
                    EditorUtility.SetDirty(carver);
                }
                GUI.backgroundColor = originalBg;
            }

            EditorGUILayout.Space(6);

            // 3. Manual Carve & Revert Buttons
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Manual Recarve Now", GUILayout.Height(30)))
                {
                    carver.CarveDynamic();
                    EditorUtility.SetDirty(carver);
                }

                if (GUILayout.Button("Revert to Original", GUILayout.Height(30)))
                {
                    carver.RestoreAllSnapshots();
                    carver.EnableDynamicCarve = false;
                    EditorUtility.SetDirty(carver);
                }
            }

            EditorGUILayout.Space(8);

            // 4. Permanent Baking Action Button
            using (new EditorGUI.DisabledGroupScope(!carver.EnableDynamicCarve))
            {
                if (GUILayout.Button("Bake Into Terrain (Stamp Permanently)", GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog("Bake Into Terrain",
                        "This will permanently stamp the carved riverbed heightmap and painted textures into the terrain so you can use standard Unity Terrain sculpting and painting brushes on it.\n\nProceed?",
                        "Bake", "Cancel"))
                    {
                        carver.BakeIntoTerrain();
                        EditorUtility.SetDirty(carver);
                    }
                }
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.Space(8);

            // 1. Spline & Asset References Group
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SnapshotAsset"));
            }

            EditorGUILayout.Space(8);

            // 2. Carving Profile & Geometry Group
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

            // 3. Terrain Texture Painting Group
            SerializedProperty enableTexPaintProp = serializedObject.FindProperty("m_EnableTexturePainting");
            EditorGUILayout.LabelField("Terrain Texture Painting", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(enableTexPaintProp);
                if (enableTexPaintProp != null && enableTexPaintProp.boolValue)
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

            // 4. Carve Mode & Smoothing Settings Group
            EditorGUILayout.LabelField("Carve & Smoothing Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_CarveMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SampleSpacing"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SmoothPasses"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_SmoothStrength"));
            }

            EditorGUILayout.Space(8);

            // 5. Target Terrains Group
            EditorGUILayout.LabelField("Target Terrains", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_TargetTerrains"), true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            RiverTerrainCarver carver = (RiverTerrainCarver)target;
            if (carver == null || carver.Container == null || carver.Container.Spline == null) return;

            // Detect active user spline drag interactions for Fast Mode switching (ignore camera orbit/pan)
            Event e = Event.current;
            if (e != null)
            {
                if (!e.alt && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                {
                    carver.IsActivelyEditing = true;
                }
                else if (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow)
                {
                    carver.IsActivelyEditing = false;
                }
            }

            carver.SamplePolyline(out var samples, out float maxHalfWidth, out float maxInfluence);
            if (samples == null || samples.Count < 2) return;

            Handles.matrix = Matrix4x4.identity;

            // Draw center spline line
            Handles.color = new Color(0f, 0.8f, 1f, 0.9f);
            for (int i = 0; i < samples.Count - 1; i++)
            {
                Handles.DrawLine(samples[i].Position, samples[i + 1].Position, 2f);
            }

            // Draw bed width outlines (where carved riverbed channel ends)
            Handles.color = new Color(0.2f, 0.5f, 1f, 0.7f);
            float bedWidthRatio = carver.BedWidthRatio;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                Vector3 left0 = samples[i].Position - samples[i].Right * (samples[i].HalfWidth * bedWidthRatio);
                Vector3 left1 = samples[i + 1].Position - samples[i + 1].Right * (samples[i + 1].HalfWidth * bedWidthRatio);
                Vector3 right0 = samples[i].Position + samples[i].Right * (samples[i].HalfWidth * bedWidthRatio);
                Vector3 right1 = samples[i + 1].Position + samples[i + 1].Right * (samples[i + 1].HalfWidth * bedWidthRatio);

                Handles.DrawLine(left0, left1, 1.5f);
                Handles.DrawLine(right0, right1, 1.5f);
            }

            // Draw bank falloff boundaries (where terrain returns to original height)
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            float falloff = carver.BankFalloff;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                Vector3 bankLeft0 = samples[i].Position - samples[i].Right * (samples[i].HalfWidth * bedWidthRatio + falloff);
                Vector3 bankLeft1 = samples[i + 1].Position - samples[i + 1].Right * (samples[i + 1].HalfWidth * bedWidthRatio + falloff);
                Vector3 bankRight0 = samples[i].Position + samples[i].Right * (samples[i].HalfWidth * bedWidthRatio + falloff);
                Vector3 bankRight1 = samples[i + 1].Position + samples[i + 1].Right * (samples[i + 1].HalfWidth * bedWidthRatio + falloff);

                Handles.DrawLine(bankLeft0, bankLeft1, 1f);
                Handles.DrawLine(bankRight0, bankRight1, 1f);
            }

            // Draw texture paint bank falloff boundaries (purple/magenta lines) when texture painting is active
            if (carver.EnableTexturePainting)
            {
                Handles.color = new Color(0.85f, 0.35f, 1f, 0.6f);
                float texWidthRatio = carver.TextureWidthRatio;
                float texFalloff = carver.TextureBankFalloff;
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Vector3 texLeft0 = samples[i].Position - samples[i].Right * (samples[i].HalfWidth * texWidthRatio + texFalloff);
                    Vector3 texLeft1 = samples[i + 1].Position - samples[i + 1].Right * (samples[i + 1].HalfWidth * texWidthRatio + texFalloff);
                    Vector3 texRight0 = samples[i].Position + samples[i].Right * (samples[i].HalfWidth * texWidthRatio + texFalloff);
                    Vector3 texRight1 = samples[i + 1].Position + samples[i + 1].Right * (samples[i + 1].HalfWidth * texWidthRatio + texFalloff);

                    Handles.DrawLine(texLeft0, texLeft1, 1.2f);
                    Handles.DrawLine(texRight0, texRight1, 1.2f);
                }
            }
        }
    }
}
