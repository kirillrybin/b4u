using TMPro;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Playground: shows one of several Blockly programs (JSON or XML text assets) in a <see cref="WorkspaceView"/>.
    /// Next/Previous are meant to be wired to buttons.
    /// </summary>
    public sealed class PlaygroundController : MonoBehaviour
    {
        [SerializeField] TextAsset[] programs;
        [SerializeField] WorkspaceView view;
        [SerializeField] TMP_Text title;
        [SerializeField] bool cleanUpOnLoad = true;
        [SerializeField] float contentMargin = 40f;

        int index;

        public int Index => index;
        public int Count => programs == null ? 0 : programs.Length;

        void Start()
        {
            if (Count > 0) Show(0);
        }

        public void Configure(TextAsset[] programAssets, WorkspaceView workspaceView, TMP_Text titleText)
        {
            programs = programAssets; view = workspaceView; title = titleText;
        }

        public void Next() => Show((index + 1) % Mathf.Max(1, Count));
        public void Previous() => Show((index + Count - 1) % Mathf.Max(1, Count));

        public void Show(int i)
        {
            if (Count == 0 || view == null) return;
            index = Mathf.Clamp(i, 0, Count - 1);
            var asset = programs[index];
            var ws = StandardBlockSet.Load(asset.text);
            view.SetWorkspace(ws);
            if (cleanUpOnLoad) view.CleanUp();
            var rt = (RectTransform)view.transform;
            rt.sizeDelta += new Vector2(contentMargin, contentMargin);   // breathing room inside the scroll area
            rt.anchoredPosition = new Vector2(contentMargin * 0.5f, -contentMargin * 0.5f);
            if (title != null) title.text = (index + 1) + " / " + Count + "   " + asset.name;
        }
    }
}
