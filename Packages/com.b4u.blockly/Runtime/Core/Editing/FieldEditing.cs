using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Blockly.Json;
using Blockly.Layout;

namespace Blockly.Editing
{
    public enum DropdownAction { Select, RenameVariable, DeleteVariable }

    /// <summary>One entry of a dropdown field's menu.</summary>
    public sealed class DropdownItem
    {
        public string Label;
        /// <summary>The value stored in the field (a variable's id for variable fields). For Rename/Delete: the variable they act on.</summary>
        public string Value;
        public DropdownAction Action;
        public bool Selected;
    }

    /// <summary>Typed-in values for editable fields: validation like Blockly's fields, plus the side effects of a change.</summary>
    public static class FieldEditing
    {
        public static bool IsEditableText(Field f)
            => f.Spec.FieldType == "field_input" || f.Spec.FieldType == "field_number";

        public static bool IsDropdown(Field f)
            => f.Spec.FieldType == "field_dropdown" || f.Spec.FieldType == "field_variable";

        /// <summary>The menu for a dropdown or variable field. Variables are sorted by name, followed by Rename / Delete for the current one.</summary>
        public static List<DropdownItem> GetDropdownItems(Workspace ws, Field field)
        {
            var items = new List<DropdownItem>();
            if (field.Spec.FieldType == "field_variable")
            {
                foreach (var v in ws.Variables.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase))
                    items.Add(new DropdownItem { Label = v.Name, Value = v.Id, Selected = v.Id == field.Value });
                var current = ws.GetVariableById(field.Value);
                if (current != null)
                {
                    items.Add(new DropdownItem { Label = Loc.T("Rename variable..."), Value = current.Id, Action = DropdownAction.RenameVariable });
                    items.Add(new DropdownItem { Label = Loc.T("Delete the \"{0}\" variable", current.Name), Value = current.Id, Action = DropdownAction.DeleteVariable });
                }
            }
            else if (field.Spec.Options != null)
            {
                foreach (var o in field.Spec.Options)
                    items.Add(new DropdownItem { Label = Blockly.Layout.BlockLayouter.StripBidi(Loc.T(o.Label)), Value = o.Value, Selected = o.Value == field.Value });
            }
            return items;
        }

