using System;
using System.Collections.Generic;
using Blockly.Editing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// A popup list under a dropdown field. It lives directly under the root canvas with its own sorting, so the
    /// workspace's scroll mask never clips it. Clicking anywhere else closes it.
    /// </summary>
    public sealed class DropdownMenu : MonoBehaviour
    {
        const float ItemHeight = 30f, Padding = 4f, MaxHeight = 320f, FontSize = 15f;
        static Color Back => BlockStyle.MenuBackground;
        static Color Hover => BlockStyle.MenuHover;
        static Color SelectedBack => BlockStyle.MenuSelected;

        readonly List<Button> buttons = new List<Button>();
        readonly List<DropdownItem> items = new List<DropdownItem>();
        Action<DropdownItem> onPick;
        Action onClose;
        bool closed;

        public IReadOnlyList<DropdownItem> Items => items;
        public IReadOnlyList<Button> ItemButtons => buttons;
        public RectTransform Panel { get; private set; }
        public bool IsOpen => !closed;

        /// <param name="screenPoint">Where the menu's top-left corner goes (the bottom-left of the field), in screen pixels.</param>
        public static DropdownMenu Open(Canvas anyCanvasInHierarchy, Vector2 screenPoint, float minWidth, IList<DropdownItem> entries, Action<DropdownItem> onPick, Action onClose = null)
        {
            var root = anyCanvasInHierarchy.rootCanvas;
            var go = new GameObject("DropdownMenu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root.transform, false);
            Stretch(rt);
            var c = go.GetComponent<Canvas>();
            c.overrideSorting = true; c.sortingOrder = 200;                 // above the workspace and its masks

            var menu = go.AddComponent<DropdownMenu>();
            menu.onPick = onPick; menu.onClose = onClose;
            menu.Build(root, rt, screenPoint, minWidth, entries);
            return menu;
        }

        void Build(Canvas root, RectTransform rootRt, Vector2 screenPoint, float minWidth, IList<DropdownItem> entries)
        {
            // full-screen catcher: a click anywhere outside the list closes it
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            var brt = (RectTransform)blocker.transform;
            brt.SetParent(rootRt, false); Stretch(brt);
            blocker.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            blocker.GetComponent<Button>().onClick.AddListener(Close);
            blocker.GetComponent<Button>().transition = Selectable.Transition.None;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            Panel = (RectTransform)panel.transform;
            Panel.SetParent(rootRt, false);
            panel.GetComponent<Image>().color = Back;

            // measure with the real font so labels never wrap or clip
            var probe = MakeLabel(Panel, "probe", "");
            float widest = minWidth;
            foreach (var e in entries) { probe.text = e.Label; widest = Mathf.Max(widest, probe.GetPreferredValues(e.Label).x + 28f); }
            if (Application.isPlaying) Destroy(probe.gameObject); else DestroyImmediate(probe.gameObject);

            float height = Padding * 2 + entries.Count * ItemHeight;
            float shown = Mathf.Min(height, MaxHeight);

            var content = new GameObject("Content", typeof(RectTransform));
            var crt = (RectTransform)content.transform;
            crt.SetParent(Panel, false);
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, height); crt.anchoredPosition = Vector2.zero;

            if (height > MaxHeight)
            {
                var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
                var vrt = (RectTransform)vp.transform;
                vrt.SetParent(Panel, false); Stretch(vrt);
                crt.SetParent(vrt, false);
                var scroll = panel.AddComponent<ScrollRect>();
                scroll.viewport = vrt; scroll.content = crt; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 25f;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var item = entries[i];
                items.Add(item);
                float y = Padding + i * ItemHeight;
                bool sepBefore = item.Action != DropdownAction.Select && (i == 0 || entries[i - 1].Action == DropdownAction.Select);

                var b = new GameObject("Item " + item.Label, typeof(RectTransform), typeof(Image), typeof(Button));
                var brt2 = (RectTransform)b.transform;
                brt2.SetParent(crt, false);
                brt2.anchorMin = new Vector2(0, 1); brt2.anchorMax = new Vector2(1, 1); brt2.pivot = new Vector2(0.5f, 1);
                brt2.offsetMin = new Vector2(Padding, -(y + ItemHeight)); brt2.offsetMax = new Vector2(-Padding, -y);
                var img = b.GetComponent<Image>();
                img.color = item.Selected ? SelectedBack : new Color(0, 0, 0, 0);
                var btn = b.GetComponent<Button>();
                btn.targetGraphic = img;
                var colours = btn.colors; colours.normalColor = Color.white; colours.highlightedColor = Color.white; colours.pressedColor = Color.white; btn.colors = colours;
                btn.transition = Selectable.Transition.None;
                var hover = b.AddComponent<MenuItemHover>(); hover.Init(img, item.Selected ? SelectedBack : new Color(0, 0, 0, 0), Hover);
                int index = i;
                btn.onClick.AddListener(() => Pick(index));
                buttons.Add(btn);

                var label = MakeLabel(brt2, "Label", item.Label);
                var lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(10, 0); lrt.offsetMax = new Vector2(-6, 0);
                if (sepBefore)
                {
                    var line = new GameObject("Separator", typeof(RectTransform), typeof(Image));
                    var lnrt = (RectTransform)line.transform;
                    lnrt.SetParent(brt2, false);
                    lnrt.anchorMin = new Vector2(0, 1); lnrt.anchorMax = new Vector2(1, 1); lnrt.pivot = new Vector2(0.5f, 1);
                    lnrt.sizeDelta = new Vector2(0, 1); lnrt.anchoredPosition = new Vector2(0, 0);
                    line.GetComponent<Image>().color = new Color(1, 1, 1, 0.25f); line.GetComponent<Image>().raycastTarget = false;
                }
            }

            Place(root, rootRt, screenPoint, widest, shown);
        }

        /// <summary>
        /// Puts the panel's top-left corner at the screen point, then keeps it on screen: slides left, or opens upwards when
        /// there is no room below. Placement goes through a world position on the canvas plane, so it does not depend on the
        /// canvas rectangle having been laid out yet.
        /// </summary>
        void Place(Canvas root, RectTransform rootRt, Vector2 screenPoint, float width, float height)
        {
            Canvas.ForceUpdateCanvases();                                       // make sure the canvas rectangle is current
            Panel.anchorMin = Panel.anchorMax = new Vector2(0.5f, 0.5f);
            Panel.pivot = new Vector2(0, 1);
            Panel.sizeDelta = new Vector2(width, height);

            var cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rootRt, screenPoint, cam, out var world);
            Panel.position = world;

            var bounds = rootRt.rect;
            if (bounds.width < 1f || bounds.height < 1f) return;               // no usable canvas yet: leave it where it was asked for

            // measure in the canvas's own coordinates (the panel's corners relative to the canvas)
            var corners = new Vector3[4];
            Panel.GetWorldCorners(corners);
            float left = rootRt.InverseTransformPoint(corners[0]).x, top = rootRt.InverseTransformPoint(corners[1]).y;
            float right = rootRt.InverseTransformPoint(corners[2]).x, bottom = rootRt.InverseTransformPoint(corners[0]).y;

            float dx = 0, dy = 0;
            if (right > bounds.xMax) dx = bounds.xMax - right;
            if (left + dx < bounds.xMin) dx = bounds.xMin - left;
            if (bottom < bounds.yMin) dy = Mathf.Min(bounds.yMax - top, height + 30f);   // not enough room below: open upwards over the field
            Panel.anchoredPosition += new Vector2(dx, dy);
        }

        static TextMeshProUGUI MakeLabel(RectTransform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = FontSize; t.color = Color.white; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void Pick(int index)
        {
            if (closed || index < 0 || index >= items.Count) return;
            var item = items[index];
            Close();
            onPick?.Invoke(item);
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            onClose?.Invoke();
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) Close();      // not Blockly.Input
        }
#endif
    }
}
