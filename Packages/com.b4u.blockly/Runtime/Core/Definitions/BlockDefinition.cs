using System;
using System.Collections.Generic;
using System.Globalization;
using Blockly.Json;

namespace Blockly
{
    public enum ArgKind { Label, Field, InputValue, InputStatement, InputDummy }

    public sealed class DropdownOption
    {
        public string Label;
        public string Value;
    }

    /// <summary>One item of a block's message layout: a label, an editable field, or an input.</summary>
    public sealed class ArgSpec
    {
        public ArgKind Kind;
        public string Name;
        public string FieldType;           // e.g. field_input, field_number, field_dropdown, field_variable ...
        public string Text;                // label text / default text value
        public string[] Check;             // type check for inputs
        public string Align;
        public List<DropdownOption> Options;
        public double? Min, Max, Precision;
        public bool Checked;
        public string VariableType;
        public string Src;                 // field_image: where the picture comes from (resolved by the UI)
        public float ImageWidth, ImageHeight;
        public string TranslatedPrefix;    // a label made of this translated start and a name the user typed ("with:" + the parameters)
        public bool NoTranslate;           // a label that shows a name the user typed (not translated)
    }

    /// <summary>
    /// Mutators change a block's shape from <c>extraState</c> (controls_if, lists_create_with, procedures...).
    /// </summary>
    public interface IBlockMutator
    {
        /// <summary>True when Blockly stores this block's extraState as an XML string (mutationToDom only).</summary>
        bool SerializeAsXml { get; }
        /// <summary>Applies state to a freshly built block. <paramref name="state"/> is null for a new block.</summary>
        void Apply(Block block, Dictionary<string, object> extraState);
        /// <summary>
        /// Called after field values were loaded or edited (idempotent). Lets a block reshape itself from a
        /// dropdown, e.g. lists_getIndex shows the AT input only for "# / # from end".
        /// </summary>
        void FieldsChanged(Block block);
        Dictionary<string, object> Save(Block block);
    }

    public sealed class BlockDefinition
    {
        public string Type;
        public List<ArgSpec> Parts = new List<ArgSpec>();
        public bool HasOutput;
        public string[] OutputCheck;
        public bool HasPrevious, HasNext;
        public string[] PreviousCheck, NextCheck;
        public string ColourHex = "#a5a5a5";
        public bool? InputsInline;
        public string Tooltip, HelpUrl;
        public IBlockMutator Mutator;

        /// <summary>Parses a block definition in the Blockly JSON definition format.</summary>
        public static BlockDefinition FromJson(Dictionary<string, object> json)
        {
            var def = new BlockDefinition { Type = Json.Json.GetString(json, "type") };
            if (string.IsNullOrEmpty(def.Type)) throw new FormatException("Block definition has no 'type'");

            for (int n = 0; ; n++)
            {
                string message = Json.Json.GetString(json, "message" + n);
                if (message == null) break;
                var args = Json.Json.GetArray(json, "args" + n) ?? new List<object>();
                ParseMessage(def, message, args);
            }

            if (json.ContainsKey("output")) { def.HasOutput = true; def.OutputCheck = ReadCheck(json["output"]); }
            if (json.ContainsKey("previousStatement")) { def.HasPrevious = true; def.PreviousCheck = ReadCheck(json["previousStatement"]); }
            if (json.ContainsKey("nextStatement")) { def.HasNext = true; def.NextCheck = ReadCheck(json["nextStatement"]); }
            if (json.TryGetValue("colour", out var col)) def.ColourHex = Colour.Parse(col);
            if (json.TryGetValue("inputsInline", out var il) && il is bool b) def.InputsInline = b;
            string mutator = Json.Json.GetString(json, "mutator");
            if (mutator != null) def.Mutator = Mutators.Get(mutator);
            def.Tooltip = Json.Json.GetString(json, "tooltip");
            def.HelpUrl = Json.Json.GetString(json, "helpUrl");
            return def;
        }

        static void ParseMessage(BlockDefinition def, string message, List<object> args)
        {
            int i = 0;
            bool trailing = false; // fields/labels not yet closed by an input
            var text = new System.Text.StringBuilder();
            void FlushText()
            {
                var t = text.ToString().Trim();
                text.Clear();
                if (t.Length > 0) { def.Parts.Add(new ArgSpec { Kind = ArgKind.Label, Text = t }); trailing = true; }
            }
            while (i < message.Length)
            {
                char c = message[i];
                if (c == '%' && i + 1 < message.Length && message[i + 1] == '%') { text.Append('%'); i += 2; continue; }
                if (c == '%' && i + 1 < message.Length && char.IsDigit(message[i + 1]))
                {
                    int j = i + 1;
                    while (j < message.Length && char.IsDigit(message[j])) j++;
                    int idx = int.Parse(message.Substring(i + 1, j - i - 1), CultureInfo.InvariantCulture);
                    FlushText();
                    if (idx >= 1 && idx <= args.Count && args[idx - 1] is Dictionary<string, object> a)
                    {
                        var spec = ParseArg(a);
                        def.Parts.Add(spec);
                        trailing = spec.Kind == ArgKind.Label || spec.Kind == ArgKind.Field;
                    }
                    i = j;
                    continue;
                }
                text.Append(c);
                i++;
            }
            FlushText();
            // Like Blockly's interpolate_: leftover fields at the end of a message form their own dummy input.
            if (trailing) def.Parts.Add(new ArgSpec { Kind = ArgKind.InputDummy });
        }

