using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Blockly.EditorTools
{
    /// <summary>
    /// Player builds from the command line: <c>-executeMethod Blockly.EditorTools.BuildTool.BuildFromCommandLine
    /// -buildTarget WebGL -buildOut &lt;dir&gt;</c>. Builds the scenes listed in the Build Settings.
    /// Only targets whose module is installed in the editor can be built.
    /// </summary>
    public static class BuildTool
    {
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string target = Arg(args, "-buildTarget") ?? "WebGL";
            string output = Arg(args, "-buildOut") ?? throw new ArgumentException("-buildOut <dir> is required");
            EditorApplication.Exit(Build(target, output) ? 0 : 1);
        }

        public static bool Build(string targetName, string output)
        {
            var (target, group, location) = Resolve(targetName, output);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError("[Build] The " + targetName + " module is not installed in this editor.");
                return false;
            }

            // The mobile and web targets ship with IL2CPP (ahead-of-time, with code stripping), so build them that way: a problem the
            // AOT compiler or the stripper would cause shows up here and not on a device. Desktop keeps the project's backend
            // (IL2CPP there needs an extra module).
            if (target == BuildTarget.Android || target == BuildTarget.iOS || target == BuildTarget.WebGL)
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group), ScriptingImplementation.IL2CPP);
            if (target == BuildTarget.Android) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) { Debug.LogError("[Build] No enabled scenes in Build Settings."); return false; }

            Directory.CreateDirectory(Path.GetDirectoryName(location) ?? output);
            var options = new BuildPlayerOptions { scenes = scenes, locationPathName = location, target = target, targetGroup = group, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log("[Build] " + targetName + ": " + summary.result + ", " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings, "
                      + (summary.totalSize / (1024 * 1024)) + " MB, " + summary.totalTime.TotalSeconds.ToString("0") + " s -> " + location);
            return summary.result == BuildResult.Succeeded;
        }

        static (BuildTarget, BuildTargetGroup, string) Resolve(string name, string output)
        {
            switch (name.ToLowerInvariant())
            {
                case "webgl": return (BuildTarget.WebGL, BuildTargetGroup.WebGL, output);
                case "android": return (BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(output, "BlocklyPlayground.apk"));
                case "ios": return (BuildTarget.iOS, BuildTargetGroup.iOS, output);
                case "windows":
                case "standalonewindows64": return (BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(output, "BlocklyPlayground.exe"));
                case "macos":
                case "standaloneosx": return (BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, Path.Combine(output, "BlocklyPlayground.app"));
                default: throw new ArgumentException("Unknown -buildTarget '" + name + "' (WebGL, Android, iOS, Windows, macOS)");
            }
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
