using System;
using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;

namespace Blockly.Editing
{
    public enum CategoryKind { Static, Variables, Procedures }

    public sealed class ToolboxCategory
    {
        public string Name;
        public string ColourHex = "#a5a5a5";
        /// <summary>Optional picture for the category button (a name the UI resolves through BlockImages).</summary>
        public string Icon;
        public CategoryKind Kind;
        /// <summary>Block specs in Blockly's JSON block format (type, fields, inputs with shadows, extraState).</summary>
        public List<Dictionary<string, object>> Blocks = new List<Dictionary<string, object>>();
    }

    /// <summary>
    /// A toolbox in Blockly's JSON format: {"kind":"categoryToolbox","contents":[{"kind":"category","name":...,
    /// "colour":..., "contents":[{"kind":"block","type":...}]}]}. A category may be dynamic ("custom":"VARIABLE" /
    /// "PROCEDURE"), in which case its content is derived from the workspace each time it is opened.
    /// </summary>
    public sealed class ToolboxDefinition
    {
        public readonly List<ToolboxCategory> Categories = new List<ToolboxCategory>();

        public static ToolboxDefinition FromJson(string json)
        {
            var root = Json.Json.AsObject(Json.Json.Parse(json)) ?? throw new FormatException("Toolbox JSON must be an object");
            var def = new ToolboxDefinition();
            foreach (var c in Json.Json.GetArray(root, "contents") ?? new List<object>())
            {
                var co = Json.Json.AsObject(c);
                if (Json.Json.GetString(co, "kind") != "category") continue;
                var cat = new ToolboxCategory
                {
                    Name = Json.Json.GetString(co, "name") ?? "",
                    ColourHex = Colour.Parse(co.TryGetValue("colour", out var col) ? col : null),
                    Icon = Json.Json.GetString(co, "icon"),
                };
                string custom = Json.Json.GetString(co, "custom");
                cat.Kind = custom == "VARIABLE" ? CategoryKind.Variables : custom == "PROCEDURE" ? CategoryKind.Procedures : CategoryKind.Static;
                foreach (var item in Json.Json.GetArray(co, "contents") ?? new List<object>())
                {
                    var io = Json.Json.AsObject(item);
                    if (Json.Json.GetString(io, "kind") == "block") cat.Blocks.Add(io);
                }
                def.Categories.Add(cat);
            }
            return def;
        }
    }

    /// <summary>The blocks shown when a category is opened, laid out in a column of their own workspace.</summary>
    public sealed class Flyout
    {
        public Workspace Workspace;
        /// <summary>Top-level blocks in display order, each with the spec it was made from (used to spawn real copies).</summary>
        public readonly List<KeyValuePair<Block, Dictionary<string, object>>> Items = new List<KeyValuePair<Block, Dictionary<string, object>>>();

        public Dictionary<string, object> SpecOf(Block block)
        {
            foreach (var kv in Items) if (kv.Key == block) return kv.Value;
            return null;
        }

        public const float Gap = 18f;
        /// <summary>Room left above the first block for a button ("Create variable...") drawn by the UI.</summary>
        public const float VariablesHeaderHeight = 44f;
        public float HeaderHeight;

        /// <summary>Resolves dynamic categories against the main workspace and builds a ready-to-draw flyout workspace.</summary>
        public static Flyout Build(ToolboxCategory category, Workspace main, ITextMeasurer measurer)
        {
            var flyout = new Flyout { Workspace = new Workspace(main.Registry) };
            foreach (var v in main.Variables) flyout.Workspace.GetOrCreateVariable(v.Name, v.Id, v.Type);

            foreach (var spec in Specs(category, main))
            {
                var block = WorkspaceJson.CreateBlock(flyout.Workspace, spec);
                flyout.Workspace.TopBlocks.Add(block);
                flyout.Items.Add(new KeyValuePair<Block, Dictionary<string, object>>(block, spec));
            }
            new BlockLayouter(flyout.Workspace, measurer).CleanUp(Gap);
            if (category.Kind == CategoryKind.Variables)
            {
                flyout.HeaderHeight = VariablesHeaderHeight;
                foreach (var b in flyout.Workspace.TopBlocks) b.Y += VariablesHeaderHeight;
            }
            return flyout;
        }

        // ------------------------------------------------------------------ dynamic content
        public static List<Dictionary<string, object>> Specs(ToolboxCategory category, Workspace main)
        {
            switch (category.Kind)
            {
                case CategoryKind.Variables: return VariableSpecs(main);
                case CategoryKind.Procedures: return ProcedureSpecs(category, main);
                default: return category.Blocks;
            }
        }

        static Dictionary<string, object> Obj(params (string, object)[] kv)
        {
            var d = new Dictionary<string, object>();
            foreach (var (k, v) in kv) d[k] = v;
            return d;
        }

        /// <summary>An input holding a default number that shows greyed-out until the user plugs something in.</summary>
        static Dictionary<string, object> NumberShadow(double value)
            => Obj(("shadow", Obj(("type", "math_number"), ("fields", Obj(("NUM", value))))));

        static Dictionary<string, object> VarField(string name, string id) =>
            Obj(("VAR", id != null ? Obj(("id", id), ("name", name)) : Obj(("name", name))));

        /// <summary>set / change for the first variable, then a "get" for every variable. With none yet, "item" is offered.</summary>
        static List<Dictionary<string, object>> VariableSpecs(Workspace main)
        {
            var list = new List<Dictionary<string, object>>();
            var vars = main.Variables.ToList();
            string firstName = vars.Count > 0 ? vars[0].Name : "item";
            string firstId = vars.Count > 0 ? vars[0].Id : null;

            list.Add(Obj(("kind", "block"), ("type", "variables_set"), ("fields", VarField(firstName, firstId))));
            list.Add(Obj(("kind", "block"), ("type", "math_change"), ("fields", VarField(firstName, firstId)),
                         ("inputs", Obj(("DELTA", NumberShadow(1))))));
            if (vars.Count == 0) list.Add(Obj(("kind", "block"), ("type", "variables_get"), ("fields", VarField("item", null))));
            foreach (var v in vars)
                list.Add(Obj(("kind", "block"), ("type", "variables_get"), ("fields", VarField(v.Name, v.Id))));
            return list;
        }

        /// <summary>The static definition blocks, then one call block for every procedure already defined.</summary>
        static List<Dictionary<string, object>> ProcedureSpecs(ToolboxCategory category, Workspace main)
        {
            var list = new List<Dictionary<string, object>>(category.Blocks);
            foreach (var def in main.AllBlocks().Where(ProcedureInfo.IsDefinition))
            {
                string name = def.GetFieldValue("NAME");
                var ps = ProcedureInfo.Params(def).Select(p => (object)p.Name).ToList();
                var state = Obj(("name", name));
                if (ps.Count > 0) state["params"] = ps;
                list.Add(Obj(("kind", "block"), ("type", def.Type == "procedures_defreturn" ? "procedures_callreturn" : "procedures_callnoreturn"), ("extraState", state)));
            }
            return list;
        }
    }
}
