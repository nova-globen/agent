# Product Specification: Agent Sync

Agent Sync is a Git-native CLI for managing AI-agent skills and instruction files across multiple AI coding agents.

It lets a developer write one canonical skill and mirror it into different agent-specific formats.

## Problem

Modern repositories may contain many agent instruction systems: `AGENTS.md`, `CLAUDE.md`, `.cursor/rules/*.mdc`, `.github/copilot-instructions.md`, `.gemini/GEMINI.md`, OpenAI/ChatGPT skill folders, Claude skill folders, MCP configuration, prompt libraries, tool definitions, and eval files.

Without tooling, these files drift.

## MVP

The MVP must support:

```bash
agent init
agent status
agent sync
agent diff
agent validate
agent doctor
agent install-hooks
git agent status
```

## Non-Goals

Do not build a hosted SaaS, web UI, package registry, prompt optimizer, runtime agent orchestrator, or replacement for Git.
