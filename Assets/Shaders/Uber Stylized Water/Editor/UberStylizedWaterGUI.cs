using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UberStylizedWater.Editor
{
    /// <summary>
    /// Material inspector for the Uber Stylized Water shader (compiled and Shader Graph versions).
    /// Each feature gets a collapsible header; a disabled feature hides its properties.
    /// </summary>
    public class UberStylizedWaterGUI : ShaderGUI
    {
        const string GuideUrl = "https://matrixrex.github.io/Uber-Stylized-Water/#/";
        const string PropsUrl = GuideUrl + "usage-guide/shader-properties/";
        const string PlanarSetupUrl = GuideUrl + "usage-guide/Additional-Components/planner-reflection-setup";
        const string PrefsPrefix = "UberStylizedWater.Section.";

        // Feature toggles that also drive a shader keyword (property name == keyword).
        static readonly string[] KeywordToggles =
        {
            "_ENABLESHORELINE", "_ENABLE_UNDERWATERLAYER", "_ENABLENORMAL",
            "_ENABLEPLANERREFLECTION", "_ENABLEREFRACTION", "_ENABLECAUSTICS", "_ENABLEWAVE",
        };

        static readonly Color HeaderColor = new Color(0.1f, 0.1f, 0.1f, 0.2f);
        static readonly Color HeaderColorLight = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color HeaderLine = new Color(0.12f, 0.12f, 0.12f, 1f);
        static readonly Color HeaderLineLight = new Color(0.6f, 0.6f, 0.6f, 1f);

        MaterialEditor _editor;
        MaterialProperty[] _props;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            _editor = materialEditor;
            _props = properties;
            // Standard inspector widths so every field starts at the same column.
            EditorGUIUtility.labelWidth = 0f;
            EditorGUIUtility.fieldWidth = 0f;
            bool wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            DrawTopBar();
            DrawPipelineWarnings();

            Section("Base", "Water color, depth and shore fade.", "shader-prop-base", null, DrawBase);
            Section("Surface Foam", "Moving foam pattern on the water surface.", "shader-prop-SurfacefoamDistortion", "_Enable_SurfaceFoam", DrawSurfaceFoam);
            if (IsOn("_Enable_SurfaceFoam") || IsOn("_Enable_Intersection"))
                Section("Surface Distortion", "Distorts Surface Foam and Intersection Foam for extra motion.", "shader-prop-SurfacefoamDistortion", null, DrawSurfaceDistortion);
            Section("Intersection Foam", "Foam where the water touches other objects.", "shader-prop-intersection", "_Enable_Intersection", DrawIntersection);
            Section("Shoreline", "Animated foam lines moving towards or away from the shore.", "shader-prop-shoreline", "_ENABLESHORELINE", DrawShoreline);
            Section("Underwater Layer", "A second foam layer under the surface, e.g. foam shadows.", "shader-prop-underwater", "_ENABLE_UNDERWATERLAYER", DrawUnderwater);
            Section("Normal Map", "Fake micro waves. Affects lighting, reflection and refraction.", "shader-prop-normal", "_ENABLENORMAL", DrawNormal);
            Section("Lighting", "Shadow color and specular highlights.", "shader-prop-lighting", null, DrawLighting);
            Section("Reflection", "Reflection probes, with optional planar reflection.", "shader-prop-reflection", null, DrawReflection);
            Section("Refraction", "Bends the view of objects under the water.", "shader-prop-refraction", "_ENABLEREFRACTION", DrawRefraction);
            Section("Caustics", "Animated light patterns projected on underwater geometry.", "shader-prop-caustics", "_ENABLECAUSTICS", DrawCaustics);
            Section("Wave", "Two Gerstner waves that move the surface vertices.", "shader-prop-wave", "_ENABLEWAVE", DrawWave);
            Section("Advanced", "Render queue and instancing.", null, null, DrawAdvanced);

            EditorGUIUtility.wideMode = wideMode;
        }

        // ------------------------------------------------------------------
        // Sections

        void DrawBase()
        {
            Prop("_Color_Shallow", "Shallow Color", "Color of shallow water. Alpha controls water opacity.");
            Prop("_Color_Deep", "Deep Color", "Color of deep water. Alpha controls water opacity.");
            Prop("_Water_Depth", "Depth", "Depth where the shallow color blends into the deep color.");
            Prop("_WorldSpaceDepth", "World Space Depth", "On: depth is measured in world space and stays stable when the camera rotates.\nOff: screen space depth.");
            Prop("_DistanceMask_Start", "Distance Start", "Distance from the camera where far-distance values (e.g. Normal Distance Strength) start.");
            Prop("_DistanceMask_Fade", "Distance Fade", "Smoothness of the near to far transition.");
            if (Prop("_ShoreFade", "Shore Fade", "Fades the water edge where it touches geometry. Applied over every layer."))
                Indented(() => Prop("_ShoreFade_Smoothness", "Smoothness", "Smoothness of the shore fade."));
        }

        void DrawSurfaceFoam()
        {
            Texture("_SurfFoam_Map", "Foam Map", "Foam pattern texture. Required.", true);
            Prop("_SurfFoam_Color", "Color", "Foam color. Alpha controls foam strength.");
            Prop("_SurfFoam_AlphaBlend", "Alpha Blend", "Blends the foam alpha with the water alpha.");
            Prop("_Invert_SurfFoam", "Invert", "Inverts the foam map.");
            Prop("_SurfFoam_Scale", "Scale", "Size of the foam pattern.");
            Vector("_SurfFoam_Tile", 2, "Tiling", "Per axis tiling. Usually kept at 1:1.");
            Vector("_SurfFoam_Pan", 2, "Pan Speed", "Movement speed on X and Y.");
            Prop("_SurfFoam_Edge", "Edge", "Thickness of the foam. Works best with a blurry foam map.");
            Prop("_SurfFoam_EdgeSmooth", "Edge Smoothness", "Softens the foam edge. A small value removes jagged edges.");
        }

        void DrawSurfaceDistortion()
        {
            Texture("_SurfaceDistortion_Map", "Distortion Map", "Texture used to distort the foam. Required.", true);
            Prop("_SurfaceDistortion_Strength", "Strength", "Amount of distortion.");
            Prop("_SurfaceDistortion_Scale", "Scale", "Size of the distortion pattern.");
            Vector("_SurfaceDistortion_Pan", 2, "Pan Speed", "Movement speed on X and Y.");
        }

        void DrawIntersection()
        {
            Prop("_InterSec_Color", "Color", "Foam color. Alpha controls foam strength.");
            Prop("_InterSec_Width", "Width", "Width of the foam around intersecting objects.");
            Prop("_InterSec_Edge_Fade", "Edge Fade", "Fades the outer edge of the foam.");
            Header("Mask");
            Texture("_InterSec_Foam_Mask", "Foam Mask", "Optional. Without a mask the foam is a clean line.", false);
            Prop("_InterSec_Foam_Invert", "Invert", "Inverts the foam mask.");
            Prop("_InterSec_Dissolve", "Dissolve", "How much the mask breaks up the foam. 0 gives a clean line.");
            Prop("_InterSec_Foam_Smooth", "Dissolve Smoothness", "How soft or sharp the dissolve is.");
            Prop("_InterSec_GradientDissolve", "Gradient Dissolve", "On: dissolve is weaker near the intersection and stronger further away.\nOff: dissolve is even.");
            Prop("_InterSec_Foam_Distortion", "Distortion", "Strength of the Surface Distortion applied to the mask.");
            Prop("_InterSec_Foam_Scale", "Scale", "Size of the mask pattern.");
            Vector("_InterSec_Foam_Tile", 2, "Tiling", "Per axis tiling. Usually kept at 1:1.");
            Vector("_InterSec_Foam_Pan", 2, "Pan Speed", "Movement speed on X and Y.");
        }

        void DrawShoreline()
        {
            Prop("_SL_Color", "Color", "Foam line color. Alpha controls strength.");
            Prop("_SL_WaterDepth", "Depth", "Like Base Depth, but only for the shoreline foam.");
            Prop("_SL_Speed", "Speed", "Speed of the moving lines. Negative values reverse the direction.");
            Prop("_SL_Ammount", "Amount", "Number of foam lines.");
            Prop("_SL_Thickness", "Thickness", "Thickness of the foam lines.");
            Prop("_SL_CenterMask", "Center Mask", "Hides the lines towards the water center, limiting how far they reach from the shore.");
            Prop("_SL_CenterMaskFade", "Center Mask Fade", "Smoothness of the center mask.");
            if (Prop("_SL_EnableTrail", "Trail", "Adds a fading trail behind each foam line."))
                Indented(() => Prop("_SL_Trail_Fade", "Trail Fade", "Fade strength of the trail."));
            Header("Dissolve");
            Texture("_SL_Dissolve_Mask", "Dissolve Mask", "Texture used to break up the lines.", false);
            Prop("_SL_Dissolve", "Dissolve", "Amount of dissolve.");
            Prop("_SL_GradientDissolve", "Shore Gradient", " 0: even dissolve.\n 1: dissolves more towards the water center (shoreline widens).\n-1: dissolves more towards the shore (shoreline shrinks).");
            Prop("_SL_MaskScale", "Scale", "Size of the dissolve pattern.");
            Vector("_SL_MaskTile", 2, "Tiling", "Per axis tiling. Usually kept at 1:1.");
            Vector("_SL_MaskPan", 2, "Pan Speed", "Movement speed on X and Y.");
        }

        void DrawUnderwater()
        {
            if (!IsOn("_Enable_SurfaceFoam") && !IsOn("_Enable_Intersection"))
                EditorGUILayout.HelpBox("Repeats the Surface / Intersection foam under the water. Enable one of them to see this layer.", MessageType.Info);
            Prop("_Underwater_Color", "Color", "Layer color. Alpha controls strength.");
            Prop("_UnderWater_Depth", "Depth", "How far below the surface the layer sits. Use negative values.");
            Prop("_ConformToGeometry", "Conform To Geometry", "On: the layer follows the underwater geometry.\nOff: the layer stays parallel to the surface.");
            Prop("_UnderWater_ScaleModifier", "Scale Modifier", "Size relative to the surface foam. 0 = same, 1 = double, -1 = half.");
            Prop("_UnderWater_Start", "Distance Start", "Distance from the camera where the layer starts to disappear.");
            Prop("_Underwater_Fade", "Distance Fade", "Smoothness of the distance fade.");
        }

        void DrawNormal()
        {
            Texture("_Normal_Map", "Normal Map", "Normal texture. Sampled twice in opposite directions for natural motion.", false);
            Prop("_Normal_Strength", "Strength", "Normal strength close to the camera.");
            Prop("_Normal_DistanceStrength", "Distance Strength", "Normal strength far away. Lower values reduce noise. Distance is set in Base > Distance Start.");
            Prop("_Normal_Scale", "Scale", "Size of the normal pattern.");
            Prop("_Normal_Pan", "Pan Speed", "Single speed value because of dual sampling.");
        }

        void DrawLighting()
        {
            Prop("_ShadowColor", "Shadow Color", "Color of shadows on the water. Alpha controls shadow strength.");
            Prop("_Specular_Color", "Specular Color", "Highlight color. Overrides the light color. Use HDR intensity for brightness, 0 removes highlights.");
            Prop("_Specular_Spread", "Spread", "0 gives a focused highlight, higher values give softer and wider highlights.");
            Prop("_Specular_Hardness", "Hardness", "Blends between soft and hard highlight edges.");
            var hardness = Find("_Specular_Hardness");
            using (new EditorGUI.DisabledScope(hardness != null && !hardness.hasMixedValue && hardness.floatValue <= 0f))
                Prop("_Specular_Size", "Size", "Size of the hard highlight. Needs Hardness above 0.");
        }

        void DrawReflection()
        {
            Prop("_Reflection_Strength", "Strength", "Opacity of the reflection.");
            Prop("_Reflection_Fresnel", "Fresnel", "Higher values hide the reflection when looking straight down.");
            Prop("_Reflection_Distortion", "Distortion", "How much the normal map distorts the reflection.");
            if (Prop("_ENABLEPLANERREFLECTION", "Planar Reflection", "Real-time mirror reflection. Needs a Planar Reflection Volume in the scene. Off: reflection probes only."))
            {
                if (UnityEngine.Object.FindAnyObjectByType<PlanarReflectionVolume>() == null)
                    HelpBoxWithButton("No Planar Reflection Volume found in the open scene.", MessageType.Warning, "Setup Guide", () => Application.OpenURL(PlanarSetupUrl));
            }
        }

        void DrawRefraction()
        {
            Prop("_Refraction_Strength", "Strength", "Amount of light bending. Not affected by Normal Strength.");
            Prop("_Refraction_Distance_Strength", "Distance Strength", "Refraction strength far away.");
            Prop("_Refraction_Distance_Fade", "Distance Fade", "How quickly refraction fades with distance.");
        }

        void DrawCaustics()
        {
            Texture("_Caustics_Map", "Caustics Map", "Caustics pattern texture. Required.", true);
            Prop("_Caustics_Strength", "Strength", "Brightness of the caustics.");
            Prop("_Caustics_Depth", "Depth", "How far below the surface the caustics appear. Use negative values.");
            Prop("_Caustics_Scale", "Scale", "Size of the caustics pattern.");
            Prop("_Caustics_Pan", "Pan Speed", "Single speed value because of dual sampling.");
            Prop("_Caustics_Start", "Distance Start", "Distance from the camera where caustics start to fade.");
            Prop("_Caustics_Fade", "Distance Fade", "Smoothness of the distance fade.");
            Header("Distortion");
            Texture("_Caustics_Distortion_Map", "Distortion Map", "Optional. Warps the caustics for extra motion.", false);
            Prop("_Caustics_Distortion_Strength", "Strength", "Amount of distortion.");
            Prop("_Caustics_Distortion_Scale", "Scale", "Size of the distortion pattern.");
        }

        void DrawWave()
        {
            Prop("_Wave_Top_Color", "Crest Color", "Color added on top of the waves. Alpha controls strength.");
            Header("Wave 1");
            WaveProps("_1st");
            Header("Wave 2");
            WaveProps("_2nd");
        }

        void WaveProps(string prefix)
        {
            Prop(prefix + "_Wave_Length", "Length", "Distance between wave peaks.");
            Prop(prefix + "_Wave_Height", "Height", "Wave height in meters. Usually very small.");
            Prop(prefix + "_Wave_Speed", "Speed", "Movement speed of the wave.");
            Vector(prefix + "_Wave_Direction", 3, "Direction", "Movement direction. Uses the X and Z components.");
            Prop(prefix + "_Wave_Sharpness", "Sharpness", "Sharpness of the wave crest.");
        }

        void DrawAdvanced()
        {
            _editor.RenderQueueField();
            _editor.EnableInstancingField();
            _editor.DoubleSidedGIField();
        }

        // ------------------------------------------------------------------
        // Project checks

        void DrawPipelineWarnings()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp))
                return;

            if (!urp.supportsCameraDepthTexture)
                HelpBoxWithButton("The water needs the Depth Texture. Enable it in the active URP Asset.", MessageType.Warning, "Enable", () =>
                {
                    Undo.RecordObject(urp, "Enable Depth Texture");
                    urp.supportsCameraDepthTexture = true;
                    EditorUtility.SetDirty(urp);
                });

            if (IsOn("_ENABLEREFRACTION") && !urp.supportsCameraOpaqueTexture)
                HelpBoxWithButton("Refraction needs the Opaque Texture. Enable it in the active URP Asset.", MessageType.Warning, "Enable", () =>
                {
                    Undo.RecordObject(urp, "Enable Opaque Texture");
                    urp.supportsCameraOpaqueTexture = true;
                    EditorUtility.SetDirty(urp);
                });
        }

        // ------------------------------------------------------------------
        // Keywords

        public override void ValidateMaterial(Material material)
        {
            foreach (var keyword in KeywordToggles)
            {
                if (material.HasProperty(keyword))
                    SetKeyword(material, keyword, material.GetFloat(keyword) > 0.5f);
            }
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ValidateMaterial(material);
        }

        static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        // ------------------------------------------------------------------
        // Drawing helpers

        void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Uber Stylized Water", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Guide", "Open the online guide."), EditorStyles.miniButton, GUILayout.Width(50f)))
                    Application.OpenURL(GuideUrl + "usage-guide/Usage-Guide");
            }
        }

        /// <summary>Draws a feature header and, when the feature is enabled and expanded, its content.</summary>
        void Section(string title, string tooltip, string page, string toggleName, Action content)
        {
            var toggle = toggleName != null ? Find(toggleName) : null;
            bool enabled = toggle == null || toggle.hasMixedValue || toggle.floatValue > 0.5f;

            EditorGUILayout.Space(2f);
            var rect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));

            var bg = rect;
            bg.xMin = 0f;
            bg.width += 4f;
            EditorGUI.DrawRect(bg, EditorGUIUtility.isProSkin ? HeaderColor : HeaderColorLight);
            EditorGUI.DrawRect(new Rect(bg.x, bg.y, bg.width, 1f), EditorGUIUtility.isProSkin ? HeaderLine : HeaderLineLight);

            string prefKey = PrefsPrefix + title;
            bool expanded = EditorPrefs.GetBool(prefKey, true);

            var arrowRect = new Rect(rect.x, rect.y + 2f, 13f, 16f);
            var toggleRect = new Rect(arrowRect.xMax + 2f, rect.y + 2f, 16f, 16f);
            float labelX = (toggle != null ? toggleRect.xMax : arrowRect.xMax) + 4f;
            var helpRect = new Rect(rect.xMax - 18f, rect.y + 2f, 16f, 16f);
            var labelRect = new Rect(labelX, rect.y + 1f, helpRect.x - labelX - 2f, 18f);

            bool newExpanded = expanded;
            if (enabled)
                newExpanded = GUI.Toggle(arrowRect, expanded, GUIContent.none, EditorStyles.foldout);

            if (toggle != null)
            {
                EditorGUI.showMixedValue = toggle.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                bool on = EditorGUI.Toggle(toggleRect, toggle.floatValue > 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    SetToggle(toggle, on, title);
                    enabled = on;
                }
                EditorGUI.showMixedValue = false;
            }

            using (new EditorGUI.DisabledScope(!enabled))
                EditorGUI.LabelField(labelRect, new GUIContent(title, tooltip), EditorStyles.boldLabel);

            if (page != null && GUI.Button(helpRect, EditorGUIUtility.TrIconContent("_Help", $"Open the {title} guide."), EditorStyles.iconButton))
                Application.OpenURL(PropsUrl + page);

            var e = Event.current;
            if (enabled && e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
            {
                newExpanded = !newExpanded;
                e.Use();
            }

            if (newExpanded != expanded)
                EditorPrefs.SetBool(prefKey, newExpanded);

            if (enabled && newExpanded)
            {
                EditorGUILayout.Space(2f);
                content();
                EditorGUILayout.Space(4f);
            }
        }

        void SetToggle(MaterialProperty prop, bool on, string undoName)
        {
            _editor.RegisterPropertyChangeUndo(undoName);
            prop.floatValue = on ? 1f : 0f;
            if (Array.IndexOf(KeywordToggles, prop.name) >= 0)
            {
                foreach (var target in _editor.targets)
                    SetKeyword((Material)target, prop.name, on);
            }
        }

        /// <summary>Draws a property. Returns true if it is a toggle that is on (or mixed), for nesting child properties.</summary>
        bool Prop(string name, string label, string tooltip)
        {
            var prop = Find(name);
            if (prop == null)
                return false;

            var content = new GUIContent(label, tooltip);
            if (Array.IndexOf(KeywordToggles, name) >= 0)
            {
                // Drawn manually so the keyword stays in sync for every selected material.
                EditorGUI.showMixedValue = prop.hasMixedValue;
                EditorGUI.BeginChangeCheck();
                bool on = EditorGUILayout.Toggle(content, prop.floatValue > 0.5f);
                if (EditorGUI.EndChangeCheck())
                    SetToggle(prop, on, label);
                EditorGUI.showMixedValue = false;
            }
            else
            {
                _editor.ShaderProperty(prop, content);
            }

            return prop.hasMixedValue || prop.floatValue > 0.5f;
        }

        /// <summary>Draws a 2 or 3 component vector on one line, aligned with the other fields.</summary>
        void Vector(string name, int dimensions, string label, string tooltip)
        {
            var prop = Find(name);
            if (prop == null)
                return;

            var rect = EditorGUILayout.GetControlRect();
            var fieldRect = EditorGUI.PrefixLabel(rect, new GUIContent(label, tooltip));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            Vector4 value = prop.vectorValue;
            if (dimensions == 2)
            {
                Vector2 v = EditorGUI.Vector2Field(fieldRect, GUIContent.none, value);
                value = new Vector4(v.x, v.y, value.z, value.w);
            }
            else
            {
                Vector3 v = EditorGUI.Vector3Field(fieldRect, GUIContent.none, value);
                value = new Vector4(v.x, v.y, v.z, value.w);
            }
            if (EditorGUI.EndChangeCheck())
                prop.vectorValue = value;
            EditorGUI.showMixedValue = false;

            EditorGUI.indentLevel = indent;
        }

        void Texture(string name, string label, string tooltip, bool required)
        {
            var prop = Find(name);
            if (prop == null)
                return;

            _editor.TexturePropertySingleLine(new GUIContent(label, tooltip), prop);
            if (required && !prop.hasMixedValue && prop.textureValue == null)
                EditorGUILayout.HelpBox($"Assign a {label} for this effect to work.", MessageType.Warning);
        }

        static void Header(string label)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        }

        static void Indented(Action draw)
        {
            EditorGUI.indentLevel++;
            draw();
            EditorGUI.indentLevel--;
        }

        static void HelpBoxWithButton(string message, MessageType type, string button, Action onClick)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(message, type);
                if (GUILayout.Button(button, GUILayout.Width(80f), GUILayout.ExpandHeight(true)))
                    onClick();
            }
        }

        bool IsOn(string name)
        {
            var prop = Find(name);
            return prop != null && (prop.hasMixedValue || prop.floatValue > 0.5f);
        }

        MaterialProperty Find(string name) => FindProperty(name, _props, false);
    }
}
