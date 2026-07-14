using UnityEngine;
using UnityEditor;

namespace RiverTools
{
    [CustomEditor(typeof(VertexColorPainter))]
    public class VertexColorPainterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw default inspector (which will be empty since fields are hidden)
            DrawDefaultInspector();

            VertexColorPainter painter = (VertexColorPainter)target;

            EditorGUILayout.HelpBox("Use UWa -> Vertex Color Painter window to paint. Actions below modify this mesh directly.", MessageType.Info);

            if (GUILayout.Button("Open Painter Window", GUILayout.Height(30)))
            {
                VertexColorPainterWindow.ShowWindow();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Reset Mesh", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Mesh", "Are you sure you want to discard painted changes and revert to the original mesh?", "Yes", "No"))
                {
                    painter.ResetToOriginal();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Export Painted Mesh", GUILayout.Height(30)))
            {
                string originalName = painter.OriginalMesh != null ? painter.OriginalMesh.name : "mesh";
                string savePath = EditorUtility.SaveFilePanelInProject("Export Painted Mesh", originalName + "_Painted", "asset", "Save the painted mesh as a Unity asset.");
                if (!string.IsNullOrEmpty(savePath))
                {
                    painter.ExportMesh(savePath);
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
