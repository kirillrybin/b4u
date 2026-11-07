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
        Flyout current;
        int selected = -1;

        public int SelectedIndex => selected;
        public bool IsFlyoutOpen => flyoutPanel != null && flyoutPanel.gameObject.activeSelf;
        public Flyout CurrentFlyout => current;
        public ToolboxDefinition Definition => definition;
        public WorkspaceView Main => main;
        public WorkspaceView FlyoutView => flyoutView;

        static readonly Color ButtonText = Color.white;

        public void Configure(WorkspaceView mainView, RectTransform list, RectTransform panel, WorkspaceView flyout)
        {
            main = mainView; categoryList = list; flyoutPanel = panel; flyoutView = flyout;
        }

        void Awake() => Initialise();

        void Initialise()
        {
            if (definition != null) return;
            definition = StandardBlockSet.LoadToolbox();
            if (flyoutView != null) flyoutView.Spawner = this;
            if (main != null)
            {
                main.DeleteArea = (RectTransform)transform;
                main.BlockDragStarted += _ => CloseFlyout();
                main.WorkspaceEdited += RefreshOpenFlyout;
                main.DeleteHoverChanged += over => { var bg = GetComponent<Image>(); if (bg != null) bg.color = over ? new Color(0.6f, 0.15f, 0.15f) : DefaultBackground; };
            }
            BuildButtons();
            CloseFlyout();
        }

        static readonly Color DefaultBackground = new Color(0.22f, 0.22f, 0.26f);

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
            buttons.Clear();

            float y = 0;
            for (int i = 0; i < definition.Categories.Count; i++)
            {
                int index = i;
                var cat = definition.Categories[i];
                var go = new GameObject(cat.Name, typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(categoryList, false);
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.sizeDelta = new Vector2(0, 40);
                rt.anchoredPosition = new Vector2(0, -y);
                y += 42;

                var img = go.GetComponent<Image>();
                img.color = Shade(BlockStyle.Parse(cat.ColourHex), 0.75f);
                go.GetComponent<Button>().targetGraphic = img;
                go.GetComponent<Button>().onClick.AddListener(() => ToggleCategory(index));
                buttons.Add(img);

                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                var lrt = (RectTransform)label.transform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                var icon = BlockImages.Get(cat.Icon);
                if (icon != null)
                {
                    var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    var irt = (RectTransform)ig.transform;
                    irt.SetParent(rt, false);
                    irt.anchorMin = irt.anchorMax = new Vector2(0, 0.5f); irt.pivot = new Vector2(0, 0.5f);
                    irt.sizeDelta = new Vector2(24, 24); irt.anchoredPosition = new Vector2(8, 0);
                    var iimg = ig.GetComponent<Image>();
                    iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
                }
                lrt.offsetMin = new Vector2(icon != null ? 38 : 10, 0); lrt.offsetMax = new Vector2(-4, 0);
                var t = label.GetComponent<TextMeshProUGUI>();
                t.text = cat.Name; t.fontSize = 16; t.color = ButtonText;
                t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        void HighlightSelected()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var baseColour = BlockStyle.Parse(definition.Categories[i].ColourHex);
                buttons[i].color = i == selected ? baseColour : Shade(baseColour, 0.75f);
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
            rt.sizeDelta += new Vector2(24, 24);                     // padding inside the scroll area
            rt.anchoredPosition = new Vector2(12, -12);
            if (current.HeaderHeight > 0) BuildCreateVariableButton();
            HighlightSelected();
        }

        public void CloseFlyout()
        {
            Initialise();
            selected = -1;
            current = null;
            if (flyoutPanel != null) flyoutPanel.gameObject.SetActive(false);
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
            img.color = new Color(0.55f, 0.3f, 0.5f);
            createVariableButton = go.GetComponent<Button>();
            createVariableButton.targetGraphic = img;
            createVariableButton.onClick.AddListener(() => main.RequestCreateVariable());

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var t = label.GetComponent<TextMeshProUGUI>();
            t.text = "Create variable..."; t.fontSize = 15; t.color = Color.white; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
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
