#!/usr/bin/env bash
set -u
codex() {
  if [ "${1:-}" = "exec" ]; then
    shift
    local args=("$@")
    local i
    for i in "${!args[@]}"; do
      if [ "$i" -gt 0 ] && [ "${args[$((i-1))]}" = "-c" ]; then args[$i]="${args[$i]//\"/}"; fi
    done
    /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\nodejs\node_global\node_modules\codex.ps1' exec -m gpt-6-astra -c model_reasoning_effort=high -c model_catalog_json=E:/ncs-codex-model-catalog.json "${args[@]}"
  fi
}
prompt=$(cat <<'PROMPT'
Task: Continue this task.
Task Name: test

You are the Initializer Agent. Create files. Do not Think X.
PROMPT
)
codex exec -c 'approval_policy="never"' -c 'sandbox_mode="danger-full-access"' --skip-git-repo-check --json "$prompt"