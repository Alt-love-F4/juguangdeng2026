#!/bin/sh
set -eu

repository_root=$(git rev-parse --show-toplevel)
cd "$repository_root"

font_changes=$(git -c core.quotepath=false diff --cached --name-only \
    --diff-filter=ACDMRTUXB -- \
    ':(glob,icase)Assets/Fonts/**/*.asset' \
    ':(glob,icase)Assets/Resources/Fonts/**/*.asset' \
    ':(glob,icase)Assets/TextMesh Pro/Resources/Fonts & Materials/**/*.asset')

if [ -z "$font_changes" ]; then
    exit 0
fi

if [ "${UNITY_ALLOW_FONT_ASSET_CHANGES:-}" = "1" ]; then
    printf '%s\n' 'Explicit font asset changes allowed:' "$font_changes"
    exit 0
fi

printf '%s\n' \
    'Commit blocked: staged font assets may contain generated Unity/TMP caches.' \
    "$font_changes" \
    '' \
    'No files were modified or unstaged.' \
    'Review the staged diff. Unstage cache-only changes, not the whole Fonts directory.' \
    'For an intentional font update or a reviewed merge, set UNITY_ALLOW_FONT_ASSET_CHANGES=1 for that commit.' \
    'See Docs/Collaboration/FontAssetGitWorkflow.md.' >&2
exit 1
