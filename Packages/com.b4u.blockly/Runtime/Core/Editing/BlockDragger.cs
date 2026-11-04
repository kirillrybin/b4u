using System;
using System.Collections.Generic;
using Blockly.Layout;

namespace Blockly.Editing
{
    /// <summary>The pair of connections a dragged block would snap to if dropped now.</summary>
    public struct SnapCandidate
    {
        public Connection Dragged, Target;
        public Vec2 DraggedPoint, TargetPoint;
        public float Distance;
    }

    public enum DropOutcome { Moved, Connected, Cancelled }

    /// <summary>
    /// One drag-and-drop of a block (together with the blocks that follow it in its stack), following Blockly's rules:
    /// the block detaches from its parent when the drag starts; while moving, the closest compatible connection within
    /// <see cref="SnapRadius"/> is offered as a snap candidate; dropping connects it. Pure model logic (no UI).
    /// </summary>
    public sealed class BlockDragger
    {
        public const float SnapRadius = 28f;
        /// <summary>How far a displaced block is pushed away when it has nowhere to go.</summary>
        public const float BumpDistance = 32f;

        readonly Workspace workspace;
        readonly BlockLayouter layouter;

        Block dragged, lastInChain;
        HashSet<Block> draggedSet;
        LayoutResult staticLayout;
        Connection originalParent;               // parent-side connection the block was attached to, if any
        double startX, startY;

        public BlockDragger(Workspace workspace, BlockLayouter layouter)
        {
            this.workspace = workspace;
            this.layouter = layouter;
        }

        public Block Dragged => dragged;
        public bool IsDragging => dragged != null;
        public SnapCandidate? Candidate { get; private set; }

        /// <summary>Layout of the dragged stack at its current position (updated on every move).</summary>
        public LayoutResult DraggedLayout { get; private set; }

        /// <summary>The blocks being dragged (the block and everything after it in its stack, with their children).</summary>
        public IReadOnlyCollection<Block> DraggedBlocks => draggedSet;

        public bool IsBeingDragged(Block block) => draggedSet != null && draggedSet.Contains(block);

        // ------------------------------------------------------------------ begin
        public bool Begin(Block block)
        {
            if (IsDragging || block == null || block.IsShadow) return false;

            var before = layouter.LayoutWorkspace();
            if (!before.ByBlock.TryGetValue(block, out var box)) return false;
            startX = box.X; startY = box.Y;

            var child = block.Output ?? block.Previous;
            originalParent = child?.Target;
            if (originalParent != null)
            {
                child.Disconnect();
                workspace.TopBlocks.Add(block);
            }
            block.X = startX; block.Y = startY;

            dragged = block;
            draggedSet = new HashSet<Block>(block.Descendants());
            lastInChain = block;
            while (lastInChain.NextBlock != null) lastInChain = lastInChain.NextBlock;

            staticLayout = layouter.LayoutWorkspace();   // after detaching, so the old parent has already shrunk
            DraggedLayout = layouter.LayoutStack(dragged, (float)dragged.X, (float)dragged.Y);
            Candidate = null;
            return true;
        }

        // ------------------------------------------------------------------ move
        public void MoveBy(double dx, double dy) => MoveTo(dragged.X + dx, dragged.Y + dy);

        /// <summary>Moves the block's top-left corner to workspace position (x, y) and refreshes the snap candidate.</summary>
        public void MoveTo(double x, double y)
        {
            if (!IsDragging) return;
            dragged.X = x; dragged.Y = y;
            Candidate = FindCandidate();
        }

        SnapCandidate? FindCandidate()
        {
            var layout = DraggedLayout = layouter.LayoutStack(dragged, (float)dragged.X, (float)dragged.Y);
            var mine = new List<KeyValuePair<Connection, Vec2>>();
            Collect(layout, dragged, mine, headAndInputs: true);
            if (lastInChain.Next != null) Collect(layout, lastInChain, mine, headAndInputs: false);

            SnapCandidate? best = null;
            foreach (var box in staticLayout.Boxes)
            {
                if (box.IsShadow || draggedSet.Contains(box.Block)) continue;
                foreach (var target in box.Connections)
                    foreach (var a in mine)
                    {
                        if (!CanConnect(a.Key, target.Key)) continue;
                        float dx = a.Value.X - target.Value.X, dy = a.Value.Y - target.Value.Y;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d > SnapRadius) continue;
                        if (best == null || d < best.Value.Distance)
                            best = new SnapCandidate { Dragged = a.Key, Target = target.Key, DraggedPoint = a.Value, TargetPoint = target.Value, Distance = d };
                    }
            }
            return best;
        }

