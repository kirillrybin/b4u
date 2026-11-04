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
            => Detach(ws, layouter, block, heal, out _);

        /// <summary>
        /// Takes a block out of its stack or input without deleting it, and makes it a top-level block again, where it
        /// was drawn (nudged by <paramref name="bump"/> so it does not sit exactly on its former parent). With
        /// <paramref name="heal"/> the blocks that followed it stay connected to what preceded it.
        /// </summary>
        public static void Unplug(Workspace ws, BlockLayouter layouter, Block block, bool heal, float bump = 0f)
        {
            Detach(ws, layouter, block, heal, out var box);
            if (box != null) { block.X = box.X + bump; block.Y = box.Y + bump; }
            else { block.X += bump; block.Y += bump; }
            ws.TopBlocks.Add(block);
        }

        static void Detach(Workspace ws, BlockLayouter layouter, Block block, bool heal, out BlockBox box)
        {
            LayoutResult layout = layouter?.LayoutWorkspace();
            box = null;
            layout?.ByBlock.TryGetValue(block, out box);

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
                    if (layout != null && layout.ByBlock.TryGetValue(next, out var nb)) { next.X = nb.X; next.Y = nb.Y; }
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

        /// <summary>The clipboard text for a block: JSON of the block with its inputs (not its followers), without ids.</summary>
        public static string Copy(Workspace ws, Block block)
        {
            while (block != null && block.IsShadow) block = block.ShadowOf?.Owner;
            return block == null ? null : Json.Json.Write(WorkspaceJson.SaveBlockCopy(ws, block));
        }

        /// <summary>Distance a pasted block is moved down-right so it does not hide the original.</summary>
        public const float PasteOffset = 24f;

        /// <summary>
        /// Creates a block from <see cref="Copy"/> text as a new top-level block near (<paramref name="x"/>, <paramref name="y"/>),
        /// shifted further while another top block already sits there. Returns null if the text is not a usable block
        /// (empty, malformed, or a type this workspace does not know); the workspace is unchanged then.
        /// </summary>
        public static Block Paste(Workspace ws, string clipboard, float x, float y)
        {
            if (string.IsNullOrEmpty(clipboard)) return null;
            Block block;
            try
            {
                var spec = Json.Json.AsObject(Json.Json.Parse(clipboard));
                if (spec == null) return null;
                block = Spawn(ws, spec);
            }
            catch (System.Exception e) when (e is System.FormatException || e is KeyNotFoundException || e is System.InvalidCastException)
            {
                return null;
            }
            x += PasteOffset; y += PasteOffset;
            while (ws.TopBlocks.Any(t => System.Math.Abs(t.X - x) < 1 && System.Math.Abs(t.Y - y) < 1)) { x += PasteOffset; y += PasteOffset; }
            block.X = x; block.Y = y;
            ws.TopBlocks.Add(block);
            return block;
        }
    }
}
