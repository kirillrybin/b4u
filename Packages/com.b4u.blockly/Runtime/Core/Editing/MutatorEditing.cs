using System.Collections.Generic;
using System.Linq;
using Blockly.Layout;

namespace Blockly.Editing
{
    /// <summary>What a mutator part is: the "else if" branches and the "else" branch of an if block, or the items of a join / list block.</summary>
    public enum PartKind { ElseIf, Else, Item }

    /// <summary>One part of a mutable block, in the order of its inputs. <see cref="Index"/> counts from 1 for an else if and from 0 for an item.</summary>
    public readonly struct MutatorPart
    {
        public readonly PartKind Kind;
        public readonly int Index;
        public MutatorPart(PartKind kind, int index) { Kind = kind; Index = index; }
    }

    /// <summary>
    /// Changing the shape of the blocks that have a mutator: the else if / else branches of an if, the items of text_join and lists_create_with.
    /// The blocks plugged into the parts travel with their part (Blockly's compose / reconnect), so adding, removing or moving a part never
    /// loses or mixes up what is plugged in; the blocks of a removed part stay in the workspace, unplugged.
    /// </summary>
    public static class MutatorEditing
    {
        public enum Kind { None, If, Items }

        public static Kind KindOf(Block block)
        {
            var m = block?.Definition?.Mutator;
            return m is IfMutator ? Kind.If : m is ItemsMutator ? Kind.Items : Kind.None;
        }

        /// <summary>A block has a mutator the user can open when it is one of the three kinds and not a shadow.</summary>
        public static bool CanMutate(Block block) => block != null && !block.IsShadow && KindOf(block) != Kind.None;

        public static int ElseIfCount(Block b) { int n = 0; while (b.GetInput("IF" + (n + 1)) != null) n++; return n; }
        public static bool HasElse(Block b) => b.GetInput("ELSE") != null;
        public static int ItemCount(Block b) { int n = 0; while (b.GetInput("ADD" + n) != null) n++; return n; }

        /// <summary>The parts after the fixed first branch of an if, or the items, in order.</summary>
        public static List<MutatorPart> Parts(Block b)
        {
            var list = new List<MutatorPart>();
            switch (KindOf(b))
            {
                case Kind.If:
                    for (int i = 1; i <= ElseIfCount(b); i++) list.Add(new MutatorPart(PartKind.ElseIf, i));
                    if (HasElse(b)) list.Add(new MutatorPart(PartKind.Else, 0));
                    break;
                case Kind.Items:
                    for (int i = 0; i < ItemCount(b); i++) list.Add(new MutatorPart(PartKind.Item, i));
                    break;
            }
            return list;
        }

        // ------------------------------------------------------------------ the operations
        /// <summary>Adds an else if after the last one (before the else).</summary>
        public static bool AddElseIf(Workspace ws, BlockLayouter layouter, Block b)
        {
            if (KindOf(b) != Kind.If) return false;
            var order = Enumerable.Range(1, ElseIfCount(b)).ToList(); order.Add(0);
            ReshapeIf(ws, layouter, b, order, HasElse(b));
            return true;
        }

        /// <summary>Adds the else branch; an if has at most one.</summary>
        public static bool AddElse(Workspace ws, BlockLayouter layouter, Block b)
        {
            if (KindOf(b) != Kind.If || HasElse(b)) return false;
            ReshapeIf(ws, layouter, b, Enumerable.Range(1, ElseIfCount(b)).ToList(), true);
            return true;
        }

        /// <summary>Adds an item at the end.</summary>
        public static bool AddItem(Workspace ws, BlockLayouter layouter, Block b)
        {
            if (KindOf(b) != Kind.Items) return false;
            var order = Enumerable.Range(0, ItemCount(b)).ToList(); order.Add(-1);
            ReshapeItems(ws, layouter, b, order);
            return true;
        }

        /// <summary>Takes a part away; what is plugged into it stays in the workspace.</summary>
        public static bool Remove(Workspace ws, BlockLayouter layouter, Block b, MutatorPart part)
        {
            switch (KindOf(b))
            {
                case Kind.If:
                    if (part.Kind == PartKind.Else && HasElse(b))
                    { ReshapeIf(ws, layouter, b, Enumerable.Range(1, ElseIfCount(b)).ToList(), false); return true; }
                    if (part.Kind == PartKind.ElseIf && part.Index >= 1 && part.Index <= ElseIfCount(b))
                    { ReshapeIf(ws, layouter, b, Enumerable.Range(1, ElseIfCount(b)).Where(i => i != part.Index).ToList(), HasElse(b)); return true; }
                    return false;
                case Kind.Items:
                    if (part.Kind != PartKind.Item || part.Index < 0 || part.Index >= ItemCount(b)) return false;
                    ReshapeItems(ws, layouter, b, Enumerable.Range(0, ItemCount(b)).Where(i => i != part.Index).ToList());
                    return true;
            }
            return false;
        }

