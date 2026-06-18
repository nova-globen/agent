#!/usr/bin/env bash
# Release smoke checks (no GitHub required):
#   1. archive naming + runtime list are consistent between the workflow and docs
#   2. install.sh maps OS/architecture to the documented runtime identifiers
#   3. both 'agent' and 'git-agent' publish, and 'git-agent' delegates to the same CLI
#
# Usage: scripts/release-smoke.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

pass() { echo "PASS: $*"; }
fail() { echo "FAIL: $*" >&2; exit 1; }

workflow=".github/workflows/release.yml"
install_sh="scripts/install.sh"

# 1. Runtimes the release workflow builds.
expected_runtimes="linux-x64 linux-arm64 osx-x64 osx-arm64 win-x64"
for rid in $expected_runtimes; do
  grep -q "$rid" "$workflow" || fail "release workflow does not mention runtime '$rid'."
done
pass "release workflow references all expected runtimes."

# Archive naming convention is used by the workflow.
grep -q 'agent-sync-${tag}-${rid}' "$workflow" || fail "release workflow archive naming pattern changed."
grep -q 'checksums.txt' "$workflow" || fail "release workflow does not produce checksums.txt."
pass "archive naming + checksums present in workflow."

# 2. install.sh OS/architecture mapping.
for token in 'linux' 'osx' 'x64' 'arm64' '.tar.gz'; do
  grep -q "$token" "$install_sh" || fail "install.sh missing expected mapping token '$token'."
done
grep -q 'AGENT_SYNC_INSTALL_DIR' "$install_sh" || fail "install.sh missing AGENT_SYNC_INSTALL_DIR override."
grep -q '.agent-sync/bin' "$install_sh" || fail "install.sh missing default install dir."
pass "install.sh OS/architecture mapping and overrides present."
grep -q 'win-x64' "scripts/install.ps1" || fail "install.ps1 missing win-x64 mapping."
pass "install.ps1 references win-x64."

# 3. Publish both executables for the host runtime and confirm delegation.
host_rid="$(dotnet --info 2>/dev/null | sed -n 's/^[[:space:]]*RID:[[:space:]]*//p' | head -1)"
[ -n "$host_rid" ] || host_rid="linux-x64"
echo "Publishing self-contained binaries for $host_rid ..."

out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

for proj in src/AgentSync.Cli src/AgentSync.GitAgent; do
  dotnet publish "$proj" -c Release -r "$host_rid" --self-contained true \
    -p:PublishSingleFile=true -p:DebugType=none -o "$out" >/dev/null
done

agent_bin="$out/agent"
gitagent_bin="$out/git-agent"
[ -x "$agent_bin" ] || fail "published output missing executable 'agent'."
[ -x "$gitagent_bin" ] || fail "published output missing executable 'git-agent'."
pass "both 'agent' and 'git-agent' executables were produced."

agent_ver="$("$agent_bin" --version)"
gitagent_ver="$("$gitagent_bin" --version)"
[ "$agent_ver" = "$gitagent_ver" ] || fail "git-agent --version ('$gitagent_ver') != agent --version ('$agent_ver')."
pass "git-agent delegates to the same CLI ($agent_ver)."

echo "All release smoke checks passed."
