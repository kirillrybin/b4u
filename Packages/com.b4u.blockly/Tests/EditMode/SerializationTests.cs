using System.Linq;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class SerializationTests
    {
        static void AssertHelloShape(Workspace ws)
        {
            Assert.AreEqual(1, ws.TopBlocks.Count);
            var set = ws.TopBlocks[0];
            Assert.AreEqual("variables_set", set.Type);
            Assert.AreEqual(20, set.X); Assert.AreEqual(30, set.Y);
            Assert.AreEqual("count", ws.GetVariableById(set.GetFieldValue("VAR")).Name);
            Assert.AreEqual("3", set.GetInputTargetBlock("VALUE").GetFieldValue("NUM"));

            var rep = set.NextBlock;
            Assert.AreEqual("controls_repeat_ext", rep.Type);
            Assert.AreSame(set, rep.Parent);
            var times = rep.GetInputTargetBlock("TIMES");
            Assert.AreEqual("variables_get", times.Type);
            Assert.AreEqual("10", rep.GetInput("TIMES").Connection.Shadow.GetFieldValue("NUM"));

            var print = rep.GetInputTargetBlock("DO");
            Assert.AreEqual("text_print", print.Type);
            Assert.AreEqual("hi", print.GetInput("TEXT").Connection.Shadow.GetFieldValue("TEXT"));
            Assert.AreEqual(5, ws.AllBlocks().Count());
        }

        [Test]
        public void LoadsJson()
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceJson.Load(ws, TestSupport.Read("Tests", "EditMode", "Fixtures", "hello.json"));
            AssertHelloShape(ws);
        }

        [Test]
        public void LoadsXml()
        {
            var ws = TestSupport.NewWorkspace();
            WorkspaceXml.Load(ws, TestSupport.Read("Tests", "EditMode", "Fixtures", "hello.xml"));
            AssertHelloShape(ws);
        }

        [Test]
        public void JsonRoundTripIsStable()
        {
            var a = TestSupport.NewWorkspace();
            WorkspaceJson.Load(a, TestSupport.Read("Tests", "EditMode", "Fixtures", "hello.json"));
            string saved = WorkspaceJson.Save(a);
            var b = TestSupport.NewWorkspace();
            WorkspaceJson.Load(b, saved);
            AssertHelloShape(b);
            Assert.AreEqual(saved, WorkspaceJson.Save(b));
        }

        [Test]
        public void XmlAndJsonProduceSameWorkspace()
        {
            var j = TestSupport.NewWorkspace();
            WorkspaceJson.Load(j, TestSupport.Read("Tests", "EditMode", "Fixtures", "hello.json"));
            var x = TestSupport.NewWorkspace();
            WorkspaceXml.Load(x, TestSupport.Read("Tests", "EditMode", "Fixtures", "hello.xml"));
            Assert.AreEqual(WorkspaceJson.Save(j), WorkspaceJson.Save(x));
        }
    }

    public class ModelTests
    {
        [Test]
        public void MessageWithTrailingTextGetsOwnDummyInput()
        {
            var ws = TestSupport.NewWorkspace();
            var b = ws.CreateBlock("controls_repeat_ext");
            // "repeat [TIMES] times" row, then "do [DO]" row
            Assert.AreEqual(new[] { "TIMES", null, "DO" }, b.Inputs.Select(i => i.Name).ToArray());
            Assert.AreEqual("times", b.Inputs[1].Fields[0].Value);
            Assert.AreEqual("do", b.Inputs[2].Fields[0].Value);
        }

        [Test]
        public void ConnectionsCheckTypesAndPreventCycles()
        {
            var ws = TestSupport.NewWorkspace();
            var add = ws.CreateBlock("math_arithmetic");
            var num = ws.CreateBlock("math_number");
            var text = ws.CreateBlock("text");
            Assert.IsTrue(add.GetInput("A").Connection.CheckCompatible(num.Output));
            Assert.IsFalse(add.GetInput("A").Connection.CheckCompatible(text.Output)); // String into Number
            add.GetInput("A").Connection.Connect(num.Output);
            Assert.AreSame(add, num.Parent);

            // cycle: put an ancestor inside its own descendant
            var outer = ws.CreateBlock("math_arithmetic");
            var inner = ws.CreateBlock("math_arithmetic");
            outer.GetInput("A").Connection.Connect(inner.Output);
            Assert.Throws<System.InvalidOperationException>(() => inner.GetInput("A").Connection.Connect(outer.Output));
            Assert.Throws<System.InvalidOperationException>(() => outer.GetInput("B").Connection.Connect(outer.Output)); // itself
        }

        [Test]
        public void HueColourMatchesBlockly()
        {
            Assert.AreEqual("#5b67a5", Colour.FromHue(230));
            Assert.AreEqual("#5ba55b", Colour.FromHue(120));
        }
    }
}
