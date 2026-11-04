using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Blockly
{
    /// <summary>
    /// Registry of named mutators. A block definition refers to one with <c>"mutator": "name"</c>.
    /// State arrives either as a JSON object (e.g. {"itemCount":2}) or as XML mutation attributes
    /// (e.g. items="2"); both are normalised to a dictionary before reaching a mutator.
    /// </summary>
    public static class Mutators
    {
        static readonly Dictionary<string, IBlockMutator> map = new Dictionary<string, IBlockMutator>
        {
            { "controls_if_mutator", new IfMutator() },
            { "text_join_mutator", new ItemsMutator("itemCount", "items", 2, "create text with", null) },
            { "lists_create_with_mutator", new ItemsMutator("itemCount", "items", 3, "create list with", "create empty list") },
            { "math_number_property_mutator", new OptionalInputsMutator(new[] { new Opt("divisor_input", "DIVISOR") }) },
            { "lists_setIndex_mutator", new AtFromWhereMutator(new[] { ("WHERE", "AT") }) },
            { "lists_getIndex_mutator", new GetIndexMutator() },
            { "text_charAt_mutator", new AtFromWhereMutator(new[] { ("WHERE", "AT") }) },
            { "text_getSubstring_mutator", new AtFromWhereMutator(new[] { ("WHERE1", "AT1"), ("WHERE2", "AT2") }) },
            { "lists_getSublist_mutator", new AtFromWhereMutator(new[] { ("WHERE1", "AT1"), ("WHERE2", "AT2") }) },
            { "lists_split_mutator", new SplitMutator() },
            { "procedures_def_mutator", new ProcedureDefMutator() },
            { "procedures_call_mutator", new ProcedureCallMutator() },
            { "procedures_ifreturn_mutator", new IfReturnMutator() },
        };

        public static IBlockMutator Get(string name)
            => map.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException("Unknown mutator '" + name + "'");

        public static void Register(string name, IBlockMutator m) => map[name] = m;

        /// <summary>Parses the XML-string form of extraState: &lt;mutation a="b"&gt;&lt;/mutation&gt;.</summary>
        public static Dictionary<string, object> ParseMutationXml(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var d = new Dictionary<string, object>();
            if (doc.DocumentElement != null)
                foreach (XmlAttribute a in doc.DocumentElement.Attributes) d[a.LocalName] = a.Value;
            return d;
        }

        public static string ToMutationXml(Dictionary<string, object> state)
        {
            var sb = new StringBuilder("<mutation");
            foreach (var kv in state)
                sb.Append(' ').Append(kv.Key).Append("=\"").Append(Convert(kv.Value)).Append('"');
            return sb.Append("></mutation>").ToString();
        }

        // ---- tolerant readers: accept bool/double (JSON) and "true"/"1"/"2" strings (XML) ----
        internal static bool Bool(Dictionary<string, object> s, bool def, params string[] keys)
        {
            if (s != null)
                foreach (var k in keys)
                    if (s.TryGetValue(k, out var v))
                        return v is bool b ? b : v is double d ? d != 0 : v is string str && (str == "true" || str == "1");
            return def;
        }

        internal static int Int(Dictionary<string, object> s, int def, params string[] keys)
        {
            if (s != null)
                foreach (var k in keys)
                    if (s.TryGetValue(k, out var v))
                    {
                        if (v is double d) return (int)d;
                        if (v is string str && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
                    }
            return def;
        }

        static string Convert(object v)
            => v is bool b ? (b ? "true" : "false") : v is double d ? JsValue.Shortest(d) : v?.ToString() ?? "";

        internal static Field Label(string text)
            => new Field { Spec = new ArgSpec { Kind = ArgKind.Label, Text = text }, Value = text };

        internal static Input ValueInput(Block b, string name, string[] check = null)
        {
            var i = new Input { Name = name, Kind = InputKind.Value };
            i.Connection = new Connection(b, ConnectionType.InputValue, i, check);
            return i;
        }

        internal static Input StatementInput(Block b, string name)
        {
            var i = new Input { Name = name, Kind = InputKind.Statement };
            i.Connection = new Connection(b, ConnectionType.NextStatement, i, null);
            return i;
        }
    }

    sealed class Opt
    {
        public readonly string Key, InputName;
        public readonly bool DefaultOn;   // value assumed when the attribute is absent (matches Blockly's domToMutation)
        public Opt(string key, string inputName, bool defaultOn = false) { Key = key; InputName = inputName; DefaultOn = defaultOn; }
    }

    /// <summary>controls_if: extra "else if" / "else" branches.</summary>
    sealed class IfMutator : IBlockMutator
    {
        public bool SerializeAsXml => false;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            int elseIf = Mutators.Int(state, 0, "elseIfCount", "elseif");
            bool hasElse = Mutators.Bool(state, false, "hasElse", "else");
            for (int i = 1; i <= elseIf; i++)
            {
                var cond = Mutators.ValueInput(b, "IF" + i, new[] { "Boolean" });
                cond.Fields.Add(Mutators.Label("else if"));
                b.Inputs.Add(cond);
                var body = Mutators.StatementInput(b, "DO" + i);
                body.Fields.Add(Mutators.Label("then"));
                b.Inputs.Add(body);
            }
            if (hasElse)
            {
                var e = Mutators.StatementInput(b, "ELSE");
                e.Fields.Add(Mutators.Label("else"));
                b.Inputs.Add(e);
            }
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
        {
            var d = new Dictionary<string, object>();
            int n = 0;
            while (b.GetInput("IF" + (n + 1)) != null) n++;
            if (n > 0) d["elseIfCount"] = (double)n;
            if (b.GetInput("ELSE") != null) d["hasElse"] = true;
            return d;
        }
    }

    /// <summary>text_join / lists_create_with: a variable number of ADDn inputs.</summary>
    sealed class ItemsMutator : IBlockMutator
    {
        readonly string jsonKey, xmlKey, label, emptyLabel;
        readonly int defaultCount;
        public bool SerializeAsXml => false;

        public ItemsMutator(string jsonKey, string xmlKey, int defaultCount, string label, string emptyLabel)
        { this.jsonKey = jsonKey; this.xmlKey = xmlKey; this.defaultCount = defaultCount; this.label = label; this.emptyLabel = emptyLabel; }

        public void Apply(Block b, Dictionary<string, object> state)
        {
            int count = Mutators.Int(state, defaultCount, jsonKey, xmlKey);
            b.Inputs.Clear();
            if (count == 0 && emptyLabel != null)
            {
                var e = new Input { Name = "EMPTY", Kind = InputKind.Dummy };
                e.Fields.Add(Mutators.Label(emptyLabel));
                b.Inputs.Add(e);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var input = Mutators.ValueInput(b, "ADD" + i);
                if (i == 0) input.Fields.Add(Mutators.Label(label));
                b.Inputs.Add(input);
            }
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
        {
            int n = 0;
            while (b.GetInput("ADD" + n) != null) n++;
            return new Dictionary<string, object> { { jsonKey, (double)n } };
        }
    }

    /// <summary>Blocks whose optional value inputs are toggled by boolean mutation attributes.</summary>
    sealed class OptionalInputsMutator : IBlockMutator
    {
        readonly Opt[] opts;
        public bool SerializeAsXml => true;
        public OptionalInputsMutator(Opt[] opts) { this.opts = opts; }

        public void Apply(Block b, Dictionary<string, object> state)
        {
            foreach (var o in opts)
                if (Mutators.Bool(state, o.DefaultOn, o.Key) && b.GetInput(o.InputName) == null)
                    b.Inputs.Add(Mutators.ValueInput(b, o.InputName, new[] { "Number" }));
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
        {
            var d = new Dictionary<string, object>();
            foreach (var o in opts) d[o.Key] = b.GetInput(o.InputName) != null;
            return d;
        }
    }

    /// <summary>
    /// The AT input after a WHERE dropdown exists only while WHERE is "# " or "# from end"
    /// (lists_getIndex/setIndex, text_charAt, text_getSubstring, lists_getSublist).
    /// </summary>
    static class WhereAt
    {
        public static void Reshape(Block b, string whereField, string atInput)
        {
            string w = b.GetFieldValue(whereField);
            bool wants = w == "FROM_START" || w == "FROM_END";
            var at = b.GetInput(atInput);
            if (wants)
            {
                if (at != null) return;
                var input = Mutators.ValueInput(b, atInput, new[] { "Number" });
                int idx = b.Inputs.FindIndex(i => i.Fields.Exists(f => f.Name == whereField));
                b.Inputs.Insert(idx < 0 ? b.Inputs.Count : idx + 1, input);
            }
            else if (at != null && at.Connection.Target == null)
            {
                b.Inputs.Remove(at);
            }
        }
    }

    /// <summary>No saved state; the AT inputs follow their WHERE dropdowns.</summary>
    sealed class AtFromWhereMutator : IBlockMutator
    {
        readonly (string where, string at)[] pairs;
        public bool SerializeAsXml => false;
        public AtFromWhereMutator((string, string)[] pairs) { this.pairs = pairs; }
        public void Apply(Block b, Dictionary<string, object> state) => FieldsChanged(b);
        public void FieldsChanged(Block b) { foreach (var p in pairs) WhereAt.Reshape(b, p.where, p.at); }
        public Dictionary<string, object> Save(Block b) => new Dictionary<string, object>();
    }

    /// <summary>lists_getIndex: JSON {"isStatement":true} (XML: statement="true"); AT follows WHERE.</summary>
    sealed class GetIndexMutator : IBlockMutator
    {
        public bool SerializeAsXml => false;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            if (Mutators.Bool(state, false, "isStatement", "statement"))
            {
                b.Output = null;
                b.Previous = new Connection(b, ConnectionType.PreviousStatement);
                b.Next = new Connection(b, ConnectionType.NextStatement);
            }
            WhereAt.Reshape(b, "WHERE", "AT");
        }

        public void FieldsChanged(Block b) => WhereAt.Reshape(b, "WHERE", "AT");

        public Dictionary<string, object> Save(Block b)
        {
            var d = new Dictionary<string, object>();
            if (b.Output == null) d["isStatement"] = true;
            return d;
        }
    }

    /// <summary>lists_split: MODE decides whether the block splits text into a list or joins a list into text.</summary>
    sealed class SplitMutator : IBlockMutator
    {
        public bool SerializeAsXml => false;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            if (state != null && state.TryGetValue("mode", out var m) && m is string mode)
                b.SetFieldValue("MODE", mode);
            FieldsChanged(b);
        }

        public void FieldsChanged(Block b)
        {
            bool join = b.GetFieldValue("MODE") == "JOIN";
            var input = b.GetInput("INPUT")?.Connection;
            if (input != null) input.Check = new[] { join ? "Array" : "String" };
            if (b.Output != null) b.Output.Check = new[] { join ? "String" : "Array" };
        }

        public Dictionary<string, object> Save(Block b)
            => new Dictionary<string, object> { { "mode", b.GetFieldValue("MODE") ?? "SPLIT" } };
    }

    /// <summary>Procedure parameter lists, shared by definitions and calls.</summary>
    public static class ProcedureInfo
    {
        public struct Param { public string Name, Id; }

        /// <summary>Reads "params" from a block's extra state; entries are names (calls) or {name,id} objects (definitions).</summary>
        public static List<Param> Params(Block b)
        {
            var result = new List<Param>();
            if (b.ExtraState != null && b.ExtraState.TryGetValue("params", out var o) && o is List<object> list)
                foreach (var e in list)
                {
                    if (e is string name) result.Add(new Param { Name = name });
                    else if (e is Dictionary<string, object> d)
                        result.Add(new Param { Name = Json.Json.GetString(d, "name"), Id = Json.Json.GetString(d, "id") });
                }
            return result;
        }

        public static string Name(Block b)
        {
            if (b.ExtraState != null && b.ExtraState.TryGetValue("name", out var n) && n is string s) return s;
            return b.GetFieldValue("NAME");
        }

        public static bool IsDefinition(Block b) => b.Type == "procedures_defnoreturn" || b.Type == "procedures_defreturn";
    }

    /// <summary>procedures_defnoreturn / defreturn: parameters are shown as "with: a, b" in the header row.</summary>
    sealed class ProcedureDefMutator : IBlockMutator
    {
        public bool SerializeAsXml => false;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            var ps = ProcedureInfo.Params(b);
            if (ps.Count == 0) return;
            var top = b.GetInput("TOPROW");
            top?.Fields.Add(Mutators.Label("with: " + string.Join(", ", ps.ConvertAll(p => p.Name))));
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
        {
            var d = new Dictionary<string, object>();
            var ps = ProcedureInfo.Params(b);
            if (ps.Count == 0) return d;
            var list = new List<object>();
            foreach (var p in ps)
                list.Add(new Dictionary<string, object> { { "name", p.Name }, { "id", p.Id ?? p.Name } });
            d["params"] = list;
            return d;
        }
    }

    /// <summary>procedures_callnoreturn / callreturn: the callee name plus one ARGn input per parameter.</summary>
    sealed class ProcedureCallMutator : IBlockMutator
    {
        public bool SerializeAsXml => false;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            string name = ProcedureInfo.Name(b);
            var top = b.GetInput("TOPROW");
            if (top != null && !string.IsNullOrEmpty(name)) top.Fields.Add(Mutators.Label(name));
            var ps = ProcedureInfo.Params(b);
            for (int i = 0; i < ps.Count; i++)
            {
                var input = Mutators.ValueInput(b, "ARG" + i);
                input.Fields.Add(Mutators.Label(ps[i].Name));
                b.Inputs.Add(input);
            }
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
        {
            var d = new Dictionary<string, object>();
            string name = ProcedureInfo.Name(b);
            if (name != null) d["name"] = name;
            var ps = ProcedureInfo.Params(b);
            if (ps.Count > 0) d["params"] = ps.ConvertAll(p => (object)p.Name);
            return d;
        }
    }

    /// <summary>procedures_ifreturn: optional VALUE input (XML-string state: value="1").</summary>
    sealed class IfReturnMutator : IBlockMutator
    {
        public bool SerializeAsXml => true;

        public void Apply(Block b, Dictionary<string, object> state)
        {
            if (!Mutators.Bool(state, true, "value")) return;
            var input = Mutators.ValueInput(b, "VALUE");
            input.Fields.Add(Mutators.Label("return"));
            b.Inputs.Add(input);
        }

        public void FieldsChanged(Block b) { }

        public Dictionary<string, object> Save(Block b)
            => new Dictionary<string, object> { { "value", b.GetInput("VALUE") != null ? "1" : "0" } };
    }
}
