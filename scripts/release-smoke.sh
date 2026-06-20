#!/usr/bin/env bash
# Release smoke checks (no GitHub required):
#   1. archive naming + runtime list are consistent between the workflow and docs
#   2. install.sh maps OS/architecture to the documented runtime identifiers
#   3. both 'agent' and 'git-agent' publish, and 'git-agent' delegates to the same CLI
#   4. the optional UI ('agent-sync-ui') packages separately, runs, and serves /healthz
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

# 4. Optional UI ('agent-sync-ui') packaging — separate from the CLI artifacts.

# 4a. The release workflow publishes the UI project separately with its own artifact names.
grep -q 'AgentSync.Ui.Web' "$workflow" || fail "release workflow does not publish AgentSync.Ui.Web."
grep -q 'agent-sync-ui-${tag}-${rid}' "$workflow" || fail "release workflow UI artifact naming pattern missing."
grep -q 'release-ui:' "$workflow" || fail "release workflow has no separate UI job."
# The UI job must depend on the CLI release job so a UI failure cannot block the CLI release.
grep -q 'needs: release' "$workflow" || fail "release-ui job must 'needs: release' (so it cannot block the CLI release)."
pass "release workflow publishes the UI as separate, optional artifacts."

# 4b. The CLI 'dotnet tool' packages must stay CLI-only: the CLI 'release' job's pack loop
# lists exactly the two CLI tool projects and never the UI project.
grep -Eq 'for proj in src/AgentSync.Cli src/AgentSync.GitAgent' "$workflow" \
  || fail "release workflow CLI pack/publish project list changed unexpectedly."
if grep -E 'for proj in ' "$workflow" | grep -q 'Ui.Web'; then
  fail "the CLI dotnet tool pack loop must not include AgentSync.Ui.Web."
fi
# The UI ships as its own separate 'AgentSync.Ui' .NET tool, packed in the release-ui job.
grep -Eq 'dotnet pack +src/AgentSync.Ui.Web' "$workflow" \
  || fail "release-ui job does not pack the AgentSync.Ui .NET tool."
grep -q 'PackAsTool' src/AgentSync.Ui.Web/AgentSync.Ui.Web.csproj \
  && grep -q '<PackageId>AgentSync.Ui</PackageId>' src/AgentSync.Ui.Web/AgentSync.Ui.Web.csproj \
  || fail "AgentSync.Ui.Web is not configured as the 'AgentSync.Ui' .NET tool."
pass "CLI tool packages stay CLI-only; the UI ships as the separate AgentSync.Ui tool."

# 4b-ii. The host serves static assets via MapStaticAssets (not UseStaticFiles) so the
# published/tool-installed UI's CSS/JS load instead of 404ing.
grep -q 'MapStaticAssets' src/AgentSync.Ui.Web/Program.cs \
  || fail "UI host must serve static assets with MapStaticAssets (CSS/JS 404 otherwise)."
pass "UI host serves static assets with MapStaticAssets."

# 4c. Publish the UI for the host runtime and confirm the shippable shape.
echo "Publishing self-contained UI for $host_rid ..."
ui_out="$(mktemp -d)"
trap 'rm -rf "$out" "$ui_out"' EXIT
dotnet publish src/AgentSync.Ui.Web/AgentSync.Ui.Web.csproj -c Release -r "$host_rid" \
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -o "$ui_out" >/dev/null

