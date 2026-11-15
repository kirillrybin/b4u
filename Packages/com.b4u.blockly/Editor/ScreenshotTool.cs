using System;
using System.IO;
using System.Linq;
using Blockly.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.EditorTools
{
    /// <summary>
    /// Renders a Blockly program with the real UGUI view into a PNG, so layout changes can be checked visually
    /// without entering play mode. Needs a graphics device (do NOT pass -nographics).
    /// Usage: Unity -batchmode -projectPath . -executeMethod Blockly.EditorTools.ScreenshotTool.CaptureFromCommandLine
    ///        -blocklyProgram file.json -blocklyOut out.png [-blocklyWidth 900 -blocklyHeight 600 -blocklyScale 1.5]
    /// </summary>
    public static class ScreenshotTool
    {
        /// <summary>
        /// Does what the project's own startup code would do when the game starts: the <c>ApplyBlockTheme</c> component of the scene (registers its theme) and the
        /// <c>PurpleIcons</c> installer (registers the category pictures). Found by name, because the package does not reference the project's scripts; absent ones are skipped.
        /// </summary>
        static void RunEditModeInit()
        {
            const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var installer = asm.GetType("PurpleIcons");
                installer?.GetMethod("Install", any)?.Invoke(null, null);
            }
            foreach (var m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (m != null && m.GetType().Name == "ApplyBlockTheme") m.GetType().GetMethod("OnEnable", any)?.Invoke(m, null);
        }

        static string Arg(string name, string def = null)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                string program = Arg("-blocklyProgram") ?? throw new ArgumentException("-blocklyProgram is required");
                string output = Arg("-blocklyOut") ?? throw new ArgumentException("-blocklyOut is required");
                Capture(program, output, int.Parse(Arg("-blocklyWidth", "900")), int.Parse(Arg("-blocklyHeight", "600")), float.Parse(Arg("-blocklyScale", "1.5"), System.Globalization.CultureInfo.InvariantCulture));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("Screenshot failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Renders the whole saved Playground scene (toolbar, workspace, console) after running some commands.
        /// Args: -blocklyPlayground name  -blocklyOut out.png  [-blocklySteps n | -blocklyRun 1]  [-blocklyWidth/-Height]
        /// </summary>
        public static void CapturePlaygroundFromCommandLine()
        {
            try
            {
                string name = Arg("-blocklyPlayground") ?? throw new ArgumentException("-blocklyPlayground is required");
                string output = Arg("-blocklyOut") ?? throw new ArgumentException("-blocklyOut is required");
                int w = int.Parse(Arg("-blocklyWidth", "1280")), h = int.Parse(Arg("-blocklyHeight", "720"));
                EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", OpenSceneMode.Single);
                var controller = UnityEngine.Object.FindFirstObjectByType<PlaygroundController>();
                if (!controller.ShowByName(name)) throw new ArgumentException("no such program: " + name);
                if (Arg("-blocklySlow") == "1") controller.ToggleSlow();
                if (Arg("-blocklyCode") == "1") controller.SetCodeVisible(true);
                if (Arg("-blocklyCodeLanguage") != null) { controller.SetCodeVisible(true); controller.CodePanel.PickLanguage(Arg("-blocklyCodeLanguage")); }
                if (Arg("-blocklyTheme") != null) 
                {
                    BuiltInThemes.Register();
                    RunEditModeInit();          // the themes and pictures of the project (Assets) register themselves when the game starts, which edit mode never does
                    BlockThemeCatalog.Select(Arg("-blocklyTheme"));
                    foreach (var skin in UnityEngine.Object.FindObjectsByType<PlaygroundSkin>(FindObjectsSortMode.None)) skin.Apply();     // edit mode does not run OnEnable
                    controller.View.UpdateGrid();
                    Canvas.ForceUpdateCanvases();
                }
                if (Arg("-blocklySearch") != null) controller.Toolbox.Search(Arg("-blocklySearch"));
                // -blocklyComment "text": puts an open comment on the first block
                if (Arg("-blocklyComment") != null && controller.View.Workspace.TopBlocks.Count > 0)
                {
                    var first = controller.View.Workspace.TopBlocks[0];
                    controller.View.AddComment(first);
                    first.Comment = Arg("-blocklyComment");
                    controller.View.Rebuild();
                    Canvas.ForceUpdateCanvases();
                }
                // -blocklyMutator 1: opens the mutator bubble of the first block that has one (and adds an else if to an if, so the rows show)
                if (Arg("-blocklyMutator") == "1")
                {
                    var mutable = controller.View.Workspace.AllBlocks().FirstOrDefault(Blockly.Editing.MutatorEditing.CanMutate);
                    if (mutable != null)
                    {
                        controller.View.ToggleMutatorBubble(mutable);
                        if (Blockly.Editing.MutatorEditing.KindOf(mutable) == Blockly.Editing.MutatorEditing.Kind.If) controller.View.MutatorAdd(mutable, Blockly.Editing.PartKind.ElseIf);
                        Canvas.ForceUpdateCanvases();
                    }
                }
                // -blocklyNote "text": puts a note on the workspace (-blocklyNoteCollapsed 1 collapses it)
                if (Arg("-blocklyNote") != null)
                {
                    var note = controller.View.AddNote(new Vector2(420, 40));
                    if (note != null)
                    {
                        note.Text = Arg("-blocklyNote");
                        note.Collapsed = Arg("-blocklyNoteCollapsed") == "1";
                        controller.View.Rebuild();
                        Canvas.ForceUpdateCanvases();
                    }
                }
                // -blocklySelect all: selects every stack, so the outlines show
                if (Arg("-blocklySelect") == "all") { controller.View.SelectAll(); Canvas.ForceUpdateCanvases(); }
                if (Arg("-blocklyFlyout") != null) controller.Toolbox.SelectCategory(int.Parse(Arg("-blocklyFlyout")));

                int steps = int.Parse(Arg("-blocklySteps", "0"));
                for (int i = 0; i < steps; i++) controller.Step();
                if (Arg("-blocklyRun") == "1")
                {
                    controller.Run();
                    for (int i = 0; i < 100000 && controller.Runner.State == RunnerState.Running; i++) controller.Tick(0.016f);
                }

                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                var cam = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
                cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam; canvas.planeDistance = 1f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;   // Screen size is meaningless in batch mode
                scaler.scaleFactor = h / 720f;
                Render(cam, canvas, output, w, h);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("Playground screenshot failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Render(Camera cam, Canvas canvas, string outputPng, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            foreach (var t in canvas.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng)));
            File.WriteAllBytes(outputPng, tex.EncodeToPNG());
            Debug.Log("Screenshot: " + outputPng);
            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// -blocklyDrag "text:text_print.TEXT"  grabs the first block of type 'text' and holds it over the TEXT input of the
        /// first 'text_print' block (offset a few units), so the snap marker is visible. Add -blocklyDrop 1 to release it.
        /// </summary>
        static void HoldDragIfRequested(WorkspaceView view, Workspace ws)
        {
            string spec = Arg("-blocklyDrag");
            if (spec == null) return;
            var parts = spec.Split(':');
            string[] target = parts[1].Split('.');
            var dragged = Nth(ws, parts[0]);
            var targetBlock = Nth(ws, target[0]);
            var box = view.Layout.ByBlock[dragged];
            var grab = new Vector2(box.X + 5, box.Y + 5);
            if (!view.BeginDrag(dragged, grab)) throw new InvalidOperationException("cannot drag " + parts[0]);

            var targetConn = target.Length > 1 ? targetBlock.GetInput(target[1]).Connection : targetBlock.Next;
            var tp = view.Layout.ByBlock[targetBlock].Connections[targetConn];
            var head = dragged.Output ?? dragged.Previous;
            var mp = view.Layout.ByBlock[dragged].Connections[head];
            float jitter = float.Parse(Arg("-blocklyJitter", "8"), System.Globalization.CultureInfo.InvariantCulture);
            view.DragTo(grab + new Vector2(tp.X - mp.X + jitter, tp.Y - mp.Y + jitter));
            if (Arg("-blocklyDrop") == "1") view.EndDrag();
        }

        /// <summary>-blocklyEdit "text@0:TEXT=some typed text" opens that field's editor with the given text typed in.</summary>
        static void OpenFieldEditorIfRequested(WorkspaceView view, Workspace ws)
        {
            string spec = Arg("-blocklyEdit");
            if (spec == null) return;
            var eq = spec.IndexOf('=');
            string typed = eq >= 0 ? spec.Substring(eq + 1) : null;
            var head = (eq >= 0 ? spec.Substring(0, eq) : spec).Split(':');
            var block = Nth(ws, head[0]);
            var field = block.GetField(head[1]) ?? throw new InvalidOperationException("no field " + head[1]);
            if (!view.BeginFieldEdit(block, field)) throw new InvalidOperationException("cannot edit " + spec);
            if (typed != null) view.SetFieldEditorText(typed);
        }

        /// <summary>-blocklyMenu "math_arithmetic:OP" opens that field's dropdown; -blocklyDialog create | rename:&lt;id&gt; | delete:&lt;id&gt; opens a variable dialog.</summary>
        static void OpenMenuOrDialogIfRequested(WorkspaceView view, Workspace ws)
        {
            string menu = Arg("-blocklyMenu");
            if (menu != null)
            {
                var p = menu.Split(':');
                var block = Nth(ws, p[0]);
                if (!view.ShowDropdown(block, block.GetField(p[1]))) throw new InvalidOperationException("cannot open menu " + menu);
            }
            string dialog = Arg("-blocklyDialog");
            if (dialog == null) return;
            var d = dialog.Split(':');
            if (d[0] == "create") view.RequestCreateVariable();
            else if (d[0] == "rename") view.RequestRenameVariable(d[1]);
            else if (d[0] == "delete") view.RequestDeleteVariable(d[1]);
            var typed = Arg("-blocklyDialogText");
            if (typed != null && view.ActiveDialog != null && view.ActiveDialog.IsPrompt) view.ActiveDialog.Input.SetTextWithoutNotify(typed);
            if (Arg("-blocklyDialogSubmit") == "1" && view.ActiveDialog != null) view.ActiveDialog.Submit(typed ?? "");     // shows validation errors
        }

        /// <summary>"type" or "type@2": the n-th (0-based) block of that type in workspace order.</summary>
        static Block Nth(Workspace ws, string spec)
        {
            var bits = spec.Split('@');
            int n = bits.Length > 1 ? int.Parse(bits[1]) : 0;
            return ws.AllBlocks().Where(b => b.Type == bits[0]).ElementAt(n);
        }

        public static void Capture(string programPath, string outputPng, int width, int height, float scale)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Camera", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.97f, 0.97f, 0.97f);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;

            var viewGo = new GameObject("Workspace", typeof(RectTransform), typeof(WorkspaceView));
            viewGo.transform.SetParent(canvasGo.transform, false);
            var vrt = (RectTransform)viewGo.transform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(0, 1);
            vrt.pivot = new Vector2(0, 1);
            vrt.anchoredPosition = new Vector2(16, -16);

            var ws = StandardBlockSet.Load(File.ReadAllText(programPath));
            var view = viewGo.GetComponent<WorkspaceView>();
            view.SetWorkspace(ws);
            if (Arg("-blocklyCleanup", "1") == "1")        // programs without coordinates would all pile up at one point
            {
                view.CleanUp();
            }
            HoldDragIfRequested(view, ws);
            OpenFieldEditorIfRequested(view, ws);
            OpenMenuOrDialogIfRequested(view, ws);

            Canvas.ForceUpdateCanvases();
            foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPng)));
            File.WriteAllBytes(outputPng, tex.EncodeToPNG());
            Debug.Log($"Screenshot: {outputPng} ({view.Layout.Boxes.Count} blocks, workspace {view.Layout.Right:0}x{view.Layout.Bottom:0})");

            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
