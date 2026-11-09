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
- A delete panel: with `ToolboxDeletePanel` the strip turns into a dashed-border panel with a trash icon and a label while a block is dragged (`DeleteBorderColour/Width`, `DeleteDashLength/Gap`, `DeleteIconColour/Size`, `DeleteLabel`, `DeleteLabelColour/Size`; the picture `toolbox/delete` replaces the drawn trash can, `ToolboxDeleteHover` fills it while the block is over it); `WorkspaceView.BlockDragEnded`.
- A collapsible category strip: with `ToolboxCollapsible` the strip shows icons only (`ToolboxCollapsedWidth`) and a tab on its edge (`ToggleSize`, `ToggleTop`, `ToggleColour`, `ToggleIconColour`) toggles it; `ToolboxView.Collapsed` does the same from code, and the Playground layout follows the width.
