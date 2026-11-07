using System.Collections.Generic;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Loads the built-in block definitions shipped in Resources/Blockly/StandardBlocks.json.</summary>
    public static class StandardBlockSet
    {
        public const string ResourcePath = "Blockly/StandardBlocks";

        static readonly List<IBlockPackage> packages = new List<IBlockPackage>();

        /// <summary>
        /// Plugs custom blocks into everything built after this call: workspaces get their definitions, the interpreter gets their
        /// handlers and the toolbox gets their categories. Adding the same instance twice is ignored.
        /// </summary>
        public static void AddPackage(IBlockPackage package)
        {
            if (package == null) throw new System.ArgumentNullException(nameof(package));
            if (packages.Contains(package)) return;
            packages.Add(package);
            package.RegisterHandlers(HandlerRegistry.Standard);
        }

        public static bool RemovePackage(IBlockPackage package) => packages.Remove(package);

        public static BlockRegistry CreateRegistry()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing Resources/" + ResourcePath + ".json");
            var registry = new BlockRegistry();
            registry.RegisterJson(asset.text);
            foreach (var p in packages) registry.RegisterJson(p.BlocksJson);
            return registry;
        }

        public const string ToolboxResourcePath = "Blockly/StandardToolbox";

        public static Blockly.Editing.ToolboxDefinition LoadToolbox()
        {
            var asset = Resources.Load<TextAsset>(ToolboxResourcePath);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing Resources/" + ToolboxResourcePath + ".json");
            var toolbox = Blockly.Editing.ToolboxDefinition.FromJson(asset.text);
            foreach (var p in packages)
                if (p.ToolboxCategories != null) toolbox.Categories.AddRange(p.ToolboxCategories);
            return toolbox;
        }

        /// <summary>Creates a workspace from Blockly JSON (starts with '{') or XML (starts with '&lt;').</summary>
        public static Workspace Load(string programText)
        {
            var ws = new Workspace(CreateRegistry());
            string t = programText.TrimStart();
            if (t.StartsWith("<")) WorkspaceXml.Load(ws, programText); else WorkspaceJson.Load(ws, programText);
            return ws;
        }
    }
}
