using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;

namespace RiverTools
{
    public class VertexColorPainterWindow : EditorWindow
    {
        [MenuItem("UWa/Vertex Color Painter")]
        public static void ShowWindow()
        {
            GetWindow<VertexColorPainterWindow>("Vertex Painter");
        }

        private enum PaintTool
        {
            Select,
            PaintColorAlpha,
            PaintAlphaOnly,
            EraseColorAlpha,
            EraseAlphaOnly
        }

        private PaintTool m_ActiveTool = PaintTool.Select;
        private float m_BrushSize = 1.0f;
        private float m_BrushStrength = 0.5f;
        private bool m_SmoothFalloff = true;
        private Color m_BrushColor = Color.red;
        private float m_BrushAlpha = 1.0f; // Slider for Alpha-only mode

        private VertexColorPainter m_ActivePainter;
        private Vector3 m_HitPointWorld;
        private Vector3 m_HitNormalWorld;
        private bool m_HasHit = false;

        private Tool m_LastActiveTool = Tool.Move;

        // UI Toolkit Panels
        private VisualElement m_NoSelectionPanel;
        private VisualElement m_InitPanel;
        private VisualElement m_BrushPanel;

        // UI Toolkit Controls
        private Button m_SelectBtn;
        private Button m_PaintBtn;
        private Button m_PaintAlphaBtn;
        private Button m_EraseBtn;
        private Button m_EraseAlphaBtn;
        private ColorField m_ColorField;
        private Slider m_AlphaSlider;
        private Slider m_SizeSlider;
        private Slider m_StrengthSlider;
        private Toggle m_FalloffToggle;

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;

            if (m_ActiveTool != PaintTool.Select)
            {
                Tools.current = m_LastActiveTool;
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // 1. No Selection Panel
            m_NoSelectionPanel = new VisualElement
            {
                style = {
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10
                }
            };
            m_NoSelectionPanel.Add(new Label("Please select a GameObject with a MeshFilter in the hierarchy.")
            {
                style = { whiteSpace = WhiteSpace.Normal }
            });
            root.Add(m_NoSelectionPanel);

            // 2. Init Panel
            m_InitPanel = new VisualElement
            {
                style = {
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10
                }
            };
            m_InitPanel.Add(new Label("Selected object is ready for painting. Click 'Init Paint' to start.")
            {
                style = { whiteSpace = WhiteSpace.Normal, marginBottom = 10 }
            });
            m_InitPanel.Add(new Button(OnInitPaintClick) { text = "Init Paint" });
            root.Add(m_InitPanel);

            // 3. Brush Panel
            m_BrushPanel = new VisualElement
            {
                style = {
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10
                }
            };

            // Toolbar Container (5 Buttons)
            var toolbarContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 10
                }
            };

            m_SelectBtn = new Button(() => SetActiveTool(PaintTool.Select))
            {
                text = "Select",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };

            m_PaintBtn = new Button(() => SetActiveTool(PaintTool.PaintColorAlpha))
            {
                text = "Paint",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };

            m_PaintAlphaBtn = new Button(() => SetActiveTool(PaintTool.PaintAlphaOnly))
            {
                text = "Paint Alpha",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };

            m_EraseBtn = new Button(() => SetActiveTool(PaintTool.EraseColorAlpha))
            {
                text = "Erase",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };

