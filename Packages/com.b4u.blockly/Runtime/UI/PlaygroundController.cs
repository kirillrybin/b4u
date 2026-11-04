using TMPro;
using UnityEngine;

namespace Blockly.UI
{
    /// <summary>
    /// Playground: shows one of several Blockly programs in a <see cref="WorkspaceView"/> and runs it with
    /// Run / Step / Stop. The block about to execute is outlined; printed text goes to the console.
    /// All commands are public methods meant to be wired to buttons.
    /// </summary>
    public sealed class PlaygroundController : MonoBehaviour
    {
        [SerializeField] TextAsset[] programs;
        [SerializeField] WorkspaceView view;
        [SerializeField] ConsoleView console;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text slowLabel;
        [SerializeField] bool cleanUpOnLoad = true;
        [SerializeField] float contentMargin = 40f;
        [SerializeField] float slowInterval = 0.35f;

        int index;
        bool slow;
        ProgramRunner runner;

        public int Index => index;
        public int Count => programs == null ? 0 : programs.Length;
        public ProgramRunner Runner => runner;
        public WorkspaceView View => view;
        public ConsoleView Console => console;
        public bool Slow => slow;
        public string Title => title != null ? title.text : "";

        void Start()
        {
            if (Count > 0) Show(0);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>One frame of the running program (Update calls this; tests call it directly).</summary>
        public void Tick(float deltaSeconds) => runner?.Pump(deltaSeconds);

        public void Configure(TextAsset[] programAssets, WorkspaceView workspaceView, ConsoleView consoleView, TMP_Text titleText, TMP_Text slowButtonLabel)
        {
            programs = programAssets; view = workspaceView; console = consoleView; title = titleText; slowLabel = slowButtonLabel;
        }

        // ---------------------------------------------------------------- programs
        public void Next() => Show((index + 1) % Mathf.Max(1, Count));
        public void Previous() => Show((index + Count - 1) % Mathf.Max(1, Count));

        /// <summary>Shows the program whose asset name contains <paramref name="name"/>. Returns false if none matches.</summary>
        public bool ShowByName(string name)
        {
            for (int i = 0; i < Count; i++)
                if (programs[i] != null && programs[i].name == name) { Show(i); return true; }
            return false;
        }

        public void Show(int i)
        {
            if (Count == 0 || view == null) return;
            runner?.Stop();
            index = Mathf.Clamp(i, 0, Count - 1);
            var asset = programs[index];

            var ws = StandardBlockSet.Load(asset.text);
            view.SetWorkspace(ws);
            if (cleanUpOnLoad) view.CleanUp();
            var rt = (RectTransform)view.transform;
            rt.sizeDelta += new Vector2(contentMargin, contentMargin);   // breathing room inside the scroll area
            rt.anchoredPosition = new Vector2(contentMargin * 0.5f, -contentMargin * 0.5f);
            if (title != null) title.text = (index + 1) + " / " + Count + "   " + asset.name;

            runner = new ProgramRunner(ws) { Slow = slow, SlowInterval = slowInterval };
            runner.Output += line => console?.Append(line);
            runner.CurrentBlockChanged += b => view.SetHighlight(b);
            runner.Finished += OnFinished;
            console?.Clear();
            console?.SetStatus("Ready", ConsoleView.NeutralColour);
        }

        // ---------------------------------------------------------------- run controls
        public void Run()
        {
            if (runner == null) return;
            if (runner.State == RunnerState.Finished || runner.State == RunnerState.Idle) console?.Clear();
            runner.Run();
            console?.SetStatus("Running…", ConsoleView.NeutralColour);
        }

        public void Step()
        {
            if (runner == null) return;
            if (runner.State == RunnerState.Finished || runner.State == RunnerState.Idle) console?.Clear();
            runner.Step();
            if (runner.State == RunnerState.Paused) console?.SetStatus("Paused", ConsoleView.NeutralColour);
        }

        public void Stop()
        {
            if (runner == null || !runner.IsActive) return;
            runner.Stop();
            console?.SetStatus("Stopped", ConsoleView.NeutralColour);
        }

        public void ToggleSlow()
        {
            slow = !slow;
            if (runner != null) runner.Slow = slow;
            if (slowLabel != null) slowLabel.text = slow ? "Slow: on" : "Slow: off";
        }

        void OnFinished(string error)
        {
            if (error == null) console?.SetStatus("Finished", ConsoleView.OkColour);
            else console?.SetStatus("Error: " + error, ConsoleView.ErrorColour);
        }
    }
}
