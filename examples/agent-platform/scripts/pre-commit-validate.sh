#!/usr/bin/env bash
# Multi-gate pre-commit validation:
#   1. AgentSync drift check (if .agent/ or projections staged)
#   2. Backlog consistency check (if docs/plans/ staged)
#   3. Build gate (if src/ or tests/ staged)
#   4. Test gate (if src/ or tests/ staged)
#   5. License check (if *.csproj or Directory.Packages.props staged with new packages)
#
# Set SKIP_REPO_GUARDS=1 to bypass all checks (e.g. for a merge/fixup commit).
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

if [[ "${SKIP_REPO_GUARDS:-0}" == "1" ]]; then
  echo "[pre-commit] SKIP_REPO_GUARDS=1 — skipping all checks."
  exit 0
fi

staged=$(git diff --cached --name-only --diff-filter=ACMR 2>/dev/null || true)

# ── 1. AgentSync drift gate ─────────────────────────────────────────────────
needs_drift_check=0
if echo "${staged}" | grep -qE '^\.agent/|^AGENTS\.md|^CLAUDE\.md|^\.gemini/|^\.claude/skills/'; then
  needs_drift_check=1
fi

if [[ "${needs_drift_check}" -eq 1 ]]; then
  echo "[pre-commit] Checking AgentSync drift..."
  if dotnet tool run agent --version >/dev/null 2>&1; then
    dotnet agent status --fail-on-drift
  elif command -v agent >/dev/null 2>&1; then
    agent status --fail-on-drift
  else
    echo "[pre-commit] WARNING: AgentSync not found. Run: dotnet tool restore" >&2
    # Don't hard-fail on missing tool here; pre-push hook enforces it.
  fi
fi

# ── 2. Backlog consistency gate ──────────────────────────────────────────────
if echo "${staged}" | grep -qE '^docs/plans/'; then
  echo "[pre-commit] Checking backlog consistency..."
  "${repo_root}/scripts/check-backlog-consistency.sh"
fi

# ── 3 & 4. Build + test gates ───────────────────────────────────────────────
needs_build=0
needs_test=0

if echo "${staged}" | grep -qE '^(src|tests)/|\.cs$|\.csproj$|\.slnx?$|Directory\.Build\.props|Directory\.Packages\.props|global\.json'; then
  needs_build=1
fi
if echo "${staged}" | grep -qE '^(src|tests)/'; then
  needs_test=1
fi

if [[ "${needs_build}" -eq 1 ]]; then
  # Auto-detect solution file
  sln_file=$(find "${repo_root}" -maxdepth 1 \( -name '*.slnx' -o -name '*.sln' \) | sort | head -1)
  if [[ -z "${sln_file}" ]]; then
    echo "[pre-commit] No solution file found at repo root — skipping build gate." >&2
  else
    echo "[pre-commit] Building $(basename "${sln_file}")..."
    build_out=$(dotnet build "${sln_file}" -v q -clp:ErrorsOnly -nologo 2>&1) || {
      echo "[pre-commit] BUILD FAILED:" >&2
      echo "${build_out}" | tail -40 >&2
      exit 1
    }
    echo "[pre-commit] Build OK."
  fi
fi

if [[ "${needs_test}" -eq 1 ]]; then
  echo "[pre-commit] Running unit tests..."
  test_failures=0
  while IFS= read -r proj; do
    result=$(dotnet test "${proj}" --no-build -v q --nologo 2>&1) || {
      echo "[pre-commit] TESTS FAILED: ${proj}" >&2
      echo "${result}" | grep -E '(FAIL|Error|Exception)' | head -10 >&2
      test_failures=$((test_failures + 1))
    }
  done < <(find "${repo_root}/tests" -name '*.Tests.csproj' 2>/dev/null)
  if [[ "${test_failures}" -gt 0 ]]; then
    echo "[pre-commit] ${test_failures} test project(s) failed." >&2
    exit 1
  fi
  echo "[pre-commit] Tests OK."
fi

# ── 5. License gate ──────────────────────────────────────────────────────────
needs_license=0
if echo "${staged}" | grep -qE 'Directory\.Packages\.props|oss-license-policy\.md|check-nuget-licenses\.sh'; then
  needs_license=1
elif echo "${staged}" | grep -qE '\.csproj$'; then
  # Check if any csproj diff adds a PackageReference
  if git diff --cached -- '*.csproj' | grep -q '+.*PackageReference'; then
    needs_license=1
  fi
fi

if [[ "${needs_license}" -eq 1 ]]; then
  echo "[pre-commit] Checking NuGet licenses..."
  "${repo_root}/scripts/check-nuget-licenses.sh" || {
    echo "[pre-commit] LICENSE CHECK FAILED. Review docs/governance/oss-license-policy.md." >&2
    exit 1
  }
fi

echo "[pre-commit] All gates passed."
exit 0
