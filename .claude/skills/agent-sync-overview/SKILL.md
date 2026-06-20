---
name: Agent Sync Overview
description: What Agent Sync is, the product shape it must keep, its CLI commands, and how drift detection works. Read this first when orienting in this repository.
---

## What Agent Sync is

Agent Sync is a Git-native consistency manager for AI-agent skills, instructions, and
configuration files. Developers define a **canonical skill once** under `.agent/` and
Agent Sync **projects** it into agent-specific formats: `AGENTS.md`, `CLAUDE.md`, Cursor
rules, GitHub Copilot instructions, Gemini instructions, and OpenAI/Claude skill folders.
It then detects drift and enforces consistency through Git hooks and CI.

The core problem is **agent instruction drift** — the same guidance copied into many
agent files that slowly diverge. Repository: https://github.com/nova-globen/agent

## Status

- **Alpha / developer preview.** The core workflow works end to end; the surface may still
  change. Current release line: `v0.2.0-alpha.4` (imports, CRUD, `agent ui` and the
  localhost web UI; the repo now runs Agent Sync on itself, ships GitHub Actions / Azure
  Pipelines CI examples, and includes marker round-trip and `sync --force` fixes). Target
  framework: **.NET 10** (`net10.0`). The full release history and current-state notes live
  under `.agent/CURRENT_STATE.md` and `.agent/NEXT_STEPS.md`.

## Core product invariant

```text
canonical skill -> generated projections -> drift detection -> Git hook / CI enforcement
```

## Required product shape

The project ships as two entry points over one implementation:

- A CLI named `agent` (`src/AgentSync.Cli`, `AssemblyName=agent`).
- A Git extension named `git-agent` (`src/AgentSync.GitAgent`, `AssemblyName=git-agent`)
  that delegates to `AgentSync.Cli.CliRunner`, so **`git agent ...` behaves exactly like
  `agent ...`**.

It must support repository wiring via `.githooks`, be usable in CI, fail loudly when the
tool is required but not installed, and stay open-source and copyleft-friendly.

## Core commands

```text
agent init            # scaffold .agent/ (code-review + using-agent-sync skills) and .githooks/
agent sync            # write missing/outdated projections (--check, --write, --force)
agent status          # report state + drift (--json, --fail-on-drift, --ci)
agent diff            # show canonical-to-projection differences
agent validate        # validate config and skills
agent doctor          # diagnose Git repo, PATH, hooks, config
agent install-hooks   # set core.hooksPath=.githooks and make hooks executable
agent import skill    # import a SKILL.md / skill folder into .agent/skills
agent import agent    # import an existing instruction file/folder into canonical skills
agent skill ...       # add | edit | delete | list | show  (alias: agent skills)
agent target ...      # add | edit | delete | list | show  (alias: agent targets)
agent ui              # launch the optional local web UI (agent-sync-ui); auto-installs it on first run
```

Every command also works as `git agent <command>` (for example `git agent status`).

## Canonical file structure

```text
.agent/
  agent.yaml          # enabled targets and their paths
  lock.json           # recorded hash for each projection
  skills/
    <skill-id>/
      skill.yaml      # id, name, description, version, per-target enable flags
      SKILL.md        # the instruction body (no leading "# Name" heading)
      assets/
      scripts/
```

Projection targets (each path is configurable in `agent.yaml`):

```text
AGENTS.md
CLAUDE.md
.cursor/rules/*.mdc
.github/copilot-instructions.md
.gemini/GEMINI.md
.chatgpt/skills/<skill-id>/SKILL.md
.claude/skills/<skill-id>/SKILL.md
```

## Drift detection

`agent status` detects missing projections, outdated projections, manually edited
generated projections, missing canonical skills / no skills, invalid config, missing
lockfile entries, and orphaned lockfile entries. For CI:

```bash
agent status --fail-on-drift --ci
```

must exit non-zero if drift or invalid state exists.

## Generated section markers

In shared files, each generated section is wrapped in stable HTML-comment markers — an
`agent-sync:start` comment carrying the skill id, target id, and a `sha256:` content hash,
and a matching `agent-sync:end` comment. The tool must not overwrite user-authored content
outside these markers. Whole-file targets (Cursor rules, OpenAI/Claude skill folders) are
managed in full and detect manual edits via the lockfile hash instead of markers. The exact
marker syntax is documented in `.agent/PRODUCT_SPEC.md`.
