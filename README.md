# Agent Sync

Agent Sync is a Git-native consistency manager for AI-agent skills and instruction files.

It lets developers define a canonical skill once and mirror it into different AI-agent formats.

## Target Commands

```bash
agent status
git agent status
```

## Current Status

This repository is bootstrapped with AI-agent instructions. Ask an AI coding agent to read `CLAUDE.md`, `AGENTS.md`, and `.ai-agent/IMPLEMENTATION_PLAN.md`, then implement the product milestone by milestone.

## Local Hook Wiring

```bash
./scripts/install-hooks.sh
```

After hooks are installed, commits and pushes fail if the `agent` tool is missing or if agent skill drift is detected.
