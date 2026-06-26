#!/usr/bin/env bash
# Audit NuGet package licenses against the project allowlist.
# Reads PackageReference from *.csproj files, queries nuget.org, checks SPDX expression.
set -euo pipefail

ALLOWLIST=(
  "MIT"
  "Apache-2.0"
  "BSD-2-Clause"
  "BSD-3-Clause"
  "ISC"
  "PostgreSQL"
  "MS-PL"
)

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

failures=0
checked=0

is_allowed() {
  local expr="$1"
  # Strip WITH clauses and parens
  expr="${expr// WITH */}"
  expr="${expr//(/}"
  expr="${expr//)/}"
  # Check if any OR branch is fully allowed (all AND parts allowed)
  IFS=' OR ' read -ra or_parts <<< "${expr}"
  for or_part in "${or_parts[@]}"; do
    local all_ok=1
    IFS=' AND ' read -ra and_parts <<< "${or_part}"
    for id in "${and_parts[@]}"; do
      id="${id// /}"
      local found=0
      for allowed in "${ALLOWLIST[@]}"; do
        if [[ "${id}" == "${allowed}" ]]; then found=1; break; fi
      done
      if [[ "${found}" -eq 0 ]]; then all_ok=0; break; fi
    done
    if [[ "${all_ok}" -eq 1 ]]; then return 0; fi
  done
  return 1
}

fetch_license() {
  local pkg="$1" ver="$2"
  pkg_lower="${pkg,,}"
  # Try registration API
  local json
  json=$(curl -sf --retry 3 --retry-delay 1 \
    "https://api.nuget.org/v3/registration5-semver1/${pkg_lower}/index.json" 2>/dev/null || true)
  if [[ -n "${json}" ]]; then
    local lic
    lic=$(echo "${json}" | grep -o '"licenseExpression":"[^"]*"' | head -1 | cut -d'"' -f4 || true)
    if [[ -n "${lic}" ]]; then echo "${lic}"; return; fi
  fi
  # Fallback: .nuspec
  local nuspec
  nuspec=$(curl -sf --retry 3 --retry-delay 1 \
    "https://api.nuget.org/v3-flatcontainer/${pkg_lower}/${ver}/${pkg_lower}.nuspec" 2>/dev/null || true)
  if [[ -n "${nuspec}" ]]; then
    local lic_url
    lic_url=$(echo "${nuspec}" | grep -o '<licenseUrl>[^<]*</licenseUrl>' | sed 's/<[^>]*>//g' | head -1 || true)
    if echo "${lic_url}" | grep -qi 'mit'; then echo "MIT"; return; fi
    if echo "${lic_url}" | grep -qi 'apache'; then echo "Apache-2.0"; return; fi
    if echo "${lic_url}" | grep -qi 'bsd'; then echo "BSD-3-Clause"; return; fi
  fi
  echo "UNKNOWN"
}

# Collect unique PackageReference entries from all .csproj files
declare -A packages
while IFS= read -r line; do
  pkg=$(echo "${line}" | grep -o 'Include="[^"]*"' | cut -d'"' -f2 || true)
  ver=$(echo "${line}" | grep -o 'Version="[^"]*"' | cut -d'"' -f2 || true)
  if [[ -n "${pkg}" && -n "${ver}" ]]; then
    packages["${pkg}"]="${ver}"
  fi
done < <(grep -rh 'PackageReference Include=' --include='*.csproj' 2>/dev/null || true)

# Also check centrally-managed versions in Directory.Packages.props
while IFS= read -r line; do
  pkg=$(echo "${line}" | grep -o 'Include="[^"]*"' | cut -d'"' -f2 || true)
  ver=$(echo "${line}" | grep -o 'Version="[^"]*"' | cut -d'"' -f2 || true)
  if [[ -n "${pkg}" && -n "${ver}" ]]; then
    packages["${pkg}"]="${ver}"
  fi
done < <(grep 'PackageVersion Include=' Directory.Packages.props 2>/dev/null || true)

for pkg in "${!packages[@]}"; do
  ver="${packages[$pkg]}"
  lic=$(fetch_license "${pkg}" "${ver}")
  checked=$((checked + 1))
  if is_allowed "${lic}"; then
    echo "[OK]   ${pkg} ${ver} — ${lic}"
  else
    echo "[FAIL] ${pkg} ${ver} — ${lic} (not in allowlist)" >&2
    failures=$((failures + 1))
  fi
done

echo ""
echo "Checked ${checked} package(s). Failures: ${failures}."
if [[ "${failures}" -gt 0 ]]; then exit 1; fi
exit 0
