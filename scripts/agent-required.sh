#!/usr/bin/env bash
set -euo pipefail

if ! command -v agent >/dev/null 2>&1; then
  echo "Agent Sync is required for this repository."
  echo "Install the 'agent' CLI and ensure it is on PATH."
  exit 3
fi

if ! command -v git-agent >/dev/null 2>&1; then
  echo "Warning: 'git-agent' was not found on PATH."
  echo "The command 'git agent ...' may not work."
fi
