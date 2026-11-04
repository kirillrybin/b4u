using System;
using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;

namespace Blockly.Editing
{
    /// <summary>Creating, renaming and deleting variables, following Blockly's rules for names and for what a delete takes with it.</summary>
    public static class VariableEditing
    {
        /// <summary>Whitespace runs become one space and the ends are trimmed (what Blockly does with a typed variable name).</summary>
        public static string Clean(string typed)
            => string.Join(" ", (typed ?? "").Split(new[] { ' ', '\t', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>Returns null if <paramref name="typed"/> is acceptable, else the message to show. <paramref name="exceptId"/> is the variable being renamed.</summary>
        public static string Validate(Workspace ws, string typed, string exceptId, out string cleaned)
        {
            string name = Clean(typed);
            cleaned = name;
            if (name.Length == 0) return "The name cannot be empty.";
            var clash = ws.Variables.FirstOrDefault(v => v.Id != exceptId && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            return clash != null ? "A variable named \"" + clash.Name + "\" already exists." : null;
        }

        /// <summary>Creates a variable. Returns it, or null with <paramref name="error"/> set when the name is not acceptable.</summary>
        public static Variable Create(Workspace ws, string typed, out string error)
        {
            error = Validate(ws, typed, null, out var name);
            return error != null ? null : ws.GetOrCreateVariable(name);
        }

        public static bool Rename(Workspace ws, string id, string typed, out string error)
        {
            var v = ws.GetVariableById(id);
            if (v == null) { error = "No such variable."; return false; }
            error = Validate(ws, typed, id, out var name);
            if (error != null) return false;
            v.Name = name;                                    // blocks show the name through the variable, so they all follow
            return true;
        }

        public static IEnumerable<Block> UsesOf(Workspace ws, string id)
            => ws.AllBlocks().Where(b => b.AllFields().Any(f => f.Spec.FieldType == "field_variable" && f.Value == id));

        public static int CountUses(Workspace ws, string id) => UsesOf(ws, id).Count();

        /// <summary>Deletes the variable and every block that uses it (their stacks close up around the gap).</summary>
        public static void Delete(Workspace ws, BlockLayouter layouter, string id)
        {
            foreach (var use in UsesOf(ws, id).ToList())
                if (ws.AllBlocks().Contains(use)) WorkspaceEditing.Delete(ws, layouter, use, heal: true);
            ws.Variables.RemoveAll(v => v.Id == id);
        }
    }
}
