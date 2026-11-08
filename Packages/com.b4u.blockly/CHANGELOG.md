# Changelog

## 0.1.0

First version.

- Block model with JSON and XML serialization; shadows, mutators, variables and procedures.
- Block layout and UGUI rendering with TextMeshPro; checked against the real Blockly.
- Interpreter for the standard blocks, with Run, Step, Stop and slow mode.
- Editing: toolbox and flyouts, drag and snap, field editing, dropdowns, variables, procedure parameters, selection and Delete, copy / cut / paste, undo / redo, zoom.
- Playground scene and controller with a File menu to save, open, delete, copy and paste programs; saved programs persist on WebGL.
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
- Toolbox and flyout look in `BlockTheme`: which panel corners are rounded (`UiCorners`), category button colours, sizes and gap, icons by category name (`category/<name>`, `@selected`, `plate/<name>`), strip width, flyout title and padding.
- A collapsible category strip: with `ToolboxCollapsible` the strip shows icons only (`ToolboxCollapsedWidth`) and a round tab (`ToggleSize`, `ToggleTop`, `ToggleColour`, `ToggleIconColour`) toggles it; `ToolboxView.Collapsed` does the same from code, and the Playground layout follows the width.