        /// <summary>Moves an else if or an item one place up (<paramref name="delta"/> -1) or down (+1). The first branch of an if and the else do not move.</summary>
        public static bool Move(Workspace ws, BlockLayouter layouter, Block b, MutatorPart part, int delta)
        {
            if (delta != -1 && delta != 1) return false;
            switch (KindOf(b))
            {
                case Kind.If:
                {
                    int n = ElseIfCount(b), to = part.Index + delta;
                    if (part.Kind != PartKind.ElseIf || part.Index < 1 || part.Index > n || to < 1 || to > n) return false;
                    var order = Enumerable.Range(1, n).ToList();
                    (order[part.Index - 1], order[to - 1]) = (order[to - 1], order[part.Index - 1]);
                    ReshapeIf(ws, layouter, b, order, HasElse(b));
                    return true;
                }
                case Kind.Items:
                {
                    int n = ItemCount(b), to = part.Index + delta;
                    if (part.Kind != PartKind.Item || part.Index < 0 || part.Index >= n || to < 0 || to >= n) return false;
                    var order = Enumerable.Range(0, n).ToList();
                    (order[part.Index], order[to]) = (order[to], order[part.Index]);
                    ReshapeItems(ws, layouter, b, order);
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ reshaping
        /// <param name="elseIfOrder">The new else ifs in order, each as its old number (1..), or 0 for a new one.</param>
        /// <param name="hasElse">Whether the block has an else afterwards (an old one is kept, a missing one is added empty).</param>
        static void ReshapeIf(Workspace ws, BlockLayouter layouter, Block b, List<int> elseIfOrder, bool hasElse)
        {
            int oldN = ElseIfCount(b);
            var cond = new Dictionary<int, Block>(); var body = new Dictionary<int, Block>();
            for (int k = 1; k <= oldN; k++) { cond[k] = b.GetInputTargetBlock("IF" + k); body[k] = b.GetInputTargetBlock("DO" + k); }
            var elseBody = b.GetInputTargetBlock("ELSE");
            var kept = new HashSet<int>(elseIfOrder.Where(i => i > 0));

            // the blocks of the parts that go stay in the workspace (unplugged while the block still has its shape, so they keep their place)
            for (int k = 1; k <= oldN; k++)
                if (!kept.Contains(k)) { Unplug(ws, layouter, cond[k]); Unplug(ws, layouter, body[k]); cond[k] = body[k] = null; }
            if (!hasElse) { Unplug(ws, layouter, elseBody); elseBody = null; }

            foreach (var c in cond.Values) c?.Output?.Disconnect();
            foreach (var c in body.Values) c?.Previous?.Disconnect();
            elseBody?.Previous?.Disconnect();

            b.Inputs.RemoveAll(i => i.Name == "ELSE" || IsBranchInput(i.Name));
            var state = new Dictionary<string, object>();
            if (elseIfOrder.Count > 0) state["elseIfCount"] = (double)elseIfOrder.Count;
            if (hasElse) state["hasElse"] = true;
            b.Definition.Mutator.Apply(b, state);
            b.ExtraState = state;

            for (int j = 1; j <= elseIfOrder.Count; j++)
            {
                int old = elseIfOrder[j - 1];
                if (old <= 0) continue;
                Plug(b.GetInput("IF" + j), cond[old]);
                Plug(b.GetInput("DO" + j), body[old]);
            }
            if (hasElse) Plug(b.GetInput("ELSE"), elseBody);
        }

        static bool IsBranchInput(string name)
        {
            if (name == null || name.Length < 3) return false;
            string head = name.Substring(0, 2);
            return (head == "IF" || head == "DO") && int.TryParse(name.Substring(2), out int n) && n >= 1;
        }

        /// <param name="order">The new items in order, each as its old index (0..), or -1 for a new one.</param>
        static void ReshapeItems(Workspace ws, BlockLayouter layouter, Block b, List<int> order)
        {
            int oldN = ItemCount(b);
            var child = new Dictionary<int, Block>();
            for (int k = 0; k < oldN; k++) child[k] = b.GetInputTargetBlock("ADD" + k);
            var kept = new HashSet<int>(order.Where(i => i >= 0));
            for (int k = 0; k < oldN; k++)
                if (!kept.Contains(k)) { Unplug(ws, layouter, child[k]); child[k] = null; }
            foreach (var c in child.Values) c?.Output?.Disconnect();

            var state = new Dictionary<string, object> { { "itemCount", (double)order.Count } };
            b.Definition.Mutator.Apply(b, state);                       // rebuilds every input of the block
            b.ExtraState = state;
            for (int j = 0; j < order.Count; j++)
                if (order[j] >= 0) Plug(b.GetInput("ADD" + j), child[order[j]]);
        }

        static void Unplug(Workspace ws, BlockLayouter layouter, Block child)
        {
            if (child != null) WorkspaceEditing.Unplug(ws, layouter, child, heal: false, bump: 24f);
        }

        static void Plug(Input input, Block child)
        {
            if (child == null || input?.Connection == null) return;
            input.Connection.Connect(child.Output ?? child.Previous);
        }
    }
}
