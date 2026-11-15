using System;
using System.Collections.Generic;
using System.Globalization;
using Blockly.Json;

namespace Blockly
{
    /// <summary>Reads/writes the Blockly JSON workspace format (Blockly.serialization.workspaces).</summary>
    public static class WorkspaceJson
    {
        public static void Load(Workspace ws, string json)
        {
            var root = Json.Json.AsObject(Json.Json.Parse(json))
                       ?? throw new FormatException("Workspace JSON must be an object");

            foreach (var v in Json.Json.GetArray(root, "variables") ?? new List<object>())
            {
                var vd = Json.Json.AsObject(v);
                string name = Json.Json.GetString(vd, "name");
                if (name != null) ws.GetOrCreateVariable(name, Json.Json.GetString(vd, "id"), Json.Json.GetString(vd, "type", ""));
            }

            var blocks = Json.Json.GetArray(Json.Json.GetObject(root, "blocks"), "blocks");
            if (blocks == null) return;
            foreach (var b in blocks)
            {
                var block = LoadBlock(ws, Json.Json.AsObject(b), false);
                block.X = Json.Json.GetNumber(Json.Json.AsObject(b), "x");
                block.Y = Json.Json.GetNumber(Json.Json.AsObject(b), "y");
                ws.TopBlocks.Add(block);
            }
        }

        /// <summary>Replaces the whole content of <paramref name="ws"/> with a saved state (what undo/redo does).</summary>
        public static void Restore(Workspace ws, string json)
        {
            ws.Clear();
            Load(ws, json);
        }

        /// <summary>
        /// Creates a block (with its shadows and children) from a Blockly JSON block object, e.g. a toolbox entry.
        /// The block is not added to the workspace's top blocks; the caller places it.
        /// </summary>
        public static Block CreateBlock(Workspace ws, Dictionary<string, object> spec) => LoadBlock(ws, spec, false);

        static Block LoadBlock(Workspace ws, Dictionary<string, object> d, bool shadow)
        {
            string type = Json.Json.GetString(d, "type") ?? throw new FormatException("Block without 'type'");
            var block = ws.CreateBlock(type, Json.Json.GetString(d, "id"));
            block.IsShadow = shadow;
            block.Collapsed = Json.Json.GetBool(d, "collapsed");
            block.Disabled = Json.Json.GetBool(d, "disabled");
            if (d.TryGetValue("inline", out var il) && il is bool inl) block.InputsInline = inl;
            var comment = Json.Json.GetObject(Json.Json.GetObject(d, "icons"), "comment");      // Blockly: "icons": { "comment": { "text", "pinned", "height", "width" } }
            if (comment != null)
            {
                block.Comment = Json.Json.GetString(comment, "text") ?? "";
                block.CommentPinned = Json.Json.GetBool(comment, "pinned");
            }

            if (d.TryGetValue("extraState", out var es))
            {
                if (es is string xmlState && xmlState.TrimStart().StartsWith("<mutation") && block.Definition.Mutator != null)
                    es = Mutators.ParseMutationXml(xmlState);
                block.ExtraState = es as Dictionary<string, object> ?? new Dictionary<string, object> { { "__raw", es } };
                if (block.Definition.Mutator != null) block.Build(block.ExtraState);
            }

            var fields = Json.Json.GetObject(d, "fields");
            if (fields != null)
                foreach (var kv in fields)
                {
                    var f = block.GetField(kv.Key);
                    if (f == null) continue;
                    f.Value = FieldFromJson(ws, f, kv.Value);
                }
            block.Definition.Mutator?.FieldsChanged(block);

            var inputs = Json.Json.GetObject(d, "inputs");
            if (inputs != null)
                foreach (var kv in inputs)
                {
                    var input = block.GetInput(kv.Key);
                    if (input?.Connection == null) continue;
                    ConnectChildren(ws, input.Connection, Json.Json.AsObject(kv.Value));
                }

            var next = Json.Json.GetObject(d, "next");
            if (next != null && block.Next != null) ConnectChildren(ws, block.Next, next);

            // A variable field nobody set still holds the definition's default *name*; store the variable's id instead.
            foreach (var f in block.AllFields())
                if (f.Spec.FieldType == "field_variable" && ws.GetVariableById(f.Value) == null)
                    f.Value = ws.GetOrCreateVariable(f.Value).Id;
            return block;
        }

        static void ConnectChildren(Workspace ws, Connection parent, Dictionary<string, object> holder)
        {
            if (holder == null) return;
            var sh = Json.Json.GetObject(holder, "shadow");
            if (sh != null) { parent.Shadow = LoadBlock(ws, sh, true); parent.Shadow.ShadowOf = parent; }
            var bl = Json.Json.GetObject(holder, "block");
            if (bl != null)
            {
                var child = LoadBlock(ws, bl, false);
                var childConn = child.Output ?? child.Previous
                                ?? throw new FormatException($"Block '{child.Type}' cannot be connected");
                parent.Connect(childConn);
            }
        }

