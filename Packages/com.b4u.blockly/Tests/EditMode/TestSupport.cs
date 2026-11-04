using System;
using System.IO;

namespace Blockly.Tests
{
    static class TestSupport
    {
        public static string PackageRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                {
                    var p = Path.Combine(d.FullName, "Packages", "com.b4u.blockly");
                    if (Directory.Exists(p)) return p;
                }
            throw new DirectoryNotFoundException("Cannot locate Packages/com.b4u.blockly");
        }

        public static string Read(params string[] rel) => File.ReadAllText(Path.Combine(PackageRoot(), Path.Combine(rel)));

        public static Blockly.Editing.ToolboxDefinition NewToolbox()
            => Blockly.Editing.ToolboxDefinition.FromJson(Read("Runtime", "Resources", "Blockly", "StandardToolbox.json"));

        public static Workspace NewWorkspace()
        {
            var reg = new BlockRegistry();
            reg.RegisterJson(Read("Runtime", "Resources", "Blockly", "StandardBlocks.json"));
            return new Workspace(reg);
        }
    }
}
