using System.Runtime.InteropServices;

namespace Blockly.UI
{
    /// <summary>Platform details of writing files. Only WebGL needs anything: its files live in memory until flushed to the browser's storage.</summary>
    public static class PlatformFiles
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Blockly_SyncFileSystem();
#endif

        /// <summary>Call after writing or deleting a file that must survive a restart.</summary>
        public static void Flush()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Blockly_SyncFileSystem();
#endif
        }
    }
}
