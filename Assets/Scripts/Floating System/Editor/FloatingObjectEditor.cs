using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for FloatingObject.
/// Shows a clear "Preview In Edit Mode" toggle and setup guidance.
/// </summary>
[CustomEditor(typeof(FloatingObject))]
public class FloatingObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FloatingObject fo = (FloatingObject)target;

        // ── Edit Mode Preview Toggle ───────────────────────────────────────
        EditorGUILayout.Space(4);

        bool currentPreview = fo.previewInEditMode;
        Color btnColor      = currentPreview ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.8f, 0.8f, 0.8f);

        using (new BackgroundColorScope(btnColor))
        {
            string label = currentPreview ? "■  Edit Mode Preview: ON" : "▶  Edit Mode Preview: OFF";
            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                Undo.RecordObject(fo, "Toggle Edit Mode Preview");
                fo.previewInEditMode = !fo.previewInEditMode;

                // Repaint the scene when toggling
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.Space(6);

        // ── Validation Hints ──────────────────────────────────────────────
        bool hasWater     = WaterFloatingSystem.Instance != null
                             || FindObjectOfType<WaterFloatingSystem>() != null;
        bool hasSamplePts   = fo.SamplePoints != null && fo.SamplePoints.Count > 0;

        if (!hasWater)
        {
            EditorGUILayout.HelpBox(
                "No WaterFloatingSystem found in the scene.\n" +
                "Add the 'Water Floating System' component to your water plane.",
                MessageType.Warning);
        }

        if (!hasSamplePts)
        {
            EditorGUILayout.HelpBox(
                "No FloatingSamplePoint children found.\n" +
                "Create empty child GameObjects and add 'Floating Sample Point' to each.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{fo.SamplePoints.Count} sample point(s) active.",
                MessageType.None);
        }

        EditorGUILayout.Space(4);

        // ── Default Inspector (shows all remaining public fields) ─────────
        DrawDefaultInspector();
    }

    // ── Repaint scene continuously while preview is active ────────────────
    private void OnSceneGUI()
    {
        FloatingObject fo = (FloatingObject)target;
        if (fo.previewInEditMode && !Application.isPlaying)
        {
            // Force the scene to keep updating so the preview animates
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    // ── Helper: temporary background colour scope ─────────────────────────
    private struct BackgroundColorScope : System.IDisposable
    {
        private readonly Color prev;
        public BackgroundColorScope(Color c) { prev = GUI.backgroundColor; GUI.backgroundColor = c; }
        public void Dispose()                { GUI.backgroundColor = prev; }
    }
}
