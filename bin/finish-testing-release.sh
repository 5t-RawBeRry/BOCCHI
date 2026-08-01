#!/bin/bash
set -euo pipefail
cd "/d/Dev Drive/BOCCHI"
rm -rf plugins
gh repo clone OhKannaDuh/plugins
cd plugins

cd manifest-generator
npm install
manifest_output=$(npx tsx src/index.ts)
echo "$manifest_output"
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

cd discord-message-generator
npm install
echo "------------------------"
npx tsx src/index.ts
cd ../..

rm -rf plugins
echo "Plugins manifest update complete."
