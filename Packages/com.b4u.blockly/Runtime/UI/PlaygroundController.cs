using System;
using System.Collections.Generic;
using Blockly.Editing;
using Blockly.Storage;
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
        [SerializeField] TMP_Text consoleToggleLabel;
        [SerializeField] RectTransform themeButton;
        [SerializeField] TMP_Text themeLabel;
        DropdownMenu themeMenu;
        [SerializeField] ToolboxView toolbox;
        [SerializeField] UnityEngine.UI.Button undoButton;
        [SerializeField] UnityEngine.UI.Button redoButton;
        [SerializeField] bool cleanUpOnLoad = true;
        [SerializeField] float contentMargin = 40f;
        [SerializeField] float slowInterval = 0.35f;

        int index;
        bool slow;
        ProgramRunner runner;

        // saving and loading
        [SerializeField] RectTransform fileButton;
        IProgramStore store;
        bool dirty;
        string savedName;
        string label = "";
        DropdownMenu fileMenu;
        DialogView dialog;

        // autosave: unsaved work is written to the store a moment after the last edit and when the app is left, and offered again at the next start
        [SerializeField] float autosaveDelay = 2f;
        float autosaveTimer = -1f;

        /// <summary>The name the current program was saved or opened under, or null if it has never been saved.</summary>
        public string SavedName => savedName;
        /// <summary>The program changed since it was shown, opened or saved.</summary>
        public bool IsDirty => dirty;
        public DropdownMenu ActiveMenu => fileMenu != null && fileMenu.IsOpen ? fileMenu : null;
        public DialogView ActiveDialog => dialog != null && dialog.IsOpen ? dialog : null;

        /// <summary>Where saved programs go. Defaults to a "programs" folder in <see cref="Application.persistentDataPath"/>.</summary>
        public IProgramStore Store
        {
            get => store ?? (store = new FileProgramStore(System.IO.Path.Combine(Application.persistentDataPath, "programs")));
            set => store = value;
        }

        FileExchange exchange;
        /// <summary>The folder used to export and import program files where there is no file dialog. Defaults to "exports" in <see cref="Application.persistentDataPath"/>.</summary>
        public FileExchange Exchange
        {
            get => exchange ?? (exchange = new FileExchange(System.IO.Path.Combine(Application.persistentDataPath, "exports")));
            set => exchange = value;
        }

        public int Index => index;
        public int Count => programs == null ? 0 : programs.Length;
        public ProgramRunner Runner => runner;
        public WorkspaceView View => view;
        public ConsoleView Console => console;
        public bool Slow => slow;
        public string Title => title != null ? title.text : "";

        void Start() => BeginSession();

        // the scene keeps the label and the button as serialized fields, so SetThemeButton is not called in Play: follow the theme from here too
        void OnEnable() { BlockStyle.ThemeChanged -= UpdateThemeLabel; BlockStyle.ThemeChanged += UpdateThemeLabel; UpdateThemeLabel(); }
        void OnDisable() { BlockStyle.ThemeChanged -= UpdateThemeLabel; }
        void OnDestroy() { BlockStyle.ThemeChanged -= UpdateThemeLabel; }

        /// <summary>Shows the first program, or the unsaved work of the last session if there is some (what Start does).</summary>
        public void BeginSession()
        {
            string draft = ReadDraft();                                // before Show: showing a program discards the stored draft
            if (Count > 0) Show(0);
            if (draft != null) RestoreFrom(draft);
        }

        void Update()
        {
            Tick(Time.deltaTime);
            TickAutosave(Time.unscaledDeltaTime);
#if ENABLE_LEGACY_INPUT_MANAGER
            bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl) || UnityEngine.Input.GetKey(KeyCode.LeftCommand) || UnityEngine.Input.GetKey(KeyCode.RightCommand);
            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.S) && ActiveDialog == null) Save();
