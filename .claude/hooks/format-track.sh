#!/usr/bin/env bash
set -uo pipefail

input=$(cat)
session=$(jq -r '.session_id // empty' <<<"$input" 2>/dev/null)
file=$(jq -r '.tool_input.file_path // empty' <<<"$input" 2>/dev/null)

[[ "$file" == *.cs && -n "$session" ]] || exit 0

queue="${XDG_RUNTIME_DIR:-${TMPDIR:-/tmp}}/tralys-format/$session"
mkdir -p -- "${queue%/*}" && printf '%s\n' "$file" >>"$queue"
exit 0
