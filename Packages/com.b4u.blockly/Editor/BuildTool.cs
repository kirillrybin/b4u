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
    /// -buildTarget WebGL -buildOut &lt;dir&gt;</c> (add <c>-iosSimulator</c> to <c>-buildTarget iOS</c> to build for Xcode's simulator). Builds the scenes listed in the Build Settings.
    /// Only targets whose module is installed in the editor can be built.
    /// </summary>
    public static class BuildTool
    {
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string target = Arg(args, "-buildTarget") ?? "WebGL";
            string output = Arg(args, "-buildOut") ?? throw new ArgumentException("-buildOut <dir> is required");
            // Unity itself only accepts its own platform names after -buildTarget, so the simulator is asked for with a flag.
            if (Array.IndexOf(args, "-iosSimulator") >= 0 && string.Equals(target, "ios", StringComparison.OrdinalIgnoreCase)) target = "iOSSimulator";
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
            // Both ABIs: some current phones (e.g. the Galaxy A13) still run a 32-bit-only Android userland and cannot install an ARM64-only APK.
            // "iOSSimulator" builds the project against the simulator SDK (it runs in Xcode's simulator, not on a device).
            if (target == BuildTarget.iOS)
            {
                bool simulator = string.Equals(targetName, "iossimulator", StringComparison.OrdinalIgnoreCase);
                PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                // the simulator runs the code of the Mac's own architecture
                if (simulator)
                    PlayerSettings.iOS.simulatorSdkArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                        ? AppleMobileArchitectureSimulator.ARM64 : AppleMobileArchitectureSimulator.X86_64;
            }
            if (target == BuildTarget.Android) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            // The Android build is landscape only (both directions, never portrait).
            if (target == BuildTarget.Android)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            }
            if (target == BuildTarget.WebGL)
            {
                // A phone's browser tab has little memory and has to compile the whole wasm module: build for size, strip what is unused.
                SetWebGlCodeOptimizationToDiskSize();
                PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
                // Static hosts such as GitHub Pages serve the precompressed .gz files without a Content-Encoding header; the loader then unpacks them itself.
                PlayerSettings.WebGL.decompressionFallback = true;
                // A page where the canvas fills the browser window (Assets/WebGLTemplates/Fullscreen), when the project has it
                if (File.Exists("Assets/WebGLTemplates/Fullscreen/index.html")) PlayerSettings.WebGL.template = "PROJECT:Fullscreen";
            }

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

        /// <summary>
        /// UnityEditor.WebGL exists only where the WebGL build module is installed, and this assembly is compiled by every project that
        /// uses the package. So the setting is reached by name: a project without the module compiles, and a WebGL build (which needs
        /// the module anyway) still gets the size optimisation.
        /// </summary>
        static void SetWebGlCodeOptimizationToDiskSize()
        {
            const string assembly = "UnityEditor.WebGL.Extensions";
            var settings = Type.GetType("UnityEditor.WebGL.UserBuildSettings, " + assembly);
            var optimization = Type.GetType("UnityEditor.WebGL.WasmCodeOptimization, " + assembly);
            var property = settings?.GetProperty("codeOptimization");
            if (property == null || optimization == null)
            {
                Debug.LogWarning("[Build] The WebGL build module is not installed; skipping the code size setting.");
                return;
            }
            property.SetValue(null, Enum.Parse(optimization, "DiskSize"));
        }

        static (BuildTarget, BuildTargetGroup, string) Resolve(string name, string output)
        {
            switch (name.ToLowerInvariant())
            {
                case "webgl": return (BuildTarget.WebGL, BuildTargetGroup.WebGL, output);
                case "android": return (BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(output, "BlocklyPlayground.apk"));
                case "ios":
                case "iossimulator": return (BuildTarget.iOS, BuildTargetGroup.iOS, output);
                case "windows":
                case "standalonewindows64": return (BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(output, "BlocklyPlayground.exe"));
                case "macos":
                case "standaloneosx": return (BuildTarget.StandaloneOSX, BuildTargetGroup.Standalone, Path.Combine(output, "BlocklyPlayground.app"));
                default: throw new ArgumentException("Unknown -buildTarget '" + name + "' (WebGL, Android, iOS, iOSSimulator, Windows, macOS)");
            }
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
