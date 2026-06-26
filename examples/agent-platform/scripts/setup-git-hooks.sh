#!/usr/bin/env bash
# Configure git hooks path, make hooks executable, and restore dotnet tools.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "${repo_root}"

git config core.hooksPath .githooks

chmod +x .githooks/commit-msg \
         .githooks/pre-commit \
         .githooks/post-checkout \
         .githooks/post-merge \
         .githooks/pre-push \
         scripts/pre-commit-validate.sh \
         scripts/validate-commit-message.sh \
         scripts/check-nuget-licenses.sh \
         scripts/check-backlog-consistency.sh

dotnet tool restore
dotnet agent sync

echo "[setup-git-hooks] Done. Git hooks are active; AgentSync projections refreshed."
