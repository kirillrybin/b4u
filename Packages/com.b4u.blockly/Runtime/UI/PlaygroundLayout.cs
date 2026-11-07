using System.Collections.Generic;
using Blockly.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// Makes the Playground fit the screen it is on: the content stays inside the safe area (notch, rounded corners), phones get a
    /// scale from the screen density so buttons are big enough for a finger, the toolbar wraps onto more rows when it does not fit
    /// on one, and the console and flyout shrink on small screens. On a desktop window nothing changes.
    /// </summary>
    public sealed class PlaygroundLayout : MonoBehaviour
    {
        [SerializeField] CanvasScaler scaler;
        [SerializeField] RectTransform safeArea;
        [SerializeField] RectTransform toolbar;
        [SerializeField] RectTransform[] toolbarItems;
        [SerializeField] RectTransform title;
        [SerializeField] RectTransform workspace;
        [SerializeField] RectTransform toolbox;
        [SerializeField] RectTransform flyout;
        [SerializeField] RectTransform console;

        public const float RowHeight = 48f, RowPadding = 6f, ItemGap = 8f, ItemHeight = 36f, SideMargin = 8f;
        public const float ToolboxWidth = 130f, FlyoutWidth = 360f, FlyoutMargin = 24f, ConsoleHeight = 190f, MinWorkspaceHeight = 220f;
        const float DpPerInch = 160f;
        const float MinTitleWidth = 140f;

        // the layout of the scene as it was built (a wide window): used as is while it fits
        float[] originalX, widths;
        float titleX, titleWidth;

        Vector2Int lastScreen;
        Rect lastSafe;

        public int ToolbarRows { get; private set; } = 1;
        public float ToolbarHeight { get; private set; } = RowHeight;
        public float EffectiveConsoleHeight { get; private set; } = ConsoleHeight;

        public void Configure(CanvasScaler canvasScaler, RectTransform safe, RectTransform toolbarRect, RectTransform[] items, RectTransform titleRect,
                              RectTransform workspaceRect, RectTransform toolboxRect, RectTransform flyoutRect, RectTransform consoleRect)
        {
            scaler = canvasScaler; safeArea = safe; toolbar = toolbarRect; toolbarItems = items; title = titleRect;
            workspace = workspaceRect; toolbox = toolboxRect; flyout = flyoutRect; console = consoleRect;
        }

        void Awake() => Capture();

        void Capture()
        {
            if (originalX != null || toolbarItems == null) return;
            originalX = new float[toolbarItems.Length];
            widths = new float[toolbarItems.Length];
            for (int i = 0; i < toolbarItems.Length; i++) { originalX[i] = toolbarItems[i].anchoredPosition.x; widths[i] = toolbarItems[i].sizeDelta.x; }
            if (title != null) { titleX = title.anchoredPosition.x; titleWidth = title.sizeDelta.x; }
        }

        void Start() => Refresh(force: true);

        void Update() => Refresh(force: false);

        void Refresh(bool force)
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (!force && screen == lastScreen && safe == lastSafe) return;
            lastScreen = screen; lastSafe = safe;
            ApplyScale();
            ApplySafeArea(safe, screen);
            if (safeArea != null) { Canvas.ForceUpdateCanvases(); Apply(safeArea.rect.width, safeArea.rect.height); }
        }

        /// <summary>Phones: one unit is about one density-independent pixel. Elsewhere the window is scaled to a 1280 x 720 design.</summary>
        void ApplyScale()
        {
            if (scaler == null) return;
            if (Application.isMobilePlatform)
            {
                float dpi = Screen.dpi > 0 ? Screen.dpi : 320f;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Clamp(dpi / DpPerInch, 1f, 4f);
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        /// <summary>Keeps the content inside <paramref name="safe"/> (pixels of a screen of the given size).</summary>
        public void ApplySafeArea(Rect safe, Vector2Int screen)
        {
            if (safeArea == null || screen.x <= 0 || screen.y <= 0) return;
            safeArea.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            safeArea.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Lays the toolbar and the panels out for a safe area of the given size in canvas units.</summary>
        public void Apply(float width, float height)
        {
            Capture();
            if (toolbar == null || toolbarItems == null || width <= 0 || height <= 0) return;

            // toolbar: the design as built if it fits, otherwise rows
            float designRight = titleX + titleWidth + SideMargin;
            if (designRight <= width) PlaceDesign();
            else PlaceRows(width);
            toolbar.sizeDelta = new Vector2(0, ToolbarHeight);

            // panels
            float consoleHeight = Mathf.Clamp(height - ToolbarHeight - MinWorkspaceHeight, 70f, ConsoleHeight);
            EffectiveConsoleHeight = consoleHeight;
            float flyoutWidth = Mathf.Min(FlyoutWidth, width - ToolboxWidth - FlyoutMargin);
            SetPanel(workspace, ToolboxWidth, consoleHeight, 0, ToolbarHeight);
            if (toolbox != null) { toolbox.offsetMin = new Vector2(0, consoleHeight); toolbox.offsetMax = new Vector2(ToolboxWidth, -ToolbarHeight); }
            if (flyout != null) { flyout.offsetMin = new Vector2(ToolboxWidth, consoleHeight); flyout.offsetMax = new Vector2(ToolboxWidth + Mathf.Max(120f, flyoutWidth), -ToolbarHeight); }
            if (console != null) console.sizeDelta = new Vector2(0, consoleHeight);
        }

        static void SetPanel(RectTransform rt, float left, float bottom, float right, float top)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        void PlaceDesign()
        {
            ToolbarRows = 1; ToolbarHeight = RowHeight;
            for (int i = 0; i < toolbarItems.Length; i++) Put(toolbarItems[i], originalX[i], 0, widths[i]);
            if (title != null) Put(title, titleX, 0, titleWidth);
        }

        void PlaceRows(float width)
        {
            float available = width - 2 * SideMargin;
            var slots = RowFlow.Arrange(widths, available, ItemGap, SideMargin, out int rows);
            for (int i = 0; i < toolbarItems.Length; i++) Put(toolbarItems[i], slots[i].X, slots[i].Row, widths[i]);
            if (title != null)
            {
                float end = RowFlow.EndOfLastRow(widths, slots, ItemGap, SideMargin);
                float room = width - SideMargin - end;
                if (room >= MinTitleWidth) Put(title, end, rows - 1, room);
                else { Put(title, SideMargin, rows, available); rows++; }
            }
            ToolbarRows = rows;
            ToolbarHeight = rows * RowHeight + (rows > 1 ? RowPadding : 0);
        }

        static void Put(RectTransform rt, float x, int row, float w)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -(RowPadding + row * RowHeight));
            rt.sizeDelta = new Vector2(w, ItemHeight);
        }
    }
}
