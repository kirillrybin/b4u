using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Blockly.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Blockly.UI.Tests
{
    /// <summary>
    /// Timings for a program of about 500 blocks: load, layout, building the UGUI hierarchy, editing, dragging and running.
    /// The numbers are written to the log ("[Perf] ..."); the assertions are deliberately loose and only catch a collapse.
    /// </summary>
    public class PerformanceProfile
    {
        GameObject root;

        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        /// <summary>Stacks of <paramref name="perStack"/> statements, each "print(join('n=', a + b))": 6 blocks apiece.</summary>
        static string BigProgram(int stacks, int perStack)
        {
            var sb = new StringBuilder("{\"blocks\":{\"languageVersion\":0,\"blocks\":[");
            for (int s = 0; s < stacks; s++)
            {
                if (s > 0) sb.Append(',');
                sb.Append("{\"x\":").Append(20 + (s % 4) * 330).Append(",\"y\":").Append(20 + (s / 4) * 700).Append(',');
                string close = "";
                for (int i = 0; i < perStack; i++)
                {
                    sb.Append("\"type\":\"text_print\",\"inputs\":{\"TEXT\":{\"block\":{\"type\":\"text_join\",\"extraState\":{\"itemCount\":2},\"inputs\":{")
                      .Append("\"ADD0\":{\"block\":{\"type\":\"text\",\"fields\":{\"TEXT\":\"n=\"}}},")
                      .Append("\"ADD1\":{\"block\":{\"type\":\"math_arithmetic\",\"fields\":{\"OP\":\"ADD\"},\"inputs\":{")
                      .Append("\"A\":{\"block\":{\"type\":\"math_number\",\"fields\":{\"NUM\":").Append(s).Append("}}},")
                      .Append("\"B\":{\"block\":{\"type\":\"math_number\",\"fields\":{\"NUM\":").Append(i).Append("}}}}}}}}}}");
                    if (i < perStack - 1) { sb.Append(",\"next\":{\"block\":{"); close += "}}"; }
                }
                sb.Append(close).Append('}');
            }
            return sb.Append("]}}").ToString();
        }

        static double Time(System.Action a, int repeat = 1)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < repeat; i++) a();
            return sw.Elapsed.TotalMilliseconds / repeat;
        }

        static readonly List<string> report = new List<string>();
        static void Report(string what, double ms) => report.Add(what.PadRight(34) + ms.ToString("0.00").PadLeft(10) + " ms");

        [Test]
        public void ProfileAProgramOfFiveHundredBlocks()
        {
            report.Clear();
            string json = BigProgram(8, 10);
            Workspace ws = null;
            Report("load JSON", Time(() => ws = StandardBlockSet.Load(json)));
            int blocks = ws.AllBlocks().Count();
            Assert.AreEqual(8 * 10 * 6, blocks);
            Report("save JSON", Time(() => WorkspaceJson.Save(ws), 5));

            root = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("View", typeof(RectTransform), typeof(WorkspaceView));
            go.transform.SetParent(root.transform, false);
            var view = go.GetComponent<WorkspaceView>();
            double first = Time(() => view.SetWorkspace(ws));
            Report("first build (font setup included)", first);
            Report("Rebuild (layout + UGUI)", Time(view.Rebuild, 5));
            var measurer = view.Measurer;
            Report("layout only", Time(() => new BlockLayouter(ws, measurer).LayoutWorkspace(), 5));
            int graphics = go.GetComponentsInChildren<Graphic>(true).Length;
            int objects = go.GetComponentsInChildren<Transform>(true).Length;

            var target = ws.TopBlocks[0].NextBlock.NextBlock.NextBlock;       // a statement inside the first stack
            Report("select (outline)", Time(() => { view.Select(target); view.Select(ws.TopBlocks[1]); }, 10) / 2);

            // dragging: pick up a statement in the middle of a stack and move it over the others
            var box = view.Layout.ByBlock[target];
            var start = new Vector2((float)target.X, (float)target.Y);
            Report("BeginDrag (rebuild included)", Time(() => view.BeginDrag(target, start + new Vector2(10, 10))));
            double frames = Time(() =>
            {
                for (int i = 0; i < 60; i++) view.DragTo(start + new Vector2(10 + i * 8, 10 + i * 5));
            });
            Report("DragTo, per frame (60 frames)", frames / 60);
            Report("EndDrag", Time(view.EndDrag));
            Report("Undo", Time(() => view.Undo()));
            Report("Redo", Time(() => view.Redo()));
            Report("Zoom step", Time(() => view.SetZoom(view.Zoom * 1.1f, Vector2.zero), 5));

            var lines = new List<string>();
            var it = new Interpreter(ws) { Print = lines.Add };
            Report("run program (" + 80 + " prints)", Time(() => { it.Start(); it.RunToCompletion(1000000); }));
            Assert.IsNull(it.Error);

            Debug.Log("[Perf] " + blocks + " blocks, " + objects + " objects, " + graphics + " graphics\n[Perf] " + string.Join("\n[Perf] ", report));
            Assert.Less(first, 20000, "building 500 blocks took more than 20 s");
        }
    }
}
