using UnityEngine;
using UnityEditor;

namespace RiverTools
{
    public class VertexColorPainterWindow : EditorWindow
    {
        [MenuItem("Window/River Tools/Vertex Color Painter")]
        public static void ShowWindow()
        {
            GetWindow<VertexColorPainterWindow>("Vertex Painter");
        }

        private float m_BrushSize = 1.0f;
        private float m_BrushStrength = 0.5f;
        private bool m_SmoothFalloff = true;
        private Color m_BrushColor = Color.red;
        private bool m_IsEraseMode = false;
        private bool m_IsPainting = false;

        private VertexColorPainter m_ActivePainter;
        private Vector3 m_HitPointWorld;
        private Vector3 m_HitNormalWorld;
        private bool m_HasHit = false;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                m_ActivePainter = selected.GetComponent<VertexColorPainter>();
            }
            else
            {
                m_ActivePainter = null;
            }
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Label("Vertex Color Painter", EditorStyles.boldLabel);

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Please select a GameObject in the scene hierarchy.", MessageType.Info);
                return;
            }

            MeshFilter filter = selected.GetComponent<MeshFilter>();
            if (filter == null)
            {
                EditorGUILayout.HelpBox("Selected GameObject must have a MeshFilter.", MessageType.Warning);
                return;
            }

            if (m_ActivePainter == null)
            {
                if (GUILayout.Button("Init Paint"))
                {
                    Undo.AddComponent<VertexColorPainter>(selected);
                    m_ActivePainter = selected.GetComponent<VertexColorPainter>();
                    m_ActivePainter.Initialize(filter.sharedMesh);
                    m_IsPainting = true;
                    SceneView.RepaintAll();
                }
                return;
            }

            // Brush Settings
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            m_IsPainting = EditorGUILayout.Toggle("Active Paint Mode", m_IsPainting);
            m_BrushSize = EditorGUILayout.Slider("Brush Size", m_BrushSize, 0.1f, 10f);
            m_BrushStrength = EditorGUILayout.Slider("Brush Strength", m_BrushStrength, 0.01f, 1.0f);
            m_SmoothFalloff = EditorGUILayout.Toggle("Smooth Falloff", m_SmoothFalloff);

            using (new EditorGUI.DisabledGroupScope(m_IsEraseMode))
            {
                m_BrushColor = EditorGUILayout.ColorField("Brush Color", m_BrushColor);
            }
            m_IsEraseMode = EditorGUILayout.Toggle("Erase Mode", m_IsEraseMode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Reset Mesh"))
            {
                if (EditorUtility.DisplayDialog("Reset Mesh", "Are you sure you want to discard all painted changes and revert to the original mesh?", "Yes", "No"))
                {
                    m_IsPainting = false;
                    m_ActivePainter.ResetToOriginal();
                    m_ActivePainter = null;
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button("Export Painted Mesh"))
            {
                string originalName = m_ActivePainter.OriginalMesh != null ? m_ActivePainter.OriginalMesh.name : "mesh";
                string savePath = EditorUtility.SaveFilePanelInProject("Export Painted Mesh", originalName + "_Painted", "asset", "Save the painted mesh as a Unity asset.");
                if (!string.IsNullOrEmpty(savePath))
                {
                    m_IsPainting = false;
                    m_ActivePainter.ExportMesh(savePath);
                    m_ActivePainter = null;
                    SceneView.RepaintAll();
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_ActivePainter == null || !m_IsPainting)
                return;

            Event current = Event.current;

            // Exit painting mode on ESC
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                m_IsPainting = false;
                current.Use();
                Repaint();
                return;
            }

            Transform targetTransform = m_ActivePainter.transform;
            Mesh copyMesh = m_ActivePainter.CopyMesh;
            if (copyMesh == null) return;

            // Intercept control clicks to prevent default Unity selection changes in Scene view
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            // Compute Local Ray
            Ray worldRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 localOrigin = targetTransform.InverseTransformPoint(worldRay.origin);
            Vector3 localDirection = targetTransform.InverseTransformDirection(worldRay.direction).normalized;
            Ray localRay = new Ray(localOrigin, localDirection);

            m_HasHit = RaycastMesh(localRay, copyMesh, out Vector3 localHitPoint, out Vector3 localHitNormal, out float hitDistance);

            if (m_HasHit)
            {
                m_HitPointWorld = targetTransform.TransformPoint(localHitPoint);
                m_HitNormalWorld = targetTransform.TransformDirection(localHitNormal).normalized;

                // Render brush outline
                Handles.color = m_IsEraseMode ? Color.blue : m_BrushColor;
                Handles.DrawWireDisc(m_HitPointWorld, m_HitNormalWorld, m_BrushSize);

                // Perform painting on MouseDown/Drag
                if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0)
                {
                    PaintMesh(m_HitPointWorld, targetTransform, copyMesh);
                    current.Use();
                }
            }

            sceneView.Repaint();
        }

        private void PaintMesh(Vector3 hitPointWorld, Transform targetTransform, Mesh mesh)
        {
            Undo.RegisterCompleteObjectUndo(mesh, "Paint Vertex Colors");

            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 0);
            }

            // Fixed time delta for painting flow in editor SceneView
            float deltaTime = 0.016f;
            float opacityStep = m_BrushStrength * deltaTime * 10f;
            bool isDirty = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertexWorld = targetTransform.TransformPoint(vertices[i]);
                float dist = Vector3.Distance(vertexWorld, hitPointWorld);

                if (dist <= m_BrushSize)
                {
                    float factor = 1.0f - (dist / m_BrushSize);
                    if (m_SmoothFalloff)
                    {
                        factor = factor * factor * (3.0f - 2.0f * factor); // Hermite smoothstep
                    }

                    float stepStrength = opacityStep * factor;
                    Color oldColor = colors[i];
                    Color targetColor;

                    if (m_IsEraseMode)
                    {
                        float newAlpha = oldColor.a * (1.0f - stepStrength);
                        float newR = oldColor.r * (1.0f - stepStrength);
                        float newG = oldColor.g * (1.0f - stepStrength);
                        float newB = oldColor.b * (1.0f - stepStrength);
                        targetColor = new Color(newR, newG, newB, newAlpha);
                    }
                    else
                    {
                        float newAlpha = oldColor.a * (1.0f - stepStrength) + stepStrength;
                        float newR = oldColor.r * (1.0f - stepStrength) + m_BrushColor.r * stepStrength;
                        float newG = oldColor.g * (1.0f - stepStrength) + m_BrushColor.g * stepStrength;
                        float newB = oldColor.b * (1.0f - stepStrength) + m_BrushColor.b * stepStrength;
                        targetColor = new Color(newR, newG, newB, newAlpha);
                    }

                    if (oldColor != targetColor)
                    {
                        colors[i] = targetColor;
                        isDirty = true;
                    }
                }
            }

            if (isDirty)
            {
                mesh.colors = colors;
                mesh.UploadMeshData(false);
            }
        }

        private static bool RaycastMesh(Ray localRay, Mesh mesh, out Vector3 localHitPoint, out Vector3 localHitNormal, out float hitDistance)
        {
            localHitPoint = Vector3.zero;
            localHitNormal = Vector3.up;
            hitDistance = float.MaxValue;
            bool hasHit = false;

            if (!mesh.bounds.IntersectRay(localRay, out float boundsDistance))
                return false;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];

                if (RayTriangleIntersection(localRay, v0, v1, v2, out float t, out Vector3 normal))
                {
                    if (t < hitDistance)
                    {
                        hitDistance = t;
                        localHitPoint = localRay.GetPoint(t);
                        localHitNormal = normal;
                        hasHit = true;
                    }
                }
            }

            return hasHit;
        }

        private static bool RayTriangleIntersection(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float t, out Vector3 normal)
        {
            t = 0;
            normal = Vector3.zero;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(ray.direction, edge2);
            float a = Vector3.Dot(edge1, h);

            if (a > -0.00001f && a < 0.00001f)
                return false;

            float f = 1.0f / a;
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.direction, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            t = f * Vector3.Dot(edge2, q);
            if (t > 0.00001f)
            {
                normal = Vector3.Cross(edge1, edge2).normalized;
                return true;
            }

            return false;
        }
    }
}
