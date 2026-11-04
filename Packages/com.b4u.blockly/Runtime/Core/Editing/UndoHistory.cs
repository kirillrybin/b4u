using System;
using System.Collections.Generic;

namespace Blockly.Editing
{
    /// <summary>
    /// Undo/redo by snapshots: before a user action the caller takes <see cref="Snapshot"/>, afterwards <see cref="Commit"/>
    /// records it if (and only if) the workspace really changed. An action that ends up changing nothing (a cancelled
    /// drag, typing the same value) leaves no step. Undo/redo restore into the same <see cref="Workspace"/> object.
    /// </summary>
    public sealed class UndoHistory
    {
        readonly Workspace workspace;
        readonly int limit;
        readonly List<string> undo = new List<string>();
        readonly List<string> redo = new List<string>();

        public UndoHistory(Workspace workspace, int limit = 100)
        {
            this.workspace = workspace;
            this.limit = limit;
        }

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;

        /// <summary>Raised whenever the stacks change (to refresh Undo/Redo buttons).</summary>
        public event Action Changed;

        public string Snapshot() => WorkspaceJson.Save(workspace);

        /// <summary>Records <paramref name="before"/> as an undo step if the workspace now differs from it. Clears redo.</summary>
        public bool Commit(string before)
        {
            if (before == null || before == Snapshot()) return false;
            undo.Add(before);
            if (undo.Count > limit) undo.RemoveAt(0);
            redo.Clear();
            Changed?.Invoke();
            return true;
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            string current = Snapshot();
            string target = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            redo.Add(current);
            WorkspaceJson.Restore(workspace, target);
            Changed?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            string current = Snapshot();
            string target = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            undo.Add(current);
            WorkspaceJson.Restore(workspace, target);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            undo.Clear(); redo.Clear();
            Changed?.Invoke();
        }
    }
}
