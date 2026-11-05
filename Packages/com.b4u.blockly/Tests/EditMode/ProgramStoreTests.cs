using System.IO;
using System.Linq;
using Blockly.Storage;
using NUnit.Framework;

namespace Blockly.Tests
{
    public class ProgramStoreTests
    {
        string dir;

        [SetUp]
        public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "b4u-store-" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static System.Collections.Generic.IEnumerable<IProgramStore> Stores(string dir)
        {
            yield return new MemoryProgramStore();
            yield return new FileProgramStore(dir);
        }

        [TestCase("hello", "hello")]
        [TestCase("  a   b  ", "a b")]
        [TestCase("a/b\\c", "a_b_c")]
        [TestCase("../etc/passwd", "_etc_passwd")]
        [TestCase("..", "untitled")]
        [TestCase("", "untitled")]
        [TestCase("   ", "untitled")]
        [TestCase("name.", "name")]
        [TestCase("q?*<>|\":x", "q_______x")]
        public void NamesAreMadeSafe(string typed, string expected) => Assert.AreEqual(expected, ProgramNames.Sanitize(typed));

        [Test]
        public void ALongNameIsCappedAndNeverEndsWithADot()
        {
            string name = ProgramNames.Sanitize(new string('x', 79) + ".." + new string('y', 50));
            Assert.LessOrEqual(name.Length, ProgramNames.MaxLength);
            Assert.IsFalse(name.EndsWith("."));
            Assert.AreEqual("untitled", ProgramNames.Sanitize(null));
        }

        [Test]
        public void ValidatingTypedNames()
        {
            Assert.IsNotNull(ProgramNames.Validate(""));
            Assert.IsNotNull(ProgramNames.Validate("  "));
            Assert.IsNull(ProgramNames.Validate("my program"));
        }

        [Test]
        public void SaveLoadListAndDeleteWorkTheSameInEveryStore()
        {
            foreach (var store in Stores(dir))
            {
                string kind = store.GetType().Name;
                Assert.AreEqual(0, store.List().Count, kind);
                Assert.IsNull(store.Load("nope"), kind);
                Assert.IsFalse(store.Exists("nope"), kind);

                Assert.AreEqual("b prog", store.Save("b prog", "{\"x\":1}"), kind);
                store.Save("A prog", "{}");
                CollectionAssert.AreEqual(new[] { "A prog", "b prog" }, store.List().ToArray(), kind + ": sorted ignoring case");
                Assert.AreEqual("{\"x\":1}", store.Load("b prog"), kind);
                Assert.IsTrue(store.Exists("b prog"), kind);

                store.Save("b prog", "{\"x\":2}");
                Assert.AreEqual("{\"x\":2}", store.Load("b prog"), kind + ": overwritten");
                Assert.AreEqual(2, store.List().Count, kind);

                Assert.IsTrue(store.Delete("b prog"), kind);
                Assert.IsFalse(store.Delete("b prog"), kind);
                CollectionAssert.AreEqual(new[] { "A prog" }, store.List().ToArray(), kind);
            }
        }

        [Test]
        public void AnUnsafeNameCannotEscapeTheFolder()
        {
            var store = new FileProgramStore(dir);
            string used = store.Save("../../escape", "{}");
            Assert.AreEqual(Path.GetFullPath(dir), Path.GetDirectoryName(Path.GetFullPath(Path.Combine(dir, used + ".json"))));
            Assert.AreEqual(1, Directory.GetFiles(dir).Length);
            Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(dir), "escape.json")));
        }

        [Test]
        public void TheFileStoreKeepsUnicodeAndLeavesNoTemporaryFiles()
        {
            var store = new FileProgramStore(dir);
            string text = "{\"name\":\"привет ✓\"}";
            store.Save("программа", text);
            Assert.AreEqual(text, store.Load("программа"));
            store.Save("программа", text + " ");
            CollectionAssert.AreEqual(new[] { "программа.json" }, Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }

        [Test]
        public void AMissingFolderIsAnEmptyList()
        {
            Assert.AreEqual(0, new FileProgramStore(Path.Combine(dir, "does", "not", "exist")).List().Count);
        }
    }
}
