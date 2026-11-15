# Blockly for Unity

A native C# port of [Blockly](https://developers.google.com/blockly) for Unity 6. It loads Blockly programs (JSON workspaces and legacy XML), draws the blocks with UGUI and TextMeshPro, lets the user edit them, and runs them with a built-in interpreter. There is no WebView: the same code runs on Windows, macOS, iOS, Android and WebGL.

- **Model and serialization:** blocks, inputs, fields, shadows, mutators, variables, procedures; Blockly JSON and XML in and out.
- **Interpreter:** runs the standard blocks (logic, loops, math, text, lists, variables, procedures) with Blockly's JavaScript semantics; Run, Step, Stop, slow mode and a highlighted current block.
- **Editor UI:** toolbox and flyouts, drag and snap, field editing and dropdowns, variables, procedure parameters, selection, Delete, copy / cut / paste, undo / redo, zoom, and a File menu to save and open programs.
- **Extensible:** add your own blocks from JSON definitions and C# handlers (see below).

`Blockly.Core` has no Unity dependencies, so the model, layout and interpreter also run in plain .NET. Block layout and execution are checked against the real Blockly in differential tests.

## Requirements

Unity 6 (6000.0 or newer; developed and tested on 6000.3), `com.unity.ugui` 2.0.0 (includes TextMeshPro). Import the TextMeshPro essentials once with **Blockly > Setup > Import TMP Essentials**.

### Compatibility

- **Unity:** 6000.0 or newer (tested on 6000.3). Unity 2022 and older ship `com.unity.ugui` 1.0.0, so the dependency does not resolve there.
- **UI and render pipeline:** UGUI with TextMeshPro, drawn in Screen Space Overlay, so any render pipeline works (Built-in is tested; URP and HDRP were not tried).
- **Input:** the mouse and touch work through the EventSystem. The keyboard (Ctrl+C / V / X / Z / Y, Delete, zoom keys, Esc in menus and dialogs) needs the old Input Manager: set Player Settings > Active Input Handling to *Input Manager (Old)* or *Both*. The Playground scene uses `StandaloneInputModule`; with only the new Input System, use `InputSystemUIInputModule` in your own scene (then there are no keyboard shortcuts).
- **Platforms:** see the table below. `Blockly.Core` has no Unity references and can be used in any C# project.

## Install

Copy the `com.b4u.blockly` folder into your project's `Packages/` folder (an embedded package), or point to it from `Packages/manifest.json`:

```json
"com.b4u.blockly": "file:../path/to/com.b4u.blockly"
```

or install it from git (Window > Package Manager > + > Add package from git URL):

```
https://github.com/kirillrybin/b4u.git?path=/Packages/com.b4u.blockly
```

## Try it

**Blockly > Create Playground Scene** builds `Assets/Playground/Playground.unity` with a toolbar, a toolbox, a workspace, an output console and a set of example programs. Press Play, pick a program, press Run. The **Theme** button lists the themes of `BlockThemeCatalog` (Classic, the built-in Dark and High Contrast, plus whatever is registered with `BlockThemeCatalog.Register("name", theme)`, or `Register("name", () => makeTheme())` to build it only when it is chosen) and applies the one you pick; the choice is remembered for the next start. The **Console** button hides the output console to give the workspace its room (an error shows it again).

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

## Block appearance

A `BlockTheme` asset (Assets > Create > Blockly > Block Theme) sets the outline colour and width, slot and field colours, font and font size, and the shape: corner radius, round notches and tabs, round field plates. Connection shapes (`TabWidth/Height/Top`, `NotchX/Width/Depth`) set the size of the output tab and the stacking notch; layout and snapping follow them. They are clamped to fit the minimum sizes of a value hole and of a statement opening (`SlotMinHeight`, `StatementMinWidth`, also in the theme), so raise those first for bigger notches. `Rules` restyle chosen blocks (`Match` is a block type, `text_*` or `*`; override the fill, outline colour and width, label, field fill and field text); disabled blocks fade towards grey (`DisabledBlend`); selection, running-block and snap colours are in the theme. A rule with `Hat` ticked draws a cap (`HatHeight`, `HatWidth`; `HatStyle` Bump or Dome with `HatPeak` and `HatFillet`) above matching blocks that start a stack (no previous or output connection), such as a block that starts a program; no other block gets one unless a rule asks for it. Fonts: blocks have `Font`, `FontSize` and `FontStyle`; the toolbar, toolbox, console, menus and dialogs each have their own `ThemeFont` (a font asset, a size multiplier and a style; the defaults change nothing). `EdgeSmoothing` (1 by default) softens block edges across about one screen pixel in the mesh itself, so the stair-steps disappear without MSAA on every platform; set it to 0 for hard edges. The theme also holds the interface colours: panels, toolbar buttons, toolbox, flyout, console, menus and dialogs and their corner radii (`ButtonCornerRadius`, `PanelCornerRadius`; a generated sprite, no art needed) (`PlaygroundSkin` paints the Playground scene; a custom scene can add the same component). The toolbox and flyout have their own settings: which corners of each panel are rounded (`ToolboxCorners`, `ToolboxCornersWithFlyout` while a flyout is open, `FlyoutCorners`, so the two can join), the strip width, category button height, gap and icon size, a plate colour for category buttons (`CategoryPlate`, `CategoryPlateSelected`, `CategoryTextSelected`), and a title with the category name above the flyout blocks (`FlyoutTitle`, `FlyoutPadding`). Category pictures come from `BlockImages`: a category with no `Icon` uses the sprite named `category/<name>`; `<icon>@selected` replaces the icon while the category is selected; `plate/<name>` is a nine-sliced picture for the selected plate (a gradient, say). With `ToolboxCollapsible` ticked the strip can collapse to square icon buttons (`ToolboxCollapsedWidth`; a category with no icon keeps its text) and a tab on its edge toggles it (`ToggleSize`, `ToggleTop`, `ToggleColour`, `ToggleIconColour`; sprites `toolbox/expand` and `toolbox/collapse` replace the drawn arrow); With `ToolboxDeletePanel` ticked the strip becomes a delete panel while a block is dragged: a dashed border, a trash icon (the picture `toolbox/delete`, or a drawn can) and a label (`DeleteBorderColour`, `DeleteBorderWidth`, `DeleteDashLength`, `DeleteDashGap`, `DeleteIconColour`, `DeleteIconSize`, `DeleteLabel`, `DeleteLabelColour`, `DeleteLabelSize`; `ToolboxDeleteHover` fills it while the block is over it). `ToolboxView.Collapsed` does it from code and the Playground layout follows the width. The spacing (row height, side padding, gaps, field and label heights) is configurable too. Notch and tab sizes stay fixed, so snapping works the same. Defaults are Blockly's values. Assign it with `BlockStyle.Theme = theme;`; open views redraw themselves. Block fill colours come from the block definitions. The **Purple skin** sample is the full look of a design (icon rail, gradient plates, delete panel, Poppins): import it, add the `PurpleSkin` component to a scene and press Play; use it or the Block themes sample, not both. The **Block themes** sample applies a ready-made look with one component.

