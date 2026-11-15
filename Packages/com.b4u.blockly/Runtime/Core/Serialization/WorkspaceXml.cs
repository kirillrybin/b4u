using System;
using System.Collections.Generic;
using System.Xml;

namespace Blockly
{
    /// <summary>Reads the legacy Blockly XML workspace format (Blockly.Xml.workspaceToDom).</summary>
    public static class WorkspaceXml
    {
        public static void Load(Workspace ws, string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            var root = doc.DocumentElement ?? throw new FormatException("Empty XML");

            foreach (XmlElement e in Elements(root, "variables"))
                foreach (XmlElement v in Elements(e, "variable"))
                    ws.GetOrCreateVariable(v.InnerText, v.GetAttribute("id") is { Length: > 0 } id ? id : null, v.GetAttribute("type"));

            foreach (XmlElement e in Elements(root, "comment"))                  // <comment id x y w h collapsed>text</comment>
            {
                var note = new WorkspaceComment { Id = e.GetAttribute("id") is { Length: > 0 } cid ? cid : ws.NewCommentId(), Text = e.InnerText, Collapsed = e.GetAttribute("collapsed") == "true" };
                note.X = Num(e, "x", 0); note.Y = Num(e, "y", 0); note.W = Num(e, "w", 160); note.H = Num(e, "h", 100);
                ws.Comments.Add(note);
            }

            foreach (XmlElement e in Elements(root, "block"))
            {
                var b = LoadBlock(ws, e, false);
                if (double.TryParse(e.GetAttribute("x"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)) b.X = x;
                if (double.TryParse(e.GetAttribute("y"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)) b.Y = y;
                ws.TopBlocks.Add(b);
            }
        }

        static double Num(XmlElement e, string attribute, double fallback)
            => double.TryParse(e.GetAttribute(attribute), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

        static IEnumerable<XmlElement> Elements(XmlElement parent, string name)
        {
            foreach (XmlNode n in parent.ChildNodes)
                if (n is XmlElement e && e.LocalName == name) yield return e;
        }

        static Block LoadBlock(Workspace ws, XmlElement e, bool shadow)
        {
            string type = e.GetAttribute("type");
            string id = e.GetAttribute("id");
            var block = ws.CreateBlock(type, id.Length > 0 ? id : null);
            block.IsShadow = shadow;
            block.Collapsed = e.GetAttribute("collapsed") == "true";
            block.Disabled = e.GetAttribute("disabled") == "true";
            if (e.HasAttribute("inline")) block.InputsInline = e.GetAttribute("inline") == "true";
            foreach (XmlElement c in Elements(e, "comment"))                      // <comment pinned="true" h="80" w="160">text</comment>
            {
                block.Comment = c.InnerText;
                block.CommentPinned = c.GetAttribute("pinned") == "true";
            }

            foreach (XmlElement m in Elements(e, "mutation"))
            {
                var state = new Dictionary<string, object>();
                foreach (XmlAttribute a in m.Attributes) state[a.LocalName] = a.Value;
                var args = new List<object>();
                foreach (XmlElement a in Elements(m, "arg"))
                    args.Add(new Dictionary<string, object> { { "name", a.GetAttribute("name") }, { "id", a.GetAttribute("varid") is { Length: > 0 } vid ? vid : null } });
                if (args.Count > 0) state["params"] = args;
                block.ExtraState = state;
                if (block.Definition.Mutator != null) block.Build(state);
            }

            foreach (XmlElement f in Elements(e, "field"))
            {
                var field = block.GetField(f.GetAttribute("name"));
                if (field == null) continue;
                if (field.Spec.FieldType == "field_variable")
                {
                    string vid = f.GetAttribute("id");
                    field.Value = ws.GetOrCreateVariable(f.InnerText, vid.Length > 0 ? vid : null, f.GetAttribute("variabletype")).Id;
                }
                else field.Value = f.InnerText;
            }
            block.Definition.Mutator?.FieldsChanged(block);

            foreach (var tag in new[] { "value", "statement" })
                foreach (XmlElement v in Elements(e, tag))
                {
                    var input = block.GetInput(v.GetAttribute("name"));
                    if (input?.Connection != null) ConnectChildren(ws, input.Connection, v);
                }

            foreach (XmlElement n in Elements(e, "next"))
                if (block.Next != null) ConnectChildren(ws, block.Next, n);
            return block;
        }

        static void ConnectChildren(Workspace ws, Connection parent, XmlElement holder)
        {
            foreach (XmlElement s in Elements(holder, "shadow")) { parent.Shadow = LoadBlock(ws, s, true); parent.Shadow.ShadowOf = parent; }
            foreach (XmlElement b in Elements(holder, "block"))
            {
                var child = LoadBlock(ws, b, false);
                parent.Connect(child.Output ?? child.Previous ?? throw new FormatException($"Block '{child.Type}' cannot be connected"));
            }
        }
    }
}