        /// <summary>The connections of the dragged stack that may snap: head (output/previous), own empty inputs, last next.</summary>
        void Collect(LayoutResult layout, Block block, List<KeyValuePair<Connection, Vec2>> into, bool headAndInputs)
        {
            if (!layout.ByBlock.TryGetValue(block, out var box)) return;
            if (headAndInputs)
            {
                foreach (var c in new[] { block.Output, block.Previous })
                    if (c != null && box.Connections.TryGetValue(c, out var p)) into.Add(new KeyValuePair<Connection, Vec2>(c, p));
                foreach (var input in block.Inputs)
                    if (input.Connection != null && input.Connection.Target == null && box.Connections.TryGetValue(input.Connection, out var p))
                        into.Add(new KeyValuePair<Connection, Vec2>(input.Connection, p));
            }
            else if (block.Next != null && box.Connections.TryGetValue(block.Next, out var np))
            {
                into.Add(new KeyValuePair<Connection, Vec2>(block.Next, np));
            }
        }

        bool CanConnect(Connection a, Connection b)
        {
            if (!a.CheckCompatible(b)) return false;
            if (!a.IsParentSide)
            {
                // a = dragged block's output/previous (child side), b = a parent-side connection somewhere else
                var occupant = b.Target?.Owner;
                if (occupant == null) return true;
                if (b.Type == ConnectionType.NextStatement)
                {
                    // insertion into a stack: the displaced blocks must be able to follow the dragged stack
                    return lastInChain.Next != null && occupant.Previous != null && lastInChain.Next.CheckCompatible(occupant.Previous);
                }
                return true;       // value input already filled: the old value is re-homed or bumped
            }
            // a = a parent-side connection of the dragged stack; b = child side of a block that must not be attached yet
            return b.Target == null;
        }

        // ------------------------------------------------------------------ end
        public DropOutcome End()
        {
            if (!IsDragging) return DropOutcome.Cancelled;
            var outcome = DropOutcome.Moved;
            if (Candidate.HasValue)
            {
                Perform(Candidate.Value.Dragged, Candidate.Value.Target);
                outcome = DropOutcome.Connected;
            }
            Reset();
            return outcome;
        }

        /// <summary>Puts the block back exactly where (and attached to what) it was before the drag.</summary>
        public void Cancel()
        {
            if (!IsDragging) return;
            var child = dragged.Output ?? dragged.Previous;
            if (originalParent != null && child != null && originalParent.Target == null && child.Target == null)
            {
                originalParent.Connect(child);
                workspace.TopBlocks.Remove(dragged);
            }
            else
            {
                dragged.X = startX; dragged.Y = startY;
            }
            Reset();
        }

        void Reset()
        {
            dragged = null; lastInChain = null; draggedSet = null; staticLayout = null; originalParent = null; DraggedLayout = null;
            Candidate = null;
        }

        // ------------------------------------------------------------------ connecting
        void Perform(Connection a, Connection b)
        {
            bool draggedIsChild = !a.IsParentSide;
            var parentSide = draggedIsChild ? b : a;
            var childSide = draggedIsChild ? a : b;

            Block orphan = parentSide.Target?.Owner;
            if (orphan != null)
            {
                parentSide.Disconnect();
                if (staticLayout.ByBlock.TryGetValue(orphan, out var ob)) { orphan.X = ob.X; orphan.Y = ob.Y; }
                workspace.TopBlocks.Add(orphan);
            }

            parentSide.Connect(childSide);
            workspace.TopBlocks.Remove(childSide.Owner);

            if (orphan != null) Rehome(orphan, parentSide, childSide.Owner);
        }

        /// <summary>A displaced block goes after the dragged stack, into a lone matching empty input, or gets bumped aside.</summary>
        void Rehome(Block orphan, Connection formerParent, Block newChild)
        {
            if (formerParent.Type == ConnectionType.NextStatement)
            {
                var last = newChild;
                while (last.NextBlock != null) last = last.NextBlock;
                if (last.Next != null && orphan.Previous != null && last.Next.CheckCompatible(orphan.Previous))
                {
                    last.Next.Connect(orphan.Previous);
                    workspace.TopBlocks.Remove(orphan);
                    return;
                }
            }
            else if (orphan.Output != null)
            {
                Connection only = null; int count = 0;
                foreach (var input in newChild.Inputs)
                    if (input.Kind == InputKind.Value && input.Connection.Target == null && input.Connection.CheckCompatible(orphan.Output))
                    { only = input.Connection; count++; }
                if (count == 1)
                {
                    only.Connect(orphan.Output);
                    workspace.TopBlocks.Remove(orphan);
                    return;
                }
            }
            orphan.X += BumpDistance;
            orphan.Y += BumpDistance;
        }
    }
}
