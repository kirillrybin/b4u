using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>
    /// The workspace background: a click deselects, a right click or a press held still opens the background menu, and with Shift held (or the
    /// select mode of touch screens on) a drag draws a rectangle that selects the stacks it touches instead of scrolling. Own file: see <see cref="FlyoutDismiss"/>.
    /// </summary>
    public sealed class SelectionClear : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] WorkspaceView view;
        bool pressing; Vector2 pressPoint; float pressStarted;
        bool marqueeArmed, marqueeing;
        ScrollRect scroll;
        bool scrollWasOn;

        public void Configure(WorkspaceView target) => view = target;

        bool WantsMarquee => view != null && view.MultiSelectEnabled && (view.SelectMode || WorkspaceView.ShiftHeld);

        public void OnPointerClick(PointerEventData e)
        {
            if (view == null) return;
            if (e == null || e.button == PointerEventData.InputButton.Left) { if (!WantsMarquee && !WorkspaceView.ModifierHeld) view.Select(null); }
            else if (e.button == PointerEventData.InputButton.Right) view.ShowWorkspaceMenu(e.position);
        }

        // a press held still on the background opens the same menu as a right click (touch screens)
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressing = true; pressPoint = e.position; pressStarted = Time.unscaledTime;
            marqueeArmed = WantsMarquee;
            if (marqueeArmed) SetScroll(false);                  // before the drag starts, or the scroll view takes the gesture
        }

        public void OnPointerUp(PointerEventData e) { pressing = false; if (!marqueeing) { marqueeArmed = false; SetScroll(true); } }

        public void OnBeginDrag(PointerEventData e)
        {
            pressing = false;                                    // the finger moved: it is a pan or a rectangle, not a long press
            marqueeing = marqueeArmed;
        }

        public void OnDrag(PointerEventData e) { if (marqueeing && view != null) view.UpdateMarquee(e.pressPosition, e.position); }

        public void OnEndDrag(PointerEventData e)
        {
            if (marqueeing && view != null) view.EndMarquee(e.pressPosition, e.position, WorkspaceView.ModifierHeld);
            marqueeing = false; marqueeArmed = false;
            SetScroll(true);
        }

        void SetScroll(bool on)
        {
            if (scroll == null) scroll = GetComponent<ScrollRect>();
            if (scroll == null) return;
            if (!on) { scrollWasOn = scroll.enabled; scroll.enabled = false; }
            else if (!scroll.enabled && scrollWasOn) { scroll.enabled = true; scrollWasOn = false; }
        }

        void OnDisable() { marqueeing = false; marqueeArmed = false; SetScroll(true); }

        void Update()
        {
            if (!pressing || view == null) return;
            if (Time.unscaledTime - pressStarted < WorkspaceView.LongPressSeconds) return;
            pressing = false;
            if (!view.IsDragging) view.ShowWorkspaceMenu(pressPoint);
        }
    }
}
