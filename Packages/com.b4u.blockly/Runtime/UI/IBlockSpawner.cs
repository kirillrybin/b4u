using UnityEngine.EventSystems;

namespace Blockly.UI
{
    /// <summary>
    /// Implemented by whoever owns a flyout. When the user grabs a block in the flyout, nothing moves there; the spawner
    /// creates a real copy in the main workspace and takes over the drag.
    /// </summary>
    public interface IBlockSpawner
    {
        /// <param name="flyoutBlock">The top-level flyout block that was grabbed.</param>
        bool TrySpawn(Block flyoutBlock, PointerEventData eventData, WorkspaceView flyoutView);
    }
}
