using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;

namespace Blockly.Editing
{
    /// <summary>Changing the parameter list of a procedure definition, and keeping its calls and body in step.</summary>
    public static class ProcedureEditing
    {
        /// <summary>"a, b ,c" -> a, b, c. Empty pieces are dropped, so an empty text means no parameters.</summary>
        public static List<string> ParseNames(string text)
        {
            var names = new List<string>();
            foreach (var piece in (text ?? "").Split(','))
            {
                string name = string.Join(" ", piece.Split(new[] { ' ', '\t', '\n' }, System.StringSplitOptions.RemoveEmptyEntries));
                if (name.Length > 0) names.Add(name);
            }
            return names;
        }

        /// <summary>The first thing wrong with a parameter list typed by the user, or null if it is fine.</summary>
        public static string Validate(string text)
        {
            var seen = new HashSet<string>();
            foreach (var name in ParseNames(text))
                if (!seen.Add(name)) return "Duplicate parameter \"" + name + "\".";
            return null;
        }

        public static List<string> CurrentNames(Block definition) => ProcedureInfo.Params(definition).Select(p => p.Name).ToList();

        /// <summary>
        /// Replaces the parameters of <paramref name="definition"/> by <paramref name="names"/>. A parameter at the same
        /// position keeps its variable (so the body keeps working) and the variable is renamed; new positions get a
        /// variable named like the parameter. Every call gets matching ARGn inputs: values already plugged into
        /// inputs that still exist stay, the ones whose input disappeared become loose top-level blocks.
        /// Returns false when nothing changed.
        /// </summary>
        public static bool SetParams(Workspace ws, BlockLayouter layouter, Block definition, IList<string> names)
        {
            if (!ProcedureInfo.IsDefinition(definition)) return false;
            names = names.Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
            var old = ProcedureInfo.Params(definition);
            if (old.Select(p => p.Name).SequenceEqual(names)) return false;

            var list = new List<object>();
            var used = new HashSet<string>();
            for (int i = 0; i < names.Count; i++)
            {
                Variable v = null;
                if (i < old.Count && old[i].Id != null)
                {
                    v = ws.GetVariableById(old[i].Id);
                    if (v != null && v.Name != names[i])
                    {
                        var other = ws.GetVariableByName(names[i]);
                        if (other == null) v.Name = names[i];     // nobody else has the name: rename, the body follows
                        else v = other;                           // the name is taken by another variable: use that one
                    }
                }
                v = v ?? ws.GetOrCreateVariable(names[i]);
                if (!used.Add(v.Id)) v = ws.GetOrCreateVariable(names[i] + "_" + (i + 1));   // two parameters resolved to one variable
                used.Add(v.Id);
                list.Add(new Dictionary<string, object> { { "name", v.Name }, { "id", v.Id } });
            }

            var state = definition.ExtraState ?? (definition.ExtraState = new Dictionary<string, object>());
            if (list.Count > 0) state["params"] = list; else state.Remove("params");
            ReshapeDefinition(definition);

            string name = ProcedureInfo.Name(definition);
            var finalNames = ProcedureInfo.Params(definition).Select(p => p.Name).ToList();
            foreach (var call in ws.AllBlocks().Where(b => IsCall(b) && ProcedureInfo.Name(b) == name).ToList())
                ReshapeCall(ws, layouter, call, finalNames);
            return true;
        }

        static bool IsCall(Block b) => b.Type == "procedures_callnoreturn" || b.Type == "procedures_callreturn";

        static void ReshapeDefinition(Block definition)
        {
            var top = definition.GetInput("TOPROW");
            top?.Fields.RemoveAll(f => f.IsLabel && f.Value != null && f.Value.StartsWith("with: "));
            definition.Definition.Mutator?.Apply(definition, definition.ExtraState);
        }

        static void ReshapeCall(Workspace ws, BlockLayouter layouter, Block call, List<string> names)
        {
            var state = call.ExtraState ?? (call.ExtraState = new Dictionary<string, object>());
            if (names.Count > 0) state["params"] = names.Select(n => (object)n).ToList(); else state.Remove("params");

            // inputs beyond the new count go, their blocks stay in the workspace
            for (int i = names.Count; ; i++)
            {
                var gone = call.GetInput("ARG" + i);
                if (gone == null) break;
                var child = gone.Connection?.Target?.Owner;
                if (child != null) WorkspaceEditing.Unplug(ws, layouter, child, heal: false, bump: 24f);
                call.Inputs.Remove(gone);
            }
            for (int i = 0; i < names.Count; i++)
            {
                var input = call.GetInput("ARG" + i);
                if (input == null)
                {
                    input = Mutators.ValueInput(call, "ARG" + i);
                    input.Fields.Add(Mutators.Label(names[i]));
                    call.Inputs.Add(input);
                }
                else
                {
                    var label = input.Fields.Find(f => f.IsLabel);
                    if (label != null) label.Value = names[i];
                }
            }
        }
    }
}
