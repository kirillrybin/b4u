# Blockly for Unity

A native C# port of [Blockly](https://developers.google.com/blockly) for Unity 6. It loads Blockly programs (JSON workspaces and legacy XML), draws the blocks with UGUI and TextMeshPro, lets the user edit them, and runs them with a built-in interpreter. There is no WebView: the same code runs on Windows, macOS, iOS, Android and WebGL.

- **Model and serialization:** blocks, inputs, fields, shadows, mutators, variables, procedures; Blockly JSON and XML in and out.
- **Interpreter:** runs the standard blocks (logic, loops, math, text, lists, variables, procedures) with Blockly's JavaScript semantics; Run, Step, Stop, slow mode and a highlighted current block.
- **Editor UI:** toolbox and flyouts, drag and snap, field editing and dropdowns, variables, procedure parameters, selection, Delete, copy / cut / paste, undo / redo, zoom, and a File menu to save and open programs.
- **Extensible:** add your own blocks from JSON definitions and C# handlers (see below).

`Blockly.Core` has no Unity dependencies, so the model, layout and interpreter also run in plain .NET. Block layout and execution are checked against the real Blockly in differential tests.

## Requirements

Unity 6 (6000.0 or newer; developed and tested on 6000.3), `com.unity.ugui` 2.0.0 (includes TextMeshPro). Import the TextMeshPro essentials once with **Blockly > Setup > Import TMP Essentials**.

## Install

Copy the `com.b4u.blockly` folder into your project's `Packages/` folder (an embedded package), or point to it from `Packages/manifest.json`:

```json
"com.b4u.blockly": "file:../path/to/com.b4u.blockly"
```

## Try it

**Blockly > Create Playground Scene** builds `Assets/Playground/Playground.unity` with a toolbar, a toolbox, a workspace, an output console and a set of example programs. Press Play, pick a program, press Run.

## Use it from code

```csharp
using Blockly;
using Blockly.UI;

// Load a program (Blockly JSON starts with '{', XML with '<').
Workspace ws = StandardBlockSet.Load(programText);

// Run it.
var output = new List<string>();
var interpreter = new Interpreter(ws) { Print = output.Add };
interpreter.Start();
interpreter.RunToCompletion();
if (interpreter.Error != null) Debug.LogError(interpreter.Error);

// Save it again.
string json = WorkspaceJson.Save(ws);
```

To show and edit a workspace, put a `WorkspaceView` under a Canvas and call `view.SetWorkspace(ws)`. `PlaygroundController` shows how a toolbox, a console and the run controls fit together. Use `ProgramRunner` instead of `Interpreter` to run a program a little each frame without blocking.

## Your own blocks

Implement `IBlockPackage` and register it before the first workspace is created:

```csharp
public sealed class MyBlocks : IBlockPackage
{
    public string BlocksJson => "[{\"type\":\"my_seven\",\"message0\":\"seven\",\"output\":\"Number\",\"colour\":20}]";

    public void RegisterHandlers(HandlerRegistry r) =>
        r.Expression("my_seven", (it, block) => 7.0);

    public IEnumerable<ToolboxCategory> ToolboxCategories => null;   // or a category listing your blocks
}

StandardBlockSet.AddPackage(new MyBlocks());
```

The **Custom blocks** sample (Package Manager > Blockly for Unity > Samples) is a complete example with a toolbox category.

## Saving programs

`PlaygroundController` saves to `Application.persistentDataPath/programs` through `IProgramStore` (`FileProgramStore`; `MemoryProgramStore` for tests). On WebGL the files are flushed to the browser's IndexedDB so they survive a page reload.

## Platforms

| Platform | Status |
|---|---|
| WebGL | Built and run in headless Chrome and on a phone browser (Samsung Galaxy A13) |
| Android | Built (IL2CPP, ARMv7 + ARM64) and run on a Samsung Galaxy A13; a 32-bit-only phone needs ARMv7 |
| macOS | Built and run |
| Windows | Built (Mono); not run |
| iOS | Xcode project generated; not built or run yet |

Command-line builds: `Unity -batchmode -executeMethod Blockly.EditorTools.BuildTool.BuildFromCommandLine -buildTarget WebGL -buildOut <dir>` (targets: WebGL, Android, iOS, Windows, macOS).

## Known limits

- In WebGL, File > Paste reads the browser's clipboard, which the browser may ask the user to allow (and refuses outside a click); then the status line says the clipboard cannot be read. The Ctrl+V of copied blocks inside the editor does not use the system clipboard.
- Only the blocks in `Runtime/Resources/Blockly` are provided; there is no code generator for other languages.

## Tests

Open **Window > General > Test Runner** and run the EditMode tests. The platform-independent tests also run with `dotnet test dev/Core.Tests` in the repository.