Pictures: `field_image` (`src`, `width`, `height`) in a block's JSON draws a sprite; a toolbox category takes `"icon"`. A name is resolved by `BlockImages`: a sprite registered with `BlockImages.Register(name, sprite)`, then `BlockImages.Resolver`, then a Sprite in a Resources folder (path without extension). Nothing is downloaded. A missing picture leaves its space empty.

## Languages

Texts go through `Loc.T("English text")`: the English text is the key and a text without a translation stays in English. English and Russian come with the package. **File > Language…** in the Playground switches the language (it is remembered); from code set `Loc.Language = "ru"`. To add a language, or the texts of your own blocks:

```csharp
Loc.Register("de", "Deutsch", new Dictionary<string, string> { { "Run", "Ausführen" }, { "repeat", "wiederhole" } });
Loc.Language = "de";
```

Labels of blocks and the texts of their dropdowns are translated (a name typed by the user, such as a procedure, is not); `{0}` marks a value in a message (`"Saved \"{0}\""`). The scene builder's toolbar buttons are translated from the text the scene was made with.

## Saving programs

`PlaygroundController` saves to `Application.persistentDataPath/programs` through `IProgramStore` (`FileProgramStore`; `MemoryProgramStore` for tests). On WebGL the files are flushed to the browser's IndexedDB so they survive a page reload. Unsaved work is backed up a moment after each edit and offered again at the next start.

File > Export and Import move a program as a plain `.json` (or legacy `.xml`) file. In a browser that is a real download and a file picker. Elsewhere there is no file dialog without a plugin, so export writes `Application.persistentDataPath/exports/<name>.json` and import lists the program files in that folder (put files there to import them); `PlaygroundController.Exchange` changes the folder.

## Platforms

| Platform | Status |
|---|---|
| WebGL | Built and run in headless Chrome (desktop and an emulated phone) and on a phone browser (Samsung Galaxy A13) |
| Android | Built (IL2CPP, ARMv7 + ARM64) and run on a Samsung Galaxy A13, layout included; a 32-bit-only phone needs ARMv7 |
| macOS | Built and run |
| Windows | Built (Mono); not run |
| iOS | Built for Xcode's simulator (arm64) and started in an iPhone simulator, layout checked on a screenshot; touch input, a real device and code signing not tried |

Command-line builds: `Unity -batchmode -executeMethod Blockly.EditorTools.BuildTool.BuildFromCommandLine -buildTarget WebGL -buildOut <dir>` (targets: WebGL, Android, iOS, Windows, macOS). For the iOS simulator add `-iosSimulator` to `-buildTarget iOS`, then build the generated Xcode project with `xcodebuild -sdk iphonesimulator ARCHS=arm64`.

### Phones

`PlaygroundLayout` fits the Playground to the screen: the content stays inside the safe area (notch, rounded corners), phones get a UI scale from the screen density so buttons are big enough for a finger, the toolbar wraps onto more rows when it does not fit on one, toolbar buttons widen to fit their labels in a larger or bolder toolbar font, and the console and flyout shrink on small screens. A desktop window keeps the original layout.

## Known limits

- In WebGL, File > Paste reads the browser's clipboard, which the browser may ask the user to allow (and refuses outside a click); then the status line says the clipboard cannot be read. The Ctrl+V of copied blocks inside the editor does not use the system clipboard.
- Only the blocks in `Runtime/Resources/Blockly` are provided; there is no code generator for other languages.

## Tests

Open **Window > General > Test Runner** and run the EditMode tests. The platform-independent tests also run with `dotnet test dev/Core.Tests` in the repository.

## License

MIT (see `LICENSE.md`): free to use, change and sell, including in commercial products, as long as the copyright line and licence text stay with copies. The standard block definitions (`StandardBlocks.json`, the toolbox, test fixtures) come from [Blockly](https://github.com/google/blockly) by Google and stay under Apache 2.0 (see `NOTICE.md`).
