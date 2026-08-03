#!/bin/bash
set -euo pipefail

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <tag> [--testing]"
  exit 1
fi

TAG="$1"
IS_TESTING=false
if [ "${2:-}" == "--testing" ]; then
  IS_TESTING=true
fi

# Git Bash / MSYS often miss Windows tool installs on PATH.
resolve_tool() {
  local name="$1"
  shift

  if command -v "$name" >/dev/null 2>&1; then
    command -v "$name"
    return 0
  fi

  local candidate
  for candidate in "$@"; do
    # Git Bash often reports .exe as non-executable via -x; -f is enough to invoke.
    if [ -n "$candidate" ] && { [ -x "$candidate" ] || [ -f "$candidate" ]; }; then
      echo "$candidate"
      return 0
    fi
  done

  return 1
}

DOTNET="$(resolve_tool dotnet \
  "${DOTNET_ROOT:+$DOTNET_ROOT/dotnet}" \
  "${DOTNET_ROOT:+$DOTNET_ROOT/dotnet.exe}" \
  "$HOME/.dotnet/dotnet" \
  "$HOME/.dotnet/dotnet.exe" \
  "/c/Program Files/dotnet/dotnet.exe" \
  "/c/Program Files (x86)/dotnet/dotnet.exe"
)" || {
  echo "Error: dotnet not found. Install the .NET SDK or add it to PATH."
  exit 1
}
echo "Using dotnet: $DOTNET"

GH="$(resolve_tool gh \
  "/c/Program Files/GitHub CLI/gh.exe" \
  "$HOME/AppData/Local/Programs/GitHub CLI/gh.exe" \
  "$HOME/scoop/apps/gh/current/bin/gh.exe" \
  "/c/ProgramData/chocolatey/bin/gh.exe"
)" || {
  echo "Error: GitHub CLI (gh) not found."
  echo "Install it, then re-run (or finish a partial release manually):"
  echo "  winget install --id GitHub.cli"
  echo "  https://cli.github.com/"
  exit 1
}
echo "Using gh: $GH"

echo "Ensuring head is not detached and working tree is clean..."

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [ "$BRANCH" = "HEAD" ]; then
  echo "Error: detached HEAD; checkout a branch before releasing."
  exit 1
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Error: You have uncommitted changes. Commit or stash them before releasing."
  exit 1
fi

if ! git rev-parse --abbrev-ref --symbolic-full-name "@{u}" >/dev/null 2>&1; then
  echo "No upstream set for '$BRANCH'. Pushing to origin and setting upstream..."
  git push --set-upstream origin "$BRANCH"
fi

git fetch origin "$BRANCH" --tags

read -r REMOTE_ONLY LOCAL_ONLY < <(git rev-list --left-right --count "origin/$BRANCH...HEAD")
if [ "$REMOTE_ONLY" -gt 0 ]; then
  echo "Error: Your branch '$BRANCH' is behind origin/$BRANCH by $REMOTE_ONLY commit(s). Pull/rebase first."
  exit 1
fi

if [ "$LOCAL_ONLY" -gt 0 ]; then
  echo "Pushing $LOCAL_ONLY local commit(s) on '$BRANCH' to origin..."
  git push origin "$BRANCH"
fi


SLN_FILE=$(find . -maxdepth 1 -name "*.sln" | head -n 1 || true)
if [ -z "$SLN_FILE" ]; then
  echo "Error: no solution (.sln) file found in repo root."
  exit 1
fi

PROJECT=$(basename "$SLN_FILE" .sln)
if [ -z "$PROJECT" ]; then
  echo "Error: could not determine project name from $SLN_FILE"
  exit 1
fi

# Directory.Build.props sets Platforms=x64 → output under bin/x64/Release/
ZIP_PATH="$PROJECT/bin/x64/Release/$PROJECT/latest.zip"
ZIP_PATH_FALLBACK="$PROJECT/bin/Release/$PROJECT/latest.zip"
CSPROJ="$PROJECT/$PROJECT.csproj"

