using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>
    /// Makes a block's object grabbable. The drag is handed over to the <see cref="WorkspaceView"/> because this
    /// object is destroyed and rebuilt as soon as the block leaves its parent, and Unity would keep sending drag
    /// events to the dead object. Shadow blocks drag their parent, like in Blockly.
    /// </summary>
    public sealed class BlockDragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        WorkspaceView view;
        Block block;

        public Block Block => block;

        public void Init(WorkspaceView owner, Block b) { view = owner; block = b; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button == PointerEventData.InputButton.Right) { if (view != null) view.ShowBlockMenu(block, e.position); return; }
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            if (view != null) view.Select(block);
        }

        // a press that is held still opens the block menu (the right click of a touch screen)
        public void OnPointerDown(PointerEventData e) { if (view != null && e.button == PointerEventData.InputButton.Left) view.BeginPress(block, e.position); }
        public void OnPointerUp(PointerEventData e) { if (view != null) view.EndPress(); }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || view == null) return;
            view.EndPress();

            if (view.Spawner != null)                    // flyout: any part of a block grabs the whole top-level block
            {
                var top = block;
                while (top != null)
                {
                    if (top.IsShadow) top = top.ShadowOf?.Owner;
                    else if (top.Parent != null) top = top.Parent;
                    else break;
                }
                if (top != null) view.Spawner.TrySpawn(top, e, view);
                return;
            }

            var grabbed = block;
            while (grabbed != null && grabbed.IsShadow) grabbed = grabbed.ShadowOf?.Owner;   // a shadow is not 'connected', so Parent is null
            if (grabbed == null) return;

            // grab at the press position so the block stays under the finger instead of jumping by the drag threshold
            var point = view.ScreenToWorkspace(e.pressPosition, e.pressEventCamera);
            view.Select(grabbed);
            if (view.BeginDrag(grabbed, point))
            {
                e.pointerDrag = view.gameObject;       // later Drag/EndDrag events go to the view
                view.OnDrag(e);                        // catch up to the current pointer position right away
            }
        }

        // Only reached if the hand-over did not happen (e.g. wrong button); nothing to do then.
        public void OnDrag(PointerEventData e) { }
        public void OnEndDrag(PointerEventData e) { }
    }
}