            m_EraseAlphaBtn = new Button(() => SetActiveTool(PaintTool.EraseAlphaOnly))
            {
                text = "Erase Alpha",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            toolbarContainer.Add(m_SelectBtn);
            toolbarContainer.Add(m_PaintBtn);
            toolbarContainer.Add(m_PaintAlphaBtn);
            toolbarContainer.Add(m_EraseBtn);
            toolbarContainer.Add(m_EraseAlphaBtn);
            m_BrushPanel.Add(toolbarContainer);

            // Size Slider
            m_SizeSlider = new Slider("Brush Size", 0.1f, 10f) { value = m_BrushSize };
            m_SizeSlider.RegisterValueChangedCallback(evt => m_BrushSize = evt.newValue);
            m_BrushPanel.Add(m_SizeSlider);

            // Strength Slider
            m_StrengthSlider = new Slider("Brush Strength", 0.01f, 1.0f) { value = m_BrushStrength };
            m_StrengthSlider.RegisterValueChangedCallback(evt => m_BrushStrength = evt.newValue);
            m_BrushPanel.Add(m_StrengthSlider);

            // Falloff Toggle
            m_FalloffToggle = new Toggle("Smooth Falloff") { value = m_SmoothFalloff };
            m_FalloffToggle.RegisterValueChangedCallback(evt => m_SmoothFalloff = evt.newValue);
            m_BrushPanel.Add(m_FalloffToggle);

            // Color Field (Paint Color+Alpha)
            m_ColorField = new ColorField("Brush Color (RGBA)") { value = m_BrushColor };
            m_ColorField.RegisterValueChangedCallback(evt => m_BrushColor = evt.newValue);
            m_BrushPanel.Add(m_ColorField);

            // Alpha Slider (Paint Alpha-Only)
            m_AlphaSlider = new Slider("Brush Alpha", 0.0f, 1.0f) { value = m_BrushAlpha };
            m_AlphaSlider.RegisterValueChangedCallback(evt => m_BrushAlpha = evt.newValue);
            m_BrushPanel.Add(m_AlphaSlider);

            root.Add(m_BrushPanel);

            UpdateToolbarStyles();
            OnSelectionChanged();
        }

        private void SetActiveTool(PaintTool tool)
        {
            m_ActiveTool = tool;

            if (m_ActiveTool == PaintTool.Select)
            {
                Tools.current = m_LastActiveTool;
            }
            else
            {
                if (Tools.current != Tool.None)
                {
                    m_LastActiveTool = Tools.current;
                }
                Tools.current = Tool.None;
            }

            UpdateToolbarStyles();
            SceneView.RepaintAll();
        }

        private void UpdateToolbarStyles()
        {
            if (m_SelectBtn == null || m_PaintBtn == null || m_PaintAlphaBtn == null || m_EraseBtn == null || m_EraseAlphaBtn == null || m_ColorField == null || m_AlphaSlider == null) return;

            Color activeColor = new Color(0.2f, 0.4f, 0.6f);

            m_SelectBtn.style.backgroundColor = m_ActiveTool == PaintTool.Select ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_PaintBtn.style.backgroundColor = m_ActiveTool == PaintTool.PaintColorAlpha ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_PaintAlphaBtn.style.backgroundColor = m_ActiveTool == PaintTool.PaintAlphaOnly ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_EraseBtn.style.backgroundColor = m_ActiveTool == PaintTool.EraseColorAlpha ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_EraseAlphaBtn.style.backgroundColor = m_ActiveTool == PaintTool.EraseAlphaOnly ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);

            // Show ColorField only for Paint (Color + Alpha)
            m_ColorField.style.display = m_ActiveTool == PaintTool.PaintColorAlpha ? DisplayStyle.Flex : DisplayStyle.None;

            // Show AlphaSlider only for Paint Alpha
            m_AlphaSlider.style.display = m_ActiveTool == PaintTool.PaintAlphaOnly ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnInitPaintClick()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;

            MeshFilter filter = selected.GetComponent<MeshFilter>();
            if (filter == null) return;

            Undo.AddComponent<VertexColorPainter>(selected);
            m_ActivePainter = selected.GetComponent<VertexColorPainter>();
            m_ActivePainter.Initialize(filter.sharedMesh);
            
