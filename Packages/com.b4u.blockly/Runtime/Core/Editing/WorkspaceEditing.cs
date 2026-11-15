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

        // ---------------------------------------------------------------- several stacks at once
        /// <summary>The top-level block of each given block's stack (a shadow counts as its owner), each stack once, in the given order.</summary>
        public static List<Block> Stacks(IEnumerable<Block> blocks)
        {
            var result = new List<Block>();
            foreach (var b0 in blocks)
            {
                var b = b0;
                while (b != null && b.IsShadow) b = b.ShadowOf?.Owner;
                if (b == null) continue;
                var top = b.Root;
                if (!result.Contains(top)) result.Add(top);
            }
            return result;
        }

        /// <summary>
        /// The clipboard text for several stacks: each stack with everything in it and after it, and where it sits relative to the first one
        /// (so a paste keeps the arrangement). Null when there is nothing to copy.
        /// </summary>
        public static string CopyMany(Workspace ws, IEnumerable<Block> blocks)
        {
            var stacks = Stacks(blocks);
            if (stacks.Count == 0) return null;
            var list = new List<object>();
            foreach (var s in stacks)
                list.Add(new Dictionary<string, object>
                {
                    { "dx", s.X - stacks[0].X }, { "dy", s.Y - stacks[0].Y },
                    { "block", WorkspaceJson.SaveStackCopy(ws, s) },
                });
            return Json.Json.Write(new Dictionary<string, object> { { "blocks", list } });
        }

        /// <summary>
        /// Creates the stacks of <see cref="CopyMany"/> text as new top-level blocks near (x, y), the group shifted down-right while another top
        /// block sits at its first stack. Also accepts the text of <see cref="Copy"/> (one block). Empty when the text is not usable; the
        /// workspace is unchanged then.
        /// </summary>
        public static List<Block> PasteMany(Workspace ws, string clipboard, float x, float y)
        {
            var result = new List<Block>();
            if (string.IsNullOrEmpty(clipboard)) return result;
            Dictionary<string, object> root;
            try { root = Json.Json.AsObject(Json.Json.Parse(clipboard)); }
            catch (System.Exception e) when (e is System.FormatException || e is System.InvalidCastException) { return result; }
            if (root == null) return result;
            var entries = Json.Json.GetArray(root, "blocks");
            if (entries == null)
            {
                var one = Paste(ws, clipboard, x, y);
                if (one != null) result.Add(one);
                return result;
            }
            var spawned = new List<KeyValuePair<Block, KeyValuePair<double, double>>>();
            try
            {
                foreach (var e in entries)
                {
                    var d = Json.Json.AsObject(e);
                    var spec = Json.Json.GetObject(d, "block");
                    if (spec == null) continue;
                    spawned.Add(new KeyValuePair<Block, KeyValuePair<double, double>>(Spawn(ws, spec), new KeyValuePair<double, double>(Json.Json.GetNumber(d, "dx"), Json.Json.GetNumber(d, "dy"))));
                }
            }
            catch (System.Exception e) when (e is System.FormatException || e is KeyNotFoundException || e is System.InvalidCastException) { return result; }
            if (spawned.Count == 0) return result;
            x += PasteOffset; y += PasteOffset;
            while (ws.TopBlocks.Any(t => System.Math.Abs(t.X - x) < 1 && System.Math.Abs(t.Y - y) < 1)) { x += PasteOffset; y += PasteOffset; }
            foreach (var kv in spawned)
            {
                kv.Key.X = x + kv.Value.Key; kv.Key.Y = y + kv.Value.Value;
                ws.TopBlocks.Add(kv.Key);
                result.Add(kv.Key);
            }
            return result;
        }

        /// <summary>Removes the stacks the given blocks belong to, each with everything in it and after it. Returns how many stacks were removed (deleting a procedure definition also removes its calls, which may be other given stacks).</summary>
        public static int DeleteMany(Workspace ws, BlockLayouter layouter, IEnumerable<Block> blocks)
        {
            int n = 0;
            foreach (var s in Stacks(blocks))
            {
                if (!ws.TopBlocks.Contains(s)) continue;                 // already gone with a procedure definition
                Delete(ws, layouter, s, heal: false);
                n++;
            }
            return n;
        }

        /// <summary>Moves the given top-level blocks by the same amount.</summary>
        public static void MoveStacks(IEnumerable<Block> tops, double dx, double dy)
        {
            foreach (var t in tops) { t.X += dx; t.Y += dy; }
        }

        /// <summary>Sets <see cref="Block.Disabled"/> on every statement block of the given stacks.</summary>
        public static void SetDisabledMany(IEnumerable<Block> blocks, bool disabled)
        {
            foreach (var s in Stacks(blocks))
                for (var b = s; b != null; b = b.NextBlock)
                    if (b.Output == null && (b.Previous != null || b.Next != null)) b.Disabled = disabled;
        }
    }
}
