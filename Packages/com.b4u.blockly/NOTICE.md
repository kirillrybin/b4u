Blockly for Unity
Copyright (c) 2026 Kirill Rybin, MIT License (see LICENSE.md).

Third-party material

Blockly (https://github.com/google/blockly), Copyright 2011 Google LLC, licensed under the Apache License, Version 2.0
(LICENSE-Blockly-Apache-2.0.md).

These files are generated from the block definitions of Blockly (dev/blockly-ref/gen-defs.js) and stay under the Apache License 2.0:
  - Runtime/Resources/Blockly/StandardBlocks.json
  - the reference data in Tests/EditMode/Fixtures, produced by running the real Blockly (dev/blockly-ref)
Runtime/Resources/Blockly/StandardToolbox.json follows the category layout of Blockly's default toolbox, so it is kept under the same licence to be safe.
The colour palette of the colour field (Runtime/Core/Definitions/BlockDefinition.cs, `Colour.Palette`) is the one of Blockly's colour field. The colour blocks
(`colour_picker`, `colour_random`, `colour_rgb`, `colour_blend`) are written by hand after the ones Blockly had in its core, whose block list now lives in a plugin.
Changes: reduced to the blocks this package supports, with mutators replaced by the package's own, and re-saved as JSON.

The C# code is written for this package and is MIT; it follows the behaviour, the JSON/XML formats and the rendering rules of Blockly.
