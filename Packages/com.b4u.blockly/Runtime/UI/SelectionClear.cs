using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>Clicking the workspace background deselects the selected block. Own file: see <see cref="FlyoutDismiss"/>.</summary>
    public sealed class SelectionClear : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] WorkspaceView view;

        public void Configure(WorkspaceView target) => view = target;

        public void OnPointerClick(PointerEventData e)
        {
            if (view != null && (e == null || e.button == PointerEventData.InputButton.Left)) view.Select(null);
        }
    }
}
