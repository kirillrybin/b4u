using System;
using System.Collections.Generic;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Draws a <see cref="Workspace"/> with UGUI. Every block becomes a small hierarchy
    /// (body polygon, input holes, field backgrounds and TMP texts); children are siblings drawn after their
    /// parent, so inline and statement children appear on top of the parent's holes.
    /// Place it under a Canvas; its RectTransform's top-left is the workspace origin.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WorkspaceView : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        Workspace workspace;
        TmpTextMeasurer measurer;
        int measuredVersion = -1;
        TMP_Text measureTemplate;
        readonly Dictionary<Block, RectTransform> blockObjects = new Dictionary<Block, RectTransform>();

        RectTransform highlightObject;
        PolygonGraphic highlightShape;

        // undo / redo
        string dragBefore;

        /// <summary>One step per user action; null for a flyout (nothing there is editable).</summary>
        public UndoHistory History { get; private set; }
        public bool CanUndo => History != null && History.CanUndo;
        public bool CanRedo => History != null && History.CanRedo;
        /// <summary>The undo/redo stacks changed (refresh the buttons).</summary>
        public event Action HistoryChanged;

        // drag & drop
        BlockDragger dragger;
        Vector2 grabOffset;
        RectTransform snapMarker;
        PolygonGraphic snapShape;
        readonly List<RectTransform> draggedObjects = new List<RectTransform>();

        // field editing
        TMP_InputField fieldInput;
        Block editBlock;
        Field editField;

        /// <summary>A drag started (the program should stop running; the model is about to change).</summary>
        public event Action<Block> BlockDragStarted;
        /// <summary>A drag finished: the blocks were dropped, deleted or the drag was cancelled.</summary>
        public event Action BlockDragEnded;
        /// <summary>The user changed the workspace (a block was dropped, moved, reconnected, created or deleted).</summary>
        public event Action WorkspaceEdited;
        /// <summary>The user opened a field for editing (the running program should stop; the model is about to change).</summary>
        public event Action<Block> FieldEditStarted;
        /// <summary>The dragged blocks entered / left the delete area.</summary>
        public event Action<bool> DeleteHoverChanged;

        /// <summary>Dropping dragged blocks inside this rectangle deletes them (the toolbox strip).</summary>
        public RectTransform DeleteArea;
        /// <summary>Non-null turns this view into a flyout: grabbing a block asks the spawner for a copy instead of moving it.</summary>
        public IBlockSpawner Spawner;
        public bool OverDeleteArea { get; private set; }

        public Workspace Workspace => workspace;
        public LayoutResult Layout { get; private set; }
        /// <summary>The block currently outlined as "about to execute", or null.</summary>
        public Block HighlightedBlock { get; private set; }

        /// <summary>The block the user clicked (Delete removes it), or null. Never a shadow block.</summary>
        public Block SelectedBlock { get; private set; }
        /// <summary>The selection changed (a block was clicked, deselected, or deleted).</summary>
        public event Action<Block> SelectionChanged;
        RectTransform selectionObject;
        PolygonGraphic selectionShape;
        public static Color SelectionColour => BlockStyle.SelectionColour;

        public void SetWorkspace(Workspace ws)
        {
            workspace = ws;
            SubscribeToTheme();
            SelectedBlock = null;
            History = ws != null && Spawner == null ? new UndoHistory(ws) : null;
            if (History != null) History.Changed += () => HistoryChanged?.Invoke();
            HistoryChanged?.Invoke();
            Rebuild();
        }

        // ------------------------------------------------------------------ undo / redo
        public bool Undo() => StepHistory(undo: true);
        public bool Redo() => StepHistory(undo: false);

        bool StepHistory(bool undo)
        {
            if (History == null || IsDragging || IsEditingField || ActiveDialog != null) return false;   // finish what is open first
            if (undo ? !History.CanUndo : !History.CanRedo) return false;
            CloseMenu();
            FieldEditStarted?.Invoke(null);                       // a running program is about to be edited: stop it
            if (undo) History.Undo(); else History.Redo();
            Rebuild();                                            // the restored blocks are new objects
            WorkspaceEdited?.Invoke();
            return true;
        }

        /// <summary>Ctrl/Cmd+Z undo, Ctrl/Cmd+Shift+Z or Ctrl/Cmd+Y redo. Ignored while text is being typed (the input field has its own).</summary>
        public bool HandleShortcut(KeyCode key, bool ctrlOrCommand, bool shift)
        {
            if (IsEditingField || ActiveDialog != null) return false;
            if (key == KeyCode.Delete || key == KeyCode.Backspace) return !ctrlOrCommand && DeleteSelected();
            if (!ctrlOrCommand) return false;
            if (key == KeyCode.Equals || key == KeyCode.Plus || key == KeyCode.KeypadPlus) return ZoomBy(ZoomStep, ViewportCentreOnScreen);
            if (key == KeyCode.Minus || key == KeyCode.KeypadMinus) return ZoomBy(1f / ZoomStep, ViewportCentreOnScreen);
            if (key == KeyCode.Alpha0 || key == KeyCode.Keypad0) return SetZoom(1f, ViewportCentreOnScreen);
            if (key == KeyCode.C) return CopySelected();
            if (key == KeyCode.X) return CutSelected();
            if (key == KeyCode.V) return Paste();
            if (key == KeyCode.Z) return shift ? Redo() : Undo();
            if (key == KeyCode.Y) return Redo();
            return false;
        }

        // ------------------------------------------------------------------ selection
        /// <summary>Selects a block (a shadow selects the block that owns it). Null or a block not in the workspace clears it.</summary>
        public void Select(Block block)
        {
            while (block != null && block.IsShadow) block = block.ShadowOf?.Owner;
            if (block != null && (workspace == null || Spawner != null || !workspace.AllBlocks().Contains(block))) block = null;
            if (block == SelectedBlock) return;
            SelectedBlock = block;
            UpdateSelectionOutline();
            SelectionChanged?.Invoke(block);
        }

        /// <summary>Deletes the selected block and its inputs; the blocks that followed it close the gap (Blockly's Delete key). One undo step.</summary>
        public bool DeleteSelected()
        {
            var block = SelectedBlock;
            if (block == null || workspace == null || IsDragging || History == null) return false;
            CloseMenu();
            FieldEditStarted?.Invoke(null);                       // a running program is about to be edited: stop it
            string before = SnapshotForUndo();
            EnsureMeasurer();
            WorkspaceEditing.Delete(workspace, new BlockLayouter(workspace, measurer), block, heal: true);
            SelectedBlock = null;
            Rebuild();
            RecordForUndo(before);
            SelectionChanged?.Invoke(null);
            WorkspaceEdited?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ copy / cut / paste
        /// <summary>What Ctrl+C copied; shared by all views so a block can be pasted into another program.</summary>
        public static string Clipboard { get; set; }
        static float clipboardX, clipboardY;
        public static bool HasClipboard => !string.IsNullOrEmpty(Clipboard);

        /// <summary>Copies the selected block with its inputs (not the blocks after it).</summary>
        public bool CopySelected()
        {
            var block = SelectedBlock;
            if (block == null || workspace == null || Layout == null || !Layout.ByBlock.TryGetValue(block, out var box)) return false;
            var text = WorkspaceEditing.Copy(workspace, block);
            if (text == null) return false;
            Clipboard = text; clipboardX = box.X; clipboardY = box.Y;
            return true;
        }

        /// <summary>Copy, then delete the selected block (the blocks after it close the gap).</summary>
        public bool CutSelected() => CopySelected() && DeleteSelected();

        /// <summary>Puts the copied block into the workspace next to where it was copied from and selects it. One undo step.</summary>
        public bool Paste()
        {
            if (workspace == null || History == null || IsDragging || !HasClipboard) return false;
            CloseMenu();
            string before = SnapshotForUndo();
            var block = WorkspaceEditing.Paste(workspace, Clipboard, clipboardX, clipboardY);
            if (block == null) return false;
            FieldEditStarted?.Invoke(null);                       // a running program is about to be edited: stop it
            Rebuild();
            Select(block);
            RecordForUndo(before);
            WorkspaceEdited?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ block menu
        public const string MenuDuplicate = "duplicate", MenuCopy = "copy", MenuCut = "cut", MenuDisable = "disable", MenuCollapse = "collapse", MenuDelete = "delete";
        public const float LongPressSeconds = 0.5f;
        Block pressBlock; Vector2 pressPoint; float pressStarted;

        /// <summary>A press began on a block; held still for <see cref="LongPressSeconds"/> it opens the block menu.</summary>
        public void BeginPress(Block block, Vector2 screenPoint)
        {
            if (Spawner != null) return;
            pressBlock = block; pressPoint = screenPoint; pressStarted = Time.unscaledTime;
        }

        public void EndPress() => pressBlock = null;

        /// <summary>Called every frame: opens the block menu once a press has been held long enough.</summary>
        public void CheckLongPress(float now)
        {
            if (pressBlock == null || now - pressStarted < LongPressSeconds) return;
            var block = pressBlock; pressBlock = null;
            if (!IsDragging) ShowBlockMenu(block, pressPoint);
        }

        /// <summary>A block can be disabled when it is a statement (an expression block always runs as part of its parent).</summary>
        public static bool CanDisable(Block block) => block != null && block.Output == null && (block.Previous != null || block.Next != null);

        /// <summary>A block can be collapsed when something can be inside it (an input), and any collapsed block can be expanded.</summary>
        public static bool CanCollapse(Block block) => block != null && (block.Collapsed || block.Inputs.Any(i => i.Kind != InputKind.Dummy));

        /// <summary>Opens the menu of a block (right click, or a long press): duplicate, copy, cut, disable / enable, delete. Selects the block.</summary>
        public bool ShowBlockMenu(Block block, Vector2 screenPoint)
        {
            while (block != null && block.IsShadow) block = block.ShadowOf?.Owner;
            if (workspace == null || Spawner != null || IsDragging || block == null || RootCanvas == null || ActiveDialog != null) return false;
            if (IsEditingField) SubmitFieldEdit(fieldInput.text);
            CloseMenu();
            Select(block);
            var items = new System.Collections.Generic.List<DropdownItem>
            {
                new DropdownItem { Label = Loc.T("Duplicate"), Value = MenuDuplicate },
                new DropdownItem { Label = Loc.T("Copy"), Value = MenuCopy },
                new DropdownItem { Label = Loc.T("Cut"), Value = MenuCut },
            };
            if (CanDisable(block)) items.Add(new DropdownItem { Label = Loc.T(block.Disabled ? "Enable block" : "Disable block"), Value = MenuDisable });
            if (CanCollapse(block)) items.Add(new DropdownItem { Label = Loc.T(block.Collapsed ? "Expand block" : "Collapse block"), Value = MenuCollapse });
            items.Add(new DropdownItem { Label = Loc.T("Delete"), Value = MenuDelete });
            Canvas.ForceUpdateCanvases();
            menu = DropdownMenu.Open(RootCanvas, screenPoint, 170f, items, item => ChooseBlockMenuItem(block, item.Value));
            return true;
        }

        /// <summary>What a block menu entry does (also what clicking it calls). Each change is one undo step.</summary>
        public bool ChooseBlockMenuItem(Block block, string action)
        {
            Select(block);
            if (SelectedBlock != block) return false;
            switch (action)
            {
                case MenuDuplicate: return DuplicateSelected();
                case MenuCopy: return CopySelected();
                case MenuCut: return CutSelected();
                case MenuDisable: return ToggleDisabledSelected();
                case MenuCollapse: return ToggleCollapsedSelected();
                case MenuDelete: return DeleteSelected();
                default: return false;
            }
        }

        /// <summary>Puts a copy of the selected block (with its inputs) just below and right of it and selects the copy. The clipboard is not touched.</summary>
        public bool DuplicateSelected()
        {
            var block = SelectedBlock;
            if (block == null || workspace == null || Layout == null || History == null || IsDragging || !Layout.ByBlock.TryGetValue(block, out var box)) return false;
            var text = WorkspaceEditing.Copy(workspace, block);
            if (text == null) return false;
            CloseMenu();
            string before = SnapshotForUndo();
            var copy = WorkspaceEditing.Paste(workspace, text, box.X, box.Y);
            if (copy == null) return false;
            FieldEditStarted?.Invoke(null);
            Rebuild();
            Select(copy);
            RecordForUndo(before);
            WorkspaceEdited?.Invoke();
            return true;
        }

        /// <summary>Disables the selected statement block (it is skipped when the program runs and looks grey), or enables it again.</summary>
        public bool ToggleDisabledSelected()
        {
            var block = SelectedBlock;
            if (!CanDisable(block) || workspace == null || History == null || IsDragging) return false;
            CloseMenu();
            string before = SnapshotForUndo();
            FieldEditStarted?.Invoke(null);
            block.Disabled = !block.Disabled;
            Rebuild();
            RecordForUndo(before);
            WorkspaceEdited?.Invoke();
            return true;
        }

        /// <summary>Collapses the selected block to one line (what is inside it is kept and runs as before), or expands it again.</summary>
        public bool ToggleCollapsedSelected()
        {
            var block = SelectedBlock;
            if (!CanCollapse(block) || workspace == null || History == null || IsDragging) return false;
            CloseMenu();
            string before = SnapshotForUndo();
            FieldEditStarted?.Invoke(null);
            block.Collapsed = !block.Collapsed;
            Rebuild();
            RecordForUndo(before);
            WorkspaceEdited?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ procedure parameters
        RectTransform paramsButton;
        public const string ParamsButtonName = "params button";

        /// <summary>The small "params" button shown on a selected procedure definition; null when it is not shown.</summary>
        public RectTransform ParamsButton => paramsButton != null && paramsButton.gameObject.activeSelf ? paramsButton : null;

        void UpdateParamsButton(BlockBox box)
        {
            bool wanted = Spawner == null && ProcedureInfo.IsDefinition(SelectedBlock);
            if (!wanted) { if (paramsButton != null) paramsButton.gameObject.SetActive(false); return; }
            if (paramsButton == null)
            {
                paramsButton = NewRect(ParamsButtonName, transform, 0, 0, 58, 22);
                var img = paramsButton.gameObject.AddComponent<Image>();
                img.color = SelectionColour;
                var btn = paramsButton.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => RequestEditParams(SelectedBlock));
                var label = NewRect("text", paramsButton, 0, 0, 58, 22);
                var t = label.gameObject.AddComponent<TextMeshProUGUI>();
                Configure(t);
                t.text = Loc.T("params");
                t.fontSize = 12;
                t.color = Color.white;
                t.alignment = TextAlignmentOptions.Center;
            }
            paramsButton.gameObject.SetActive(true);
            paramsButton.anchoredPosition = new Vector2(box.X + box.W - 58, -(box.Y - 22 - BlockGeometry.Current.HatFor(box.Block)));   // above the cap, if the block has one   // just above the block's top-right corner
            paramsButton.SetAsLastSibling();
        }

        /// <summary>Asks for the parameter names of a procedure definition ("a, b") and applies them. One undo step.</summary>
        public void RequestEditParams(Block definition)
        {
            if (workspace == null || Spawner != null || definition == null || !ProcedureInfo.IsDefinition(definition) || RootCanvas == null) return;
            if (IsDragging || IsEditingField || ActiveDialog != null) return;
            CloseMenu();
            dialog = DialogView.ShowPrompt(RootCanvas, Loc.T("Parameters of \"{0}\" (separated by commas):", ProcedureInfo.Name(definition)),
                string.Join(", ", ProcedureEditing.CurrentNames(definition)),
                ProcedureEditing.Validate,
                typed => ApplyParams(definition, ProcedureEditing.ParseNames(typed)));
        }

        /// <summary>Sets the parameters of a definition and redraws. Returns true if anything changed.</summary>
        public bool ApplyParams(Block definition, IList<string> names)
        {
            if (workspace == null || definition == null) return false;
            FieldEditStarted?.Invoke(null);                       // a running program is about to be edited: stop it
            EnsureMeasurer();
            string before = SnapshotForUndo();
            bool changed = ProcedureEditing.SetParams(workspace, new BlockLayouter(workspace, measurer), definition, names);
            if (changed) { RecordForUndo(before); Rebuild(); WorkspaceEdited?.Invoke(); }
            return changed;
        }

        void UpdateSelectionOutline()
        {
            BlockBox box = null;
            if (SelectedBlock == null || Layout == null || !Layout.ByBlock.TryGetValue(SelectedBlock, out box))
            {
                if (selectionObject != null) selectionObject.gameObject.SetActive(false);
                if (paramsButton != null) paramsButton.gameObject.SetActive(false);
                return;
            }
            UpdateParamsButton(box);
            if (selectionObject == null)
            {
                selectionObject = NewRect("selection", transform, box.X, box.Y, box.W, box.H);
                selectionShape = selectionObject.gameObject.AddComponent<PolygonGraphic>();
                selectionShape.raycastTarget = false;
            }
            selectionObject.gameObject.SetActive(true);
            selectionObject.anchoredPosition = new Vector2(box.X, -box.Y);
            selectionObject.sizeDelta = new Vector2(box.W, box.H);
            selectionShape.SetShape(Translate(box.Outline, box.X, box.Y), Color.clear, SelectionColour, BlockStyle.SelectionWidth);
            selectionObject.SetAsLastSibling();
            if (highlightObject != null && highlightObject.gameObject.activeSelf) highlightObject.SetAsLastSibling();   // "executing" stays on top
        }

        string SnapshotForUndo() => History?.Snapshot();
        void RecordForUndo(string before) { if (before != null) History?.Commit(before); }

        // subscribed in SetWorkspace as well, because EditMode never calls OnEnable
        void SubscribeToTheme() { BlockStyle.ThemeChanged -= OnThemeChanged; BlockStyle.ThemeChanged += OnThemeChanged; Loc.Changed -= OnLanguageChanged; Loc.Changed += OnLanguageChanged; }
        // soft edges: about one screen pixel in local units, so it depends on the canvas scale and the zoom
        float edgeWidth;
        float lastEdgeScale = -1f, edgeRefreshAt = -1f;

        Canvas rootCanvas;

        float CanvasScale()
        {
            if (rootCanvas == null) { var c = GetComponentInParent<Canvas>(); rootCanvas = c != null ? c.rootCanvas : null; }
            return rootCanvas != null ? Mathf.Max(rootCanvas.scaleFactor, 0.01f) : 1f;
        }

        float PixelsToLocal(float pixels) => pixels <= 0f ? 0f : pixels / (CanvasScale() * Mathf.Max(Zoom, 0.05f));

        float CurrentEdgeScale() => CanvasScale() * Zoom;

        void RefreshEdgeWidth()
        {
            edgeWidth = PixelsToLocal(BlockStyle.EdgeSmoothing);
            lastEdgeScale = CurrentEdgeScale();
        }

        /// <summary>Re-fits the soft edge of every shape to the current canvas scale and zoom.</summary>
        public void RefreshEdges()
        {
            RefreshEdgeWidth();
            edgeRefreshAt = -1f;
            foreach (var g in GetComponentsInChildren<PolygonGraphic>(true)) g.EdgeWidth = edgeWidth;
        }

        void LateUpdate()
        {
            CheckLongPress(Time.unscaledTime);
            if (lastEdgeScale < 0f) return;
            float scale = CurrentEdgeScale();
            if (Mathf.Approximately(scale, lastEdgeScale)) { edgeRefreshAt = -1f; return; }
            if (!Mathf.Approximately(scale, pendingEdgeScale)) { pendingEdgeScale = scale; edgeRefreshAt = Time.unscaledTime + 0.2f; }   // still changing
            else if (edgeRefreshAt >= 0f && Time.unscaledTime >= edgeRefreshAt) RefreshEdges();
        }
        float pendingEdgeScale;

        void OnEnable() { SubscribeToTheme(); }
        void OnDisable() { BlockStyle.ThemeChanged -= OnThemeChanged; Loc.Changed -= OnLanguageChanged; }
        void OnDestroy() { BlockStyle.ThemeChanged -= OnThemeChanged; Loc.Changed -= OnLanguageChanged; }

        /// <summary>The labels of the blocks are in another language: lay them out and draw them again.</summary>
        void OnLanguageChanged()
        {
            if (this == null) { Loc.Changed -= OnLanguageChanged; return; }
            if (workspace != null && Spawner == null) Rebuild();      // the flyout is drawn again by the toolbox, which also puts its button back
        }

        void OnThemeChanged()
        {
            if (this == null) { BlockStyle.ThemeChanged -= OnThemeChanged; return; }   // destroyed without OnDestroy (EditMode)
            if (measureTemplate != null) { Configure(measureTemplate); measurer.Clear(); measuredVersion = BlockStyle.Version; }
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (workspace == null) return;
            EnsureMeasurer();
            Layout = new BlockLayouter(workspace, measurer).LayoutWorkspace();
            RefreshEdgeWidth();
            foreach (var box in Layout.Boxes) CreateBlock(box);

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Mathf.Max(0, Layout.Right), Mathf.Max(0, Layout.Bottom));

            if (SelectedBlock != null && !Layout.ByBlock.ContainsKey(SelectedBlock)) { SelectedBlock = null; SelectionChanged?.Invoke(null); }
            UpdateSelectionOutline();
        }

        /// <summary>The text measurer shared with the layout (so other views can lay out with identical metrics).</summary>
        public ITextMeasurer Measurer
        {
            get
            {
                // TextMeshPro only initialises (font, material) once enabled; under an inactive parent it would fail deep inside TMP
                if (!isActiveAndEnabled && measurer == null)
                    throw new InvalidOperationException("WorkspaceView '" + name + "' must be active in the hierarchy to measure text; show its panel first.");
                EnsureMeasurer();
                return measurer;
            }
        }

        public bool IsDragging => dragger != null && dragger.IsDragging;
        public Block DraggedBlock => dragger?.Dragged;
        public SnapCandidate? SnapCandidate => dragger?.Candidate;
        public static Color SnapColour => BlockStyle.SnapColour;

        // ------------------------------------------------------------------ zoom
        public const float MinZoom = 0.4f, MaxZoom = 2.5f, ZoomStep = 1.25f;

        /// <summary>Scale of the whole workspace (1 = normal). The view's transform is scaled, so every coordinate conversion stays valid.</summary>
        public float Zoom => transform.localScale.x;

        Camera EventCamera
        {
            get
            {
                var canvas = RootCanvas;
                return canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            }
        }

        /// <summary>The middle of the visible area (the parent viewport if there is one), in screen pixels; where keyboard zoom is centred.</summary>
        public Vector2 ViewportCentreOnScreen
        {
            get
            {
                var area = transform.parent as RectTransform ?? (RectTransform)transform;
                return RectTransformUtility.WorldToScreenPoint(EventCamera, area.TransformPoint(area.rect.center));
            }
        }

        /// <summary>Zooms to <paramref name="zoom"/> (clamped) keeping the workspace point under <paramref name="screenPivot"/> where it is. A flyout never zooms.</summary>
        public bool SetZoom(float zoom, Vector2 screenPivot)
        {
            if (Spawner != null) return false;
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (Mathf.Approximately(zoom, Zoom)) return false;

            var cam = EventCamera;
            var rt = (RectTransform)transform;
            var anchor = ScreenToWorkspace(screenPivot, cam);
            rt.localScale = new Vector3(zoom, zoom, 1f);
            var after = WorkspaceToScreen(anchor, cam);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, after, cam, out var w1)
                && RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, screenPivot, cam, out var w2))
                rt.position += w2 - w1;                              // slide the content so the anchored point is back under the pivot
            return true;
        }

        public bool ZoomBy(float factor, Vector2 screenPivot) => SetZoom(Zoom * factor, screenPivot);

        // ------------------------------------------------------------------ coordinates
        /// <summary>Screen point to workspace units (x right, y down from the workspace's top-left corner).</summary>
        public Vector2 ScreenToWorkspace(Vector2 screen, Camera cam)
        {
            var rt = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, cam, out var local);
            var r = rt.rect;
            return new Vector2(local.x - r.xMin, r.yMax - local.y);
        }

        public Vector2 WorkspaceToScreen(Vector2 workspacePoint, Camera cam)
        {
            var rt = (RectTransform)transform;
            var r = rt.rect;
            var world = rt.TransformPoint(new Vector3(r.xMin + workspacePoint.x, r.yMax - workspacePoint.y, 0));
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        // ------------------------------------------------------------------ dropdown menus and variables
        DropdownMenu menu;
        DialogView dialog;

        public DropdownMenu ActiveMenu => menu != null && menu.IsOpen ? menu : null;
        public DialogView ActiveDialog => dialog != null && dialog.IsOpen ? dialog : null;

        Canvas RootCanvas => GetComponentInParent<Canvas>();

        public void CloseMenu()
        {
            if (menu != null && menu.IsOpen) menu.Close();
            menu = null;
        }

        /// <summary>Opens the popup list under a dropdown or variable field.</summary>
        public bool ShowDropdown(Block block, Field field)
        {
            if (workspace == null || Spawner != null || IsDragging || block == null || field == null || !FieldEditing.IsDropdown(field)) return false;
            if (RootCanvas == null || Layout == null || !Layout.ByBlock.TryGetValue(block, out var blockBox)) return false;
            var fieldBox = blockBox.Fields.Find(f => f.Field == field);
            if (fieldBox == null) return false;

            if (IsEditingField) SubmitFieldEdit(fieldInput.text);
            CloseMenu();
            if (!Layout.ByBlock.TryGetValue(block, out blockBox)) return false;
            fieldBox = blockBox.Fields.Find(f => f.Field == field) ?? fieldBox;

            var items = FieldEditing.GetDropdownItems(workspace, field);
            if (items.Count == 0) return false;

            FieldEditStarted?.Invoke(block);                       // a running program stops while the user changes it
            var canvas = RootCanvas;
            Canvas.ForceUpdateCanvases();                           // the canvas scale must be current before converting to screen pixels
            var cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var below = WorkspaceToScreen(new Vector2(fieldBox.X, fieldBox.Y + fieldBox.H), cam);
            menu = DropdownMenu.Open(canvas, below, fieldBox.W, items, item => ChooseDropdownItem(block, field, item));
            return true;
        }

        /// <summary>Opens the palette under a colour field; choosing a colour sets the field.</summary>
        public bool ShowColourPicker(Block block, Field field)
        {
            if (workspace == null || Spawner != null || IsDragging || block == null || field == null || !FieldEditing.IsColour(field)) return false;
            if (RootCanvas == null || Layout == null || !Layout.ByBlock.TryGetValue(block, out var blockBox)) return false;
            if (IsEditingField) SubmitFieldEdit(fieldInput.text);
            CloseMenu();
            if (!Layout.ByBlock.TryGetValue(block, out blockBox)) return false;
            var fieldBox = blockBox.Fields.Find(f => f.Field == field);
            if (fieldBox == null) return false;

            FieldEditStarted?.Invoke(block);
            var canvas = RootCanvas;
            Canvas.ForceUpdateCanvases();
            var cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            var below = WorkspaceToScreen(new Vector2(fieldBox.X, fieldBox.Y + fieldBox.H), cam);
            menu = DropdownMenu.OpenColours(canvas, below, Colour.Palette, Colour.PaletteColumns, field.Value, item => ApplyFieldValue(block, field, item.Value));
            return true;
        }

        /// <summary>What happens when a menu entry is chosen (also what clicking an entry calls).</summary>
        public void ChooseDropdownItem(Block block, Field field, DropdownItem item)
        {
            switch (item.Action)
            {
                case DropdownAction.RenameVariable: RequestRenameVariable(item.Value); break;
                case DropdownAction.DeleteVariable: RequestDeleteVariable(item.Value); break;
                default: ApplyFieldValue(block, field, item.Value); break;
            }
        }

        /// <summary>Sets a dropdown/variable field to a value from its list and redraws. Returns true if anything changed.</summary>
        public bool ApplyFieldValue(Block block, Field field, string value)
        {
            if (workspace == null || block == null || field == null) return false;
            EnsureMeasurer();
            string before = SnapshotForUndo();
            bool changed = FieldEditing.SetValue(workspace, block, field, value, new BlockLayouter(workspace, measurer));
            if (changed) { RecordForUndo(before); Rebuild(); WorkspaceEdited?.Invoke(); }
            return changed;
        }

        /// <summary>Tell listeners (the toolbox's dynamic categories, autosave...) that the workspace changed.</summary>
        public void NotifyEdited() => WorkspaceEdited?.Invoke();

        public void RequestCreateVariable(Action<Variable> created = null)
        {
            if (workspace == null || RootCanvas == null) return;
            CloseMenu();
            dialog = DialogView.ShowPrompt(RootCanvas, Loc.T("New variable name:"), "",
                typed => VariableEditing.Validate(workspace, typed, null, out _),
                typed =>
                {
                    string before = SnapshotForUndo();
                    var v = VariableEditing.Create(workspace, typed, out _);
                    if (v == null) return;
                    RecordForUndo(before);
                    NotifyEdited();
                    created?.Invoke(v);
                });
        }

        public void RequestRenameVariable(string id)
        {
            var v = workspace?.GetVariableById(id);
            if (v == null || RootCanvas == null) return;
            dialog = DialogView.ShowPrompt(RootCanvas, Loc.T("Rename all \"{0}\" variables to:", v.Name), v.Name,
                typed => VariableEditing.Validate(workspace, typed, id, out _),
                typed =>
                {
                    FieldEditStarted?.Invoke(null);
                    string before = SnapshotForUndo();
                    if (VariableEditing.Rename(workspace, id, typed, out _)) { RecordForUndo(before); Rebuild(); WorkspaceEdited?.Invoke(); }
                });
        }

        /// <summary>Deletes a variable and everything that uses it. Asks first when more than one block is affected.</summary>
        public void RequestDeleteVariable(string id)
        {
            var v = workspace?.GetVariableById(id);
            if (v == null) return;
            int uses = VariableEditing.CountUses(workspace, id);
            if (uses <= 1 || RootCanvas == null) { DeleteVariableNow(id); return; }
            dialog = DialogView.ShowConfirm(RootCanvas, Loc.T("Delete variable"),
                Loc.T("Delete {0} uses of the \"{1}\" variable?", uses, v.Name), () => DeleteVariableNow(id));
        }

        void DeleteVariableNow(string id)
        {
            FieldEditStarted?.Invoke(null);
            EnsureMeasurer();
            string before = SnapshotForUndo();
            VariableEditing.Delete(workspace, new BlockLayouter(workspace, measurer), id);
            RecordForUndo(before);
            Rebuild();
            WorkspaceEdited?.Invoke();
        }

        // ------------------------------------------------------------------ editing fields
        public bool IsEditingField => editField != null;
        public TMP_InputField FieldEditor => fieldInput;
        public Field EditedField => editField;

        /// <summary>Opens the in-place editor over a text or number field. Returns false if the field cannot be edited here.</summary>
        public bool BeginFieldEdit(Block block, Field field)
        {
            if (workspace == null || Spawner != null || IsDragging || block == null || field == null) return false;
            if (!FieldEditing.IsEditableText(field) || Layout == null || !Layout.ByBlock.TryGetValue(block, out var blockBox)) return false;
            var fieldBox = blockBox.Fields.Find(f => f.Field == field);
            if (fieldBox == null) return false;

            if (IsEditingField) SubmitFieldEdit(fieldInput.text);      // like Blockly, opening another field accepts the first one
            // the rebuild above may have replaced the layout objects; look the box up again
            if (!Layout.ByBlock.TryGetValue(block, out blockBox)) return false;
            fieldBox = blockBox.Fields.Find(f => f.Field == field) ?? fieldBox;

            FieldEditStarted?.Invoke(block);
            EnsureFieldInput();
            editBlock = block; editField = field;

            bool number = field.Spec.FieldType == "field_number";
            fieldInput.characterValidation = number ? TMP_InputField.CharacterValidation.Decimal : TMP_InputField.CharacterValidation.None;
            fieldInput.keyboardType = number ? TouchScreenKeyboardType.DecimalPad : TouchScreenKeyboardType.Default;
            fieldInput.SetTextWithoutNotify(field.Value ?? "");

            var rt = (RectTransform)fieldInput.transform;
            rt.anchoredPosition = new Vector2(fieldBox.X - 2, -(fieldBox.Y - 2));
            rt.sizeDelta = new Vector2(Mathf.Max(fieldBox.W + 4, MinEditorWidth), fieldBox.H + 4);
            fieldInput.gameObject.SetActive(true);
            ResizeFieldEditor();
            rt.SetAsLastSibling();
            fieldInput.Select();
            fieldInput.ActivateInputField();
            return true;
        }

        const float MinEditorWidth = 64f, MaxEditorWidth = 520f;

        /// <summary>Replaces the text in the open editor (as if typed) and resizes it to fit.</summary>
        public void SetFieldEditorText(string text)
        {
            if (!IsEditingField) return;
            fieldInput.text = text;                       // raises onValueChanged, which resizes
            ResizeFieldEditor();
        }

        /// <summary>The editor grows with what is typed (like Blockly's widget) so the text is never cut off.</summary>
        void ResizeFieldEditor()
        {
            if (fieldInput == null || editField == null) return;
            var rt = (RectTransform)fieldInput.transform;
            float needed = Mathf.Ceil(fieldInput.textComponent.GetPreferredValues(fieldInput.text + " ").x) + 14f;
            rt.sizeDelta = new Vector2(Mathf.Clamp(needed, MinEditorWidth, MaxEditorWidth), rt.sizeDelta.y);
        }

        /// <summary>Applies typed text to the field being edited and closes the editor. Returns true if the value changed.</summary>
        public bool SubmitFieldEdit(string text)
        {
            if (!IsEditingField) return false;
            var block = editBlock; var field = editField;
            editBlock = null; editField = null;
            fieldInput.gameObject.SetActive(false);

            string before = SnapshotForUndo();
            bool changed = FieldEditing.SetValue(workspace, block, field, text);
            if (changed)
            {
                RecordForUndo(before);
                Rebuild();
                WorkspaceEdited?.Invoke();
            }
            return changed;
        }

        /// <summary>Closes the editor and keeps the old value.</summary>
        public void CancelFieldEdit()
        {
            if (!IsEditingField) return;
            editBlock = null; editField = null;
            fieldInput.gameObject.SetActive(false);
        }

        void EnsureFieldInput()
        {
            if (fieldInput != null) return;

            var go = new GameObject("FieldEditor", typeof(RectTransform), typeof(Image));
            go.SetActive(false);                                       // finish wiring before TMP_InputField enables
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            var bg = go.GetComponent<Image>();
            bg.color = Color.white;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            var art = (RectTransform)area.transform;
            art.SetParent(rt, false);
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(5, 1); art.offsetMax = new Vector2(-5, -1);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(art, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = BlockStyle.FontSize;
            if (BlockStyle.Theme.Font != null) text.font = BlockStyle.Theme.Font;
            text.fontStyle = BlockStyle.FontStyle;
            text.color = BlockStyle.InputText;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;

            fieldInput = go.AddComponent<TMP_InputField>();
            fieldInput.textViewport = art;
            fieldInput.textComponent = text;
            fieldInput.targetGraphic = bg;
            fieldInput.lineType = TMP_InputField.LineType.SingleLine;
            fieldInput.richText = false;
            fieldInput.onFocusSelectAll = true;
            fieldInput.restoreOriginalTextOnEscape = true;
            // Enter, Esc (restores the old text) and clicking away all end up here
            fieldInput.onEndEdit.AddListener(t => SubmitFieldEdit(t));
            fieldInput.onValueChanged.AddListener(_ => ResizeFieldEditor());
        }

        // ------------------------------------------------------------------ dragging blocks
        /// <summary>Starts dragging <paramref name="block"/> (and the blocks after it) grabbed at <paramref name="workspacePoint"/>.</summary>
        /// <param name="stateBefore">For a block that was just created from the toolbox: the snapshot taken before it existed,
        /// so creating it and dropping it is one undo step.</param>
        public bool BeginDrag(Block block, Vector2 workspacePoint, bool isNew = false, string stateBefore = null)
        {
            if (workspace == null || IsDragging || block == null) return false;
            CloseMenu();
            if (IsEditingField) SubmitFieldEdit(fieldInput.text);          // accept what was typed before moving things around
            string before = stateBefore ?? SnapshotForUndo();
            EnsureMeasurer();
            var d = new BlockDragger(workspace, new BlockLayouter(workspace, measurer));
            if (!d.Begin(block, isNew)) return false;
            dragger = d;
            dragBefore = before;
            BlockDragStarted?.Invoke(block);

            Rebuild();                                   // the old parent shrank now that the block left it
            grabOffset = workspacePoint - new Vector2((float)block.X, (float)block.Y);
            ReorderDraggedToFront();
            UpdateSnapMarker();
            return true;
        }

        public void DragTo(Vector2 workspacePoint)
        {
            if (!IsDragging) return;
            var target = workspacePoint - grabOffset;
            dragger.MoveTo(target.x, target.y);
            ApplyDraggedLayout();
            UpdateSnapMarker();
        }

        public void EndDrag()
        {
            if (!IsDragging) return;
            if (OverDeleteArea) dragger.Delete(); else dragger.End();
            FinishDrag();
        }

        /// <summary>Tells the view whether the pointer is over the delete area (pointer events compute this themselves).</summary>
        public void SetOverDeleteArea(bool over)
        {
            if (OverDeleteArea == over) return;
            OverDeleteArea = over;
            foreach (var rt in draggedObjects)
            {
                if (rt == null) continue;
                if (!rt.TryGetComponent<CanvasGroup>(out var group)) group = rt.gameObject.AddComponent<CanvasGroup>();   // not '??': Unity's fake null
                group.alpha = over ? 0.45f : 1f;           // "this will be thrown away"
            }
            DeleteHoverChanged?.Invoke(over);
        }

        public void CancelDrag()
        {
            if (!IsDragging) return;
            dragger.Cancel();
            FinishDrag();
        }

        void FinishDrag()
        {
            dragger = null;
            draggedObjects.Clear();
            bool wasOver = OverDeleteArea;
            OverDeleteArea = false;
            if (wasOver) DeleteHoverChanged?.Invoke(false);
            BlockDragEnded?.Invoke();
            Rebuild();
            RecordForUndo(dragBefore);                                    // nothing recorded if the drag changed nothing (cancelled)
            dragBefore = null;
            WorkspaceEdited?.Invoke();
        }

        void ReorderDraggedToFront()
        {
            draggedObjects.Clear();
            if (Layout == null) return;
            foreach (var box in Layout.Boxes)
                if (dragger.IsBeingDragged(box.Block) && blockObjects.TryGetValue(box.Block, out var rt))
                {
                    rt.SetAsLastSibling();               // layout order keeps parents beneath their children
                    draggedObjects.Add(rt);
                }
        }

        /// <summary>Moves the dragged blocks' objects to where the core says they are (no rebuild while dragging).</summary>
        void ApplyDraggedLayout()
        {
            var layout = dragger.DraggedLayout;
            if (layout == null) return;
            foreach (var box in layout.Boxes)
                if (blockObjects.TryGetValue(box.Block, out var rt))
                    rt.anchoredPosition = new Vector2(box.X, -box.Y);
        }

        void UpdateSnapMarker()
        {
            var cand = dragger?.Candidate;
            if (!cand.HasValue)
            {
                if (snapMarker != null) snapMarker.gameObject.SetActive(false);
                return;
            }
            if (snapMarker == null)
            {
                snapMarker = NewRect("snap", transform, 0, 0, 1, 1);
                snapShape = snapMarker.gameObject.AddComponent<PolygonGraphic>();
                snapShape.raycastTarget = false;
                var circle = new Vec2[16];
                for (int i = 0; i < circle.Length; i++)
                {
                    float a = i * Mathf.PI * 2f / circle.Length;
                    circle[i] = new Vec2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f);
                }
                snapShape.SetShape(circle, SnapColour, BlockStyle.SnapOutline, 1.5f);
            }
            snapMarker.gameObject.SetActive(true);
            snapMarker.anchoredPosition = new Vector2(cand.Value.TargetPoint.X, -cand.Value.TargetPoint.Y);
            snapMarker.SetAsLastSibling();
        }

        // Pointer events are redirected here by BlockDragTarget (the dragged block's own object is rebuilt mid-drag).
        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging) return;
            DragTo(ScreenToWorkspace(e.position, e.pressEventCamera));
            SetOverDeleteArea(DeleteArea != null && RectTransformUtility.RectangleContainsScreenPoint(DeleteArea, e.position, e.pressEventCamera));
        }

        public void OnEndDrag(PointerEventData e) => EndDrag();

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (IsDragging && UnityEngine.Input.GetKeyDown(KeyCode.Escape)) CancelDrag();   // not Blockly.Input
            if (Spawner != null) return;
            if (TypingInInputField) return;                  // Delete, Ctrl+C... belong to the text box (a dialog's, for instance)
            UpdateZoomInput();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Delete)) HandleShortcut(KeyCode.Delete, false, false);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace)) HandleShortcut(KeyCode.Backspace, false, false);
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)
                     || UnityEngine.Input.GetKey(KeyCode.LeftCommand) || UnityEngine.Input.GetKey(KeyCode.RightCommand);
            if (!ctrl) return;
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            if (UnityEngine.Input.GetKeyDown(KeyCode.C)) HandleShortcut(KeyCode.C, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.X)) HandleShortcut(KeyCode.X, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.V)) HandleShortcut(KeyCode.V, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) HandleShortcut(KeyCode.Z, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Y)) HandleShortcut(KeyCode.Y, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Equals) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadPlus)) HandleShortcut(KeyCode.Equals, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Minus) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadMinus)) HandleShortcut(KeyCode.Minus, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad0)) HandleShortcut(KeyCode.Alpha0, true, shift);
        }

        static bool TypingInInputField
        {
            get
            {
                var es = EventSystem.current;
                var selected = es != null ? es.currentSelectedGameObject : null;
                return selected != null && selected.TryGetComponent<TMP_InputField>(out var input) && input.isFocused;
            }
        }

        ScrollRect parentScroll;
        float scrollSensitivity;
        bool pinching;
        float lastPinchDistance;

        // Ctrl/Cmd + wheel zooms (the scroll view would also scroll with the same wheel, so it is silenced meanwhile); two fingers pinch.
        void UpdateZoomInput()
        {
            if (parentScroll == null) { parentScroll = GetComponentInParent<ScrollRect>(); if (parentScroll != null) scrollSensitivity = parentScroll.scrollSensitivity; }
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)
                     || UnityEngine.Input.GetKey(KeyCode.LeftCommand) || UnityEngine.Input.GetKey(KeyCode.RightCommand);
            if (parentScroll != null) parentScroll.scrollSensitivity = ctrl ? 0f : scrollSensitivity;

            if (IsDragging || IsEditingField || ActiveDialog != null) { pinching = false; return; }

            float wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (ctrl && wheel != 0f) ZoomBy(Mathf.Pow(ZoomStep, Mathf.Clamp(wheel, -3f, 3f)), UnityEngine.Input.mousePosition);

            if (UnityEngine.Input.touchCount == 2)
            {
                var a = UnityEngine.Input.GetTouch(0); var b = UnityEngine.Input.GetTouch(1);
                float distance = Vector2.Distance(a.position, b.position);
                if (pinching && lastPinchDistance > 1f && distance > 1f) ZoomBy(distance / lastPinchDistance, (a.position + b.position) * 0.5f);
                pinching = true; lastPinchDistance = distance;
            }
            else pinching = false;
        }
