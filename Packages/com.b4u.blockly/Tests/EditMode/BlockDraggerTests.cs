using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blockly.Editing;
using Blockly.Layout;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class BlockDraggerTests
    {
        sealed class FixedWidth : ITextMeasurer { public float Width(string t) => t.Length * 6f; }
        const string Ns = "xmlns=\"https://developers.google.com/blockly/xml\"";

        sealed class Env
        {
            public Workspace Ws; public BlockLayouter Layouter; public BlockDragger Dragger;
            public Env(Workspace ws) { Ws = ws; Layouter = new BlockLayouter(ws, new FixedWidth()); Dragger = new BlockDragger(ws, Layouter); }
            public Block Top(int i) => Ws.TopBlocks[i];

            /// <summary>Drags <paramref name="block"/> so that its connection <paramref name="mine"/> lands on <paramref name="target"/>.</summary>
            public DropOutcome DropOn(Block block, Func<Block, Connection> mine, Connection target, float jitter = 0f)
            {
                Assert.IsTrue(Dragger.Begin(block));
                MoveSoThat(block, mine(block), target, jitter);
                return Dragger.End();
            }

            public void MoveSoThat(Block block, Connection mine, Connection target, float jitter = 0f)
            {
                var layout = Layouter.LayoutWorkspace();                       // positions after the block was detached
                var tp = layout.ByBlock[target.Owner].Connections[target];
                var own = Layouter.LayoutStack(block, (float)block.X, (float)block.Y);
                var mp = own.ByBlock[mine.Owner].Connections[mine];
                Dragger.MoveTo(block.X + (tp.X - mp.X) + jitter, block.Y + (tp.Y - mp.Y) + jitter);
            }
        }

        static Env FromXml(string body)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, "<xml " + Ns + ">" + body + "</xml>");
            return new Env(ws);
        }

        static string Print(string t, string next = "", int x = -1, int y = 0)
            => "<block type=\"text_print\"" + (x >= 0 ? $" x=\"{x}\" y=\"{y}\"" : "") + "><value name=\"TEXT\"><block type=\"text\"><field name=\"TEXT\">" + t + "</field></block></value>"
               + (next == "" ? "" : "<next>" + next + "</next>") + "</block>";

        static string Run(Workspace ws)
        {
            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            it.Start(); it.RunToCompletion();
            Assert.IsNull(it.Error);
            return string.Join(",", lines);
        }

        // ---------------------------------------------------------------- basic moves
        [Test]
        public void DropInTheAirJustMovesTheBlock()
        {
            var e = FromXml(Print("a", "", 10, 10));
            var b = e.Top(0);
            Assert.IsTrue(e.Dragger.Begin(b));
            e.Dragger.MoveTo(400, 300);
            Assert.IsNull(e.Dragger.Candidate);
            Assert.AreEqual(DropOutcome.Moved, e.Dragger.End());
            Assert.AreEqual(400, b.X); Assert.AreEqual(300, b.Y);
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
        }

        [Test]
        public void BeginDetachesFromTheParentAndKeepsTheOnScreenPosition()
        {
            var e = FromXml("<block type=\"controls_repeat_ext\" x=\"20\" y=\"30\"><statement name=\"DO\">" + Print("a") + "</statement></block>");
            var child = e.Top(0).GetInputTargetBlock("DO");
            var before = e.Layouter.LayoutWorkspace().ByBlock[child];
            Assert.IsTrue(e.Dragger.Begin(child));
            Assert.IsNull(child.Parent);
            Assert.Contains(child, e.Ws.TopBlocks.ToList());
            Assert.AreEqual(before.X, child.X, 1e-3); Assert.AreEqual(before.Y, child.Y, 1e-3);
        }

        [Test]
        public void ShadowsCannotBeDraggedAndOnlyOneDragRunsAtATime()
        {
            var e = FromXml("<block type=\"text_print\"><value name=\"TEXT\"><shadow type=\"text\"><field name=\"TEXT\">s</field></shadow></value></block>");
            var shadow = e.Top(0).GetInput("TEXT").Connection.Shadow;
            Assert.IsFalse(e.Dragger.Begin(shadow));
            Assert.IsTrue(e.Dragger.Begin(e.Top(0)));
            Assert.IsFalse(e.Dragger.Begin(e.Top(0)), "second Begin while dragging");
            e.Dragger.Cancel();
        }

        [Test]
        public void DraggingTheMiddleOfAStackTakesTheRestOfTheStack()
        {
            var e = FromXml(Print("a", Print("b", Print("c")), 10, 10));
            var a = e.Top(0); var b = a.NextBlock; var c = b.NextBlock;
            Assert.IsTrue(e.Dragger.Begin(b));
            Assert.IsNull(a.NextBlock);
            Assert.AreSame(c, b.NextBlock);
            CollectionAssert.AreEquivalent(new[] { b, c, b.GetInputTargetBlock("TEXT"), c.GetInputTargetBlock("TEXT") }, e.Dragger.DraggedBlocks.ToList());
            Assert.AreEqual(2, e.Ws.TopBlocks.Count);
            e.Dragger.Cancel();
        }

        [Test]
        public void CancelRestoresTheOriginalConnectionAndOrder()
        {
            var e = FromXml(Print("a", Print("b", Print("c")), 10, 10));
            string before = WorkspaceJson.Save(e.Ws);
            var b = e.Top(0).NextBlock;
            e.Dragger.Begin(b);
            e.Dragger.MoveTo(500, 500);
            e.Dragger.Cancel();
            Assert.AreEqual(before, WorkspaceJson.Save(e.Ws));
            Assert.AreEqual("a,b,c", Run(e.Ws));
        }

        [Test]
        public void CancelOfATopLevelBlockRestoresItsPosition()
        {
            var e = FromXml(Print("a", "", 10, 10));
            e.Dragger.Begin(e.Top(0));
            e.Dragger.MoveTo(300, 300);
            e.Dragger.Cancel();
            Assert.AreEqual(10, e.Top(0).X); Assert.AreEqual(10, e.Top(0).Y);
        }

        // ---------------------------------------------------------------- snapping rules
        [Test]
        public void ValueDropsIntoAnEmptyInput()
        {
            var e = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"></block><block type=\"text\" x=\"300\" y=\"300\"><field name=\"TEXT\">hi</field></block>");
            var print = e.Top(0); var text = e.Top(1);
            Assert.AreEqual(DropOutcome.Connected, e.DropOn(text, b => b.Output, print.GetInput("TEXT").Connection, jitter: 6f));
            Assert.AreSame(text, print.GetInputTargetBlock("TEXT"));
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
            Assert.AreEqual("hi", Run(e.Ws));
        }

        [Test]
        public void SnapOnlyWithinTheRadius()
        {
            var e = FromXml("<block type=\"text_print\" x=\"10\" y=\"10\"></block><block type=\"text\" x=\"300\" y=\"300\"><field name=\"TEXT\">hi</field></block>");
            var print = e.Top(0); var text = e.Top(1);
            e.Dragger.Begin(text);
            e.MoveSoThat(text, text.Output, print.GetInput("TEXT").Connection, jitter: BlockDragger.SnapRadius * 0.5f);
            Assert.IsNotNull(e.Dragger.Candidate, "inside the radius");
            e.MoveSoThat(text, text.Output, print.GetInput("TEXT").Connection, jitter: BlockDragger.SnapRadius * 2f);
            Assert.IsNull(e.Dragger.Candidate, "outside the radius");
            e.Dragger.Cancel();
        }

        [Test]
        public void TypeChecksStopWrongDrops()
        {
            var e = FromXml("<block type=\"math_arithmetic\" x=\"10\" y=\"10\"><field name=\"OP\">ADD</field></block>" +
                            "<block type=\"text\" x=\"300\" y=\"300\"><field name=\"TEXT\">s</field></block>");
            var add = e.Top(0); var text = e.Top(1);
            e.Dragger.Begin(text);
            e.MoveSoThat(text, text.Output, add.GetInput("A").Connection);
            Assert.IsNull(e.Dragger.Candidate, "a String does not fit a Number input");
            e.Dragger.Cancel();
        }

        [Test]
        public void StatementInsertsIntoTheMiddleOfAStack()
        {
            var e = FromXml(Print("a", Print("b"), 10, 10) + Print("c", "", 400, 300));
            var a = e.Top(0); var c = e.Top(1);
            Assert.AreEqual(DropOutcome.Connected, e.DropOn(c, x => x.Previous, a.Next));
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
            Assert.AreEqual("a,c,b", Run(e.Ws), "the old follower b moved after the inserted block");
        }

        [Test]
        public void StatementAppendsAtTheEndOfAStack()
        {
            var e = FromXml(Print("a", "", 10, 10) + Print("b", "", 400, 300));
            Assert.AreEqual(DropOutcome.Connected, e.DropOn(e.Top(1), x => x.Previous, e.Top(0).Next));
            Assert.AreEqual("a,b", Run(e.Ws));
        }

        [Test]
        public void StatementDropsIntoACInput()
        {
            var e = FromXml("<block type=\"controls_repeat_ext\" x=\"10\" y=\"10\"><value name=\"TIMES\"><shadow type=\"math_number\"><field name=\"NUM\">3</field></shadow></value></block>" + Print("x", "", 400, 300));
            var rep = e.Top(0); var print = e.Top(1);
            e.DropOn(print, x => x.Previous, rep.GetInput("DO").Connection);
            Assert.AreSame(print, rep.GetInputTargetBlock("DO"));
            Assert.AreEqual("x,x,x", Run(e.Ws));
        }

        [Test]
        public void DroppingAStackAboveAnotherStack()
        {
            var e = FromXml(Print("a", "", 10, 10) + Print("b", "", 400, 300));
            var a = e.Top(0); var b = e.Top(1);
            // b's last next connection onto a's previous connection => b ends up above a
            Assert.AreEqual(DropOutcome.Connected, e.DropOn(b, x => x.Next, a.Previous));
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
            Assert.AreSame(b, e.Top(0));
            Assert.AreEqual("b,a", Run(e.Ws));
        }

        [Test]
        public void ADroppedValueDisplacesTheOldOneIntoItsOwnInputWhenThereIsExactlyOne()
        {
            var e = FromXml(Print("abc", "", 10, 10) + "<block type=\"text_length\" x=\"400\" y=\"300\"></block>");
            var print = e.Top(0); var len = e.Top(1);
            var old = print.GetInputTargetBlock("TEXT");
            e.DropOn(len, x => x.Output, print.GetInput("TEXT").Connection);
            Assert.AreSame(len, print.GetInputTargetBlock("TEXT"));
            Assert.AreSame(old, len.GetInputTargetBlock("VALUE"), "the old value moved into the new block's only free input");
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
            Assert.AreEqual("3", Run(e.Ws));
        }

        [Test]
        public void ADisplacedValueWithNowhereToGoIsBumpedNotLost()
        {
            var e = FromXml(Print("abc", "", 10, 10) + "<block type=\"text\" x=\"400\" y=\"300\"><field name=\"TEXT\">new</field></block>");
            var print = e.Top(0); var fresh = e.Top(1);
            var old = print.GetInputTargetBlock("TEXT");
            int count = e.Ws.AllBlocks().Count();
            e.DropOn(fresh, x => x.Output, print.GetInput("TEXT").Connection);
            Assert.AreSame(fresh, print.GetInputTargetBlock("TEXT"));
            Assert.Contains(old, e.Ws.TopBlocks.ToList(), "displaced block is still in the workspace");
            Assert.AreEqual(count, e.Ws.AllBlocks().Count());
            Assert.AreEqual("new", Run(e.Ws));
        }

        [Test]
        public void ABlockCanWrapATopLevelBlockThroughItsOwnInput()
        {
            var e = FromXml("<block type=\"logic_negate\" x=\"10\" y=\"10\"></block><block type=\"logic_boolean\" x=\"400\" y=\"300\"><field name=\"BOOL\">TRUE</field></block>");
            var neg = e.Top(0); var truth = e.Top(1);
            e.DropOn(neg, x => x.GetInput("BOOL").Connection, truth.Output);
            Assert.AreSame(truth, neg.GetInputTargetBlock("BOOL"));
            Assert.AreEqual(1, e.Ws.TopBlocks.Count);
        }

        [Test]
        public void ABlockCannotBeDroppedIntoItsOwnDescendant()
        {
            var e = FromXml("<block type=\"controls_repeat_ext\" x=\"10\" y=\"10\"><statement name=\"DO\">" + Print("a") + "</statement></block>");
            var rep = e.Top(0); var inner = rep.GetInputTargetBlock("DO");
            e.Dragger.Begin(rep);
            // align the repeat's own previous connection with a connection that lives inside the dragged subtree
            e.MoveSoThat(rep, rep.Previous, inner.Next);
            Assert.IsTrue(e.Dragger.Candidate == null || !e.Dragger.IsBeingDragged(e.Dragger.Candidate.Value.Target.Owner));
            e.Dragger.Cancel();
        }

        [Test]
        public void DisplacedStackKeepsFollowingTheInsertedOne()
        {
            var e = FromXml(Print("a", Print("b", Print("c")), 10, 10) + Print("x", Print("y"), 400, 300));
            var a = e.Top(0); var x = e.Top(1);
            e.DropOn(x, s => s.Previous, a.Next);
            Assert.AreEqual("a,x,y,b,c", Run(e.Ws), "the dragged stack goes in, the old tail hangs off its end");
        }

        // ---------------------------------------------------------------- invariants under random drags
        static string Dir => Path.Combine(TestSupport.PackageRoot(), "Tests", "EditMode", "Fixtures", "programs");

        public static string[] FuzzPrograms => new[] { "loops", "break_continue", "if_else", "lists", "text", "procedures", "logic" };

        internal static void AssertModelIsSound(Workspace ws, int expectedBlocks, string context)
        {
            var all = ws.AllBlocks().ToList();
            Assert.AreEqual(expectedBlocks, all.Count, context + ": blocks lost or duplicated");
            Assert.AreEqual(all.Count, all.Distinct().Count(), context + ": a block is reachable twice");

            var parentless = all.Where(b => b.Parent == null).ToList();
            CollectionAssert.AreEquivalent(parentless, ws.TopBlocks.ToList(), context + ": TopBlocks must be exactly the parentless blocks");

            foreach (var b in all)
            {
                foreach (var c in new[] { b.Output, b.Previous, b.Next }.Concat(b.Inputs.Select(i => i.Connection)).Where(c => c != null))
                {
                    if (c.Target == null) continue;
                    Assert.AreSame(c, c.Target.Target, context + ": asymmetric connection on " + b.Type);
                    Assert.IsTrue(c.CheckCompatible(c.Target), context + ": incompatible pair " + c.Describe() + " -> " + c.Target.Describe());
                }
                // no cycles
                int guard = 0; for (var p = b; p != null; p = p.Parent) Assert.Less(guard++, 10000, context + ": cycle");
            }
        }

        [TestCaseSource(nameof(FuzzPrograms))]
        public void RandomDragsKeepTheWorkspaceConsistent(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            var env = new Env(ws);
            int count = ws.AllBlocks().Count();
            var rng = new Random(12345 + name.Length);
            int connected = 0, moved = 0;

            for (int step = 0; step < 150; step++)
            {
                var candidates = ws.AllBlocks().ToList();
                var block = candidates[rng.Next(candidates.Count)];
                string ctx = $"{name} step {step} dragging {block.Type}";
                if (!env.Dragger.Begin(block)) continue;
                AssertModelIsSound(ws, count, ctx + " (after Begin)");

                // aim at a random connection of a random other block, or into the void
                var layout = env.Layouter.LayoutWorkspace();
                var targets = layout.Boxes.Where(b => !b.IsShadow && !env.Dragger.IsBeingDragged(b.Block)).ToList();
                if (targets.Count > 0 && rng.Next(4) != 0)
                {
                    var tb = targets[rng.Next(targets.Count)];
                    var tc = tb.Connections.Keys.ElementAt(rng.Next(tb.Connections.Count));
                    var tp = tb.Connections[tc];
                    var own = env.Layouter.LayoutStack(block, (float)block.X, (float)block.Y);
                    var heads = new List<Connection> { block.Output ?? block.Previous }.Where(c => c != null).ToList();
                    var mine = heads.Count > 0 ? heads[0] : block.Next;
                    if (mine != null && own.ByBlock[mine.Owner].Connections.TryGetValue(mine, out var mp))
                        env.Dragger.MoveTo(block.X + tp.X - mp.X + rng.Next(-8, 9), block.Y + tp.Y - mp.Y + rng.Next(-8, 9));
                }
                else env.Dragger.MoveTo(rng.Next(-200, 900), rng.Next(-200, 900));

                if (rng.Next(10) == 0) { env.Dragger.Cancel(); }
                else if (env.Dragger.End() == DropOutcome.Connected) connected++; else moved++;

                AssertModelIsSound(ws, count, ctx + " (after drop)");
                var layoutAfter = env.Layouter.LayoutWorkspace();                 // must still lay out
                Assert.AreEqual(count, layoutAfter.Boxes.Count(b => !b.IsShadow), ctx);
            }

            // the edited workspace still serializes and reloads to the same thing
            string saved = WorkspaceJson.Save(ws);
            var reloaded = TestSupport.NewWorkspace();
            WorkspaceJson.Load(reloaded, saved);
            Assert.AreEqual(count, reloaded.AllBlocks().Count());
            Assert.AreEqual(saved, WorkspaceJson.Save(reloaded));
            Assert.Greater(connected, 0, name + ": the fuzzer should have connected something");
            Assert.Greater(moved, 0);
        }

        // ---------------------------------------------------------------- random edits incl. toolbox spawn and delete
        public static string[] EditPrograms => new[] { "loops", "procedures", "lists", "if_else", "variables" };

        [TestCaseSource(nameof(EditPrograms))]
        public void RandomEditsWithSpawnAndDeleteStayConsistent(string name)
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, File.ReadAllText(Path.Combine(Dir, name + ".json")));
            var env = new Env(ws);
            var toolbox = TestSupport.NewToolbox();
            int expected = ws.AllBlocks().Count();
            var rng = new Random(777 + name.Length * 31);
            int spawned = 0, deleted = 0, connectedNew = 0;

            void AimAtRandomConnection(Block block)
            {
                var layout = env.Layouter.LayoutWorkspace();
                var targets = layout.Boxes.Where(b => !b.IsShadow && !env.Dragger.IsBeingDragged(b.Block)).ToList();
                var own = env.Layouter.LayoutStack(block, (float)block.X, (float)block.Y);
                var mine = block.Output ?? block.Previous ?? block.Next;
                if (targets.Count > 0 && mine != null && own.ByBlock[mine.Owner].Connections.TryGetValue(mine, out var mp) && rng.Next(5) != 0)
                {
                    var tb = targets[rng.Next(targets.Count)];
                    var tc = tb.Connections.Keys.ElementAt(rng.Next(tb.Connections.Count));
                    var tp = tb.Connections[tc];
                    env.Dragger.MoveTo(block.X + tp.X - mp.X + rng.Next(-6, 7), block.Y + tp.Y - mp.Y + rng.Next(-6, 7));
                }
                else env.Dragger.MoveTo(rng.Next(-100, 800), rng.Next(-100, 800));
            }

            for (int step = 0; step < 220; step++)
            {
                int op = rng.Next(10);
                string ctx = $"{name} step {step} op {op}";
                if (op <= 4)                                                        // move / connect an existing block
                {
                    var all = ws.AllBlocks().ToList();
                    if (all.Count == 0) continue;
                    var block = all[rng.Next(all.Count)];
                    if (!env.Dragger.Begin(block)) continue;
                    AimAtRandomConnection(block);
                    if (rng.Next(8) == 0) env.Dragger.Cancel(); else env.Dragger.End();
                }
                else if (op <= 7)                                                   // pull a block out of the toolbox
                {
                    var cat = toolbox.Categories[rng.Next(toolbox.Categories.Count)];
                    var specs = Flyout.Specs(cat, ws);
                    var spec = specs[rng.Next(specs.Count)];
                    var block = WorkspaceEditing.Spawn(ws, spec);
                    block.X = rng.Next(0, 600); block.Y = rng.Next(0, 600);
                    ws.TopBlocks.Add(block);
                    int size = block.Descendants().Count();
                    expected += size; spawned++;
                    Assert.IsTrue(env.Dragger.Begin(block, isNew: true), ctx);
                    AimAtRandomConnection(block);
                    if (rng.Next(4) == 0) { env.Dragger.Cancel(); expected -= size; }       // dropped on the toolbox again
                    else if (env.Dragger.End() == DropOutcome.Connected) connectedNew++;
                }
                else if (op == 8)                                                   // drag something into the trash
                {
                    var all = ws.AllBlocks().ToList();
                    if (all.Count == 0) continue;
                    var block = all[rng.Next(all.Count)];
                    if (!env.Dragger.Begin(block)) continue;
                    int atLeast = env.Dragger.DraggedBlocks.Count;
                    int before = ws.AllBlocks().Count();
                    env.Dragger.Delete();
                    int after = ws.AllBlocks().Count();
                    Assert.LessOrEqual(after, before - atLeast, ctx + ": the whole dragged stack must be gone");
                    expected = after; deleted++;
                }
                else                                                                // Delete key: remove one block, heal the stack
                {
                    var all = ws.AllBlocks().ToList();
                    if (all.Count == 0) continue;
                    var block = all[rng.Next(all.Count)];
                    int before = ws.AllBlocks().Count();
                    WorkspaceEditing.Delete(ws, env.Layouter, block, heal: true);
                    Assert.Less(ws.AllBlocks().Count(), before, ctx + ": something was deleted");
                    expected = ws.AllBlocks().Count(); deleted++;
                }

                AssertModelIsSound(ws, expected, ctx);
                if (step % 10 == 0) Assert.DoesNotThrow(() => { var it = new Interpreter(ws); it.Start(); it.RunToCompletion(20000); }, ctx);
            }

            string saved = WorkspaceJson.Save(ws);
            var reloaded = TestSupport.NewWorkspace();
            WorkspaceJson.Load(reloaded, saved);
            Assert.AreEqual(expected, reloaded.AllBlocks().Count());
            Assert.AreEqual(saved, WorkspaceJson.Save(reloaded));
            Assert.Greater(spawned, 10, name); Assert.Greater(deleted, 5, name); Assert.Greater(connectedNew, 0, name + ": some spawned blocks should have snapped in");
        }
    }
}