        static string FieldFromJson(Workspace ws, Field f, object v)
        {
            if (f.Spec.FieldType == "field_variable")
            {
                var o = Json.Json.AsObject(v);
                string id = Json.Json.GetString(o, "id");
                if (id != null)
                {
                    if (ws.GetVariableById(id) == null)
                        ws.GetOrCreateVariable(Json.Json.GetString(o, "name", id), id, Json.Json.GetString(o, "type", ""));
                    return id;
                }
                string byName = Json.Json.GetString(o, "name") ?? v as string ?? "item";
                return ws.GetOrCreateVariable(byName, null, Json.Json.GetString(o, "type", "")).Id;
            }
            if (v is bool b) return b ? "TRUE" : "FALSE";
            if (v is double n) return JsValue.Shortest(n);
            return v as string ?? "";
        }

        // ---- save ----
        public static string Save(Workspace ws, bool indented = false)
        {
            var root = new Dictionary<string, object>();
            var tops = new List<object>();
            foreach (var b in ws.TopBlocks) tops.Add(SaveBlock(ws, b, true));
            if (tops.Count > 0)
                root["blocks"] = new Dictionary<string, object> { { "languageVersion", 0.0 }, { "blocks", tops } };

            if (ws.Variables.Count > 0)
            {
                var vars = new List<object>();
                foreach (var v in ws.Variables)
                {
                    var vd = new Dictionary<string, object> { { "name", v.Name }, { "id", v.Id } };
                    if (!string.IsNullOrEmpty(v.Type)) vd["type"] = v.Type;
                    vars.Add(vd);
                }
                root["variables"] = vars;
            }
            return Json.Json.Write(root, indented);
        }

        /// <summary>
        /// A self-contained copy of one block: itself, its shadows and everything plugged into its inputs, but not the
        /// blocks that follow it (as in Blockly's copy). Block ids and the position are left out so a paste gets fresh ones.
        /// </summary>
        public static Dictionary<string, object> SaveBlockCopy(Workspace ws, Block b)
        {
            var d = SaveBlock(ws, b, false);
            d.Remove("next");
            StripIds(d);
            return d;
        }

        static void StripIds(Dictionary<string, object> block)
        {
            block.Remove("id");
            var inputs = Json.Json.GetObject(block, "inputs");
            if (inputs != null)
                foreach (var h in inputs.Values) StripHolder(Json.Json.AsObject(h));
            StripHolder(Json.Json.GetObject(block, "next"));
        }

        static void StripHolder(Dictionary<string, object> holder)
        {
            if (holder == null) return;
            var sh = Json.Json.GetObject(holder, "shadow"); if (sh != null) StripIds(sh);
            var bl = Json.Json.GetObject(holder, "block"); if (bl != null) StripIds(bl);
        }

        static Dictionary<string, object> SaveBlock(Workspace ws, Block b, bool top)
        {
            var d = new Dictionary<string, object> { { "type", b.Type }, { "id", b.Id } };
            if (top) { d["x"] = b.X; d["y"] = b.Y; }
            if (b.Collapsed) d["collapsed"] = true;
            if (b.Disabled) d["disabled"] = true;
            if (b.InputsInline != (b.Definition.InputsInline ?? false)) d["inline"] = b.InputsInline;
            if (b.Comment != null)
                d["icons"] = new Dictionary<string, object> { { "comment", new Dictionary<string, object> { { "text", b.Comment }, { "pinned", b.CommentPinned } } } };

            var mut = b.Definition.Mutator;
            var es = mut != null ? mut.Save(b) : b.ExtraState;
            if (es != null && es.Count > 0)
                d["extraState"] = mut != null && mut.SerializeAsXml ? Mutators.ToMutationXml(es)
                                : es.Count == 1 && es.ContainsKey("__raw") ? es["__raw"] : es;

            var fields = new Dictionary<string, object>();
            foreach (var f in b.AllFields())
            {
                if (f.Name == null) continue;
                if (f.Spec.FieldType == "field_variable") fields[f.Name] = new Dictionary<string, object> { { "id", f.Value } };
                else if (f.Spec.FieldType == "field_checkbox") fields[f.Name] = f.Value == "TRUE";
                else if (f.Spec.FieldType == "field_number" && double.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) fields[f.Name] = n;
                else fields[f.Name] = f.Value;
            }
            if (fields.Count > 0) d["fields"] = fields;

            var inputs = new Dictionary<string, object>();
            foreach (var i in b.Inputs)
            {
                if (i.Connection == null || i.Name == null) continue;
                var h = SaveChildren(ws, i.Connection);
                if (h != null) inputs[i.Name] = h;
            }
            if (inputs.Count > 0) d["inputs"] = inputs;

            if (b.Next != null)
            {
                var h = SaveChildren(ws, b.Next);
                if (h != null) d["next"] = h;
            }
            return d;
        }

        static Dictionary<string, object> SaveChildren(Workspace ws, Connection c)
        {
            if (c.Shadow == null && c.Target == null) return null;
            var h = new Dictionary<string, object>();
            if (c.Shadow != null) h["shadow"] = SaveBlock(ws, c.Shadow, false);
            if (c.Target != null) h["block"] = SaveBlock(ws, c.Target.Owner, false);
            return h;
        }
    }
}