#endif
        }

        /// <summary>One frame of the running program (Update calls this; tests call it directly).</summary>
        public void Tick(float deltaSeconds) => runner?.Pump(deltaSeconds);

        public void Configure(TextAsset[] programAssets, WorkspaceView workspaceView, ConsoleView consoleView, TMP_Text titleText, TMP_Text slowButtonLabel, ToolboxView toolboxView = null,
                              UnityEngine.UI.Button undo = null, UnityEngine.UI.Button redo = null, RectTransform file = null)
        {
            programs = programAssets; view = workspaceView; console = consoleView; title = titleText; slowLabel = slowButtonLabel; toolbox = toolboxView;
            undoButton = undo; redoButton = redo; fileButton = file;
        }

        /// <summary>The toolbar button that shows and hides the console writes its state on this label.</summary>
        public void SetConsoleToggleLabel(TMP_Text label) { consoleToggleLabel = label; UpdateConsoleToggleLabel(); }

        /// <summary>The toolbar's Theme button: its label shows the theme in use and a click opens the list of <see cref="BlockThemeCatalog"/>.</summary>
        public void SetThemeButton(RectTransform button, TMP_Text label)
        {
            themeButton = button; themeLabel = label;
            BlockStyle.ThemeChanged -= UpdateThemeLabel; BlockStyle.ThemeChanged += UpdateThemeLabel;
            UpdateThemeLabel();
        }

        void UpdateThemeLabel()
        {
            if (this == null) { BlockStyle.ThemeChanged -= UpdateThemeLabel; return; }
            if (themeLabel != null) themeLabel.text = "Theme: " + BlockThemeCatalog.CurrentName;
        }

        /// <summary>Opens the list of themes under the Theme button.</summary>
        public void ShowThemeMenu()
        {
            if (themeButton == null) return;
            var corners = new Vector3[4];
            themeButton.GetWorldCorners(corners);
            var canvas = CanvasForUi;
            var cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            ShowThemeMenuAt(RectTransformUtility.WorldToScreenPoint(cam, corners[0]), Mathf.Max(180f, corners[2].x - corners[0].x));
        }

        public void ShowThemeMenuAt(Vector2 screenPoint, float width = 180f)
        {
            var canvas = CanvasForUi;
            if (canvas == null || ActiveDialog != null) return;
            if (themeMenu != null && themeMenu.IsOpen) themeMenu.Close();
            string current = BlockThemeCatalog.CurrentName;
            var items = new List<DropdownItem>();
            foreach (var name in BlockThemeCatalog.Names) items.Add(new DropdownItem { Label = name, Value = name, Selected = name == current });
            themeMenu = DropdownMenu.Open(canvas, screenPoint, width, items, item => BlockThemeCatalog.Select(item.Value));
        }

        public bool ConsoleVisible => console == null || console.Visible;

        /// <summary>Shows the console, or hides it to give the workspace the room. The layout follows on its own.</summary>
        public void ToggleConsole() => SetConsoleVisible(!ConsoleVisible);

        public void SetConsoleVisible(bool visible)
        {
            if (console == null) return;
            console.Visible = visible;
            UpdateConsoleToggleLabel();
        }

        void UpdateConsoleToggleLabel()
        {
            if (consoleToggleLabel != null) consoleToggleLabel.text = ConsoleVisible ? "Console: on" : "Console: off";
        }

        public void Undo() => view?.Undo();
        public void Redo() => view?.Redo();

        void RefreshHistoryButtons()
        {
            if (undoButton != null) undoButton.interactable = view != null && view.CanUndo;
            if (redoButton != null) redoButton.interactable = view != null && view.CanRedo;
        }

        public UnityEngine.UI.Button UndoButton => undoButton;
        public UnityEngine.UI.Button RedoButton => redoButton;

        public ToolboxView Toolbox => toolbox;

        // ---------------------------------------------------------------- programs
        public void Next() => Guard(() => Show((index + 1) % Mathf.Max(1, Count)));
        public void Previous() => Guard(() => Show((index + Count - 1) % Mathf.Max(1, Count)));

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
            index = Mathf.Clamp(i, 0, Count - 1);
            var asset = programs[index];
            Display(StandardBlockSet.Load(asset.text), (index + 1) + " / " + Count + "   " + asset.name, null, dirtyNow: false);
        }

        /// <summary>Puts a workspace on screen and makes it the current program (stops a running one, resets the console and history).</summary>
        void Display(Workspace ws, string titleText, string saved, bool dirtyNow)
        {
            if (view == null) return;
            runner?.Stop();
            CloseFileUi();
            view.BlockDragStarted -= OnDragStarted; view.FieldEditStarted -= OnDragStarted; view.HistoryChanged -= RefreshHistoryButtons; view.WorkspaceEdited -= OnEdited;

            view.SetWorkspace(ws);
            view.BlockDragStarted += OnDragStarted;
            view.FieldEditStarted += OnDragStarted;
            view.HistoryChanged += RefreshHistoryButtons;
            view.WorkspaceEdited += OnEdited;
            if (cleanUpOnLoad) view.CleanUp(recordUndo: false);        // arranging a freshly loaded program is not something to undo
            RefreshHistoryButtons();
            var rt = (RectTransform)view.transform;
            rt.sizeDelta += new Vector2(contentMargin, contentMargin);   // breathing room inside the scroll area
            rt.anchoredPosition = new Vector2(contentMargin * 0.5f, -contentMargin * 0.5f);

            label = titleText; savedName = saved; dirty = dirtyNow;
            RefreshTitle();
            ClearDraft();                                              // whatever was kept for the program that was on screen is no longer wanted
            autosaveTimer = dirtyNow ? autosaveDelay : -1f;

            runner = new ProgramRunner(ws) { Slow = slow, SlowInterval = slowInterval };
            runner.Output += line => console?.Append(line);
            runner.CurrentBlockChanged += b => view.SetHighlight(b);
            runner.Finished += OnFinished;
            console?.Clear();
            console?.SetStatus("Ready", ConsoleView.NeutralColour);
            toolbox?.CloseFlyout();
        }

        void OnEdited()
        {
            if (!dirty) { dirty = true; RefreshTitle(); }
            autosaveTimer = autosaveDelay;                             // every edit restarts the wait, so a burst of edits is written once
        }

        // ---------------------------------------------------------------- autosave
        /// <summary>Counts down to the next automatic backup of unsaved work (Update calls this; tests call it directly).</summary>
        public void TickAutosave(float deltaSeconds)
        {
            if (autosaveTimer < 0f) return;
            autosaveTimer -= deltaSeconds;
            if (autosaveTimer <= 0f) WriteDraft();
        }

        /// <summary>Writes the unsaved program to the store now. Does nothing if there are no unsaved changes.</summary>
        public bool WriteDraft()
        {
            autosaveTimer = -1f;
            if (!dirty || view == null || view.Workspace == null) return false;
            try
            {
                // the first line is the name the program was saved under (empty if none), the rest is the program
                Store.Save(ProgramNames.DraftName, (savedName ?? "") + "\n" + WorkspaceJson.Save(view.Workspace));
                PlatformFiles.Flush();
                return true;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { return false; }   // a backup must never get in the way
        }

        void ClearDraft()
        {
            try
            {
                if (Store.Delete(ProgramNames.DraftName)) PlatformFiles.Flush();
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { }
        }

        string ReadDraft()
        {
            try { string text = Store.Load(ProgramNames.DraftName); return string.IsNullOrEmpty(text) ? null : text; }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { return null; }
        }

        /// <summary>Brings back the unsaved work kept by the last session, if there is any. Returns whether something was restored.</summary>
        public bool RestoreDraft()
        {
            string draft = ReadDraft();
            return draft != null && RestoreFrom(draft);
        }

        bool RestoreFrom(string text)
        {
            int eol = text.IndexOf('\n');
            string name = eol > 0 ? text.Substring(0, eol) : null;
            string body = eol >= 0 ? text.Substring(eol + 1) : text;
            if (!LoadText(body, name ?? "restored program", name, dirtyNow: true)) { ClearDraft(); return false; }
            WriteDraft();                                              // showing it cleared the copy in the store; keep one until the user saves or discards
            Status("Restored your unsaved changes", false);
            return true;
        }

        void OnApplicationPause(bool paused) { if (paused) WriteDraft(); }
        void OnApplicationFocus(bool focused) { if (!focused) WriteDraft(); }
        void OnApplicationQuit() => WriteDraft();

        void RefreshTitle()
        {
            if (title != null) title.text = label + (dirty ? " *" : "");
        }

        // ---------------------------------------------------------------- saving and loading
        Canvas CanvasForUi => view != null ? view.GetComponentInParent<Canvas>() : null;

        void CloseFileUi()
        {
            if (fileMenu != null && fileMenu.IsOpen) fileMenu.Close();
            fileMenu = null;
        }

        /// <summary>Runs <paramref name="proceed"/> now, or after the user agrees to throw away unsaved changes.</summary>
        void Guard(Action proceed)
        {
            var canvas = CanvasForUi;
            if (!dirty || canvas == null || ActiveDialog != null) { proceed(); return; }
            dialog = DialogView.ShowConfirm(canvas, "Unsaved changes", "The program was changed since it was saved. Discard the changes?", proceed, null, "Discard");
        }

        void Status(string message, bool error)
            => console?.SetStatus(message, error ? ConsoleView.ErrorColour : ConsoleView.OkColour);

        /// <summary>Starts an empty program.</summary>
        public void NewProgram() => Guard(() =>
        {
            Display(new Workspace(StandardBlockSet.CreateRegistry()), "new program", null, dirtyNow: false);
        });

        /// <summary>Saves under the current name, or asks for one if the program has none yet.</summary>
        public void Save()
        {
            if (view == null || view.Workspace == null) return;
            if (savedName != null) SaveTo(savedName); else SaveAs();
        }

        /// <summary>Asks for a name and saves; asks again before replacing a different saved program.</summary>
        public void SaveAs()
        {
            var canvas = CanvasForUi;
            if (view == null || view.Workspace == null || canvas == null || ActiveDialog != null) return;
            dialog = DialogView.ShowPrompt(canvas, "Save program as:", savedName ?? "my program", ProgramNames.Validate, typed =>
            {
                string name = ProgramNames.Sanitize(typed);
                if (name != savedName && Store.Exists(name))
                    dialog = DialogView.ShowConfirm(canvas, "Replace \"" + name + "\"?", "A saved program with this name exists. Replace it?", () => SaveTo(name), null, "Replace");
                else SaveTo(name);
            });
        }

        /// <summary>Writes the program to the store under <paramref name="name"/>. Returns false (and says why in the console) if that failed.</summary>
        public bool SaveTo(string name)
        {
            if (view == null || view.Workspace == null) return false;
            if (ProgramNames.IsReserved(name)) { Status(ProgramNames.Validate(name), true); return false; }
            try
            {
                string used = Store.Save(name, WorkspaceJson.Save(view.Workspace));
                PlatformFiles.Flush();
                savedName = used; label = used; dirty = false; autosaveTimer = -1f;
                ClearDraft();
                RefreshTitle();
                Status("Saved \"" + used + "\"", false);
                return true;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                Status("Cannot save: " + e.Message, true);
                return false;
            }
        }

        /// <summary>Opens a saved program. Returns false if there is none by that name or it cannot be read.</summary>
        public bool OpenSaved(string name)
        {
            string text;
            try { text = Store.Load(name); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Status("Cannot open: " + e.Message, true); return false; }
            if (text == null) { Status("No saved program \"" + name + "\"", true); return false; }
            string used = ProgramNames.Sanitize(name);
            return LoadText(text, used, used, dirtyNow: false);
        }

        /// <summary>Shows a program given as Blockly JSON or XML. The current program stays if the text is not a valid program.</summary>
        public bool LoadText(string text, string titleText, string saved, bool dirtyNow)
        {
            Workspace ws;
            try
            {
                if (string.IsNullOrWhiteSpace(text)) throw new FormatException("the text is empty");
                ws = StandardBlockSet.Load(text);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                Status("Cannot load: " + e.Message, true);
                return false;
            }
            Display(ws, titleText, saved, dirtyNow);
            return true;
        }

        public void DeleteSaved(string name)
        {
            var canvas = CanvasForUi;
            Action delete = () =>
            {
                try
                {
                    if (!Store.Delete(name)) { Status("No saved program \"" + name + "\"", true); return; }
                    PlatformFiles.Flush();
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Status("Cannot delete: " + e.Message, true); return; }
                if (ProgramNames.Sanitize(name) == savedName) { savedName = null; dirty = true; RefreshTitle(); }
                Status("Deleted \"" + name + "\"", false);
            };
            if (canvas == null || ActiveDialog != null) delete();
            else dialog = DialogView.ShowConfirm(canvas, "Delete \"" + name + "\"?", "This saved program will be removed for good.", delete);
        }

        /// <summary>Puts the program on the clipboard as Blockly workspace JSON (loads in blockly's own playground too).</summary>
        public void CopyJson()
        {
            if (view == null || view.Workspace == null) return;
            PlatformClipboard.Write(WorkspaceJson.Save(view.Workspace));
            Status("Copied the program as JSON", false);
        }

        /// <summary>Replaces the program with Blockly JSON or XML from the clipboard.</summary>
        public void PasteProgram() => PlatformClipboard.Read(text =>
        {
            if (string.IsNullOrWhiteSpace(text)) { Status("The clipboard is empty or cannot be read", true); return; }
            Guard(() => LoadText(text, "pasted program", null, dirtyNow: true));
        });

        // ---- program files
        /// <summary>Gives the program as a <c>.json</c> file: a download in a browser, otherwise a file in <see cref="Exchange"/>.</summary>
        public void ExportProgram()
        {
            if (view == null || view.Workspace == null) return;
            string name = savedName ?? "program";
            string json = WorkspaceJson.Save(view.Workspace);
            if (PlatformFileTransfer.UsesBrowser)
            {
                PlatformFileTransfer.Download(ProgramNames.Sanitize(name) + ".json", json);
                Status("Downloaded " + ProgramNames.Sanitize(name) + ".json", false);
                return;
            }
            try { Status("Exported to " + Exchange.Export(name, json), false); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Status("Cannot export: " + e.Message, true); }
        }

        /// <summary>Loads a program from a file: the one the user picks in a browser, otherwise one chosen from the files in <see cref="Exchange"/>.</summary>
        public void ImportProgram(Vector2 menuPoint, float width = 220f)
        {
            if (PlatformFileTransfer.UsesBrowser)
            {
                Status("Choose a file in the panel on the page", false);
                PlatformFileTransfer.Pick(
                    (fileName, text) =>
                    {
                        Status("Read " + fileName + " (" + text.Length + " characters)", false);
                        Guard(() => LoadText(text, System.IO.Path.GetFileNameWithoutExtension(fileName), null, dirtyNow: true));
                    },
                    reason => Status("Cannot read the file: " + reason, true));
                return;
            }
            var canvas = CanvasForUi;
            if (canvas == null) return;
            IReadOnlyList<string> files;
            try { files = Exchange.List(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Status("Cannot read " + Exchange.Folder + ": " + e.Message, true); return; }
            if (files.Count == 0) { Status("Put .json or .xml program files into " + Exchange.Folder + " to import them", true); return; }
            var items = new List<DropdownItem>();
            foreach (var f in files) items.Add(new DropdownItem { Label = f, Value = ImportPrefix + f });
            fileMenu = DropdownMenu.Open(canvas, menuPoint, width, items, item => OnFileCommand(item.Value, menuPoint, width));
        }

        /// <summary>Loads a file of the <see cref="Exchange"/> folder as an unsaved program.</summary>
        public bool ImportFile(string fileName)
        {
            string text;
            try { text = Exchange.Read(fileName); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is ArgumentException) { Status("Cannot import: " + e.Message, true); return false; }
            if (text == null) { Status("The file " + fileName + " is gone", true); return false; }
            return LoadText(text, System.IO.Path.GetFileNameWithoutExtension(fileName), null, dirtyNow: true);
        }

        // ---- the File menu
        const string CmdExport = "export", CmdImport = "import", ImportPrefix = "import:";
        const string CmdNew = "new", CmdOpen = "open", CmdSave = "save", CmdSaveAs = "saveas", CmdDelete = "delete", CmdCopy = "copy", CmdPaste = "paste";
        const string OpenPrefix = "open:", DeletePrefix = "delete:";

        /// <summary>Opens the File menu under the File button.</summary>
        public void ShowFileMenu()
        {
            if (fileButton == null) return;
            var corners = new Vector3[4];
            fileButton.GetWorldCorners(corners);
            var canvas = CanvasForUi;
            var cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            ShowFileMenuAt(RectTransformUtility.WorldToScreenPoint(cam, corners[0]), Mathf.Max(200f, corners[2].x - corners[0].x));
        }

        public void ShowFileMenuAt(Vector2 screenPoint, float width = 220f)
        {
            var canvas = CanvasForUi;
            if (canvas == null || ActiveDialog != null) return;
            CloseFileUi();
            var items = new List<DropdownItem>
            {
                new DropdownItem { Label = "New", Value = CmdNew },
                new DropdownItem { Label = "Open…", Value = CmdOpen },
                new DropdownItem { Label = "Save", Value = CmdSave },
                new DropdownItem { Label = "Save as…", Value = CmdSaveAs },
                new DropdownItem { Label = "Delete…", Value = CmdDelete },
                new DropdownItem { Label = "Copy as JSON", Value = CmdCopy },
                new DropdownItem { Label = "Paste JSON or XML", Value = CmdPaste },
                new DropdownItem { Label = PlatformFileTransfer.UsesBrowser ? "Download as file" : "Export to file", Value = CmdExport },
                new DropdownItem { Label = PlatformFileTransfer.UsesBrowser ? "Upload a file…" : "Import from file…", Value = CmdImport },
            };
            fileMenu = DropdownMenu.Open(canvas, screenPoint, width, items, item => OnFileCommand(item.Value, screenPoint, width));
        }

        /// <summary>What choosing an entry of the File menu does (also the way tests press them).</summary>
        public void OnFileCommand(string command, Vector2 menuPoint, float width = 220f)
        {
            switch (command)
            {
                case CmdNew: NewProgram(); break;
                case CmdSave: Save(); break;
                case CmdSaveAs: SaveAs(); break;
                case CmdCopy: CopyJson(); break;
                case CmdPaste: PasteProgram(); break;
                case CmdExport: ExportProgram(); break;
                case CmdImport: ImportProgram(menuPoint, width); break;
                case CmdOpen: ShowSavedList(menuPoint, width, OpenPrefix); break;
                case CmdDelete: ShowSavedList(menuPoint, width, DeletePrefix); break;
                default:
                    if (command.StartsWith(ImportPrefix)) { string f = command.Substring(ImportPrefix.Length); Guard(() => ImportFile(f)); }
                    else if (command.StartsWith(OpenPrefix)) { string n = command.Substring(OpenPrefix.Length); Guard(() => OpenSaved(n)); }
                    else if (command.StartsWith(DeletePrefix)) DeleteSaved(command.Substring(DeletePrefix.Length));
                    break;
            }
        }

        void ShowSavedList(Vector2 point, float width, string prefix)
        {
            var canvas = CanvasForUi;
            if (canvas == null) return;
            IReadOnlyList<string> names;
            try { names = Store.List(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { Status("Cannot read the saved programs: " + e.Message, true); return; }
            var items = new List<DropdownItem>();
            foreach (var n in names) if (!ProgramNames.IsReserved(n)) items.Add(new DropdownItem { Label = n, Value = prefix + n, Selected = n == savedName });
            if (items.Count == 0) items.Add(new DropdownItem { Label = "(no saved programs)", Value = "" });
            fileMenu = DropdownMenu.Open(canvas, point, width, items, item => { if (item.Value.Length > 0) OnFileCommand(item.Value, point, width); });
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

        void OnDragStarted(Block _)
        {
            // editing a program that is running (or paused mid-way) invalidates it; the next Run starts fresh
            if (runner != null && runner.IsActive) Stop();
        }

        void OnFinished(string error)
        {
            if (error == null) console?.SetStatus("Finished", ConsoleView.OkColour);
            else { SetConsoleVisible(true); console?.SetStatus("Error: " + error, ConsoleView.ErrorColour); }     // an error is never left unseen
        }
    }
}
