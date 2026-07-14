using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Globalization;

namespace RiverTools
{
    [CustomEditor(typeof(RiverExtrude))]
    public class RiverExtrudeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RiverExtrude extrude = (RiverExtrude)target;

            if (extrude.IsBaked)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("This River is currently BAKED to a static mesh asset. Procedural updates are disabled.", MessageType.Info);

                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.ObjectField("Baked Mesh", extrude.BakedMesh, typeof(Mesh), false);
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Release Bake", GUILayout.Height(40)))
                {
                    ReleaseBake(extrude);
                }
                return;
            }

            DrawDefaultInspector();

            GUILayout.Space(15);
            GUILayout.Label("Bake Mesh Tools", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake to Asset", GUILayout.Height(30)))
                {
                    BakeToAsset(extrude);
                }

                if (GUILayout.Button("Export to OBJ", GUILayout.Height(30)))
                {
                    BakeToOBJ(extrude);
                }
            }
        }

        private static void BakeToAsset(RiverExtrude extrude)
        {
            extrude.Rebuild();

            Mesh sourceMesh = extrude.GeneratedMesh;
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog("Bake Mesh Error", "No generated mesh found on RiverExtrude. Make sure the spline is set up correctly.", "OK");
                return;
            }

            string defaultName = extrude.gameObject.name + "_RiverMesh";
            string path = EditorUtility.SaveFilePanelInProject("Save Mesh Asset", defaultName, "asset", "Please enter a file name to save the mesh to");
            if (string.IsNullOrEmpty(path))
                return;

            Mesh meshToSave = Instantiate(sourceMesh);
            meshToSave.name = Path.GetFileNameWithoutExtension(path);
            meshToSave.hideFlags = HideFlags.None;

            AssetDatabase.CreateAsset(meshToSave, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Load the saved asset reference
            Mesh savedMeshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMeshAsset == null)
            {
                EditorUtility.DisplayDialog("Bake Mesh Error", "Failed to load the saved mesh asset.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(extrude, "Bake to Asset");

            // Turn off spline (SplineContainer on same GameObject)
            var splineContainer = extrude.GetComponent<UnityEngine.Splines.SplineContainer>();
            if (splineContainer != null)
            {
                Undo.RegisterCompleteObjectUndo(splineContainer, "Disable SplineContainer");
                splineContainer.enabled = false;
                EditorUtility.SetDirty(splineContainer);
            }

            extrude.BakedMesh = savedMeshAsset;
            extrude.IsBaked = true;

            // Apply to MeshFilter & Collider
            var filter = extrude.GetComponent<MeshFilter>();
            if (filter != null)
            {
                Undo.RecordObject(filter, "Assign Baked Mesh");
                filter.sharedMesh = savedMeshAsset;
            }

            if (extrude.GetComponent<MeshCollider>() != null && extrude.UpdateMeshCollider)
            {
                var collider = extrude.GetComponent<MeshCollider>();
                Undo.RecordObject(collider, "Assign Baked Mesh Collider");
                collider.sharedMesh = savedMeshAsset;
            }

            EditorUtility.SetDirty(extrude);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(extrude.gameObject.scene);

            EditorUtility.DisplayDialog("Bake Mesh Success", $"Mesh successfully saved to:\n{path}\n\nThe RiverExtrude component is now locked in baked mode.", "OK");
        }

        private static void ReleaseBake(RiverExtrude extrude)
        {
            Undo.RegisterCompleteObjectUndo(extrude, "Release Bake");

            // Re-enable SplineContainer on same GameObject if it exists
            var splineContainer = extrude.GetComponent<UnityEngine.Splines.SplineContainer>();
            if (splineContainer != null)
            {
                Undo.RegisterCompleteObjectUndo(splineContainer, "Enable SplineContainer");
                splineContainer.enabled = true;
                EditorUtility.SetDirty(splineContainer);
            }

            extrude.IsBaked = false;
            extrude.BakedMesh = null;

            // Force a rebuild to recreate the procedural mesh
            extrude.Rebuild();

            EditorUtility.SetDirty(extrude);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(extrude.gameObject.scene);
        }

        private static void BakeToOBJ(RiverExtrude extrude)
        {
            extrude.Rebuild();

            Mesh sourceMesh = extrude.GeneratedMesh;
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog("Bake Mesh Error", "No generated mesh found on RiverExtrude. Make sure the spline is set up correctly.", "OK");
                return;
            }

            string defaultName = extrude.gameObject.name + "_RiverMesh";
            string path = EditorUtility.SaveFilePanel("Save OBJ File", "Assets", defaultName, "obj");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                string objText = MeshToObjString(sourceMesh);
                File.WriteAllText(path, objText);

                // If the path is within the project directory, refresh the database.
                string absoluteDataPath = Path.GetFullPath(Application.dataPath);
                string absolutePath = Path.GetFullPath(path);
                if (absolutePath.StartsWith(absoluteDataPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.Refresh();
                }

                EditorUtility.DisplayDialog("Bake OBJ Success", $"Mesh successfully exported to OBJ at:\n{path}", "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Bake OBJ Error", $"Failed to export OBJ:\n{ex.Message}", "OK");
            }
        }

        private static string MeshToObjString(Mesh mesh)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# River Extrude Mesh OBJ Export");
            sb.AppendLine($"# Vertices: {mesh.vertexCount}");
            sb.AppendLine($"# Triangles: {mesh.triangles.Length / 3}");
            sb.AppendLine();

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            // Vertices: Flip X for Unity (left-handed) to OBJ (right-handed) conversion.
            foreach (Vector3 v in vertices)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F6} {1:F6} {2:F6}", -v.x, v.y, v.z));
            }
            sb.AppendLine();

            // Normals: Flip X to match vertices conversion.
            if (normals != null && normals.Length > 0)
            {
                foreach (Vector3 n in normals)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F6} {1:F6} {2:F6}", -n.x, n.y, n.z));
                }
                sb.AppendLine();
            }

            // UVs
            if (uvs != null && uvs.Length > 0)
            {
                foreach (Vector2 uv in uvs)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "vt {0:F6} {1:F6}", uv.x, uv.y));
                }
                sb.AppendLine();
            }

            // Faces: Flip winding order because of the X flip to maintain correct face orientation.
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int v1 = triangles[i] + 1;
                int v2 = triangles[i + 1] + 1;
                int v3 = triangles[i + 2] + 1;

                if (uvs != null && uvs.Length > 0 && normals != null && normals.Length > 0)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", v3, v2, v1));
                }
                else if (uvs != null && uvs.Length > 0)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "f {0}/{0} {1}/{1} {2}/{2}", v3, v2, v1));
                }
                else if (normals != null && normals.Length > 0)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "f {0}//{0} {1}//{1} {2}//{2}", v3, v2, v1));
                }
                else
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "f {0} {1} {2}", v3, v2, v1));
                }
            }

            return sb.ToString();
        }
    }
}