ui_exe="$ui_out/agent-sync-ui"
[ -x "$ui_exe" ] || ui_exe="$ui_out/agent-sync-ui.exe"
[ -e "$ui_exe" ] || fail "published UI output missing executable 'agent-sync-ui'."
[ -d "$ui_out/wwwroot" ] || fail "published UI output missing 'wwwroot' (static web assets must ship with it)."
ls "$ui_out"/*.staticwebassets.endpoints.json >/dev/null 2>&1 \
  || fail "published UI output missing the static web assets manifest."
pass "agent-sync-ui publishes as a self-contained executable with its static web assets."

# 4d. Invalid args exit non-zero with usage (no browser, no repo needed).
if "$ui_exe" --bogus >/dev/null 2>"$ui_out/err.txt"; then
  fail "agent-sync-ui --bogus should exit non-zero."
fi
grep -qi 'Usage: agent-sync-ui' "$ui_out/err.txt" || fail "agent-sync-ui did not print usage on bad args."
pass "agent-sync-ui rejects invalid args with usage text."

# 4e. Best-effort live readiness check (/healthz) — never a browser.
if command -v curl >/dev/null 2>&1; then
  ui_repo="$(mktemp -d)"; ( cd "$ui_repo" && "$agent_bin" init >/dev/null 2>&1 || true )
  ui_token="smoke-$$"
  ui_port=""
  for p in $(seq 41000 41050); do
    if ! (exec 3<>"/dev/tcp/127.0.0.1/$p") 2>/dev/null; then ui_port="$p"; break; fi
    exec 3>&- 2>/dev/null || true
  done
  [ -n "$ui_port" ] || ui_port=41099
  # Launch with the working directory set to the managed repo (NOT the UI's install dir) —
  # this is what `agent ui` does, and it is the case that broke static assets: the content
  # root must come from the executable's base directory, not the CWD, or MapStaticAssets
  # finds no wwwroot/manifest and serves empty 200s. Running from $ui_repo guards that.
  ( cd "$ui_repo" && exec "$ui_exe" --repo "$ui_repo" --port "$ui_port" --token "$ui_token" --no-open >/dev/null 2>&1 ) &
  ui_pid=$!
  ok=""
  for _ in $(seq 1 40); do
    code="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:${ui_port}/healthz" 2>/dev/null || true)"
    [ "$code" = "200" ] && { ok=1; break; }
    sleep 0.25
  done
  unauth="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:${ui_port}/" 2>/dev/null || true)"
  # Exchange the token for the session cookie, then confirm a static asset serves with a
  # NON-EMPTY body. Checking the status code alone is not enough: the content-root bug
  # returned 200 with Content-Length 0 (no CSS/JS), which a 200-only check would pass. We
  # assert both the code and the downloaded size (blazor.web.js is ~200 KB).
  asset="000"; asset_size="0"; styles_link=""
  if [ "$ok" = "1" ]; then
    cj="$ui_out/cookies.txt"
    curl -s -c "$cj" -o /dev/null "http://127.0.0.1:${ui_port}/?token=${ui_token}" 2>/dev/null || true
    read -r asset asset_size <<EOF
$(curl -s -b "$cj" -o /dev/null -w '%{http_code} %{size_download}' "http://127.0.0.1:${ui_port}/_framework/blazor.web.js" 2>/dev/null || echo "000 0")
EOF
    # The rendered page must link the CSS-isolation bundle (AgentSync.Ui.styles.css), which
    # @imports the FluentUI component CSS. Without that link the FluentUI components render
    # as bare, unstyled HTML even though reboot.css and the web-component JS load.
    # The bundle name carries a fingerprint (e.g. AgentSync.Ui.fr5tct7ywz.styles.css), so
    # match the prefix and the .styles.css suffix with the hash in between.
    styles_link="$(curl -s -b "$cj" "http://127.0.0.1:${ui_port}/" 2>/dev/null | grep -oE 'AgentSync\.Ui[^"]*styles\.css' | head -1 || true)"
  fi
  kill "$ui_pid" >/dev/null 2>&1 || true
  rm -rf "$ui_repo"
  [ "$ok" = "1" ] || fail "agent-sync-ui did not answer /healthz with 200."
  [ "$unauth" = "401" ] || fail "agent-sync-ui served '/' without a token (expected 401, got $unauth)."
  [ "$asset" = "200" ] || fail "agent-sync-ui did not serve /_framework/blazor.web.js (expected 200, got $asset) — static assets are 404ing."
  [ -n "$styles_link" ] || fail "the rendered page does not link AgentSync.Ui.styles.css — the FluentUI component CSS bundle is missing, so the UI renders unstyled."
  [ "${asset_size:-0}" -gt 1000 ] || fail "agent-sync-ui served /_framework/blazor.web.js with an empty/short body (${asset_size} bytes) — the content root is wrong, so MapStaticAssets cannot read the asset."
  pass "agent-sync-ui serves /healthz (200), gates '/' without a token (401), serves static assets (200, ${asset_size} bytes), and links the FluentUI CSS bundle."
else
  echo "SKIP: curl not found; skipped the live /healthz check."
fi

echo "All release smoke checks passed."
