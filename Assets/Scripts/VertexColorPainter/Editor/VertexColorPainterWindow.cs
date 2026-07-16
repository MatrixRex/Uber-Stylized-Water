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
            Paint,
            PaintFlow,
            Erase,
            EraseFlow
        }

        private enum PaintSubTool
        {
            ColorAndAlpha,
            OnlyAlpha
        }

        private PaintTool m_ActiveTool = PaintTool.Select;
        private PaintSubTool m_ActiveSubTool = PaintSubTool.ColorAndAlpha;

        private float m_BrushSize = 1.0f;
        private float m_BrushStrength = 0.5f;
        private bool m_SmoothFalloff = true;
        private Color m_BrushColor = Color.red;
        private float m_BrushAlpha = 1.0f;

        private VertexColorPainter m_ActivePainter;
        private Vector3 m_HitPointWorld;
        private Vector3 m_LastHitPointWorld;
        private Vector3 m_HitNormalWorld;
        private bool m_HasHit = false;
        private bool m_ShowFlowDirection = false;

        private Tool m_LastActiveTool = Tool.Move;

        // UI Panels
        private VisualElement m_NoSelectionPanel;
        private VisualElement m_InitPanel;
        private VisualElement m_BrushPanel;

        // Controls
        private Button m_SelectBtn;
        private Button m_PaintBtn;
        private Button m_PaintFlowBtn;
        private Button m_EraseBtn;
        private Button m_EraseFlowBtn;

        private VisualElement m_SubToolContainer;
        private Button m_SubColorBtn;
        private Button m_SubAlphaBtn;

        private VisualElement m_BrushSettingsContainer;
        private Slider m_SizeSlider;
        private Slider m_StrengthSlider;
        private Toggle m_FalloffToggle;
        private ColorField m_ColorField;
        private Slider m_AlphaSlider;
        private Toggle m_ShowFlowToggle;

        // Collapsible Helpboxes
        private Foldout m_SelectHelpFoldout;
        private Foldout m_PaintHelpFoldout;
        private Foldout m_EraseHelpFoldout;

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

            if (m_ShowFlowDirection)
            {
                m_ShowFlowDirection = false;
                UpdateFlowVisualization();
            }
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

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

            m_BrushPanel = new VisualElement
            {
                style = {
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10
                }
            };

            // --- Main Tools ---
            m_BrushPanel.Add(new Label("Vertex Painter Tool") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });

            var toolbarContainer = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 15
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

            m_PaintBtn = new Button(() => SetActiveTool(PaintTool.Paint))
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

            m_PaintFlowBtn = new Button(() => SetActiveTool(PaintTool.PaintFlow))
            {
                text = "Paint Flow",
                style = {
                    flexGrow = 1.0f,
                    height = 25,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };

            m_EraseBtn = new Button(() => SetActiveTool(PaintTool.Erase))
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

            m_EraseFlowBtn = new Button(() => SetActiveTool(PaintTool.EraseFlow))
            {
                text = "Erase Flow",
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
            toolbarContainer.Add(m_PaintFlowBtn);
            toolbarContainer.Add(m_EraseBtn);
            toolbarContainer.Add(m_EraseFlowBtn);
            m_BrushPanel.Add(toolbarContainer);

            // --- Paint Sub-Tools ---
            m_SubToolContainer = new VisualElement { style = { marginBottom = 15 } };
            m_SubToolContainer.Add(new Label("Paint Sub-Tool") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });

            var subToolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            m_SubColorBtn = new Button(() => SetActiveSubTool(PaintSubTool.ColorAndAlpha))
            {
                text = "Color + Alpha",
                style = {
                    flexGrow = 1.0f,
                    height = 22,
                    borderTopLeftRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderTopRightRadius = 0,
                    borderBottomRightRadius = 0
                }
            };
            m_SubAlphaBtn = new Button(() => SetActiveSubTool(PaintSubTool.OnlyAlpha))
            {
                text = "Only Alpha",
                style = {
                    flexGrow = 1.0f,
                    height = 22,
                    borderTopLeftRadius = 0,
                    borderBottomLeftRadius = 0,
                    borderTopRightRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            subToolbar.Add(m_SubColorBtn);
            subToolbar.Add(m_SubAlphaBtn);
            m_SubToolContainer.Add(subToolbar);
            m_BrushPanel.Add(m_SubToolContainer);

            // --- Brush Settings ---
            m_BrushSettingsContainer = new VisualElement { style = { marginBottom = 15 } };
            m_BrushSettingsContainer.Add(new Label("Brush Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });

            m_SizeSlider = new Slider("Brush Size", 0.1f, 10f) { value = m_BrushSize };
            m_SizeSlider.RegisterValueChangedCallback(evt => m_BrushSize = evt.newValue);
            m_BrushSettingsContainer.Add(m_SizeSlider);

            m_StrengthSlider = new Slider("Brush Strength", 0.01f, 1.0f) { value = m_BrushStrength };
            m_StrengthSlider.RegisterValueChangedCallback(evt => m_BrushStrength = evt.newValue);
            m_BrushSettingsContainer.Add(m_StrengthSlider);

            m_FalloffToggle = new Toggle("Smooth Falloff") { value = m_SmoothFalloff };
            m_FalloffToggle.RegisterValueChangedCallback(evt => m_SmoothFalloff = evt.newValue);
            m_BrushSettingsContainer.Add(m_FalloffToggle);

            m_ColorField = new ColorField("Brush Color (RGBA)") { value = m_BrushColor };
            m_ColorField.RegisterValueChangedCallback(evt => m_BrushColor = evt.newValue);
            m_BrushSettingsContainer.Add(m_ColorField);

            m_AlphaSlider = new Slider("Brush Alpha", 0.0f, 1.0f) { value = m_BrushAlpha };
            m_AlphaSlider.RegisterValueChangedCallback(evt => m_BrushAlpha = evt.newValue);
            m_BrushSettingsContainer.Add(m_AlphaSlider);

            m_ShowFlowToggle = new Toggle("Show Flow Direction") { value = m_ShowFlowDirection };
            m_ShowFlowToggle.RegisterValueChangedCallback(evt => {
                m_ShowFlowDirection = evt.newValue;
                UpdateFlowVisualization();
            });
            m_BrushSettingsContainer.Add(m_ShowFlowToggle);

            m_BrushPanel.Add(m_BrushSettingsContainer);

            // --- Collapsible Helpboxes ---
            m_BrushPanel.Add(new Label("Help & Shortcuts") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });

            m_SelectHelpFoldout = new Foldout { text = "Select Mode Help", value = false };
            m_SelectHelpFoldout.Add(new Label("• Hides the brush gizmo.\n• Allows you to use Unity's default transform tools (Move, Rotate, Scale).\n• Safe mode for selection changes.")
            {
                style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 10 }
            });
            m_BrushPanel.Add(m_SelectHelpFoldout);

            m_PaintHelpFoldout = new Foldout { text = "Paint Mode Help", value = false };
            m_PaintHelpFoldout.Add(new Label("• Drag left-mouse in Scene View to paint.\n• Sub-tools:\n  - Color + Alpha: Paints RGB color to vertex channels, and Color's Alpha to UV2.w.\n  - Only Alpha: Paints transparency values (Brush Alpha) directly to UV2.w.\n• Escape (Esc): Switched back to Select Mode.\n• Orbit (Alt + drag) & zoom are fully preserved.")
            {
                style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 10 }
            });
            m_BrushPanel.Add(m_PaintHelpFoldout);

            m_EraseHelpFoldout = new Foldout { text = "Erase Mode Help", value = false };
            m_EraseHelpFoldout.Add(new Label("• Drag left-mouse in Scene View to erase.\n• Erases both painted colors (resets to transparent) and transparency multipliers (resets to 1.0) simultaneously.\n• Escape (Esc): Switched back to Select Mode.")
            {
                style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 10 }
            });
            m_BrushPanel.Add(m_EraseHelpFoldout);

            root.Add(m_BrushPanel);

            UpdateToolStyles();
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

            UpdateToolStyles();
            SceneView.RepaintAll();
        }

        private void SetActiveSubTool(PaintSubTool subTool)
        {
            m_ActiveSubTool = subTool;
            UpdateToolStyles();
            SceneView.RepaintAll();
        }

        private void UpdateToolStyles()
        {
            if (m_SelectBtn == null || m_PaintBtn == null || m_PaintFlowBtn == null || m_EraseBtn == null || m_EraseFlowBtn == null) return;

            Color activeColor = new Color(0.2f, 0.4f, 0.6f);

            m_SelectBtn.style.backgroundColor = m_ActiveTool == PaintTool.Select ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_PaintBtn.style.backgroundColor = m_ActiveTool == PaintTool.Paint ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_PaintFlowBtn.style.backgroundColor = m_ActiveTool == PaintTool.PaintFlow ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_EraseBtn.style.backgroundColor = m_ActiveTool == PaintTool.Erase ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            m_EraseFlowBtn.style.backgroundColor = m_ActiveTool == PaintTool.EraseFlow ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);

            if (m_SubColorBtn != null && m_SubAlphaBtn != null)
            {
                m_SubColorBtn.style.backgroundColor = m_ActiveSubTool == PaintSubTool.ColorAndAlpha ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
                m_SubAlphaBtn.style.backgroundColor = m_ActiveSubTool == PaintSubTool.OnlyAlpha ? new StyleColor(activeColor) : new StyleColor(StyleKeyword.Null);
            }

            bool isPaint = m_ActiveTool == PaintTool.Paint;
            bool isErase = m_ActiveTool == PaintTool.Erase;
            bool isPaintFlow = m_ActiveTool == PaintTool.PaintFlow;
            bool isEraseFlow = m_ActiveTool == PaintTool.EraseFlow;

            if (m_SubToolContainer != null) m_SubToolContainer.style.display = isPaint ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_BrushSettingsContainer != null) m_BrushSettingsContainer.style.display = (isPaint || isErase || isPaintFlow || isEraseFlow) ? DisplayStyle.Flex : DisplayStyle.None;

            if (m_ColorField != null) m_ColorField.style.display = (isPaint && m_ActiveSubTool == PaintSubTool.ColorAndAlpha) ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_AlphaSlider != null) m_AlphaSlider.style.display = (isPaint && m_ActiveSubTool == PaintSubTool.OnlyAlpha) ? DisplayStyle.Flex : DisplayStyle.None;

            if (m_SelectHelpFoldout != null) m_SelectHelpFoldout.style.display = m_ActiveTool == PaintTool.Select ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_PaintHelpFoldout != null) m_PaintHelpFoldout.style.display = m_ActiveTool == PaintTool.Paint ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_EraseHelpFoldout != null) m_EraseHelpFoldout.style.display = m_ActiveTool == PaintTool.Erase ? DisplayStyle.Flex : DisplayStyle.None;
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
            
            SetActiveTool(PaintTool.Paint);
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            if (m_ActivePainter != null)
            {
                var renderer = m_ActivePainter.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    renderer.sharedMaterial.SetFloat("_ShowFlowDirection", 0.0f);
                }
            }

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
                UpdateFlowVisualization();
            }
        }

        private void UpdateFlowVisualization()
        {
            if (m_ActivePainter == null) return;
            var renderer = m_ActivePainter.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var mat = renderer.sharedMaterial;
            if (mat != null)
            {
                mat.SetFloat("_ShowFlowDirection", m_ShowFlowDirection ? 1.0f : 0.0f);
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
            if (m_ActivePainter == null)
                return;

            if (m_ShowFlowDirection)
            {
                DrawFlowArrows();
            }

            if (m_ActiveTool == PaintTool.Select)
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

            // Check navigation
            bool isNavigating = current.alt || current.button == 1 || current.button == 2;
            if (isNavigating)
            {
                return;
            }

            Transform targetTransform = m_ActivePainter.transform;
            Mesh copyMesh = m_ActivePainter.CopyMesh;
            if (copyMesh == null) return;

            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlID);

            Ray worldRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Vector3 localOrigin = targetTransform.InverseTransformPoint(worldRay.origin);
            Vector3 localDirection = targetTransform.InverseTransformDirection(worldRay.direction).normalized;
            Ray localRay = new Ray(localOrigin, localDirection);

            m_HasHit = RaycastMesh(localRay, copyMesh, out Vector3 localHitPoint, out Vector3 localHitNormal, out float hitDistance);

            if (m_HasHit)
            {
                m_HitPointWorld = targetTransform.TransformPoint(localHitPoint);
                m_HitNormalWorld = targetTransform.TransformDirection(localHitNormal).normalized;

                if (m_ActiveTool == PaintTool.Erase || m_ActiveTool == PaintTool.EraseFlow)
                    Handles.color = Color.blue;
                else if (m_ActiveTool == PaintTool.PaintFlow)
                    Handles.color = Color.cyan;
                else
                    Handles.color = m_BrushColor;

                Handles.DrawWireDisc(m_HitPointWorld, m_HitNormalWorld, m_BrushSize);

                if (current.type == EventType.MouseDown && current.button == 0)
                {
                    m_LastHitPointWorld = m_HitPointWorld;
                    if (m_ActiveTool == PaintTool.Paint || m_ActiveTool == PaintTool.Erase || m_ActiveTool == PaintTool.EraseFlow)
                    {
                        PaintMesh(m_HitPointWorld, Vector3.zero, targetTransform, copyMesh);
                    }
                    current.Use();
                }
                else if (current.type == EventType.MouseDrag && current.button == 0)
                {
                    if (m_ActiveTool == PaintTool.PaintFlow)
                    {
                        Vector3 dragDirWorld = m_HitPointWorld - m_LastHitPointWorld;
                        float dragDist = dragDirWorld.magnitude;
                        if (dragDist > 0.01f)
                        {
                            Vector3 dragDirWorldNormalized = dragDirWorld / dragDist;
                            PaintMesh(m_HitPointWorld, dragDirWorldNormalized, targetTransform, copyMesh);
                            m_LastHitPointWorld = m_HitPointWorld;
                        }
                    }
                    else if (m_ActiveTool == PaintTool.Paint || m_ActiveTool == PaintTool.Erase || m_ActiveTool == PaintTool.EraseFlow)
                    {
                        PaintMesh(m_HitPointWorld, Vector3.zero, targetTransform, copyMesh);
                    }
                    current.Use();
                }
            }

            sceneView.Repaint();
        }

        private void DrawFlowArrows()
        {
            Transform targetTransform = m_ActivePainter.transform;
            Mesh copyMesh = m_ActivePainter.CopyMesh;
            if (copyMesh == null) return;

            Vector3[] vertices = copyMesh.vertices;
            Vector3[] normals = copyMesh.normals;
            Vector4[] tangents = copyMesh.tangents;
            List<Vector4> uv2 = new List<Vector4>();
            copyMesh.GetUVs(1, uv2);

            if (vertices == null || uv2 == null || uv2.Count != vertices.Length) return;

            bool hasTangents = (tangents != null && tangents.Length == vertices.Length);
            bool hasNormals = (normals != null && normals.Length == vertices.Length);

            Handles.color = Color.cyan;
            int maxArrows = 1000;
            int stride = Mathf.Max(1, vertices.Length / maxArrows);

            for (int i = 0; i < vertices.Length; i += stride)
            {
                Vector4 flowData = uv2[i];
                Vector2 flowVec = new Vector2(flowData.x, flowData.y);
                float strength = flowVec.magnitude;

                if (strength > 0.01f)
                {
                    Vector3 localPos = vertices[i];
                    Vector3 worldPos = targetTransform.TransformPoint(localPos);

                    Vector3 localNormal = hasNormals ? normals[i] : Vector3.up;
                    Vector3 worldNormal = targetTransform.TransformDirection(localNormal).normalized;

                    Vector3 localTangent = Vector3.right;
                    if (hasTangents)
                    {
                        Vector4 t = tangents[i];
                        localTangent = new Vector3(t.x, t.y, t.z);
                    }
                    Vector3 localBitangent = Vector3.Cross(localNormal, localTangent).normalized;
                    if (hasTangents)
                    {
                        localBitangent *= tangents[i].w;
                    }

                    Vector3 localFlowDir = localTangent * (flowVec.x / strength) + localBitangent * (flowVec.y / strength);
                    Vector3 worldFlowDir = targetTransform.TransformDirection(localFlowDir).normalized;

                    // Calculate arrow properties based on brush size and strength
                    float arrowLength = Mathf.Clamp(m_BrushSize * 0.5f * strength, 0.1f, 2.0f);

                    Vector3 endPoint = worldPos + worldFlowDir * arrowLength;

                    // Draw main stem
                    Handles.DrawLine(worldPos, endPoint);

                    // Draw arrowhead lying flat on the surface
                    Vector3 worldRight = Vector3.Cross(worldFlowDir, worldNormal).normalized;
                    float headSize = arrowLength * 0.3f;
                    Vector3 headLeftPoint = endPoint - worldFlowDir * headSize + worldRight * (headSize * 0.5f);
                    Vector3 headRightPoint = endPoint - worldFlowDir * headSize - worldRight * (headSize * 0.5f);

                    Handles.DrawLine(endPoint, headLeftPoint);
                    Handles.DrawLine(endPoint, headRightPoint);
                }
            }
        }

        private void PaintMesh(Vector3 hitPointWorld, Vector3 dragDirWorld, Transform targetTransform, Mesh mesh)
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
                for (int i = 0; i < vertices.Length; i++) uv2.Add(new Vector4(0f, 0f, 1f, 0f));
            }

            Vector4[] tangents = mesh.tangents;
            Vector3[] normals = mesh.normals;
            if (m_ActiveTool == PaintTool.PaintFlow && (tangents == null || tangents.Length != vertices.Length))
            {
                mesh.RecalculateTangents();
                tangents = mesh.tangents;
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
                        factor = factor * factor * (3.0f - 2.0f * factor);
                    }

                    float stepStrength = opacityStep * factor;
                    
                    if (m_ActiveTool == PaintTool.Paint && m_ActiveSubTool == PaintSubTool.ColorAndAlpha)
                    {
                        Color oldColor = colors[i];
                        float newAlpha = Mathf.Lerp(oldColor.a, 0.0f, stepStrength);
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
                    else if (m_ActiveTool == PaintTool.Erase)
                    {
                        Color oldColor = colors[i];
                        float newAlpha = Mathf.Lerp(oldColor.a, 1.0f, stepStrength);
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

                    Vector4 oldUV = uv2[i];
                    Vector4 newUV = oldUV;

                    if (m_ActiveTool == PaintTool.Paint)
                    {
                        float newW = oldUV.w;
                        if (m_ActiveSubTool == PaintSubTool.ColorAndAlpha)
                        {
                            newW = Mathf.Lerp(oldUV.w, 1.0f - m_BrushColor.a, stepStrength);
                        }
                        else if (m_ActiveSubTool == PaintSubTool.OnlyAlpha)
                        {
                            newW = Mathf.Lerp(oldUV.w, 1.0f - m_BrushAlpha, stepStrength);
                        }
                        newUV.w = newW;
                    }
                    else if (m_ActiveTool == PaintTool.Erase)
                    {
                        newUV.w = Mathf.Lerp(oldUV.w, 0.0f, stepStrength);
                    }
                    else if (m_ActiveTool == PaintTool.PaintFlow)
                    {
                        Vector3 localNormal = normals[i];
                        Vector4 localTangent4 = tangents[i];
                        Vector3 localTangent = new Vector3(localTangent4.x, localTangent4.y, localTangent4.z);
                        Vector3 localBitangent = Vector3.Cross(localNormal, localTangent).normalized * localTangent4.w;

                        Vector3 localDragDir = targetTransform.InverseTransformDirection(dragDirWorld);

                        float uDir = Vector3.Dot(localDragDir, localTangent);
                        float vDir = Vector3.Dot(localDragDir, localBitangent);
                        Vector2 flowDir = new Vector2(uDir, vDir);
                        if (flowDir.sqrMagnitude > 0.0001f)
                        {
                            flowDir.Normalize();
                        }
                        else
                        {
                            flowDir = Vector2.up;
                        }

                        Vector2 oldFlow = new Vector2(oldUV.x, oldUV.y);
                        Vector2 targetFlow = flowDir * m_BrushStrength;
                        Vector2 newFlow = Vector2.Lerp(oldFlow, targetFlow, stepStrength);

                        newUV.x = newFlow.x;
                        newUV.y = newFlow.y;
                    }
                    else if (m_ActiveTool == PaintTool.EraseFlow)
                    {
                        Vector2 oldFlow = new Vector2(oldUV.x, oldUV.y);
                        Vector2 newFlow = Vector2.Lerp(oldFlow, Vector2.zero, stepStrength);
                        newUV.x = newFlow.x;
                        newUV.y = newFlow.y;
                    }

                    if (newUV != oldUV)
                    {
                        uv2[i] = newUV;
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
  
