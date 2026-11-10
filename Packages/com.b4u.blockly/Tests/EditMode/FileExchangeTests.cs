using System;
using System.IO;
using System.Linq;
using Blockly.Storage;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class FileExchangeTests
    {
        string dir;

        [SetUp] public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "blockly-exchange-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        [Test]
        public void ExportWritesANamedJsonFileAndReplacesAnOlderOne()
        {
            var x = new FileExchange(dir);
            string path = x.Export("my program", "{\"a\":1}");
            Assert.AreEqual(Path.Combine(dir, "my program.json"), path);
            Assert.AreEqual("{\"a\":1}", File.ReadAllText(path));
            x.Export("my program", "{\"a\":2}");
            Assert.AreEqual("{\"a\":2}", File.ReadAllText(path));
            Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length, "no temporary file is left behind");
        }

        [Test]
        public void CodeIsExportedWithItsOwnExtensionAndIsNotListedAsAProgram()
        {
            var x = new FileExchange(dir);
            string path = x.Export("a/my code", "print(1)\n", ".py");
            Assert.AreEqual(Path.Combine(dir, "a_my code.py"), path);
            Assert.AreEqual("print(1)\n", File.ReadAllText(path));
            CollectionAssert.IsEmpty(x.List());
        }

        [Test]
        public void ExportMakesTheNameSafe()
        {
            var x = new FileExchange(dir);
            string path = x.Export("../../evil:name", "{}");
            Assert.AreEqual(dir, Path.GetDirectoryName(path), "it stays in the folder");
            Assert.IsFalse(Path.GetFileName(path).Contains(":"));
        }

        [Test]
        public void ListShowsOnlyProgramFilesSortedAndIsEmptyForAMissingFolder()
        {
            var x = new FileExchange(dir);
            Assert.AreEqual(0, x.List().Count);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "b.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "A.xml"), "<xml/>");
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "x");
            File.WriteAllText(Path.Combine(dir, "c.json.tmp"), "x");
            CollectionAssert.AreEqual(new[] { "A.xml", "b.json" }, x.List().ToArray());
        }

        [Test]
        public void ReadReturnsTheTextAndRefusesAnythingButAPlainProgramFileName()
        {
            var x = new FileExchange(dir);
            x.Export("p", "{\"k\":true}");
            Assert.AreEqual("{\"k\":true}", x.Read("p.json"));
            Assert.IsNull(x.Read("missing.json"));
            Assert.Throws<ArgumentException>(() => x.Read("../p.json"));
            Assert.Throws<ArgumentException>(() => x.Read("sub/p.json"));
            Assert.Throws<ArgumentException>(() => x.Read("p.txt"));
            Assert.Throws<ArgumentException>(() => x.Read(""));
            Assert.Throws<ArgumentException>(() => x.Read(null));
        }
    }
}
