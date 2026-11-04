#!/bin/bash
# Renders the whole Playground scene after running commands. Usage: dev/shoot-playground.sh <out.png> <program> [steps|run] [slow]
#   dev/shoot-playground.sh /tmp/a.png loops 3      # three Step presses
#   dev/shoot-playground.sh /tmp/b.png loops run    # run to the end
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity}"
OUT="$1"; NAME="$2"; MODE="${3:-0}"
ARGS=()
if [ "$MODE" = "run" ]; then ARGS+=(-blocklyRun 1); else ARGS+=(-blocklySteps "$MODE"); fi
[ "$4" = "slow" ] && ARGS+=(-blocklySlow 1)
mkdir -p "$(dirname "$OUT")"
"$UNITY" -batchmode -projectPath "$ROOT" -executeMethod Blockly.EditorTools.ScreenshotTool.CapturePlaygroundFromCommandLine \
  -blocklyPlayground "$NAME" -blocklyOut "$OUT" "${ARGS[@]}" -logFile "${OUT%.png}.log"
if grep -E "error CS" "${OUT%.png}.log"; then exit 1; fi
