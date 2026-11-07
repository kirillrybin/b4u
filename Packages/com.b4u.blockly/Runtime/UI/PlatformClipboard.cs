using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Reads text from the system clipboard. Everywhere but WebGL that is immediate. In a browser the clipboard is only handed
    /// out asynchronously, and only during a user action, so call <see cref="Read"/> straight from the click that asked for it.
    /// </summary>
    public static class PlatformClipboard
    {
        /// <summary>Calls <paramref name="done"/> with the clipboard text (null when it could not be read), now or a little later.</summary>
        public static void Read(Action<string> done)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ClipboardReceiver.Request(done);
#else
            done(GUIUtility.systemCopyBuffer);
#endif
        }

        /// <summary>Puts text on the system clipboard. In a browser call it straight from the click that asked for it.</summary>
        public static void Write(string text)
        {
            GUIUtility.systemCopyBuffer = text;
#if UNITY_WEBGL && !UNITY_EDITOR
            Blockly_WriteClipboard(text);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Blockly_WriteClipboard(string text);
        [DllImport("__Internal")] static extern void Blockly_ReadClipboard(string receiver, string onText, string onError);
#endif

        /// <summary>The target of the browser's answer (SendMessage finds it by name).</summary>
        sealed class ClipboardReceiver : MonoBehaviour
        {
            public const string ObjectName = "BlocklyClipboard";
            static ClipboardReceiver instance;
            Action<string> pending;

            internal static void Request(Action<string> done)
            {
                if (instance == null)
                {
                    var go = new GameObject(ObjectName);
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<ClipboardReceiver>();
                }
                instance.pending = done;
#if UNITY_WEBGL && !UNITY_EDITOR
                Blockly_ReadClipboard(ObjectName, nameof(OnText), nameof(OnError));
#endif
            }

            void Finish(string text) { var cb = pending; pending = null; cb?.Invoke(text); }
            void OnText(string text) => Finish(text);
            void OnError(string reason) { Debug.Log("[Blockly] clipboard not available: " + reason); Finish(null); }
        }
    }
}
