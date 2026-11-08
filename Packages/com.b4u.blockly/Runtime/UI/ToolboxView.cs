using System.Collections.Generic;
using Blockly.Editing;
using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// The category strip plus its flyout. Choosing a category opens a flyout with that category's blocks; grabbing one
    /// creates a real block in the main workspace and continues the drag there. The strip is also the trash: dropping a
    /// dragged block on it deletes the block.
    /// </summary>
    public sealed class ToolboxView : MonoBehaviour, IBlockSpawner
    {
        [SerializeField] RectTransform categoryList;
        [SerializeField] RectTransform flyoutPanel;
        [SerializeField] WorkspaceView flyoutView;
        [SerializeField] WorkspaceView main;

        ToolboxDefinition definition;
        readonly List<Image> buttons = new List<Image>();
        readonly List<Image> icons = new List<Image>();            // null where a category has no icon
        readonly List<TMP_Text> labels = new List<TMP_Text>();
        TMP_Text flyoutTitle;
        bool collapsed;
        Image toggleImage, toggleIcon;
        Flyout current;
        int selected = -1;

        /// <summary>Raised when the strip collapses or expands (its width changes: the layout follows).</summary>
        public event System.Action CollapsedChanged;

        /// <summary>True while the strip shows only icons. Needs <see cref="BlockTheme.ToolboxCollapsible"/>; otherwise the strip is never collapsed.</summary>
        public bool Collapsed
        {
            get => collapsed && BlockStyle.ToolboxCollapsible;
            set
            {
                if (collapsed == value) return;
                collapsed = value;
                if (collapsed) CloseFlyout();
                ApplyCollapsed();
            }
        }

        /// <summary>The width the strip has now: the theme's width, or the collapsed one.</summary>
        public float CurrentWidth => Collapsed ? BlockStyle.ToolboxCollapsedWidth : BlockStyle.ToolboxWidth;

        public int SelectedIndex => selected;
        public bool IsFlyoutOpen => flyoutPanel != null && flyoutPanel.gameObject.activeSelf;
        public Flyout CurrentFlyout => current;
        public ToolboxDefinition Definition => definition;
        public WorkspaceView Main => main;
        public WorkspaceView FlyoutView => flyoutView;

        static Color ButtonText => BlockStyle.CategoryText;

        public void Configure(WorkspaceView mainView, RectTransform list, RectTransform panel, WorkspaceView flyout)
        {
            main = mainView; categoryList = list; flyoutPanel = panel; flyoutView = flyout;
        }

        void Awake() => Initialise();

        // subscribed in Initialise as well, because EditMode never calls OnEnable
        void OnEnable() { BlockStyle.ThemeChanged -= OnThemeChanged; BlockStyle.ThemeChanged += OnThemeChanged; }
        void OnDisable() { BlockStyle.ThemeChanged -= OnThemeChanged; }
        void OnDestroy() { BlockStyle.ThemeChanged -= OnThemeChanged; }

        /// <summary>Re-colours the strip, the category buttons and the flyout background after a theme change.</summary>
        void OnThemeChanged()
        {
            if (this == null) { BlockStyle.ThemeChanged -= OnThemeChanged; return; }
            var bg = GetComponent<Image>();
            if (bg != null) bg.color = DefaultBackground;
            ApplyStripCorners();
            if (flyoutPanel != null)
            {
                var fp = flyoutPanel.GetComponent<Image>();
                if (fp != null) { fp.color = BlockStyle.FlyoutBackground; UiShape.Round(fp, BlockStyle.PanelCornerRadius, BlockStyle.FlyoutCorners); }
            }
            if (createVariableButton != null) UiShape.Round(createVariableButton.targetGraphic, BlockStyle.ButtonCornerRadius);
            if (categoryList != null)
                foreach (var t in categoryList.GetComponentsInChildren<TMP_Text>(true)) BlockStyle.ToolboxFont.Apply(t, 16);
            if (createVariableButton != null)
                foreach (var t in createVariableButton.GetComponentsInChildren<TMP_Text>(true)) BlockStyle.ToolboxFont.Apply(t, 15);
            if (createVariableButton != null && createVariableButton.targetGraphic != null) createVariableButton.targetGraphic.color = BlockStyle.VariableButton;
            ApplyCollapsed();
            PaintDeletePanel();
            if (definition != null) HighlightSelected();
        }

        void Initialise()
        {
            if (definition != null) return;
            BlockStyle.ThemeChanged -= OnThemeChanged; BlockStyle.ThemeChanged += OnThemeChanged;
            definition = StandardBlockSet.LoadToolbox();
            if (flyoutView != null) flyoutView.Spawner = this;
            if (main != null)
            {
                main.DeleteArea = (RectTransform)transform;
                main.BlockDragStarted += _ => { CloseFlyout(); SetDeletePanel(true); };
                main.BlockDragEnded += () => SetDeletePanel(false);
                main.WorkspaceEdited += RefreshOpenFlyout;
                main.DeleteHoverChanged += over =>
                {
                    var bg = GetComponent<Image>(); if (bg != null) bg.color = over ? BlockStyle.ToolboxDeleteHover : DefaultBackground;
                    overDelete = over; PaintDeletePanel();
                };
            }
            BuildButtons();
            CloseFlyout();
            OnThemeChanged();                      // a theme that was set before this scene loaded
        }

        static Color DefaultBackground => BlockStyle.ToolboxBackground;

        /// <summary>Use another toolbox (tests, custom block sets).</summary>
        public void SetDefinition(ToolboxDefinition def)
        {
            Initialise();
            definition = def;
            BuildButtons();
            CloseFlyout();
        }

        // ------------------------------------------------------------------ category buttons
        void BuildButtons()
        {
            for (int i = categoryList.childCount - 1; i >= 0; i--)
            {
                var c = categoryList.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            buttons.Clear(); icons.Clear(); labels.Clear();

            float y = 0;
            for (int i = 0; i < definition.Categories.Count; i++)
            {
                int index = i;
                var cat = definition.Categories[i];
                var go = new GameObject(cat.Name, typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(categoryList, false);
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                PlaceButton(rt, ref y);

                var img = go.GetComponent<Image>();
                go.GetComponent<Button>().targetGraphic = img;
                go.GetComponent<Button>().onClick.AddListener(() => ToggleCategory(index));
                buttons.Add(img);

                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                var lrt = (RectTransform)label.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                var icon = BlockImages.Get(IconKey(cat));
                Image iconImage = null;
                if (icon != null)
                {
                    var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    var irt = (RectTransform)ig.transform;
                    irt.SetParent(rt, false);
                    irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
                    PlaceIcon(irt);
                    var iimg = ig.GetComponent<Image>();
                    iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
                    iconImage = iimg;
                }
                icons.Add(iconImage);
                PlaceLabel(lrt, icon != null);
                var t = label.GetComponent<TextMeshProUGUI>();
                t.text = cat.Name; BlockStyle.ToolboxFont.Apply(t, 16); labels.Add(t);
                t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>The strip's corners: the theme's <c>ToolboxCorners</c>, or <c>ToolboxCornersWithFlyout</c> while the flyout is open beside it.</summary>
        void ApplyStripCorners()
        {
            var bg = GetComponent<Image>();
            if (bg != null) UiShape.Round(bg, BlockStyle.PanelCornerRadius, IsFlyoutOpen ? BlockStyle.ToolboxCornersWithFlyout : BlockStyle.ToolboxCorners);
            UpdateToggle();
        }

        /// <summary>Lays the buttons out for the current width and tells the layout that the width may have changed.</summary>
        void ApplyCollapsed()
        {
            LayoutButtons();
            UpdateToggle();
            CollapsedChanged?.Invoke();
        }

        /// <summary>The round tab on the right edge of the strip: shown when the theme allows collapsing and no flyout covers it.</summary>
        void UpdateToggle()
        {
            bool show = BlockStyle.ToolboxCollapsible && !IsFlyoutOpen && !dragging;
            if (toggleImage == null)
            {
                if (!show) return;
                var go = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 0.5f);
                toggleImage = go.GetComponent<Image>();
                go.GetComponent<Button>().targetGraphic = toggleImage;
                go.GetComponent<Button>().onClick.AddListener(() => Collapsed = !Collapsed);
                var ig = new GameObject("Chevron", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)ig.transform;
                irt.SetParent(rt, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f);
                toggleIcon = ig.GetComponent<Image>(); toggleIcon.raycastTarget = false;
            }
            toggleImage.gameObject.SetActive(show);
            if (!show) return;
            float size = BlockStyle.ToggleSize;
            var trt = (RectTransform)toggleImage.transform;
            float height = size * 1.2f;                                    // a tab that grows out of the strip's edge: flat on the strip, round outside
            trt.sizeDelta = new Vector2(size, height);
            trt.anchoredPosition = new Vector2(size * 0.5f, -(BlockStyle.ToggleTop + height * 0.5f));
            toggleImage.color = BlockStyle.ToggleColour;
            UiShape.Round(toggleImage, height * 0.5f, UiCorners.Right);
            var irect = (RectTransform)toggleIcon.transform;
            irect.sizeDelta = new Vector2(size * 0.9f, size * 0.9f);
            irect.anchoredPosition = new Vector2(-size * 0.08f, 0f);
            toggleIcon.sprite = BlockImages.Get(Collapsed ? "toolbox/expand" : "toolbox/collapse") ?? UiShape.Chevron(Collapsed);
            toggleIcon.color = BlockStyle.ToggleIconColour;
        }

        GameObject deletePanel;
        Image deleteBackground, deleteIcon;
        DashedBorder deleteBorder;
        TMP_Text deleteLabel;
        bool dragging, overDelete;

        /// <summary>While a block is dragged the strip shows the delete panel (when the theme asks for it): a dashed border, a trash icon and a label.</summary>
        void SetDeletePanel(bool on)
        {
            dragging = on; if (!on) overDelete = false;
            UpdateToggle();
            if (!on || !BlockStyle.ToolboxDeletePanel) { if (deletePanel != null) deletePanel.SetActive(false); return; }
            if (deletePanel == null)
            {
                deletePanel = new GameObject("DeletePanel", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)deletePanel.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                deleteBackground = deletePanel.GetComponent<Image>(); deleteBackground.raycastTarget = false;
                var b = new GameObject("Border", typeof(RectTransform), typeof(DashedBorder));
                var brt = (RectTransform)b.transform; brt.SetParent(rt, false);
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
                deleteBorder = b.GetComponent<DashedBorder>();
                var i = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)i.transform; irt.SetParent(rt, false);
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f);
                deleteIcon = i.GetComponent<Image>(); deleteIcon.raycastTarget = false; deleteIcon.preserveAspect = true;
                var l = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var lrt = (RectTransform)l.transform; lrt.SetParent(rt, false);
                lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(1, 0.5f); lrt.pivot = new Vector2(0.5f, 0.5f);
                deleteLabel = l.GetComponent<TextMeshProUGUI>();
                deleteLabel.alignment = TextAlignmentOptions.Center; deleteLabel.raycastTarget = false;
                deleteLabel.textWrappingMode = TextWrappingModes.NoWrap;
            }
            deletePanel.SetActive(true);
            deletePanel.transform.SetAsLastSibling();
            PaintDeletePanel();
        }

        void PaintDeletePanel()
        {
            if (deletePanel == null || !deletePanel.activeSelf) return;
            deleteBackground.color = overDelete ? BlockStyle.ToolboxDeleteHover : BlockStyle.ToolboxBackground;
            UiShape.Round(deleteBackground, BlockStyle.PanelCornerRadius, IsFlyoutOpen ? BlockStyle.ToolboxCornersWithFlyout : BlockStyle.ToolboxCorners);
            deleteBorder.Set(BlockStyle.DeleteBorderColour, BlockStyle.DeleteBorderWidth, BlockStyle.DeleteDashLength, BlockStyle.DeleteDashGap);
            float size = BlockStyle.DeleteIconSize;
            var irt = (RectTransform)deleteIcon.transform;
            irt.sizeDelta = new Vector2(size, size); irt.anchoredPosition = new Vector2(0, size * 0.25f);
            deleteIcon.sprite = BlockImages.Get("toolbox/delete") ?? UiShape.Trash();
            deleteIcon.color = BlockStyle.DeleteIconColour;
            float fs = BlockStyle.DeleteLabelSize;
            var lrt = (RectTransform)deleteLabel.transform;
            lrt.sizeDelta = new Vector2(0, fs * 1.6f); lrt.anchoredPosition = new Vector2(0, -(size * 0.25f + fs * 0.9f));
            deleteLabel.text = BlockStyle.DeleteLabel; deleteLabel.color = BlockStyle.DeleteLabelColour;
            BlockStyle.ToolboxFont.Apply(deleteLabel, fs);
        }

        /// <summary>The picture name of a category: its Icon, or <c>category/&lt;name&gt;</c> when it has none.</summary>
        static string IconKey(ToolboxCategory cat) => string.IsNullOrEmpty(cat.Icon) ? "category/" + cat.Name : cat.Icon;

        /// <summary>Width of the area the buttons live in: the strip minus the padding of the button list.</summary>
        float ListWidth()
        {
            float pad = categoryList != null ? categoryList.offsetMin.x - categoryList.offsetMax.x : 12f;
            return Mathf.Max(0f, CurrentWidth - pad);
        }

        void PlaceButton(RectTransform rt, ref float y)
        {
            float h = BlockStyle.CategoryButtonHeight;
            // collapsed: a square plate in the middle of the strip
            float inset = Collapsed ? Mathf.Max(0f, (ListWidth() - h) / 2f) : 0f;
            rt.sizeDelta = new Vector2(-2f * inset, h);
            rt.anchoredPosition = new Vector2(0, -y);
            y += h + BlockStyle.CategoryGap;
        }

        void PlaceIcon(RectTransform irt)
        {
            float size = BlockStyle.CategoryIconSize;
            irt.sizeDelta = new Vector2(size, size);
            if (Collapsed)
            {
                irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
            }
            else
            {
                irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
                irt.anchoredPosition = new Vector2(Mathf.Min(8f, (BlockStyle.CategoryButtonHeight - size) / 2f), 0);
            }
        }

        void PlaceLabel(RectTransform lrt, bool hasIcon)
        {
            float indent = Mathf.Min(8f, (BlockStyle.CategoryButtonHeight - BlockStyle.CategoryIconSize) / 2f);
            lrt.offsetMin = new Vector2(hasIcon ? indent + BlockStyle.CategoryIconSize + 6f : 10f, 0); lrt.offsetMax = new Vector2(-4, 0);
            // a category without an icon keeps its text, so the button is never empty
            lrt.gameObject.SetActive(!Collapsed || !hasIcon);
        }

        /// <summary>Applies the theme's button size, gap and icon size, and the collapsed state, to the buttons that are already there.</summary>
        void LayoutButtons()
        {
            if (categoryList == null) return;
            float y = 0;
            for (int i = 0; i < categoryList.childCount; i++)
            {
                var rt = (RectTransform)categoryList.GetChild(i);
                PlaceButton(rt, ref y);
                var icon = rt.Find("Icon") as RectTransform;
                if (icon != null) PlaceIcon(icon);
                var text = rt.Find("Text") as RectTransform;
                if (text != null) PlaceLabel(text, icon != null);
            }
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>
        /// Paints the category buttons: each plate is the theme's <see cref="BlockTheme.CategoryPlate"/> (or its category colour, dimmed, when
        /// that is transparent); the selected one uses <see cref="BlockTheme.CategoryPlateSelected"/> or a sprite registered as
        /// <c>plate/&lt;category name&gt;</c> (see <see cref="BlockImages"/>), and an icon registered as <c>&lt;icon&gt;@selected</c> replaces its icon.
        /// </summary>
        void HighlightSelected()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var cat = definition.Categories[i];
                bool on = i == selected;
                var baseColour = BlockStyle.Parse(cat.ColourHex);
                var plate = on ? BlockStyle.CategoryPlateSelected : BlockStyle.CategoryPlate;
                var picture = on ? BlockImages.Get("plate/" + cat.Name) : null;
                var img = buttons[i];
                if (picture != null)
                {
                    img.sprite = picture; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; img.color = Color.white;
                }
                else
                {
                    if (img.sprite != null && img.sprite == BlockImages.Get("plate/" + cat.Name)) { img.sprite = null; img.type = Image.Type.Simple; img.pixelsPerUnitMultiplier = 1f; }   // the picture of the selected plate
                    img.color = plate.a > 0f ? plate : on ? baseColour : Shade(baseColour, BlockStyle.CategoryShade);
                    UiShape.Round(img, BlockStyle.ButtonCornerRadius);
                }
                if (icons[i] != null)
                    icons[i].sprite = (on ? BlockImages.Get(IconKey(cat) + "@selected") : null) ?? BlockImages.Get(IconKey(cat)) ?? icons[i].sprite;
                if (labels[i] != null) labels[i].color = on ? BlockStyle.CategoryTextSelected : ButtonText;
            }
        }

        // ------------------------------------------------------------------ flyout
        public void ToggleCategory(int index)
        {
            if (IsFlyoutOpen && selected == index) CloseFlyout(); else SelectCategory(index);
        }

        public void SelectCategory(int index)
        {
            Initialise();
            if (main == null || main.Workspace == null || index < 0 || index >= definition.Categories.Count) return;
            selected = index;
            flyoutPanel.gameObject.SetActive(true);                 // before measuring: TMP needs an active hierarchy
            current = Flyout.Build(definition.Categories[index], main.Workspace, flyoutView.Measurer);
            flyoutView.SetWorkspace(current.Workspace);
            var rt = (RectTransform)flyoutView.transform;
            float pad = BlockStyle.FlyoutPadding;
            rt.sizeDelta += new Vector2(2 * pad, 2 * pad);           // padding inside the scroll area
            rt.anchoredPosition = new Vector2(pad, -pad);
            ShowFlyoutTitle(definition.Categories[index].Name);
            ApplyStripCorners();
            if (current.HeaderHeight > 0) BuildCreateVariableButton();
            HighlightSelected();
        }

        /// <summary>The name of the open category above its blocks, when the theme asks for it; the blocks scroll below it.</summary>
        void ShowFlyoutTitle(string text)
        {
            var scroll = flyoutPanel.GetComponent<ScrollRect>();
            var viewport = scroll != null && scroll.viewport != null ? scroll.viewport : null;
            bool show = BlockStyle.FlyoutTitle;
            float height = show ? BlockStyle.FlyoutTitleHeight : 0f;
            if (viewport != null) viewport.offsetMax = new Vector2(viewport.offsetMax.x, -height);
            if (flyoutTitle == null && show)
            {
                var go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rt = (RectTransform)go.transform;
                rt.SetParent(flyoutPanel, false);
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1);
                flyoutTitle = go.GetComponent<TextMeshProUGUI>();
                flyoutTitle.alignment = TextAlignmentOptions.MidlineLeft; flyoutTitle.raycastTarget = false;
                flyoutTitle.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (flyoutTitle == null) return;
            flyoutTitle.gameObject.SetActive(show);
            if (!show) return;
            var trt = flyoutTitle.rectTransform;
            trt.offsetMin = new Vector2(BlockStyle.FlyoutPadding + 8f, -height); trt.offsetMax = new Vector2(-8f, 0f);
            flyoutTitle.text = text; flyoutTitle.color = BlockStyle.FlyoutTitleColour;
            BlockStyle.ToolboxFont.Apply(flyoutTitle, BlockStyle.FlyoutTitleSize);
            titleAligned = false; titleFrames = 0;
        }

        bool titleAligned;
        int titleFrames;

        // The scroll view puts the list in place during its first frames, so the title is aligned once that is done; after that the
        // title stays where it is, however the list is scrolled.
        void LateUpdate() { if (!titleAligned && ++titleFrames > 2) AlignTitle(); }

        /// <summary>Starts the flyout title at the leftmost drawn edge of the blocks as they are now (an output tab counts), minus the side bearing of the font.</summary>
        public void AlignTitle()
        {
            if (flyoutTitle == null || !flyoutTitle.gameObject.activeSelf || flyoutView == null || !IsFlyoutOpen) return;
            var trt = flyoutTitle.rectTransform;
            var parent = (RectTransform)trt.parent;
            var corners = new Vector3[4];
            float left = float.MaxValue;
            foreach (var g in flyoutView.GetComponentsInChildren<PolygonGraphic>())
            {
                if (g.Polygon == null || g.Polygon.Length == 0) continue;
                g.rectTransform.GetWorldCorners(corners);
                float minX = float.MaxValue;
                foreach (var v in g.Polygon) minX = Mathf.Min(minX, (float)v.X);
                left = Mathf.Min(left, parent.InverseTransformPoint(corners[0]).x + minX * g.transform.lossyScale.x / parent.lossyScale.x);
            }
            if (left == float.MaxValue) return;
            flyoutTitle.ForceMeshUpdate();
            float bearing = flyoutTitle.textInfo.characterCount > 0 ? flyoutTitle.textInfo.characterInfo[0].bottomLeft.x - trt.rect.xMin : 0f;
            trt.offsetMin = new Vector2(left - parent.rect.xMin - bearing, trt.offsetMin.y);
            titleAligned = true;
        }

        public void CloseFlyout()
        {
            Initialise();
            selected = -1;
            current = null;
            if (flyoutPanel != null) flyoutPanel.gameObject.SetActive(false);
            ApplyStripCorners();
            if (buttons.Count > 0) HighlightSelected();
        }

        void RefreshOpenFlyout()
        {
            // variables / procedures changed: dynamic categories must reflect that
            if (IsFlyoutOpen && selected >= 0) SelectCategory(selected);
        }

        public const string CreateVariableButtonName = "Create variable";

        Button createVariableButton;
        public Button VariableButton => createVariableButton;

        void BuildCreateVariableButton()
        {
            var go = new GameObject(CreateVariableButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(flyoutView.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(8, -6); rt.sizeDelta = new Vector2(190, 32);
            var img = go.GetComponent<Image>();
            img.color = BlockStyle.VariableButton;
            UiShape.Round(img, BlockStyle.ButtonCornerRadius);
            createVariableButton = go.GetComponent<Button>();
            createVariableButton.targetGraphic = img;
            createVariableButton.onClick.AddListener(() => main.RequestCreateVariable());

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var t = label.GetComponent<TextMeshProUGUI>();
            t.text = "Create variable..."; BlockStyle.ToolboxFont.Apply(t, 15); t.color = Color.white; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        }

        // ------------------------------------------------------------------ spawning
        public bool TrySpawn(Block flyoutBlock, PointerEventData e, WorkspaceView source)
        {
            var spec = current?.SpecOf(flyoutBlock);
            if (spec == null || main == null || main.Workspace == null) return false;
            var cam = e.pressEventCamera;

            // keep the grab point: the new block appears under the pointer exactly where the flyout block was held
            var inFlyout = source.ScreenToWorkspace(e.pressPosition, cam);
            var box = source.Layout.ByBlock[flyoutBlock];
            var inside = new Vector2(inFlyout.x - box.X, inFlyout.y - box.Y);
            var inMain = main.ScreenToWorkspace(e.pressPosition, cam);

            var ws = main.Workspace;
            string before = main.History?.Snapshot();                // taken before the block exists: creating + dropping it is ONE undo step
            var block = WorkspaceEditing.Spawn(ws, spec);
            block.X = inMain.x - inside.x; block.Y = inMain.y - inside.y;
            ws.TopBlocks.Add(block);

            if (!main.BeginDrag(block, inMain, isNew: true, stateBefore: before))
            {
                ws.TopBlocks.Remove(block);
                return false;
            }
            e.pointerDrag = main.gameObject;                       // the rest of the gesture belongs to the main workspace
            main.OnDrag(e);
            return true;
        }
    }
}
