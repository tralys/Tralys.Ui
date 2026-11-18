#!/usr/bin/env bash
set -uo pipefail

input=$(cat)
session=$(jq -r '.session_id // empty' <<<"$input" 2>/dev/null)
[[ -n "$session" ]] || exit 0

queue="${XDG_RUNTIME_DIR:-${TMPDIR:-/tmp}}/tralys-format/$session"
batch="$queue.$$"
mv -- "$queue" "$batch" 2>/dev/null || exit 0
mapfile -t edited < <(sort -u -- "$batch")
rm -f -- "$batch"

project="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"

# CLAUDE_PROJECT_DIR stays the launch checkout while the session edits a wt-<slug> worktree.
while read -r key root; do
  [[ "$key" == worktree ]] || continue
  files=()
  for f in "${edited[@]}"; do
    [[ "$f" == "$root"/* && -f "$f" ]] && files+=("$f")
  done
  [[ "${#files[@]}" -gt 0 ]] || continue
  (cd "$root" && dotnet tool restore && dotnet csharpier format "${files[@]}") >/dev/null 2>&1
done < <(git -C "$project" worktree list --porcelain 2>/dev/null)

exit 0
