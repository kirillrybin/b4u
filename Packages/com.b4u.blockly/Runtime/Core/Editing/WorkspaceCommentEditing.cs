namespace Blockly.Editing
{
    /// <summary>Changes to the notes of a workspace (Blockly's workspace comments). The caller records the undo step.</summary>
    public static class WorkspaceCommentEditing
    {
        public const double DefaultWidth = 160, DefaultHeight = 100;

        /// <summary>Puts a new empty note with its top-left corner at (x, y) on top of the others.</summary>
        public static WorkspaceComment Add(Workspace ws, double x, double y)
        {
            var note = new WorkspaceComment { Id = ws.NewCommentId(), X = x, Y = y, W = DefaultWidth, H = DefaultHeight };
            ws.Comments.Add(note);
            return note;
        }

        public static bool Remove(Workspace ws, WorkspaceComment note) => ws.Comments.Remove(note);

        public static bool Move(WorkspaceComment note, double x, double y)
        {
            if (note.X == x && note.Y == y) return false;
            note.X = x; note.Y = y;
            return true;
        }

        public static bool SetText(WorkspaceComment note, string text)
        {
            text = text ?? "";
            if (note.Text == text) return false;
            note.Text = text;
            return true;
        }

        public static bool SetCollapsed(WorkspaceComment note, bool collapsed)
        {
            if (note.Collapsed == collapsed) return false;
            note.Collapsed = collapsed;
            return true;
        }
    }
}
