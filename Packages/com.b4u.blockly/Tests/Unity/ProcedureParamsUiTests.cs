using System.Linq;
using Blockly.Editing;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>Editing a procedure's parameters through the "params" button and its dialog.</summary>
    public class ProcedureParamsUiTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            foreach (var g in Object.FindObjectsByType<DialogView>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
        }

        const string Xml =
            "<xml xmlns=\"https://developers.google.com/blockly/xml\">" +
            "<block type=\"procedures_defnoreturn\" x=\"20\" y=\"20\"><field name=\"NAME\">greet</field><statement name=\"STACK\">" +
            "<block type=\"text_print\"><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">hi</field></block></value></block></statement></block>" +
            "<block type=\"procedures_callnoreturn\" x=\"20\" y=\"250\"><mutation name=\"greet\"></mutation></block></xml>";

        WorkspaceView NewView()
        {
            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            view.SetWorkspace(StandardBlockSet.Load(Xml));
            return view;
        }

        static Block Def(WorkspaceView v) => v.Workspace.AllBlocks().First(ProcedureInfo.IsDefinition);
        static Block Call(WorkspaceView v) => v.Workspace.AllBlocks().First(b => b.Type == "procedures_callnoreturn");
        static bool Shows(WorkspaceView v, string text) => v.GetComponentsInChildren<TMP_Text>().Any(t => t.text == text);

        [Test]
        public void TheButtonAppearsOnlyOnASelectedDefinition()
        {
            var view = NewView();
            Assert.IsNull(view.ParamsButton);
            view.Select(Call(view));
            Assert.IsNull(view.ParamsButton, "a call has no parameter editor");
            view.Select(Def(view));
            Assert.IsNotNull(view.ParamsButton);
            Assert.IsTrue(Shows(view, "params"));
            view.Select(null);
            Assert.IsNull(view.ParamsButton);
        }

        [Test]
        public void ClickingTheButtonOpensADialogWithTheCurrentParameters()
        {
            var view = NewView();
            view.ApplyParams(Def(view), new[] { "a", "b" });
            view.Select(Def(view));
            view.ParamsButton.GetComponent<Button>().onClick.Invoke();
            var d = view.ActiveDialog;
            Assert.IsNotNull(d);
            Assert.AreEqual("a, b", d.Input.text);
            d.Cancel();
        }

        [Test]
        public void TypingNamesInTheDialogChangesTheProcedureAndItsCallsInOneUndoStep()
        {
            var view = NewView();
            string before = WorkspaceJson.Save(view.Workspace);
            int edits = 0; view.WorkspaceEdited += () => edits++;
            view.RequestEditParams(Def(view));
            var d = view.ActiveDialog;
            Assert.IsNotNull(d);
            Assert.IsFalse(d.Submit("x, y, x"), "duplicates are refused");
            StringAssert.Contains("Duplicate", d.ErrorText);
            Assert.IsTrue(d.Submit("x, y"));
            Assert.IsNull(view.ActiveDialog);
            Assert.AreEqual(1, edits);

            CollectionAssert.AreEqual(new[] { "x", "y" }, ProcedureEditing.CurrentNames(Def(view)));
            Assert.IsNotNull(Call(view).GetInput("ARG1"));
            Assert.IsTrue(Shows(view, "with: x, y"));

            Assert.IsTrue(view.Undo());
            Assert.AreEqual(before, WorkspaceJson.Save(view.Workspace));
            Assert.IsFalse(Shows(view, "with: x, y"));
            Assert.IsTrue(view.Redo());
            Assert.IsTrue(Shows(view, "with: x, y"));
        }

        [Test]
        public void SubmittingTheSameNamesIsNotAnEdit()
        {
            var view = NewView();
            view.RequestEditParams(Def(view));
            Assert.IsTrue(view.ActiveDialog.Submit(""));
            Assert.IsFalse(view.CanUndo);
        }

        [Test]
        public void TheDialogOnlyOpensForADefinition()
        {
            var view = NewView();
            view.RequestEditParams(Call(view));
            Assert.IsNull(view.ActiveDialog);
        }
    }
}
