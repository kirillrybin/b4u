using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Moving a program as a file through the browser: a download and a file picker. Only WebGL has them; other platforms use
    /// the <see cref="Blockly.Storage.FileExchange"/> folder instead (<see cref="UsesBrowser"/> tells which applies).
    /// </summary>
    public static class PlatformFileTransfer
    {
        public static bool UsesBrowser
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Offers <paramref name="text"/> to the user as a downloaded file.</summary>
        public static void Download(string fileName, string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Blockly_DownloadText(fileName, text);
#endif
        }

        /// <summary>
        /// Lets the user pick a file; calls <paramref name="done"/> with its name and text later. If the file cannot be read
        /// <paramref name="failed"/> gets the reason; picking nothing is not reported.
        /// </summary>
        public static void Pick(Action<string, string> done, Action<string> failed = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FileReceiver.Request(done, failed);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Blockly_DownloadText(string name, string text);
        [DllImport("__Internal")] static extern void Blockly_PickTextFile(string receiver, string onFile, string onError);
#endif

        /// <summary>The target of the browser's answer (SendMessage finds it by name).</summary>
        sealed class FileReceiver : MonoBehaviour
        {
            public const string ObjectName = "BlocklyFiles";
            static FileReceiver instance;
            Action<string, string> pending;
            Action<string> pendingFailed;

            internal static void Request(Action<string, string> done, Action<string> failed)
            {
                if (instance == null)
                {
                    var go = new GameObject(ObjectName);
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<FileReceiver>();
                }
                instance.pending = done; instance.pendingFailed = failed;
#if UNITY_WEBGL && !UNITY_EDITOR
                Blockly_PickTextFile(ObjectName, nameof(OnFile), nameof(OnError));
#endif
            }

            void OnFile(string nameAndText)
            {
                int eol = nameAndText.IndexOf('\n');
                var cb = pending; pending = null;
                if (eol >= 0) cb?.Invoke(nameAndText.Substring(0, eol), nameAndText.Substring(eol + 1));
            }

            void OnError(string reason)
            {
                var failed = pendingFailed; pending = null; pendingFailed = null;
                if (reason == "cancelled") return;
                Debug.Log("[Blockly] file not read: " + reason);
                failed?.Invoke(reason);
            }
        }
    }
}
