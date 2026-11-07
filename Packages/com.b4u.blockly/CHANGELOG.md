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
- `BuildTool` for command-line player builds (WebGL, Android, iOS, Windows, macOS).
