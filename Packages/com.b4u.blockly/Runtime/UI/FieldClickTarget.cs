using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>
    /// A click (press and release without dragging) on an editable text/number field opens the field editor. A drag that
    /// starts on the field is not handled here: it bubbles up to the block's <see cref="BlockDragTarget"/>, so the block
    /// can still be grabbed by its fields.
    /// </summary>
    public sealed class FieldClickTarget : MonoBehaviour, IPointerClickHandler
    {
        WorkspaceView view;
        Block block;
        Field field;

        public Block Block => block;
        public Field Field => field;

        public void Init(WorkspaceView owner, Block b, Field f) { view = owner; block = b; field = f; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            if (view != null) view.BeginFieldEdit(block, field);
        }
    }
}
