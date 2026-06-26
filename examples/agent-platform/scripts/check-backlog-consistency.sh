#!/usr/bin/env bash
# Verify cross-plan backlog consistency:
#   1. Each src: reference in backlog.md points to an existing plan directory.
#   2. No completed items (lines with [x]) remain in backlog.md.
# Exits 1 on hard failure; 0 on success.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
backlog="${repo_root}/docs/plans/backlog.md"

if [[ ! -f "${backlog}" ]]; then
  echo "[backlog-consistency] docs/plans/backlog.md not found — skipping." >&2
  exit 0
fi

failures=0

# Hard failure: completed items (ticked checkboxes) must not remain in the backlog.
ticked=$(grep -n '^\s*- \[x\]' "${backlog}" || true)
if [[ -n "${ticked}" ]]; then
  echo "[backlog-consistency] FAIL: completed items found in backlog.md (remove them — the backlog holds open items only):" >&2
  echo "${ticked}" >&2
  failures=$((failures + 1))
fi

# Hard failure: src: references point to non-existent plan directories.
# Skip HTML comment lines; strip leading whitespace, backticks, and trailing punctuation.
while IFS= read -r line; do
  # Skip HTML comment lines
  if echo "${line}" | grep -q '<!--'; then continue; fi
  # Extract the path after "src: "
  src_path="${line#*src: }"
  # Strip leading/trailing backticks, whitespace, and angle brackets
  src_path="${src_path//\`/}"
  src_path="${src_path//>/}"
  src_path="${src_path%%[ ,)]*}"   # trim trailing whitespace/comma/paren
  src_path="${src_path#"${src_path%%[! ]*}"}"  # ltrim whitespace
  if [[ -z "${src_path}" ]]; then continue; fi
  # Skip template placeholders
  if [[ "${src_path}" == *"NN"* ]]; then continue; fi
  full_path="${repo_root}/docs/plans/${src_path}"
  if [[ ! -d "${full_path}" ]]; then
    echo "[backlog-consistency] FAIL: src: reference not found: docs/plans/${src_path}" >&2
    failures=$((failures + 1))
  fi
done < <(grep 'src: ' "${backlog}" || true)

if [[ "${failures}" -gt 0 ]]; then
  exit 1
fi

echo "[backlog-consistency] OK"
exit 0