            SetActiveTool(PaintTool.PaintColorAlpha);
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                m_ActivePainter = null;
                SetPanelVisibility(showNoSelection: true, showInit: false, showBrush: false);
                return;
            }

            MeshFilter filter = selected.GetComponent<MeshFilter>();
            if (filter == null)
            {
                m_ActivePainter = null;
                SetPanelVisibility(showNoSelection: true, showInit: false, showBrush: false);
                return;
            }

            m_ActivePainter = selected.GetComponent<VertexColorPainter>();
            if (m_ActivePainter == null)
            {
                SetPanelVisibility(showNoSelection: false, showInit: true, showBrush: false);
            }
            else
            {
                SetPanelVisibility(showNoSelection: false, showInit: false, showBrush: true);
            }
        }

        private void SetPanelVisibility(bool showNoSelection, bool showInit, bool showBrush)
        {
            if (m_NoSelectionPanel != null) m_NoSelectionPanel.style.display = showNoSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_InitPanel != null) m_InitPanel.style.display = showInit ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_BrushPanel != null) m_BrushPanel.style.display = showBrush ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_ActivePainter == null || m_ActiveTool == PaintTool.Select)
                return;

            Event current = Event.current;

            // Exit painting mode on ESC (Switches to Select tool)
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                SetActiveTool(PaintTool.Select);
                current.Use();
                Repaint();
                return;
            }

            // Check if user is trying to navigate the Scene view (e.g. Orbit/Pan/Zoom)
            bool isNavigating = current.alt || current.button == 1 || current.button == 2;
            if (isNavigating)
            {
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
                Handles.color = (m_ActiveTool == PaintTool.EraseColorAlpha || m_ActiveTool == PaintTool.EraseAlphaOnly) ? Color.blue : m_BrushColor;
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
            Undo.RegisterCompleteObjectUndo(mesh, "Paint Vertex Colors and UV2");

            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;

            if (colors == null || colors.Length != vertices.Length)
            {
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0, 0, 0, 0);
            }

            List<Vector4> uv2 = new List<Vector4>();
            mesh.GetUVs(1, uv2);
            if (uv2 == null || uv2.Count != vertices.Length)
            {
                uv2 = new List<Vector4>(vertices.Length);
                for (int i = 0; i < vertices.Length; i++) uv2.Add(new Vector4(1f, 1f, 1f, 1f));
            }

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
                    
                    // 1. Process Colors
                    if (m_ActiveTool == PaintTool.PaintColorAlpha)
                      {
                          Color oldColor = colors[i];
                          float newAlpha = oldColor.a * (1.0f - stepStrength) + stepStrength;
                          float newR = oldColor.r * (1.0f - stepStrength) + m_BrushColor.r * stepStrength;
                          float newG = oldColor.g * (1.0f - stepStrength) + m_BrushColor.g * stepStrength;
                          float newB = oldColor.b * (1.0f - stepStrength) + m_BrushColor.b * stepStrength;
                          Color targetColor = new Color(newR, newG, newB, newAlpha);
                          if (oldColor != targetColor)
                          {
                              colors[i] = targetColor;
                              isDirty = true;
                          }
                      }
                      else if (m_ActiveTool == PaintTool.EraseColorAlpha)
                      {
                          Color oldColor = colors[i];
                          float newAlpha = oldColor.a * (1.0f - stepStrength);
                          float newR = oldColor.r * (1.0f - stepStrength);
                          float newG = oldColor.g * (1.0f - stepStrength);
                          float newB = oldColor.b * (1.0f - stepStrength);
                          Color targetColor = new Color(newR, newG, newB, newAlpha);
                          if (oldColor != targetColor)
                          {
                              colors[i] = targetColor;
                              isDirty = true;
                          }
                      }

                      // 2. Process UV2.w (transparency)
                      Vector4 oldUV = uv2[i];
                      float newW = oldUV.w;

                      if (m_ActiveTool == PaintTool.PaintColorAlpha)
                      {
                          newW = Mathf.Lerp(oldUV.w, m_BrushColor.a, stepStrength);
                      }
                      else if (m_ActiveTool == PaintTool.PaintAlphaOnly)
                      {
                          newW = Mathf.Lerp(oldUV.w, m_BrushAlpha, stepStrength);
                      }
                      else if (m_ActiveTool == PaintTool.EraseColorAlpha || m_ActiveTool == PaintTool.EraseAlphaOnly)
                      {
                          newW = Mathf.Lerp(oldUV.w, 1.0f, stepStrength);
                      }

                      if (newW != oldUV.w)
                      {
                          uv2[i] = new Vector4(oldUV.x, oldUV.y, oldUV.z, newW);
                          isDirty = true;
                      }
                  }
              }

              if (isDirty)
              {
                  mesh.colors = colors;
                  mesh.SetUVs(1, uv2);
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
  
