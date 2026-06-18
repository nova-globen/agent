# AGENTS.md

This repository builds **Agent Sync**, a Git-native consistency manager for AI-agent skills, instructions, and configuration files.

## Product Goal

Build a command-line tool that works as both:

```bash
agent status
git agent status
```

The tool helps developers define AI-agent skills once and mirror them to multiple AI agent systems such as Claude, ChatGPT/OpenAI Skills, Cursor rules, GitHub Copilot instructions, Gemini instructions, and generic `AGENTS.md`.

The core problem is **agent instruction drift**.

## Required Product Shape

The project must produce:

- A CLI command named `agent`.
- A Git extension command named `git-agent`, so users can run `git agent ...`.
- Support for `agent status` and `git agent status`.
- Repository wiring via `.githooks`.
- CI/pipeline usability.
- Failure behavior when the tool is required but not installed.
- Open-source, copyleft-friendly structure.

Use .NET 8 or newer unless the maintainer explicitly changes this decision.

## Required Commands

Minimum MVP commands:

```bash
agent init
agent status
agent sync
agent diff
agent validate
agent doctor
agent install-hooks

git agent init
git agent status
git agent sync
git agent diff
git agent validate
git agent doctor
git agent install-hooks
```

`git-agent` may delegate to `agent`.

## Canonical File Structure

Agent Sync should manage this structure:

```text
.agent/
  agent.yaml
  lock.json
  skills/
    <skill-id>/
      skill.yaml
      SKILL.md
      assets/
      scripts/
```

## Projection Targets

Initial projection targets:

```text
AGENTS.md
CLAUDE.md
.cursor/rules/*.mdc
.github/copilot-instructions.md
.gemini/GEMINI.md
.chatgpt/skills/<skill-id>/SKILL.md
.claude/skills/<skill-id>/SKILL.md
```

Actual paths must be configurable in `.agent/agent.yaml`.

## Drift Detection

`agent status` must detect missing projections, outdated projections, manually edited generated projections, missing canonical skills, invalid config, missing lockfile entries, and unsupported adapter versions.

For CI usage:

```bash
agent status --fail-on-drift --ci
```

must exit non-zero if drift or invalid state exists.

## Generated Section Markers

Generated sections must include stable markers:

```md
<!-- agent-sync:start id=<skill-id> target=<target-id> hash=sha256:<hash> -->
...
<!-- agent-sync:end -->
```

The tool must not overwrite user-authored content outside managed sections.
