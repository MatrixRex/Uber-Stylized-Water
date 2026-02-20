using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for WaterFloatingSystem.
/// Shows the auto-read wave parameters and a manual refresh button.
/// </summary>
[CustomEditor(typeof(WaterFloatingSystem))]
public class WaterFloatingSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WaterFloatingSystem wfs = (WaterFloatingSystem)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Wave parameters are automatically read from this object's Material.\n" +
            "They mirror the shader properties:\n" +
            "_Wave_Steep  ·  _Wave_Length  ·  _Wave_Speed  ·  _Wave_Detail  ·  _Wave_Direction",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── Read-only display ─────────────────────────────────────────────
        using (new EditorGUI.DisabledGroupScope(true))
        {
            EditorGUILayout.FloatField("Steepness",  wfs.Steepness);
            EditorGUILayout.FloatField("Wavelength", wfs.Wavelength);
            EditorGUILayout.FloatField("Speed",      wfs.Speed);
            EditorGUILayout.FloatField("Iterations", wfs.Iterations);
            EditorGUILayout.FloatField("Direction",  wfs.Direction);
        }

        EditorGUILayout.Space(6);

        // ── Manual refresh ─────────────────────────────────────────────────
        if (GUILayout.Button("↺  Refresh from Material"))
        {
            wfs.FetchMaterialParams();
            EditorUtility.SetDirty(wfs);
        }
    }
}
