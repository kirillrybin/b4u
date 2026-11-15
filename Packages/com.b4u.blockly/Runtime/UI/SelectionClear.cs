using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>Clicking the workspace background deselects the selected block. Own file: see <see cref="FlyoutDismiss"/>.</summary>
    public sealed class SelectionClear : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler
    {
        [SerializeField] WorkspaceView view;
        bool pressing; Vector2 pressPoint; float pressStarted;

        public void Configure(WorkspaceView target) => view = target;

        public void OnPointerClick(PointerEventData e)
        {
            if (view == null) return;
            if (e == null || e.button == PointerEventData.InputButton.Left) view.Select(null);
            else if (e.button == PointerEventData.InputButton.Right) view.ShowWorkspaceMenu(e.position);
        }

        // a press held still on the background opens the same menu as a right click (touch screens)
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { pressing = true; pressPoint = e.position; pressStarted = Time.unscaledTime; } }
        public void OnPointerUp(PointerEventData e) { pressing = false; }
        public void OnBeginDrag(PointerEventData e) { pressing = false; }          // the finger moved: it is a pan, not a long press
        public void OnDrag(PointerEventData e) { }

        void Update()
        {
            if (!pressing || view == null) return;
            if (Time.unscaledTime - pressStarted < WorkspaceView.LongPressSeconds) return;
            pressing = false;
            if (!view.IsDragging) view.ShowWorkspaceMenu(pressPoint);
        }
    }
}
