# CLAUDE.md

@AGENTS.md

This file orients a future Claude session quickly. For deeper detail, read `AGENTS.md`
(imported above), `.ai-agent/CURRENT_STATE.md`, and `.ai-agent/NEXT_STEPS.md`.

## What Agent Sync is

Agent Sync is a Git-native consistency manager for AI-agent skills, instructions, and
configuration files. Developers define a **canonical skill once** under `.agent/` and
Agent Sync **projects** it into agent-specific formats: `AGENTS.md`, `CLAUDE.md`, Cursor
rules, GitHub Copilot instructions, Gemini instructions, and OpenAI/Claude skill folders.
It then detects drift and enforces consistency through Git hooks and CI.

Repository: https://github.com/nova-globen/agent

## Current status

- **Alpha / developer preview.** Core workflow works end to end; surface may still change.
- **Release version:** `0.1.0-alpha.4`. This is the release that introduces the .NET
  tool packages (`AgentSync` / `AgentSync.Git`) on NuGet. (`alpha.3` published the
  GitHub Release binaries but its NuGet push did not clear validation; `alpha.2` was a
  version-only retag of `alpha.1`.) Local dev builds report the base `Version` from
  `Directory.Build.props`; the released artifact's version comes from the Git tag.
- **Target framework:** `.NET 10` (`net10.0`).

## Planned major features

Planned, **not yet implemented** — do not describe these as shipped. Implementation-ready
specs live under `.ai-agent/features/` (`IMPORTS.md`, `CRUD_COMMANDS.md`,
`UI_MAUI_BLAZOR.md`, `ROADMAP.md`):

- **Import commands** — `agent import skill` and `agent import agent` to adopt existing
  skill files/folders and instruction files (`AGENTS.md`, `CLAUDE.md`, Copilot, Gemini,
  Cursor, skill folders) into canonical `.agent/skills/`.
- **Skill/target CRUD commands** — `agent skill add/edit/delete/list/show` and
  `agent target add/edit/delete/list/show`.
- **`agent ui`** — an optional GUI exposing the major features.
- **MAUI Blazor Hybrid GUI** — `src/AgentSync.Ui` reusing `AgentSync.Core` services;
  the headless CLI must not depend on MAUI.

Build milestone by milestone (`features/ROADMAP.md`) and keep existing CLI behavior
backward compatible.

## CLI entry points

- `agent` — the primary CLI (`src/AgentSync.Cli`, `AssemblyName=agent`).
- `git-agent` — the Git extension (`src/AgentSync.GitAgent`, `AssemblyName=git-agent`);
  it delegates to `AgentSync.Cli.CliRunner`, so **`git agent ...` behaves exactly like
  `agent ...`**.

## Core commands

```text
agent init            # scaffold .agent/ (default code-review skill) and .githooks/
agent sync            # write missing/outdated projections (--check, --write, --force)
agent status          # report state + drift (--json, --fail-on-drift, --ci)
agent diff            # show canonical-to-projection differences
agent validate        # validate config and skills
agent doctor          # diagnose Git repo, PATH, hooks, config
agent install-hooks   # set core.hooksPath=.githooks and make hooks executable
```

## Core product invariant

```text
canonical skill -> generated projections -> drift detection -> Git hook / CI enforcement
```

## Validated on Windows (v0.1.0-alpha.1)

Manually verified on Windows with the globally installed binaries:

- Installed globally; `agent --version` and `git agent --version` both report
  `agent 0.1.0-alpha.1`.
- `agent init`, `agent sync`, `agent status --fail-on-drift --ci`, `git agent status`,
  and `agent install-hooks` all work.
- After hand-editing `AGENTS.md` inside a generated marker section,
  `agent status --fail-on-drift --ci` reported
  `[ERROR] Manually edited projection AGENTS.md (agents_md)...` and exited non-zero.
- The pre-commit hook then **blocked** `git commit`.

This proves: `manual edit -> drift detected -> CI/status fails -> Git commit blocked`.

See `.ai-agent/VALIDATION_LOG.md` for the exact steps.

## Build / test

```bash
dotnet build --configuration Release
dotnet test
scripts/release-smoke.sh   # publishes both binaries; checks git-agent delegation
```

This repository does **not** run Agent Sync on its own hand-authored `AGENTS.md` /
`CLAUDE.md` (there is no root `.agent/`). Do not introduce that without explicit
maintainer instruction.

## Do not accidentally break

- Do not change `agent sync` **write-by-default** behavior without explicit maintainer
  instruction.
- Do not retarget away from `net10.0` without explicit maintainer instruction.
- Do not remove `git-agent`; `git agent ...` is a core product requirement.
- Do not weaken path-traversal protection (`RepoPath` is the central guard).
- Do not overwrite manually edited generated sections unless `--force` is passed.
- Keep release artifact names stable
  (`agent-sync-<tag>-<rid>.tar.gz` / `...-win-x64.zip`, plus `checksums.txt`).
- Keep install-script URLs on the `master` branch while the default branch is `master`.

## Working conventions

- Run build + tests before and after changes; add tests for behavior changes.
- Prefer small, focused commits. Do not add AI/Claude trailers to commit messages.
- Keep public docs clean: no private conversation URLs and no local machine paths.
- Keep alpha positioning honest in any public-facing wording.
