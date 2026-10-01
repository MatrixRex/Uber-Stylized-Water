using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UberStylizedWater.Demo
{
    /// <summary>
    /// Demo UI that switches between the water presets (the children of <see cref="presetRoot"/>).
    /// One text button is created per preset from <see cref="buttonTemplate"/>; only the selected preset is active.
    /// The panel lays itself out at the bottom of the canvas: a 2 column grid in portrait, a wider grid in landscape,
    /// and it respects the device safe area.
    /// </summary>
    public class WaterPresetSwitcher : UIBehaviour
    {
        [Header("Presets")]
        [Tooltip("Parent whose direct children are the water presets. Only one child is active at a time.")]
        [SerializeField] Transform presetRoot;

        [Header("UI")]
        [SerializeField] RectTransform panel;
        [SerializeField] GridLayoutGroup grid;
        [Tooltip("Disabled button used as the source for every preset button.")]
        [SerializeField] Button buttonTemplate;

        [Header("Layout (canvas units)")]
        [SerializeField] int columnsPortrait = 2;
        [SerializeField] int columnsLandscape = 4;
        [SerializeField] float maxPanelWidth = 1400f;
        [SerializeField] float margin = 32f;
        [SerializeField] float buttonHeight = 110f;
        [SerializeField] float spacing = 16f;

        [Header("Style")]
        [SerializeField] Color selectedColor = new Color(0.55f, 0.82f, 1f, 1f);

        // "Water Template Anime" -> "Anime" (also matches the "Tempate" typo in one of the prefab names).
        static readonly Regex NamePrefix = new Regex(@"^Water Temp(l)?ate\s*", RegexOptions.IgnoreCase);

        readonly List<GameObject> presets = new List<GameObject>();
        readonly List<Image> buttonImages = new List<Image>();
        int current = -1;

        protected override void Start()
        {
            base.Start();
            if (!Application.isPlaying) return;

            BuildButtons();
            UpdateLayout();
        }

        void BuildButtons()
        {
            if (presetRoot == null || buttonTemplate == null || grid == null)
            {
                Debug.LogWarning("WaterPresetSwitcher is missing references.", this);
                return;
            }

            int active = -1;
            for (int i = 0; i < presetRoot.childCount; i++)
            {
                GameObject preset = presetRoot.GetChild(i).gameObject;
                int index = i;

                Button button = Instantiate(buttonTemplate, grid.transform);
                button.gameObject.name = preset.name;
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<Text>().text = NamePrefix.Replace(preset.name, "");
                button.onClick.AddListener(() => SetPreset(index));

                presets.Add(preset);
                buttonImages.Add(button.GetComponent<Image>());
                if (active < 0 && preset.activeSelf) active = i;
            }

            SetPreset(active >= 0 ? active : 0);
        }

        public void SetPreset(int index)
        {
            if (index < 0 || index >= presets.Count) return;

            current = index;
            for (int i = 0; i < presets.Count; i++)
            {
                presets[i].SetActive(i == index);
                buttonImages[i].color = i == index ? selectedColor : Color.white;
            }
        }

        public void NextPreset() => SetPreset((current + 1) % presets.Count);
        public void PreviousPreset() => SetPreset((current - 1 + presets.Count) % presets.Count);

        // Fires when the canvas size changes: window resize, device rotation, resolution change.
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateLayout();
        }

        void UpdateLayout()
        {
            if (!isActiveAndEnabled || panel == null || grid == null) return;

            var canvasRect = (RectTransform)transform;
            Vector2 size = canvasRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;

            // Safe area (notches, home indicator) converted from screen pixels to canvas units.
            Rect safe = Screen.safeArea;
            float scaleX = size.x / Mathf.Max(1, Screen.width);
            float scaleY = size.y / Mathf.Max(1, Screen.height);
            float left = safe.xMin * scaleX;
            float right = (Screen.width - safe.xMax) * scaleX;
            float bottom = safe.yMin * scaleY;

            bool portrait = size.y > size.x;
            int columns = Mathf.Max(1, portrait ? columnsPortrait : columnsLandscape);

            float available = size.x - left - right - margin * 2f;
            float width = Mathf.Min(available, maxPanelWidth);

            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2((left - right) * 0.5f, bottom + margin);
            panel.sizeDelta = new Vector2(width, panel.sizeDelta.y);

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset((int)spacing, (int)spacing, (int)spacing, (int)spacing);

            float cellWidth = (width - grid.padding.horizontal - spacing * (columns - 1)) / columns;
            grid.cellSize = new Vector2(Mathf.Max(1f, cellWidth), buttonHeight);
        }
    }
}