if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "Error: Tag '$TAG' already exists locally."
  exit 1
fi

if git ls-remote --tags origin | grep -q "refs/tags/$TAG"; then
  echo "Error: Tag '$TAG' already exists on remote."
  exit 1
fi

# Prefer msbuild (respects Directory.Build.props overrides); fall back to the csproj tag.
CS_VERSION=$("$DOTNET" msbuild "$CSPROJ" -nologo -getProperty:Version 2>/dev/null | tr -d '\r' | awk 'NF{line=$0} END{print line}' || true)
if [ -z "$CS_VERSION" ]; then
  CS_VERSION=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$CSPROJ" | head -n 1 | tr -d '\r' || true)
fi

if [ -z "$CS_VERSION" ]; then
  echo "Error: Could not read <Version> from $CSPROJ"
  echo "msbuild output:"
  "$DOTNET" msbuild "$CSPROJ" -nologo -getProperty:Version || true
  exit 1
fi

if [ "$CS_VERSION" != "$TAG" ]; then
  echo "csproj version ($CS_VERSION) does not match tag ($TAG). Updating..."

  CSPROJ_FILE="$PROJECT/$PROJECT.csproj"

  if grep -q "<Version>" "$CSPROJ_FILE"; then
    sed -i '0,/<Version>/{s|<Version>[^<]*</Version>|<Version>'"$TAG"'</Version>|}' "$CSPROJ_FILE"
  else
    sed -i '0,/<PropertyGroup/{s|<PropertyGroup>|<PropertyGroup>\n    <Version>'"$TAG"'</Version>|}' "$CSPROJ_FILE"
  fi

  git add "$CSPROJ_FILE"
  git commit -m "Version: $TAG"
fi

if ! grep -q "# $TAG" CHANGELOG.md; then
  echo "Error: CHANGELOG.md does not contain entry for $TAG."
  exit 1
fi

rm -f "$ZIP_PATH" "$ZIP_PATH_FALLBACK"
echo "Building project..."
"$DOTNET" build -c Release -p:Platform=x64
if [ ! -f "$ZIP_PATH" ]; then
  if [ -f "$ZIP_PATH_FALLBACK" ]; then
    ZIP_PATH="$ZIP_PATH_FALLBACK"
  else
    echo "Error: Build failed or zip not created (tried $ZIP_PATH and $ZIP_PATH_FALLBACK)."
    exit 1
  fi
fi

echo "Using zip: $ZIP_PATH"

echo "Creating annotated tag $TAG..."
git tag -a "$TAG" -m "$TAG"

echo "Pushing '$BRANCH' and tag '$TAG' to origin..."
git push origin "$BRANCH"
git push origin "$TAG"

echo "Creating GitHub release..."
EXTRA_ARGS=()
if [ "$IS_TESTING" = true ]; then
  EXTRA_ARGS+=(--prerelease)
fi
"$GH" release create "$TAG" --title "$TAG" --generate-notes "${EXTRA_ARGS[@]}"
"$GH" release upload "$TAG" "$ZIP_PATH" --clobber


echo "Updating manifest repo..."
rm -rf plugins
"$GH" repo clone OhKannaDuh/plugins
cd plugins

cd manifest-generator
npm install
manifest_output=$(npx tsx src/index.ts)
commit_message=$(echo "$manifest_output" | awk '/^Suggested commit message:/{getline; print}')
if [ -z "$commit_message" ]; then
    commit_message="Update Manifest"
fi
cd ..

git add manifest.json
if ! git diff --cached --quiet; then
    git commit -m "$commit_message"
    git push origin master
else
    echo "No manifest changes to commit."
fi

# --- Discord message ---------------------------------------------------------

cd discord-message-generator
npm install
echo "------------------------"
npx tsx src/index.ts
cd ../..

rm -rf plugins

echo "Release $TAG complete."
