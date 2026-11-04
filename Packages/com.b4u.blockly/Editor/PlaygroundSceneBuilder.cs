using System.IO;
using System.Linq;
using Blockly.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.EditorTools
{
    /// <summary>Creates Assets/Playground/Playground.unity with sample programs, scrolling and Prev/Next buttons.</summary>
    public static class PlaygroundSceneBuilder
    {
        const string Root = "Assets/Playground";

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

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Scroll area (pan with drag / wheel)
            var scroll = Rect("Scroll", canvasGo.transform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, -48));
            var scrollBg = scroll.gameObject.AddComponent<Image>();
            scrollBg.color = new Color(0.97f, 0.97f, 0.97f);
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", scroll, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Workspace", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(100, 100));
            content.pivot = new Vector2(0, 1);
            var view = content.gameObject.AddComponent<WorkspaceView>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            // Toolbar
            var bar = Rect("Toolbar", canvasGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 48));
            bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = new Color(0.17f, 0.17f, 0.2f);
            var prev = Button("Prev", bar, "<", new Vector2(8, -6), new Vector2(60, 36));
            var next = Button("Next", bar, ">", new Vector2(76, -6), new Vector2(60, 36));
            var title = Label("Title", bar, new Vector2(150, -6), new Vector2(900, 36));

            var controller = new GameObject("Playground").AddComponent<PlaygroundController>();
            controller.Configure(programs, view, title);
            UnityEventTools.AddPersistentListener(prev.onClick, controller.Previous);
            UnityEventTools.AddPersistentListener(next.onClick, controller.Next);

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

        static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 offsetMin, Vector2 offsetMaxOrSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            if (aMin == aMax) { rt.sizeDelta = offsetMaxOrSize; rt.anchoredPosition = offsetMin; }
            else if (name == "Toolbar") { rt.sizeDelta = offsetMaxOrSize; }
            else { rt.offsetMin = offsetMin; rt.offsetMax = offsetMaxOrSize; }
            return rt;
        }

        static Button Button(string name, RectTransform parent, string text, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            rt.pivot = new Vector2(0, 1);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.33f, 0.36f, 0.48f);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var l = Label("Text", rt, Vector2.zero, size);
            l.text = text; l.alignment = TextAlignmentOptions.Center; l.fontSize = 22;
            return b;
        }

        static TMP_Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            rt.pivot = new Vector2(0, 1);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = 18; t.color = Color.white; t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
            return t;
        }
    }
}
