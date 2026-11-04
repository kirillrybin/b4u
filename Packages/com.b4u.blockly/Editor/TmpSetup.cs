using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blockly.EditorTools
{
    /// <summary>TextMeshPro needs its "Essential Resources" (default font + shaders) in the project to draw text.</summary>
    public static class TmpSetup
    {
        /// <summary>Batch-mode entry: imports and exits only after Unity reports the package import finished.</summary>
        public static void ImportEssentialsAndExit()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) { EditorApplication.Exit(0); return; }
            AssetDatabase.importPackageCompleted += _ => { AssetDatabase.SaveAssets(); EditorApplication.Exit(0); };
            AssetDatabase.importPackageFailed += (_, error) => { Debug.LogError("TMP import failed: " + error); EditorApplication.Exit(1); };
            ImportEssentials();
        }

        [MenuItem("Blockly/Setup/Import TMP Essentials")]
        public static void ImportEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) { Debug.Log("TMP essentials already imported."); return; }
            string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg)) throw new FileNotFoundException("TMP Essential Resources not found", pkg);
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log("Imported TMP essentials from " + pkg);
        }
    }
}
