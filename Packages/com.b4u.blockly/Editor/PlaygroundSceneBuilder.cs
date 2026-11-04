using System.IO;
using System.Linq;
using Blockly.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.EditorTools
{
    /// <summary>
    /// Creates Assets/Playground/Playground.unity: toolbar (program switcher, Run / Step / Stop / Slow),
    /// a scrollable workspace and an output console.
    /// </summary>
    public static class PlaygroundSceneBuilder
    {
        const string Root = "Assets/Playground";
        const float ToolbarHeight = 48f, ConsoleHeight = 190f, StatusHeight = 24f;

        static readonly Color Dark = new Color(0.17f, 0.17f, 0.2f);
        static readonly Color ButtonColour = new Color(0.33f, 0.36f, 0.48f);

        [MenuItem("Blockly/Create Playground Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Root + "/Programs");
            string fixtures = Path.GetFullPath("Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs");
            foreach (var f in Directory.GetFiles(fixtures, "*.json").OrderBy(x => x))
                File.Copy(f, Path.Combine(Root, "Programs", Path.GetFileName(f)), true);
            AssetDatabase.Refresh();
            var programs = Directory.GetFiles(Root + "/Programs", "*.json").OrderBy(x => x)
                .Select(p => AssetDatabase.LoadAssetAtPath<TextAsset>(p.Replace('\\', '/'))).ToArray();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = (RectTransform)canvasGo.transform;

            // --- workspace (pan with drag / wheel) ---
            var scroll = Stretch("Scroll", canvas, 0, ConsoleHeight, 0, ToolbarHeight);
            scroll.gameObject.AddComponent<Image>().color = new Color(0.97f, 0.97f, 0.97f);
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            var viewport = Stretch("Viewport", scroll, 0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Anchored("Workspace", viewport, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(100, 100));
            var view = content.gameObject.AddComponent<WorkspaceView>();
            scrollRect.viewport = viewport; scrollRect.content = content;
            scrollRect.horizontal = scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // --- console ---
            var panel = Anchored("Console", canvas, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, ConsoleHeight));
            panel.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f);
            var status = Label("Status", panel, 14, 18, TextAlignmentOptions.MidlineLeft);
            status.rectTransform.anchorMin = new Vector2(0, 1); status.rectTransform.anchorMax = new Vector2(1, 1);
            status.rectTransform.pivot = new Vector2(0, 1);
            status.rectTransform.anchoredPosition = new Vector2(10, -2);
            status.rectTransform.sizeDelta = new Vector2(-20, StatusHeight);

            var outScroll = Stretch("Output", panel, 0, 0, 0, StatusHeight + 4);
            var outRect = outScroll.gameObject.AddComponent<ScrollRect>();
            var outViewport = Stretch("Viewport", outScroll, 8, 4, 8, 0);
            outViewport.gameObject.AddComponent<RectMask2D>();
            var outText = Label("Text", outViewport, 14, 18, TextAlignmentOptions.TopLeft);
            outText.color = new Color(0.9f, 0.9f, 0.9f);
            outText.richText = false;
            outText.textWrappingMode = TextWrappingModes.Normal;     // long lines wrap instead of being cut off
            var textRt = outText.rectTransform;
            textRt.anchorMin = new Vector2(0, 1); textRt.anchorMax = new Vector2(1, 1); textRt.pivot = new Vector2(0, 1);
            textRt.anchoredPosition = Vector2.zero; textRt.sizeDelta = new Vector2(0, 20);
            var fitter = textRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            outRect.viewport = outViewport; outRect.content = textRt;
            outRect.horizontal = false; outRect.vertical = true;
            outRect.movementType = ScrollRect.MovementType.Clamped; outRect.scrollSensitivity = 20f;

            var consoleView = panel.gameObject.AddComponent<ConsoleView>();
            consoleView.Configure(outText, status, outRect);

            // --- toolbar ---
            var bar = Anchored("Toolbar", canvas, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, ToolbarHeight));
            bar.gameObject.AddComponent<Image>().color = Dark;
            var prev = Button("Prev", bar, "<", 8, 56);
            var next = Button("Next", bar, ">", 70, 56);
            var run = Button("Run", bar, "Run", 150, 80);
            var step = Button("Step", bar, "Step", 236, 80);
            var stop = Button("Stop", bar, "Stop", 322, 80);
            var slow = Button("Slow", bar, "Slow: off", 408, 120);
            var title = Label("Title", bar, 18, 36, TextAlignmentOptions.MidlineLeft);
            Place(title.rectTransform, 546, 6, 700, 36);

            var controller = new GameObject("Playground").AddComponent<PlaygroundController>();
            controller.Configure(programs, view, consoleView, title, slow.GetComponentInChildren<TMP_Text>());
            Wire(prev, controller.Previous); Wire(next, controller.Next);
            Wire(run, controller.Run); Wire(step, controller.Step);
            Wire(stop, controller.Stop); Wire(slow, controller.ToggleSlow);

            Directory.CreateDirectory(Root);
            string scenePath = Root + "/Playground.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            Debug.Log("Created " + scenePath + " with " + programs.Length + " programs");
        }

        public static void CreateSceneAndExit()
        {
            try { CreateScene(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }

        // ---------------------------------------------------------------- tiny UI helpers
        static void Wire(Button b, UnityAction action) => UnityEventTools.AddPersistentListener(b.onClick, action);

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Fills the parent, inset by the given margins.</summary>
        static RectTransform Stretch(string name, Transform parent, float left, float bottom, float right, float top)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        static RectTransform Anchored(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Top-left anchored placement inside a toolbar.</summary>
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
        }

        static Button Button(string name, RectTransform parent, string text, float x, float width)
        {
            var rt = NewRect(name, parent);
            Place(rt, x, 6, width, 36);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = ButtonColour;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var l = Label("Text", rt, 20, 36, TextAlignmentOptions.Center);
            l.rectTransform.anchorMin = Vector2.zero; l.rectTransform.anchorMax = Vector2.one;
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
            l.text = text;
            return b;
        }

        static TMP_Text Label(string name, Transform parent, float fontSize, float height, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(100, height);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize; t.color = Color.white; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
