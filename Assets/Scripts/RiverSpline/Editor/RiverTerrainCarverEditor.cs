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
            EditorGUILayout.LabelField("Dynamic Layer Controls", EditorStyles.boldLabel);

            bool prevDynamic = carver.EnableDynamicCarve;
            bool newDynamic = EditorGUILayout.ToggleLeft(" Enable Dynamic Live Carve Layer", prevDynamic, EditorStyles.boldLabel);
            if (newDynamic != prevDynamic)
            {
                Undo.RecordObject(carver, "Toggle Dynamic Terrain Carve");
                carver.EnableDynamicCarve = newDynamic;
                EditorUtility.SetDirty(carver);
            }

            if (carver.EnableDynamicCarve)
            {
                EditorGUILayout.HelpBox("Dynamic Carve Layer is LIVE. Changes to splines or settings update the terrain heightmap in real time.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Dynamic Carve Layer is OFF.", MessageType.None);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Carve Dynamic Layer Now", GUILayout.Height(30)))
                {
                    carver.CarveDynamic();
                }

                if (GUILayout.Button("Revert Dynamic Layer", GUILayout.Height(30)))
                {
                    carver.RestoreAllSnapshots();
                    carver.EnableDynamicCarve = false;
                    EditorUtility.SetDirty(carver);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Permanent Baking", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(!carver.EnableDynamicCarve))
            {
                if (GUILayout.Button("Bake Into Terrain (Stamp Permanently)", GUILayout.Height(40)))
                {
                    if (EditorUtility.DisplayDialog("Bake Into Terrain",
                        "This will permanently stamp the carved riverbed into the terrain heightmaps so you can use standard Unity Terrain sculpting and painting brushes on it.\n\nProceed?",
                        "Bake", "Cancel"))
                    {
                        carver.BakeIntoTerrain();
                        EditorUtility.SetDirty(carver);
                    }
                }
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Carving & Profile Settings", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script");

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            RiverTerrainCarver carver = (RiverTerrainCarver)target;
            if (carver == null || carver.Container == null || carver.Container.Spline == null) return;

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
        }
    }
}
