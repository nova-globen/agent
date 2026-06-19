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
- **Release version:** the next intended release is `0.2.0-alpha.1` — the first to ship
  the import/CRUD/`agent ui`/localhost-UI wave and the separate UI release artifacts. The
  previously published releases were `0.1.0-alpha.1`…`0.1.0-alpha.4` (CLI binaries + NuGet
  tools only): `alpha.4` introduced the .NET tool packages (`AgentSync` / `AgentSync.Git`)
  on NuGet; `alpha.3` published the GitHub Release binaries but its NuGet push did not
  clear validation; `alpha.2` was a version-only retag of `alpha.1`. Local dev builds
  report the base `Version` from `Directory.Build.props`; the released artifact's version
  comes from the Git tag.
- **Target framework:** `.NET 10` (`net10.0`).

## Planned major features

Specs live under `.ai-agent/features/` (`IMPORTS.md`, `CRUD_COMMANDS.md`,
`UI_LOCALHOST_BLAZOR.md`, `ROADMAP.md`).

**Implemented:**

- **Import commands** — `agent import skill` and `agent import agent` adopt existing
  skill files/folders and instruction files (`AGENTS.md`, `CLAUDE.md`, Copilot, Gemini,
  Cursor, skill folders) into canonical `.agent/skills/`. Logic in
  `src/AgentSync.Core/Import/`.
- **Skill/target CRUD** — `agent skill add/edit/delete/list/show` (+ `skills`) and
  `agent target add/edit/delete/list/show` (+ `targets`). Logic in
  `src/AgentSync.Core/Authoring/`.
- **`agent ui`** — a **launcher/discovery command only**: `AgentSync.Core.UiLauncher` /
  `UiSession` locate and start a separately installed `agent-sync-ui` with
  `--repo`/`--port`/`--token` (`--no-open` supported), poll its `/healthz` readiness
  endpoint (`IUiReadinessProbe`), open the browser (`IBrowserLauncher`) at the token URL
  but print only the clean URL, fall back to the token URL on open failure / `--no-open`,
  and exit 3 with install guidance when absent or on readiness timeout. `AgentSync.Cli`
  has **no** compile-time UI reference (guarded by a test).
- **`AgentSync.Ui.Abstractions`** — UI-independent application service (`AgentSyncApp`)
  over Core; the UI binds to it (no repository logic in Razor components).
- **`AgentSync.Ui.Web`** — the **localhost Blazor Web UI** host (executable
  `agent-sync-ui`, **Interactive Server**) using **Microsoft FluentUI Blazor components**.
  Binds `127.0.0.1`, random port, per-launch session token (`SessionGate`/`TokenCheck`;
  the token is exchanged into an HttpOnly `SameSite=Strict` cookie and stripped from the
  URL on first use, with an unauthenticated `/healthz` readiness endpoint). All screens
  (Dashboard, Skills, Imports, Targets, Status/Drift, Diff, Hooks/CI, Settings) drive
  `AgentSyncApp`: file-writing actions (add/edit/import/sync) use explicit submit buttons,
  and destructive ones (delete skill/target, force sync, install hooks) require a second
  confirmation step. Page interaction logic lives in `AgentSync.Ui.Web/ViewModels/*`
  (unit-tested); no repository logic lives in Razor components. Builds with the standard
  SDK (no special workloads).
- **GUI packaging (Milestone UI-3)** — a separate `release-ui` job in `release.yml`
  (`needs: release`) publishes self-contained `agent-sync-ui-<tag>-<rid>` archives (with
  their `wwwroot`/static-web-assets manifest) independently of the CLI release, merging
  the UI checksums into `checksums.txt`. A UI build failure is visible but never blocks the
  CLI release; the CLI artifact names and CLI-only `dotnet tool` packages are unchanged.

The earlier MAUI/OpenMaui direction was **dropped**; the localhost web UI replaces it.
Keep existing CLI behavior backward compatible; keep the CLI/`git-agent`/hooks/CI/
`dotnet tool`/containers free of any UI (web host / FluentUI) dependency.

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
agent ui              # launch the optional, separately-installed local web UI (agent-sync-ui)
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
