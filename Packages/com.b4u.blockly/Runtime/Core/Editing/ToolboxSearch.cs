using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Blockly.Editing
{
    /// <summary>Finds the blocks of a toolbox by the words on them.</summary>
    public static class ToolboxSearch
    {
        /// <summary>The most blocks a search returns.</summary>
        public const int MaxResults = 30;

        /// <summary>
        /// The specs of the blocks that have every word of <paramref name="query"/> in their text, in toolbox order, the ones with the
        /// words on the block itself before the ones that only have them in a menu of the block or in its category or type name.
        /// Dynamic categories (variables, functions) are searched as they are now. An empty query finds nothing.
        /// </summary>
        public static List<Dictionary<string, object>> Find(ToolboxDefinition toolbox, Workspace main, string query, int max = MaxResults)
        {
            var found = new List<Dictionary<string, object>>();
            var words = (query ?? "").ToLowerInvariant().Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || toolbox == null || main == null) return found;

            var scratch = new Workspace(main.Registry);
            foreach (var v in main.Variables) scratch.GetOrCreateVariable(v.Name, v.Id, v.Type);

            var first = new List<Dictionary<string, object>>();
            var second = new List<Dictionary<string, object>>();
            foreach (var category in toolbox.Categories)
            {
                if (category.Kind == CategoryKind.Search) continue;
                string categoryName = category.Name + " " + Loc.T(category.Name);
                foreach (var spec in Flyout.Specs(category, main))
                {
                    Block block;
                    try { block = WorkspaceJson.CreateBlock(scratch, spec); }
                    catch (KeyNotFoundException) { continue; }                      // a block type that is not registered
                    string onBlock = OnBlock(block, scratch), elsewhere = onBlock + " " + InMenus(block) + " " + categoryName.ToLowerInvariant() + " " + block.Type.Replace('_', ' ').ToLowerInvariant();
                    if (words.All(w => onBlock.Contains(w))) first.Add(spec);
                    else if (words.All(w => elsewhere.Contains(w))) second.Add(spec);
                }
            }
            found.AddRange(first);
            found.AddRange(second);
            if (found.Count > max) found.RemoveRange(max, found.Count - max);
            return found;
        }

        /// <summary>The words written on the block, in the language in use and in English, lower case.</summary>
        static string OnBlock(Block b, Workspace ws)
        {
            var sb = new StringBuilder();
            foreach (var f in b.AllFields())
            {
                if (f.IsLabel) { sb.Append(f.Value).Append(' ').Append(f.Spec.NoTranslate ? f.Value : Loc.T(f.Value)).Append(' '); continue; }
                switch (f.Spec.FieldType)
                {
                    case "field_variable": sb.Append(ws.GetVariableById(f.Value)?.Name ?? f.Value).Append(' '); break;
                    case "field_dropdown":
                        if (f.Spec.Options != null)
                            foreach (var o in f.Spec.Options)
                                if (o.Value == f.Value) sb.Append(o.Label).Append(' ').Append(Loc.T(o.Label)).Append(' ');
                        break;
                    case "field_colour": break;
                    default: sb.Append(f.Value).Append(' '); break;
                }
            }
            return sb.ToString().ToLowerInvariant();
        }

        /// <summary>The other choices of the block's dropdowns ("square root", "absolute"), lower case.</summary>
        static string InMenus(Block b)
        {
            var sb = new StringBuilder();
            foreach (var f in b.AllFields())
                if (f.Spec.FieldType == "field_dropdown" && f.Spec.Options != null)
                    foreach (var o in f.Spec.Options) sb.Append(o.Label).Append(' ').Append(Loc.T(o.Label)).Append(' ');
            return sb.ToString().ToLowerInvariant();
        }
    }
}
