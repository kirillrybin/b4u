using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI.Tests
{
    /// <summary>Notes on the workspace: drawn under the blocks, moved by the header, collapsed, deleted, one undo step each.</summary>
    public class WorkspaceNoteUiTests
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        const string Xml = "<xml xmlns=\"https://developers.google.com/blockly/xml\"><block type=\"text_print\" x=\"20\" y=\"20\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">a</field></block></value></block></xml>";

        sealed class NoSpawn : IBlockSpawner { public bool TrySpawn(Block b, PointerEventData e, WorkspaceView v) => false; }

        WorkspaceView NewView(bool flyout = false)
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0, 1);
            viewport.sizeDelta = new Vector2(3000, 3000);
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(viewport, false);
            var vrt = (RectTransform)go.transform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0, 1);
            vrt.anchoredPosition = Vector2.zero;
            var view = go.GetComponent<WorkspaceView>();
            if (flyout) view.Spawner = new NoSpawn();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        [Test]
        public void AddingANoteDrawsItUnderTheBlocksAndIsOneUndoStep()
        {
            var v = NewView();
            var note = v.AddNote(new Vector2(300, 200));
            Assert.IsNotNull(note);
            Assert.AreEqual(1, v.Workspace.Comments.Count);
            Assert.AreEqual(1, v.Notes.Count);
            var noteTransform = v.Notes[0].transform;
            var block = v.transform.Cast<Transform>().First(t => t.name == "text_print");
            Assert.Less(noteTransform.GetSiblingIndex(), block.GetSiblingIndex(), "notes lie under the blocks");
            Assert.IsTrue(v.History.Undo());
            Assert.AreEqual(0, v.Workspace.Comments.Count);
        }

        [Test]
        public void TheDeleteButtonRemovesTheNote()
        {
            var v = NewView();
            var note = v.AddNote(new Vector2(10, 10));
            v.NoteOf(note).DeleteButton.onClick.Invoke();
            Assert.AreEqual(0, v.Workspace.Comments.Count);
            Assert.AreEqual(0, v.Notes.Count);
        }

        [Test]
        public void CollapsingHidesTheBodyAndExpandingBringsItBack()
        {
            var v = NewView();
            var note = v.AddNote(new Vector2(10, 10));
            Assert.IsNotNull(v.NoteOf(note).Input);
            v.NoteOf(note).CollapseButton.onClick.Invoke();
            Assert.IsTrue(note.Collapsed);
            Assert.IsNull(v.NoteOf(note).Input);
            Assert.AreEqual(WorkspaceNote.HeaderHeight, ((RectTransform)v.NoteOf(note).transform).sizeDelta.y);
            v.NoteOf(note).CollapseButton.onClick.Invoke();
            Assert.IsNotNull(v.NoteOf(note).Input);
        }

        [Test]
        public void TypingGoesIntoTheNoteAndIsOneUndoStepWhenTheFieldIsLeft()
        {
            var v = NewView();
            var note = v.AddNote(new Vector2(10, 10));
            var n = v.NoteOf(note);
            n.Input.onSelect.Invoke("");
            n.Input.text = "buy milk";
            n.Input.onEndEdit.Invoke("buy milk");
            Assert.AreEqual("buy milk", note.Text);
            v.History.Undo();
            Assert.AreEqual("", v.Workspace.Comments[0].Text);
        }

        [Test]
        public void DraggingTheNoteMovesItAndIsOneUndoStep()
        {
            var v = NewView();
            var note = v.AddNote(new Vector2(100, 100));
            var n = v.NoteOf(note);
            var e = new PointerEventData(EventSystem.current ?? new GameObject("es", typeof(EventSystem)).GetComponent<EventSystem>()) { position = Vector2.zero };
            n.OnBeginDrag(e);
            note.X = 140; note.Y = 160;                                   // what OnDrag does with the pointer position
            n.OnEndDrag(e);
            Assert.AreEqual(140, v.Workspace.Comments[0].X);
            v.History.Undo();
            Assert.AreEqual(100, v.Workspace.Comments[0].X);
        }

        [Test]
        public void TurningNotesOffHidesThemAndTheMenuButKeepsThem()
        {
            var v = NewView();
            v.AddNote(new Vector2(10, 10));
            v.NotesEnabled = false;
            Assert.AreEqual(0, v.Notes.Count);
            Assert.AreEqual(1, v.Workspace.Comments.Count);
            Assert.IsNull(v.AddNote(new Vector2(0, 0)));
            v.NotesEnabled = true;
            Assert.AreEqual(1, v.Notes.Count);
        }

        [Test]
        public void AFlyoutHasNoNotes()
        {
            var v = NewView(flyout: true);
            Assert.IsNull(v.AddNote(new Vector2(0, 0)));
        }

        [Test]
        public void TheScrollAreaGrowsToCoverANote()
        {
            var v = NewView();
            v.AddNote(new Vector2(2000, 1500));
            var size = ((RectTransform)v.transform).sizeDelta;
            Assert.GreaterOrEqual(size.x, 2000 + 160);
            Assert.GreaterOrEqual(size.y, 1500 + 100);
        }
    }
}
