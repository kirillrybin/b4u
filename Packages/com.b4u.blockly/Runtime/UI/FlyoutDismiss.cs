using UnityEngine;
using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>
    /// Closes the flyout when the user presses on the workspace background. Lives in its own file on purpose: Unity
    /// finds a MonoBehaviour's script by file name, so a second component class inside another file would be saved
    /// into a scene as "Missing script".
    /// </summary>
    public sealed class FlyoutDismiss : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] ToolboxView toolbox;

        public void Configure(ToolboxView view) => toolbox = view;

        public void OnPointerDown(PointerEventData e)
        {
            if (toolbox != null) toolbox.CloseFlyout();
        }
    }
}
