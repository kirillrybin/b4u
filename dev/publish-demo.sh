#!/usr/bin/env bash
# Builds the Playground for WebGL from the default branch (the Purple theme, with Classic, Dark and High Contrast in View > Theme) and publishes it to the gh-pages branch.
#   dev/publish-demo.sh            build and push to origin/gh-pages
#   dev/publish-demo.sh --dry-run  build only; the site is left in $OUT (default /tmp/b4u-demo-site)
# Needs the Unity editor with the WebGL module. UNITY can point to the editor binary.
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity}"
OUT="${OUT:-/tmp/b4u-demo-site}"
WORK="$(mktemp -d /tmp/b4u-demo-build.XXXXXX)"
BRANCH="${BRANCH:-main}"
REPO="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
DRY=0; [ "${1:-}" = "--dry-run" ] && DRY=1
trap 'rm -rf "$WORK"' EXIT

echo "== export origin/$BRANCH to $WORK"
git -C "$REPO" fetch -q origin "$BRANCH"
git -C "$REPO" archive "origin/$BRANCH" | tar -x -C "$WORK"
SHA="$(git -C "$REPO" rev-parse --short "origin/$BRANCH")"

echo "== import the TextMesh Pro essentials"
"$UNITY" -batchmode -projectPath "$WORK" -executeMethod Blockly.EditorTools.TmpSetup.ImportEssentialsAndExit -logFile "$WORK/tmp.log"
echo "== build for WebGL"
rm -rf "$OUT"
"$UNITY" -batchmode -projectPath "$WORK" -executeMethod Blockly.EditorTools.BuildTool.BuildFromCommandLine -buildTarget WebGL -buildOut "$OUT" -logFile "$WORK/build.log"
touch "$OUT/.nojekyll"
echo "== site ready in $OUT ($(du -sh "$OUT" | cut -f1))"
[ "$DRY" = 1 ] && exit 0

echo "== publish to gh-pages"
SITE="$(mktemp -d /tmp/b4u-demo-pages.XXXXXX)"
if git -C "$REPO" ls-remote --exit-code --heads origin gh-pages >/dev/null 2>&1; then
  git clone -q --depth 1 --branch gh-pages "$(git -C "$REPO" remote get-url origin)" "$SITE"
else
  git -C "$SITE" init -q -b gh-pages
  git -C "$SITE" remote add origin "$(git -C "$REPO" remote get-url origin)"
fi
git -C "$SITE" config user.name "$(git -C "$REPO" config user.name)"
git -C "$SITE" config user.email "$(git -C "$REPO" config user.email)"     # the repository's own (noreply) address, not the global one
find "$SITE" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
cp -R "$OUT"/. "$SITE"/
git -C "$SITE" add -A
git -C "$SITE" commit -q -m "chore: build the demo from $SHA"
git -C "$SITE" push -q origin gh-pages
rm -rf "$SITE"
echo "== pushed; enable Pages on the gh-pages branch (Settings > Pages) if it is not on yet"