        /// <summary>
        /// Converts what the user typed into the value the field would store. Returns false when Blockly would reject the
        /// input (the field then keeps its old value).
        /// </summary>
        public static bool TryNormalize(Field field, string input, out string value)
        {
            value = null;
            if (field.Spec.FieldType == "field_dropdown")
            {
                bool listed = field.Spec.Options != null && field.Spec.Options.Any(o => o.Value == input);
                if (listed) value = input;
                return listed;
            }
            if (field.Spec.FieldType == "field_variable")
            {
                value = input;
                return input != null;                       // existence is checked against the workspace in SetValue
            }
            if (field.Spec.FieldType != "field_number")
            {
                value = input ?? "";
                return true;
            }

            // FieldNumber: Number(text.replace(/infinity/i, 'Infinity').replace(/,/g, '') || '0'), clamp, round to precision
            string s = ReplaceIgnoreCase(input ?? "", "infinity", "Infinity").Replace(",", "");
            double n = JsValue.ParseNumber(s.Length == 0 ? "0" : s);
            if (double.IsNaN(n)) return false;

            double min = field.Spec.Min ?? double.NegativeInfinity, max = field.Spec.Max ?? double.PositiveInfinity;
            n = Math.Min(Math.Max(n, min), max);

            double precision = field.Spec.Precision ?? 0;
            if (precision > 0 && !double.IsInfinity(n))
            {
                n = Math.Floor(n / precision + 0.5) * precision;              // JS Math.round
                int dp = DecimalPlaces(precision);
                n = double.Parse(n.ToString("F" + dp, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);   // Number(n.toFixed(dp))
            }
            if (n == 0) n = 0;                                                 // -0 is stored as 0
            value = JsValue.NumberToString(n);
            return true;
        }

        static int DecimalPlaces(double precision)
        {
            string p = JsValue.NumberToString(precision);
            int dot = p.IndexOf('.');
            return dot < 0 ? 0 : p.Length - dot - 1;
        }

        /// <summary>Replaces every occurrence of <paramref name="word"/> ignoring case. Continues after each replacement,
        /// otherwise a replacement that itself matches (Infinity vs infinity) would be found again forever.</summary>
        static string ReplaceIgnoreCase(string s, string word, string with)
        {
            var sb = new System.Text.StringBuilder();
            int pos = 0, i;
            while ((i = s.IndexOf(word, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                sb.Append(s, pos, i - pos).Append(with);
                pos = i + word.Length;
            }
            return sb.Append(s, pos, s.Length - pos).ToString();
        }

        /// <summary>
        /// Applies a typed or chosen value to a field. Returns true if the stored value changed. A procedure definition's name is
        /// kept unique and its calls follow the rename.
        /// </summary>
        public static bool SetValue(Workspace ws, Block block, Field field, string input, BlockLayouter layouter = null)
        {
            if (!TryNormalize(field, input, out var value)) return false;
            if (field.Spec.FieldType == "field_variable" && ws.GetVariableById(value) == null) return false;

            if (ProcedureInfo.IsDefinition(block) && field.Name == "NAME")
                return Rename(ws, block, field, value);

            if (field.Value == value) return false;

            // lists_getIndex: choosing "remove" turns the block into a statement (and back); it has to leave its slot first
            if (block.Type == "lists_getIndex" && field.Name == "MODE")
            {
                bool wantStatement = value == "REMOVE";
                if (wantStatement != (block.Output == null))
                {
                    WorkspaceEditing.Unplug(ws, layouter, block, heal: true, bump: 24f);
                    GetIndexMutator.SetStatement(block, wantStatement);
                }
            }

            field.Value = value;
            block.Definition.Mutator?.FieldsChanged(block);
            UnplugIncompatible(ws, layouter, block);
            return true;
        }

        /// <summary>A change of shape can leave a plugged-in block that no longer fits (e.g. text into a list input): move it out.</summary>
        static void UnplugIncompatible(Workspace ws, BlockLayouter layouter, Block block)
        {
            foreach (var input in block.Inputs.ToList())
            {
                var c = input.Connection;
                if (c?.Target == null || c.CheckCompatible(c.Target)) continue;
                WorkspaceEditing.Unplug(ws, layouter, c.Target.Owner, heal: false, bump: 24f);
            }
            var own = block.Output ?? block.Previous;
            if (own?.Target != null && !own.CheckCompatible(own.Target))
                WorkspaceEditing.Unplug(ws, layouter, block, heal: false, bump: 24f);
        }

        // ------------------------------------------------------------------ procedure rename
        /// <summary>Blockly's naming rules: trimmed, single spaces, never empty, and a trailing number is bumped on a clash.</summary>
        public static string LegalProcedureName(Workspace ws, Block definition, string wanted)
        {
            string name = string.Join(" ", (wanted ?? "").Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (name.Length == 0) name = "unnamed";
            var taken = new HashSet<string>(ws.AllBlocks().Where(b => ProcedureInfo.IsDefinition(b) && b != definition).Select(b => b.GetFieldValue("NAME")));
            while (taken.Contains(name))
            {
                int end = name.Length;
                int start = end;
                while (start > 0 && char.IsDigit(name[start - 1])) start--;
                name = start == end ? name + "2" : name.Substring(0, start) + (long.Parse(name.Substring(start), CultureInfo.InvariantCulture) + 1);
            }
            return name;
        }

        static bool Rename(Workspace ws, Block definition, Field field, string typed)
        {
            string oldName = field.Value;
            string newName = LegalProcedureName(ws, definition, typed);
            if (newName == oldName) return false;

            field.Value = newName;
            foreach (var call in ws.AllBlocks().Where(b => (b.Type == "procedures_callnoreturn" || b.Type == "procedures_callreturn") && ProcedureInfo.Name(b) == oldName))
            {
                if (call.ExtraState == null) call.ExtraState = new Dictionary<string, object>();
                call.ExtraState["name"] = newName;
                foreach (var f in call.GetInput("TOPROW")?.Fields ?? new List<Field>())
                    if (f.IsLabel && f.Value == oldName) f.Value = newName;          // the label showing the callee's name
            }
            return true;
        }
    }
}
