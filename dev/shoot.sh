#!/bin/bash
# Renders Blockly fixture programs through the real UGUI view. Usage: dev/shoot.sh <out-dir> <name> [width height scale] ...
# Needs a graphics device, so it runs Unity without -nographics.
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity}"
OUT="$1"; shift
mkdir -p "$OUT"
NAME="$1"; W="${2:-1000}"; H="${3:-800}"; SCALE="${4:-1.4}"
"$UNITY" -batchmode -projectPath "$ROOT" -executeMethod Blockly.EditorTools.ScreenshotTool.CaptureFromCommandLine \
  -blocklyProgram "$ROOT/Packages/com.b4u.blockly/Tests/EditMode/Fixtures/programs/$NAME.json" \
  -blocklyOut "$OUT/$NAME.png" -blocklyWidth "$W" -blocklyHeight "$H" -blocklyScale "$SCALE" -logFile "$OUT/$NAME.log"
grep -E "error CS" "$OUT/$NAME.log" && exit 1 || true
