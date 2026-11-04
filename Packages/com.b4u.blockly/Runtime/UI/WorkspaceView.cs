using System;
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

        public void SetWorkspace(Workspace ws)
        {
            workspace = ws;
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
            if (!ctrlOrCommand || IsEditingField || ActiveDialog != null) return false;
            if (key == KeyCode.Z) return shift ? Redo() : Undo();
            if (key == KeyCode.Y) return Redo();
            return false;
        }

        string SnapshotForUndo() => History?.Snapshot();
        void RecordForUndo(string before) { if (before != null) History?.Commit(before); }

        public void Rebuild()
        {
            Clear();
            if (workspace == null) return;
            EnsureMeasurer();
            Layout = new BlockLayouter(workspace, measurer).LayoutWorkspace();
            foreach (var box in Layout.Boxes) CreateBlock(box);

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Mathf.Max(0, Layout.Right), Mathf.Max(0, Layout.Bottom));
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
        public static readonly Color SnapColour = new Color(1f, 0.85f, 0.1f, 0.95f);

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
            dialog = DialogView.ShowPrompt(RootCanvas, "New variable name:", "",
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
            dialog = DialogView.ShowPrompt(RootCanvas, "Rename all \"" + v.Name + "\" variables to:", v.Name,
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
            dialog = DialogView.ShowConfirm(RootCanvas, "Delete variable",
                "Delete " + uses + " uses of the \"" + v.Name + "\" variable?", () => DeleteVariableNow(id));
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
                snapShape.SetShape(circle, SnapColour, new Color(0.45f, 0.35f, 0f, 1f), 1.5f);
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
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl)
                     || UnityEngine.Input.GetKey(KeyCode.LeftCommand) || UnityEngine.Input.GetKey(KeyCode.RightCommand);
            if (!ctrl) return;
            bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) HandleShortcut(KeyCode.Z, true, shift);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Y)) HandleShortcut(KeyCode.Y, true, shift);
        }
#endif

        public static readonly Color HighlightColour = new Color(1f, 0.85f, 0.1f, 1f);

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
            highlightShape.SetShape(Translate(box.Outline, box.X, box.Y), Color.clear, HighlightColour, 3.5f);
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
            snapMarker = null; snapShape = null;
        }

        static void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void EnsureMeasurer()
        {
            if (measurer != null && measureTemplate != null) return;
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
            var baseColour = BlockStyle.Parse(box.Block.Definition.ColourHex);
            var root = NewRect(box.Block.Type, transform, box.X, box.Y, box.W, box.H);
            blockObjects[box.Block] = root;

            var dragTarget = root.gameObject.AddComponent<BlockDragTarget>();
            dragTarget.Init(this, box.Block);
            var body = root.gameObject.AddComponent<PolygonGraphic>();
            body.SetShape(Translate(box.Outline, box.X, box.Y), BlockStyle.Fill(baseColour, box.IsShadow), BlockStyle.Stroke(baseColour, box.IsShadow));

            foreach (var slot in box.Slots)
            {
                var s = NewRect("slot " + slot.Input.Name, root, 0, 0, box.W, box.H);
                s.gameObject.AddComponent<PolygonGraphic>().SetShape(Translate(slot.Polygon, box.X, box.Y), BlockStyle.Hole(baseColour, box.IsShadow), BlockStyle.Stroke(baseColour, box.IsShadow), 1f);
                s.GetComponent<PolygonGraphic>().raycastTarget = false;
            }

            foreach (var f in box.Fields) CreateField(root, box, f, baseColour);
        }

        void CreateField(RectTransform parent, BlockBox box, FieldBox f, Color baseColour)
        {
            float lx = f.X - box.X, ly = f.Y - box.Y;
            var rt = NewRect(f.Field.Name ?? "label", parent, lx, ly, f.W, f.H);

            if (f.Editable)
            {
                bool dropdown = f.IsDropdown;
                var rect = new[] { new Vec2(0, 0), new Vec2(f.W, 0), new Vec2(f.W, f.H), new Vec2(0, f.H) };
                var bg = rt.gameObject.AddComponent<PolygonGraphic>();
                bg.SetShape(rect, dropdown ? BlockStyle.DropdownFill(baseColour, box.IsShadow) : BlockStyle.InputFill,
                            BlockStyle.Stroke(baseColour, box.IsShadow), 1f);
                bool clickable = Spawner == null && (FieldEditing.IsEditableText(f.Field) || FieldEditing.IsDropdown(f.Field));
                bg.raycastTarget = clickable;                              // other fields pass clicks through to the block
                if (clickable) rt.gameObject.AddComponent<FieldClickTarget>().Init(this, box.Block, f.Field);
            }

            if (f.IsDropdown)
            {
                var arrow = NewRect("arrow", rt, 0, 0, f.W, f.H);
                var ag = arrow.gameObject.AddComponent<PolygonGraphic>();
                float cx = f.W - 9f, cy = f.H * 0.5f;
                ag.SetShape(new[] { new Vec2(cx - 3.5f, cy - 2f), new Vec2(cx + 3.5f, cy - 2f), new Vec2(cx, cy + 2.5f) }, new Color(1f, 1f, 1f, 0.9f));
                ag.raycastTarget = false;
            }

            if (string.IsNullOrEmpty(f.Text)) return;
            var textRt = NewRect("text", rt, 0, 0, f.W, f.H);
            var t = textRt.gameObject.AddComponent<TextMeshProUGUI>();
            Configure(t);
            t.text = f.Text;
            t.color = f.Editable && !f.IsDropdown ? BlockStyle.InputText : BlockStyle.LabelText;
            t.alignment = f.Editable ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            if (f.IsDropdown) t.rectTransform.sizeDelta = new Vector2(f.W - 12f, f.H);
        }
    }
}
