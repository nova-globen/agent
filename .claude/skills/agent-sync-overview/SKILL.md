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

- **Stable release.** The core workflow is solid end to end. Current release line: `v0.3.0`
  — adds `agent autopilot claude` (headless Claude Code CLI loop that runs continuously until
  all planned work is done, parsing each session's result and retrying on transient failures)
  and `agent init --with-samples` (installs a curated starter pack: 9 skills including
  `autopilot`, `commit-governor`, `plan-governor`, `memory-curator`, `operating-guide`, and
  more; 3 sub-agents — `planner`, `verifier`, `git-ops-executor`; and 5 Git hooks). Target
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
agent import subagent # import existing .claude/agents/*.md sub-agents into .agent/agents
agent skill ...       # add | edit | delete | list | show  (alias: agent skills)
agent target ...      # add | edit | delete | list | show  (alias: agent targets)
agent subagent ...    # add | edit | delete | list | show  (alias: agent subagents)
agent sessions ...    # backup | restore | list | providers — agent session history
agent autopilot ...   # headless autopilot loop (agent autopilot claude)
agent ui              # launch the optional local web UI (agent-sync-ui); auto-installs it on first run
```

`agent subagent` manages canonical sub-agents under `.agent/agents/<id>/` (`agent.yaml` +
`AGENT.md`); `agent sync` projects each one into a Claude Code sub-agent file
(`.claude/agents/<id>.md`) and reports its drift like any other projection.

`agent sessions backup <provider>` zips an agent's session history for the current project
(Claude Code, Codex, Copilot, Gemini, Cursor) with a restore manifest; `agent sessions
restore <archive>` replays it into the current environment, relocating the store and
translating embedded paths across WSL / Windows / Linux (e.g. `/mnt/c/...` ⇄ `C:\...`) and a
changed project path. Use `--project` to override the project directory and `--dry-run` to
preview.

Every command also works as `git agent <command>` (for example `git agent status`).

## Canonical file structure

```text
.agent/
  agent.yaml          # enabled targets and their paths
  lock.json           # recorded hash for each projection
  agents.lock.json    # recorded hash for each sub-agent projection
  skills/
    <skill-id>/
      skill.yaml      # id, name, description, version, per-target enable flags
      SKILL.md        # the instruction body (no leading "# Name" heading)
      assets/
      scripts/
  agents/
    <subagent-id>/
      agent.yaml      # id, name, description, optional model + tools allow-list
      AGENT.md        # the sub-agent system prompt body
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
.claude/agents/<subagent-id>.md     # from .agent/agents/ (sub-agents)
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
