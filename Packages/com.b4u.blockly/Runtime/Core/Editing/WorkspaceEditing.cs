using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;

namespace Blockly.Editing
{
    /// <summary>Model-level editing operations shared by the drag-and-drop code and (later) the Delete key.</summary>
    public static class WorkspaceEditing
    {
        /// <summary>
        /// Removes a block and everything inside it. With <paramref name="heal"/> the blocks that followed it in its
        /// stack are reconnected to what preceded it (Delete key); without it they leave together (dragged to the trash).
        /// Deleting a procedure definition also removes every call to it, as Blockly does.
        /// </summary>
        public static void Delete(Workspace ws, BlockLayouter layouter, Block block, bool heal)
        {
            var doomed = new HashSet<Block>(block.Descendants());
            if (heal)
                foreach (var n in NextChain(block))
                    foreach (var d in n.Descendants()) doomed.Remove(d);        // the followers survive
            var removedProcedures = doomed.Where(ProcedureInfo.IsDefinition).Select(ProcedureInfo.Name).ToList();

            RemoveOne(ws, layouter, block, heal);

            foreach (var name in removedProcedures)
                foreach (var call in ws.AllBlocks().Where(b => IsCall(b) && ProcedureInfo.Name(b) == name).ToList())
                    RemoveOne(ws, layouter, call, heal: true);
        }

        static bool IsCall(Block b) => b.Type == "procedures_callnoreturn" || b.Type == "procedures_callreturn";

        static IEnumerable<Block> NextChain(Block b)
        {
            for (var n = b.NextBlock; n != null; n = n.NextBlock) yield return n;
        }

        static void RemoveOne(Workspace ws, BlockLayouter layouter, Block block, bool heal)
        {
            var layout = layouter.LayoutWorkspace();
            layout.ByBlock.TryGetValue(block, out var box);

            var childSide = block.Output ?? block.Previous;
            var parentConn = childSide?.Target;
            var next = block.NextBlock;

            if (parentConn != null) childSide.Disconnect();
            else ws.TopBlocks.Remove(block);

            if (heal && next != null)
            {
                block.Next.Disconnect();
                if (parentConn != null && next.Previous != null && parentConn.CheckCompatible(next.Previous)) parentConn.Connect(next.Previous);
                else
                {
                    if (box != null && layout.ByBlock.TryGetValue(next, out var nb)) { next.X = nb.X; next.Y = nb.Y; }
                    ws.TopBlocks.Add(next);
                }
            }
        }

        /// <summary>"do something" -> "do something2" when the name is taken by another procedure definition.</summary>
        public static string UniqueProcedureName(Workspace ws, string wanted)
        {
            var taken = new HashSet<string>(ws.AllBlocks().Where(ProcedureInfo.IsDefinition).Select(b => b.GetFieldValue("NAME")));
            if (!taken.Contains(wanted)) return wanted;
            for (int i = 2; ; i++)
                if (!taken.Contains(wanted + i)) return wanted + i;
        }

        /// <summary>
        /// Creates a new block from a toolbox/clipboard spec. Procedure definitions get a name that is unique in the
        /// workspace. The block is not placed anywhere yet.
        /// </summary>
        public static Block Spawn(Workspace ws, Dictionary<string, object> spec)
        {
            string type = Json.Json.GetString(spec, "type");
            if (type == "procedures_defnoreturn" || type == "procedures_defreturn")
            {
                spec = new Dictionary<string, object>(spec);
                var fields = new Dictionary<string, object>(Json.Json.GetObject(spec, "fields") ?? new Dictionary<string, object>());
                string wanted = Json.Json.GetString(fields, "NAME") ?? "do something";
                fields["NAME"] = UniqueProcedureName(ws, wanted);
                spec["fields"] = fields;
            }
            return WorkspaceJson.CreateBlock(ws, spec);
        }
    }
}