        static ArgSpec ParseArg(Dictionary<string, object> a)
        {
            string type = Json.Json.GetString(a, "type") ?? "";
            var spec = new ArgSpec
            {
                Name = Json.Json.GetString(a, "name"),
                Check = ReadCheck(a.TryGetValue("check", out var ck) ? ck : null),
                Align = Json.Json.GetString(a, "align"),
                FieldType = type,
            };
            switch (type)
            {
                case "input_value": spec.Kind = ArgKind.InputValue; break;
                case "input_statement": spec.Kind = ArgKind.InputStatement; break;
                case "input_dummy": case "input_end_row": spec.Kind = ArgKind.InputDummy; break;
                case "field_image":
                    spec.Kind = ArgKind.Label;     // never serialized, no value
                    spec.Src = Json.Json.GetString(a, "src", "");
                    spec.Text = "";
                    spec.ImageWidth = (float)Json.Json.GetNumber(a, "width", 16);
                    spec.ImageHeight = (float)Json.Json.GetNumber(a, "height", 16);
                    break;
                case "field_label": case "field_label_serializable":
                    spec.Kind = type == "field_label" ? ArgKind.Label : ArgKind.Field;
                    spec.Text = Json.Json.GetString(a, "text", "");
                    break;
                default:
                    spec.Kind = ArgKind.Field;
                    spec.Text = Json.Json.GetString(a, "text", Json.Json.GetString(a, "value", ""));
                    if (type == "field_number")
                    {
                        spec.Text = Json.Json.GetString(a, "value", "0");
                        if (a.TryGetValue("min", out var mn) && mn is double d1) spec.Min = d1;
                        if (a.TryGetValue("max", out var mx) && mx is double d2) spec.Max = d2;
                        if (a.TryGetValue("precision", out var pr) && pr is double d3) spec.Precision = d3;
                    }
                    else if (type == "field_checkbox") spec.Checked = Json.Json.GetBool(a, "checked");
                    else if (type == "field_variable")
                    {
                        spec.Text = Json.Json.GetString(a, "variable", "item");
                        var vt = Json.Json.GetArray(a, "variableTypes");
                        spec.VariableType = vt != null && vt.Count > 0 ? vt[0] as string : "";
                    }
                    else if (type == "field_dropdown")
                    {
                        spec.Options = new List<DropdownOption>();
                        foreach (var o in Json.Json.GetArray(a, "options") ?? new List<object>())
                            if (o is List<object> pair && pair.Count >= 2)
                                spec.Options.Add(new DropdownOption { Label = pair[0] as string ?? "", Value = pair[1] as string ?? "" });
                        spec.Text = spec.Options.Count > 0 ? spec.Options[0].Value : "";
                    }
                    break;
            }
            return spec;
        }

        static string[] ReadCheck(object o)
        {
            if (o is string s) return new[] { s };
            if (o is List<object> l)
            {
                var r = new string[l.Count];
                for (int i = 0; i < r.Length; i++) r[i] = l[i] as string ?? "";
                return r;
            }
            return null; // null = accepts/produces any type
        }
    }

    public sealed class BlockRegistry
    {
        readonly Dictionary<string, BlockDefinition> defs = new Dictionary<string, BlockDefinition>();

        public void Register(BlockDefinition def) => defs[def.Type] = def;

        public void RegisterJson(string jsonArrayOrObject)
        {
            var parsed = Json.Json.Parse(jsonArrayOrObject);
            if (parsed is List<object> list)
                foreach (var o in list) Register(BlockDefinition.FromJson((Dictionary<string, object>)o));
            else Register(BlockDefinition.FromJson((Dictionary<string, object>)parsed));
        }

        public bool TryGet(string type, out BlockDefinition def) => defs.TryGetValue(type, out def);
        public IEnumerable<BlockDefinition> All => defs.Values;
    }

    public static class Colour
    {
        /// <summary>Blockly colour: hue number (0-360), numeric string, or #rrggbb. Returns #rrggbb.</summary>
        public static string Parse(object value)
        {
            if (value is double h) return FromHue(h);
            if (value is string s)
            {
                if (s.StartsWith("#")) return s.Length == 7 ? s.ToLowerInvariant() : "#a5a5a5";
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var hh)) return FromHue(hh);
            }
            return "#a5a5a5";
        }

        // Port of Blockly.utils.colour.hsvToHex with saturation 0.45, value 0.65 (channels are floored).
        public static string FromHue(double hue)
        {
            const double saturation = 0.45;
            double value = 0.65 * 255;
            hue = ((hue % 360) + 360) % 360;
            double sector = hue / 60;
            int sextant = (int)Math.Floor(sector);
            double rem = sector - sextant;
            double v1 = value * (1 - saturation);
            double v2 = value * (1 - saturation * rem);
            double v3 = value * (1 - saturation * (1 - rem));
            double r, g, b;
            switch (sextant)
            {
                case 1: r = v2; g = value; b = v1; break;
                case 2: r = v1; g = value; b = v3; break;
                case 3: r = v1; g = v2; b = value; break;
                case 4: r = v3; g = v1; b = value; break;
                case 5: r = value; g = v1; b = v2; break;
                default: r = value; g = v3; b = v1; break;
            }
            return "#" + ToByte(r).ToString("x2") + ToByte(g).ToString("x2") + ToByte(b).ToString("x2");
        }

        static int ToByte(double d) => (int)Math.Floor(d);
    }
}
