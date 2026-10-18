#!/usr/bin/env bash
set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_ROOT"
export PROJECT_ROOT
exec > "$PROJECT_ROOT/.autonomous/overnight-runner.log" 2>&1
echo "RUNNER_STARTED $(date -u +%Y-%m-%dT%H:%M:%SZ)"
export CODEX_PLUGIN_ROOT='/mnt/c/Users/hp/.agents'
export PATH='/usr/bin:/bin'
codex() {
  if [ "${1:-}" = "exec" ]; then
    shift
    local args=("$@")
    local prompt_index=$((${#args[@]} - 1))
    local prompt="${args[$prompt_index]}"
    unset "args[$prompt_index]"
    local i
    for i in "${!args[@]}"; do
      args[$i]="${args[$i]//\"/}"
    done
    printf '%s' "$prompt" | /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:/nodejs/node_global/node_modules/codex.ps1' exec -m gpt-6.1-sol -c model_reasoning_effort=medium -c model_catalog_json=E:/ncs-codex-model-catalog.json "${args[@]}"
  else
    /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:/nodejs/node_global/node_modules/codex.ps1' "$@"
  fi
}
export -f codex
TASK_DESC=$(cat <<'TASK'
Continue the existing Unity project NCS integration work unattended. The primary outcome is a playable build path using all existing real NCS content and available presentation assets, with formal triggers, date flow, loading, presentation, cancellation, retry, save restore, and completion settlement wired and verified. Start by reading current repository state and existing NCS reports before editing. Preserve all user and Locus changes; never reset, revert, or overwrite unrelated work. Do not delete the Revolution mainline content or enable EV_REVOLUTION. Remove or ignore only explicitly identified NPC ABC test junk. Do not invent new story text when the narrative is genuinely absent. When written narrative exists but an integration detail is missing, make a conservative decision based on existing project patterns and implement it so the content is playable; record the decision, assumption, and affected event in Docs/NcsFormalDependencies.md or the autonomous progress log. Do not ask the sleeping user questions. Use Assets/Scenes/PresentationDebugScene.unity for presentation checks. Each session must select the highest value unfinished task, make a focused change, regenerate Luban when configuration changes, validate configuration, compile Unity, run focused tests, and update .autonomous/ncs-formal-integration-overnight/task_list.md and progress.md with evidence. If Unity or an external dependency is unavailable, record the exact blocker and continue with independent static, config, and test work. Keep working through auto continuation until all safely automatable integration tasks are complete or the remaining items are explicitly documented as human only acceptance steps.
TASK
)
/bin/bash <(sed 's/\r//' '/mnt/c/Users/hp/.agents/skills/autonomous-skill/scripts/run-session.sh') --task-name ncs-formal-integration-overnight --network "$TASK_DESC"