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

Specs live under `.ai-agent/features/` (`IMPORTS.md`, `CRUD_COMMANDS.md`,
`UI_MAUI_BLAZOR.md`, `OPENMAUI_LINUX_SPIKE.md`, `ROADMAP.md`).

**Implemented:**

- **Import commands** — `agent import skill` and `agent import agent` adopt existing
  skill files/folders and instruction files (`AGENTS.md`, `CLAUDE.md`, Copilot, Gemini,
  Cursor, skill folders) into canonical `.agent/skills/`. Logic in
  `src/AgentSync.Core/Import/`.
- **Skill/target CRUD** — `agent skill add/edit/delete/list/show` (+ `skills`) and
  `agent target add/edit/delete/list/show` (+ `targets`). Logic in
  `src/AgentSync.Core/Authoring/`.
- **`agent ui`** — a **launcher/discovery command only**: `AgentSync.Core.UiLauncher`
  locates and starts a separately installed GUI executable (`agent-sync-ui`) and fails
  gracefully (exit 3) when absent. `AgentSync.Cli` has **no** compile-time MAUI/OpenMaui
  reference (guarded by a test).
- **`AgentSync.Ui.Abstractions`** — UI-independent application service (`AgentSyncApp`)
  over Core; the GUI binds to it (no repository logic in Razor components).

**Partially done / not shipped:**

- **GUI app** — `src/AgentSync.Ui.Maui` is a MAUI Blazor Hybrid skeleton **excluded from
  `AgentSync.slnx`**, so the headless build/test never need the MAUI workload. MVP
  capabilities are covered + tested via `AgentSyncApp`; the rendered MAUI MVP and
  packaging still need a MAUI-workload build (could not be built/verified in CI here).
- **Linux GUI** — **deferred**, experimental OpenMaui spike only; do not claim Linux GUI
  support until a tested build/package/runtime exists (`OPENMAUI_LINUX_SPIKE.md`).

Keep existing CLI behavior backward compatible; keep the CLI/`git-agent`/hooks/CI/
`dotnet tool`/containers free of any GUI (MAUI/OpenMaui) dependency.

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
agent import skill    # import a SKILL.md / skill folder into .agent/skills
agent import agent    # import an existing instruction file/folder into canonical skills
agent skill ...       # add | edit | delete | list | show  (alias: agent skills)
agent target ...      # add | edit | delete | list | show  (alias: agent targets)
agent ui              # launch the optional, separately-installed desktop GUI
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