#endif

        public static Color HighlightColour => BlockStyle.HighlightColour;

        /// <summary>Outlines a block (drawn above everything, including its children). Pass null to clear.</summary>
        public void SetHighlight(Block block)
        {
            HighlightedBlock = null;
            BlockBox box = null;
            if (block != null && Layout != null && Layout.ByBlock.TryGetValue(block, out box)) HighlightedBlock = block;

            if (HighlightedBlock == null)
            {
                if (highlightObject != null) highlightObject.gameObject.SetActive(false);
                return;
            }
            if (highlightObject == null)
            {
                highlightObject = NewRect("highlight", transform, box.X, box.Y, box.W, box.H);
                highlightShape = highlightObject.gameObject.AddComponent<PolygonGraphic>();
                highlightShape.raycastTarget = false;
            }
            highlightObject.gameObject.SetActive(true);
            highlightObject.anchoredPosition = new Vector2(box.X, -box.Y);
            highlightObject.sizeDelta = new Vector2(box.W, box.H);
            highlightShape.SetShape(Translate(box.Outline, box.X, box.Y), Color.clear, HighlightColour, BlockStyle.HighlightWidth);
            highlightObject.SetAsLastSibling();
        }

        /// <summary>Arranges the top blocks in one column (Blockly's "Clean up Blocks") and redraws.</summary>
        /// <param name="recordUndo">False when arranging a program that was just loaded (undo must not "unload" it).</param>
        public void CleanUp(bool recordUndo = true)
        {
            if (workspace == null) return;
            EnsureMeasurer();
            string before = recordUndo ? SnapshotForUndo() : null;
            new BlockLayouter(workspace, measurer).CleanUp();
            RecordForUndo(before);
            Rebuild();
        }

        void Clear()
        {
            CloseMenu();
            if (fieldInput != null && editField != null) { editBlock = null; editField = null; fieldInput.gameObject.SetActive(false); }
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.gameObject == (measureTemplate != null ? measureTemplate.gameObject : null)) continue;
                if (child.gameObject == (fieldInput != null ? fieldInput.gameObject : null)) continue;
                DestroyImmediateSafe(child.gameObject);
            }
            blockObjects.Clear();
            highlightObject = null; highlightShape = null; HighlightedBlock = null;
            selectionObject = null; selectionShape = null; paramsButton = null;
            snapMarker = null; snapShape = null;
        }

        static void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void EnsureMeasurer()
        {
            if (measurer != null && measureTemplate != null)
            {
                if (measuredVersion != BlockStyle.Version) { Configure(measureTemplate); measurer.Clear(); measuredVersion = BlockStyle.Version; }   // the theme changed while this view was inactive
                return;
            }
            measuredVersion = BlockStyle.Version;
            var go = new GameObject("TextMeasure", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            Configure(t);
            t.raycastTarget = false;
            t.alpha = 0f;                       // exists only to ask TMP for preferred sizes
            measureTemplate = t;
            measurer = new TmpTextMeasurer(t);
        }

        static void Configure(TMP_Text t)
        {
            t.fontSize = BlockStyle.FontSize;
            t.font = BlockStyle.Theme.Font != null ? BlockStyle.Theme.Font : TMP_Settings.defaultFontAsset;     // a theme without a font gives back the default one
            t.fontStyle = BlockStyle.FontStyle;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
        }

        RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Vec2[] Translate(Vec2[] pts, float dx, float dy)
        {
            var r = new Vec2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = new Vec2(pts[i].X - dx, pts[i].Y - dy);
            return r;
        }

        void CreateBlock(BlockBox box)
        {
            var look = BlockStyle.Look(box.Block, box.IsShadow);
            var root = NewRect(box.Block.Type, transform, box.X, box.Y, box.W, box.H);
            blockObjects[box.Block] = root;

            var dragTarget = root.gameObject.AddComponent<BlockDragTarget>();
            dragTarget.Init(this, box.Block);
            var body = root.gameObject.AddComponent<PolygonGraphic>();
            body.SetShape(Translate(box.Outline, box.X, box.Y), look.Fill, look.Stroke, look.StrokeWidth);
            body.EdgeWidth = edgeWidth;

            foreach (var slot in box.Slots)
            {
                var s = NewRect("slot " + slot.Input.Name, root, 0, 0, box.W, box.H);
                s.gameObject.AddComponent<PolygonGraphic>().SetShape(Translate(slot.Polygon, box.X, box.Y), look.Hole, look.Stroke, BlockStyle.FieldStrokeWidth);
                s.GetComponent<PolygonGraphic>().raycastTarget = false;
                s.GetComponent<PolygonGraphic>().EdgeWidth = edgeWidth;
            }

            foreach (var f in box.Fields) CreateField(root, box, f, look);
        }

        void CreateField(RectTransform parent, BlockBox box, FieldBox f, BlockStyle.BlockLook look)
        {
            float lx = f.X - box.X, ly = f.Y - box.Y;
            var rt = NewRect(f.Field.Name ?? "label", parent, lx, ly, f.W, f.H);

            if (f.ImageSrc != null)
            {
                var sprite = BlockImages.Get(f.ImageSrc);
                if (sprite == null) return;                        // the space stays reserved; nothing to draw
                var pic = rt.gameObject.AddComponent<Image>();
                pic.sprite = sprite; pic.preserveAspect = true; pic.raycastTarget = false;
                return;
            }

            if (f.Colour != null)
            {
                // a swatch: the colour itself in the field's frame
                ColorUtility.TryParseHtmlString(f.Colour, out var swatch);
                var box4 = new[] { new Vec2(0, 0), new Vec2(f.W, 0), new Vec2(f.W, f.H), new Vec2(0, f.H) };
                if (BlockStyle.FieldCornerRadius > 0) box4 = Geometry.RoundCorners(box4, BlockStyle.FieldCornerRadius).ToArray();
                var sg = rt.gameObject.AddComponent<PolygonGraphic>();
                sg.SetShape(box4, swatch, look.Stroke, Mathf.Max(1f, BlockStyle.FieldStrokeWidth));
                sg.EdgeWidth = edgeWidth;
                sg.raycastTarget = Spawner == null;
                if (Spawner == null) rt.gameObject.AddComponent<FieldClickTarget>().Init(this, box.Block, f.Field);
                return;
            }

            if (f.Editable)
            {
                bool dropdown = f.IsDropdown;
                var rect = new[] { new Vec2(0, 0), new Vec2(f.W, 0), new Vec2(f.W, f.H), new Vec2(0, f.H) };
                if (BlockStyle.FieldCornerRadius > 0) rect = Geometry.RoundCorners(rect, BlockStyle.FieldCornerRadius).ToArray();
                var bg = rt.gameObject.AddComponent<PolygonGraphic>();
                bg.SetShape(rect, dropdown ? look.DropdownFill : look.InputFill, look.Stroke, BlockStyle.FieldStrokeWidth);
                bool clickable = Spawner == null && (FieldEditing.IsEditableText(f.Field) || FieldEditing.IsDropdown(f.Field));
                bg.EdgeWidth = edgeWidth;
                bg.raycastTarget = clickable;                              // other fields pass clicks through to the block
                if (clickable) rt.gameObject.AddComponent<FieldClickTarget>().Init(this, box.Block, f.Field);
            }

            if (f.IsDropdown)
            {
                var arrow = NewRect("arrow", rt, 0, 0, f.W, f.H);
                var ag = arrow.gameObject.AddComponent<PolygonGraphic>();
                float cx = f.W - 9f, cy = f.H * 0.5f;
                ag.SetShape(new[] { new Vec2(cx - 3.5f, cy - 2f), new Vec2(cx + 3.5f, cy - 2f), new Vec2(cx, cy + 2.5f) }, BlockStyle.DropdownArrow);
                ag.raycastTarget = false;
            }

            if (string.IsNullOrEmpty(f.Text)) return;
            var textRt = NewRect("text", rt, 0, 0, f.W, f.H);
            var t = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            Configure(t);
            t.text = f.Text;
            t.color = f.Editable && !f.IsDropdown ? look.InputText : look.LabelText;
            t.alignment = f.Editable ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            if (f.IsDropdown) t.rectTransform.sizeDelta = new Vector2(f.W - 12f, f.H);
        }
    }
}
