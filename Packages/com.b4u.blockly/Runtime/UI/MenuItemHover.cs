using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI
{
    /// <summary>Highlights a dropdown menu item while the pointer is over it. (Own file: Unity finds a MonoBehaviour by file name.)</summary>
    public sealed class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Image image;
        Color normal, hover;

        public void Init(Image target, Color normalColour, Color hoverColour) { image = target; normal = normalColour; hover = hoverColour; }

        public void OnPointerEnter(PointerEventData e) { if (image != null) image.color = hover; }
        public void OnPointerExit(PointerEventData e) { if (image != null) image.color = normal; }
    }
}
