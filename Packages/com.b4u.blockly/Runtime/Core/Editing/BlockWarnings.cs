using System.Collections.Generic;
using System.Linq;

namespace Blockly.Editing
{
    /// <summary>
    /// Finds the blocks of a workspace that cannot do their job (the warnings Blockly shows as a triangle on the block): a call of a procedure that
    /// does not exist, an unknown variable, an empty input of a block that is meaningless without it, a value block that is plugged into nothing.
    /// Disabled blocks, shadow blocks and everything inside a disabled block are left out. It never changes the workspace.
    /// </summary>
    public static class BlockWarnings
    {
        /// <summary>
        /// Block types whose inputs must not be empty, with the names of those inputs. Add your own blocks here; an input counts as empty when
        /// nothing and no shadow block is plugged into it.
        /// </summary>
        public static readonly Dictionary<string, string[]> RequiredInputs = new Dictionary<string, string[]>
        {
            { "variables_set", new[] { "VALUE" } },
            { "controls_whileUntil", new[] { "BOOL" } },
            { "controls_if", new[] { "IF0" } },
            { "logic_negate", new[] { "BOOL" } },
            { "procedures_ifreturn", new[] { "CONDITION" } },
            { "procedures_defreturn", new[] { "RETURN" } },
        };

        /// <summary>The warning text of every block that has one (several problems of one block are on separate lines).</summary>
        public static Dictionary<Block, string> Check(Workspace ws)
        {
            var result = new Dictionary<Block, string>();
            if (ws == null) return result;
            var procedures = new HashSet<string>(ws.AllBlocks().Where(ProcedureInfo.IsDefinition).Select(ProcedureInfo.Name));
            foreach (var b in ws.AllBlocks())
            {
                if (b.IsShadow || IsOff(b)) continue;
                var lines = new List<string>();

                if (IsCall(b) && !procedures.Contains(ProcedureInfo.Name(b) ?? ""))
                    lines.Add(Loc.T("Call to an undefined procedure “{0}”", ProcedureInfo.Name(b)));

                if (b.Type == "variables_get" || b.Type == "variables_set")
                {
                    string v = b.GetFieldValue("VAR");
                    if (string.IsNullOrEmpty(v) || (ws.GetVariableById(v) == null && ws.GetVariableByName(v) == null))
                        lines.Add(Loc.T("Variable “{0}” is not defined", v ?? ""));
                }

                if (RequiredInputs.TryGetValue(b.Type, out var names))
                    foreach (var n in names)
                    {
                        var c = b.GetInput(n)?.Connection;
                        if (c != null && c.Target == null && c.Shadow == null)
                        {
                            lines.Add(Loc.T("This block needs a value in every empty input"));
                            break;
                        }
                    }

                if (b.Output != null && b.Parent == null)
                    lines.Add(Loc.T("This block should be connected to a statement or value input"));

                if (lines.Count > 0) result[b] = string.Join("\n", lines);
            }
            return result;
        }

        static bool IsCall(Block b) => b.Type == "procedures_callnoreturn" || b.Type == "procedures_callreturn";

        static bool IsOff(Block b)
        {
            for (var p = b; p != null; p = p.Parent) if (p.Disabled) return true;
            return false;
        }
    }
}
