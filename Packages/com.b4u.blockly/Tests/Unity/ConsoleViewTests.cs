using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blockly.UI.Tests
{
    /// <summary>The output console of the Playground.</summary>
    public class ConsoleViewTests
    {
        [Test]
        public void DraggingTheConsoleScrollsItsText()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Playground/Playground.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var console = Object.FindFirstObjectByType<ConsoleView>(FindObjectsInactive.Include);
            var scroll = console.GetComponentInChildren<ScrollRect>(true);
            console.gameObject.SetActive(true);
            scroll.viewport.sizeDelta = new Vector2(0, -40);
            scroll.content.sizeDelta = new Vector2(0, 600);          // far taller than the viewport
            scroll.content.anchoredPosition = new Vector2(0, 400);   // showing the newest lines
            float start = scroll.content.anchoredPosition.y;

            console.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(0, -50) });  // finger down (y falls on screen): older lines
            Assert.Less(scroll.content.anchoredPosition.y, start);
            console.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(0, 5000) });
            Assert.LessOrEqual(scroll.content.anchoredPosition.y, scroll.content.rect.height - scroll.viewport.rect.height, "not past the newest line");
            console.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(0, -5000) });
            Assert.AreEqual(0f, scroll.content.anchoredPosition.y, 0.01f, "not past the oldest line");
        }
    }
}
