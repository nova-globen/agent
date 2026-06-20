# Implementation Plan

## Milestone 1: Bootstrap CLI

Create a .NET solution with `src/AgentSync.Cli`, `src/AgentSync.Core`, and `tests/AgentSync.Core.Tests`. Implement `agent --version`, `agent doctor`, `agent init`, and `agent status`. Add tests. Stop when tests pass.

## Milestone 2: Config and Canonical Skills

Implement parsing and validation for `.agent/agent.yaml`, `.agent/skills/<skill-id>/skill.yaml`, and `.agent/skills/<skill-id>/SKILL.md`.

## Milestone 3: Projection Engine

Implement normalized hashing, generated section markers, lockfile writing, and safe update logic.

## Milestone 4: Adapters

Implement adapters for AGENTS.md, CLAUDE.md, Cursor, GitHub Copilot, Gemini, OpenAI skill folder, and Claude skill folder.

## Milestone 5: Drift Detection

Detect missing projection, stale projection, manual edit, config error, and lock mismatch.

## Milestone 6: Git Integration

Implement `git-agent` and `agent install-hooks`.

## Milestone 7: CI Support

Add GitHub Actions that restores, builds, tests, and runs drift check.

## Milestone 8: Release Prep

Add README, LICENSE, CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, and example repository.
