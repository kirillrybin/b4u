using UnityEngine;

namespace Blockly.UI
{
    /// <summary>Loads the built-in block definitions shipped in Resources/Blockly/StandardBlocks.json.</summary>
    public static class StandardBlockSet
    {
        public const string ResourcePath = "Blockly/StandardBlocks";

        public static BlockRegistry CreateRegistry()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new System.IO.FileNotFoundException("Missing Resources/" + ResourcePath + ".json");
            var registry = new BlockRegistry();
            registry.RegisterJson(asset.text);
            return registry;
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
