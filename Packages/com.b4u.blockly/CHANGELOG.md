# Changelog

## 0.1.0

First version.

- Block model with JSON and XML serialization; shadows, mutators, variables and procedures.
- Block layout and UGUI rendering with TextMeshPro; checked against the real Blockly.
- Interpreter for the standard blocks, with Run, Step, Stop and slow mode.
- Editing: toolbox and flyouts, drag and snap, field editing, dropdowns, variables, procedure parameters, selection and Delete, copy / cut / paste, undo / redo, zoom.
- Playground scene and controller with a File menu to save, open, delete, copy and paste programs; saved programs persist on WebGL.
- Block menu: right click on a block, or hold a press on a touch screen, for Duplicate, Copy, Cut, Disable / Enable block, Collapse / Expand block and Delete. A disabled block (and what is inside it) is grey and skipped when the program runs.
- README: a Compatibility section (Unity versions, render pipelines, input handling, platforms).
- Colour field and blocks: `field_colour` shows a swatch and opens a palette of 70 colours (`Colour.Palette`) under it; blocks `colour_picker`, `colour_random`, `colour_rgb` (0-100 per channel) and `colour_blend`, a Colour category in the toolbox; colours are `"#rrggbb"` text values for the interpreter.
- Collapsed blocks: a collapsed block is drawn as one line with a summary of what is inside (up to 40 characters); the contents stay in the program, run as before and are saved (`collapsed`).
- Localization: `Loc` translates the toolbar, menus, dialogs, messages, category names and the labels of the blocks; English and Russian come with the package, `Loc.Register` adds a language. The File menu has Language…, the choice is remembered, and on the first start the language of the device is used.
- Licensed under MIT (`LICENSE.md`, `NOTICE.md`).
- Backup of unsaved work, offered again at the next start; File > Export / Import of program files; File > Paste and Copy use the browser clipboard on WebGL.
- `IBlockPackage` and `StandardBlockSet.AddPackage` for custom blocks, with a sample.
- `BuildTool` for command-line player builds (WebGL, Android, iOS, iOS simulator, Windows, macOS).
- The Playground fits phone screens: safe area, density-based scale, toolbar wrapping onto several rows (`PlaygroundLayout`).
- `BlockTheme` and `BlockStyle.Theme` to restyle blocks (outline, fields, font), with a sample.
- `field_image` in blocks and an `icon` for toolbox categories, resolved through `BlockImages`.
- Rounded block shapes in `BlockTheme` (corner radius, round notches and tabs, round field plates) through `BlockGeometry`.
- Block spacing (row height, padding, gaps, field and label heights) in `BlockTheme`.
- Interface colours in `BlockTheme`, applied to the Playground by `PlaygroundSkin`, the toolbox, menus and dialogs.
- Rounded buttons and panels (`ButtonCornerRadius`, `PanelCornerRadius`) through a generated nine-sliced sprite (`UiShape`).
- Soft block edges (`EdgeSmoothing`) built into the mesh, matched to the canvas scale and zoom; smoother joins on rounded outlines.
- Fonts per part of the interface (`ThemeFont`: font asset, size scale, style) and a block font style in `BlockTheme`.
- Theme: size of the notch and the output tab, per-block style rules (`BlockStyleRule`), disabled-block look, and selection, highlight and snap colours.
- Hats: a cap above stack-starting blocks chosen by a style rule (`Hat`, `HatHeight`, `HatWidth`).
- A dome-shaped hat (`HatStyle`, `HatPeak`, `HatFillet`) next to the bump.
- Theme: minimum sizes of statement openings and value holes, so notches and tabs can be bigger.
- Toolbar buttons of the Playground widen to fit their label in the theme's toolbar font; the buttons after them and the title move along.
- A "Console: on/off" button in the Playground toolbar hides and shows the console (`PlaygroundController.ToggleConsole`, `ConsoleView.Visible`); the workspace takes its room, lines are kept while it is hidden, and an error shows it again.
- `BlockThemeCatalog`: a list of ready-made themes (`Register`, `Select`, `Names`, `CurrentName`; Classic is always first), and a "Theme: <name>" button in the Playground toolbar that opens it and switches the look. The package ships two ready-made themes, Dark and High Contrast (`BuiltInThemes`), and the Block themes sample registers Candy. The theme chosen in the menu is remembered (PlayerPrefs, `BlockThemeCatalog.Remember` / `RestoreSaved`) and applied at the next start.
- Toolbox and flyout look in `BlockTheme`: which panel corners are rounded (`UiCorners`), category button colours, sizes and gap, icons by category name (`category/<name>`, `@selected`, `plate/<name>`), strip width, flyout title and padding.
- Sample **Figma skin**: the look of the design as one component (`FigmaSkin`, with `FigmaIconBlocks`, the icons and Poppins).
- A delete panel: with `ToolboxDeletePanel` the strip turns into a dashed-border panel with a trash icon and a label while a block is dragged (`DeleteBorderColour/Width`, `DeleteDashLength/Gap`, `DeleteIconColour/Size`, `DeleteLabel`, `DeleteLabelColour/Size`; the picture `toolbox/delete` replaces the drawn trash can, `ToolboxDeleteHover` fills it while the block is over it); `WorkspaceView.BlockDragEnded`.
- A collapsible category strip: with `ToolboxCollapsible` the strip shows icons only (`ToolboxCollapsedWidth`) and a tab on its edge (`ToggleSize`, `ToggleTop`, `ToggleColour`, `ToggleIconColour`) toggles it; `ToolboxView.Collapsed` does the same from code, and the Playground layout follows the width.
- Code export (Core): `CodeLanguage.JavaScript.Generate(workspace)` turns a program into JavaScript for all 64 standard blocks, in the form Blockly's own generator writes (the same text for most test programs). Custom blocks get code with `CodeLanguage.JavaScript.Value(...)` / `.Statement(...)`; `CodeOptions.Print` picks the print function. Tests run the exported code in Node and compare its output with real Blockly's.
- Code panel: the Playground's Code button opens `CodeView` over the right half of the workspace, with the program as JavaScript kept up to date after every edit, a language button, Copy and Close. It follows the theme and the language. The toolbar buttons were re-spaced to make room for it.
- Python export: `CodeLanguage.Python.Generate(workspace)` for all 64 standard blocks (indents, `pass` for empty bodies, `global` in procedures, helper functions and imports first). Where Python's operators would differ from the interpreter (rounding half up, the sign of a remainder) the code uses `math.floor` / `math.fmod`. The Code panel has a language list (JavaScript, Python) and remembers the choice. Tests run the exported Python in `python3` against the output of real Blockly.
- Code panel: a Save button writes the code to a file named after the program (`.js`, `.py`; a download in a browser, otherwise a file in the exchange folder). `CodeLanguage.FileExtension` and `FileExchange.Export(name, text, extension)` are the pieces.
- Block search: a Search button at the end of the category strip opens a flyout with a text field; the blocks with every typed word on them (in the language in use or in English), in a menu of the block, or in the name of their category or type are listed, up to 30, and can be dragged out like any other block. `ToolboxSearch.Find` (Core) and `ToolboxView.Search(query)` are the pieces.
- Workspace grid: the theme fields `GridSpacing` (0 = no grid, the default), `GridColour`, `GridDot` and `GridSnap` draw a dotted grid behind the blocks (`WorkspaceGrid`, only the points in view, exact under panning and zoom); with `GridSnap` a dragged block moves in steps of the grid and a block dropped on its own lands on it, while one that snaps to another block joins it as before (`BlockDragger.GridSpacing`). The built-in Dark theme has a 24-unit grid.
- Code panel on a phone: in a workspace area narrower than 640 units the panel takes the whole width, and the four header buttons share it (their labels shrink with them) instead of covering each other.
- Category list on a phone: when the category buttons do not all fit the strip (a phone held upright, the Search button at the end) the list scrolls with a drag or the wheel, and choosing a category scrolls its button into view (`ToolboxView.ScrollCategories`).
- Code panel on a narrow screen: the panel is drawn above the toolbox (its own nested canvas) and covers the whole width, so the four header buttons keep readable labels.
- Console: a drag (or the mouse wheel) over it scrolls the output; the text is not a raycast target, so the console takes the drag itself.
- Android build (`BuildTool`) is landscape only, both directions, never portrait.
- Comments on blocks: "Add comment" in the block menu puts a "?" icon on the block; a click on it opens a text bubble beside the block (kept inside the visible area), saved with the program (Blockly JSON `icons.comment` and XML `<comment>`), and written into the exported JavaScript / Python as `//` / `#` lines. `WorkspaceView.CommentsEnabled` turns the feature off (the comments stay in the program); `CodeOptions.Comments` turns the export lines off.
- Mutators of if, join and list blocks: a gear icon on `controls_if`, `text_join` and `lists_create_with` opens a bubble with buttons to add an else if / else / item and to move or remove a part; the blocks plugged into a part travel with it, and the blocks of a removed part stay in the workspace. `MutatorEditing` (Core) does the reshaping; `WorkspaceView.MutatorsEnabled` turns the gear and the bubble off.
- Warnings on blocks: a yellow "!" icon on a block that cannot do its job (call of an unknown procedure, unknown variable, an empty input of `variables_set`, `controls_if`, `controls_whileUntil`, `logic_negate`, `procedures_ifreturn` or `procedures_defreturn`, a value block plugged into nothing); a click opens a bubble with the text. `BlockWarnings.Check` finds them, `BlockWarnings.RequiredInputs` lists the inputs that must be filled, `WorkspaceView.WarningsEnabled` turns the feature off, `BlockLayouter.ShowWarnings` is off by default. Not saved in the file, not shown in the flyout.
- Notes on the workspace (Blockly's workspace comments): right click, or hold a press, on the empty background and choose "Add note"; a yellow sheet lies under the blocks, is moved by its header, collapses to the header and is deleted with the cross. Saved in the JSON file as `workspaceComments` (and read from Blockly XML `<comment>`), never run or exported. `Workspace.Comments`, `WorkspaceCommentEditing`, `WorkspaceView.NotesEnabled` turns the feature off. Notes cannot be resized or copied yet.
