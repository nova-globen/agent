# CLAUDE.md

@AGENTS.md

This file orients a future Claude session quickly. The sections below are **generated**
from the canonical skills under `.agent/skills/` — do not edit them by hand; edit the skill
and run `agent sync`. For deeper detail, read `AGENTS.md` (imported above) and the specs
under `.agent/` (`CURRENT_STATE.md`, `NEXT_STEPS.md`, `PRODUCT_SPEC.md`, `ARCHITECTURE.md`).

<!-- agent-sync:start id=agent-sync-overview target=claude_md hash=sha256:b5d5cb18410fea28e95a5b87d30ce1272dd370b8be1b63608517da70c46472c6 -->
## Agent Sync Overview

What Agent Sync is, the product shape it must keep, its CLI commands, and how drift detection works. Read this first when orienting in this repository.

## What Agent Sync is

Agent Sync is a Git-native consistency manager for AI-agent skills, instructions, and
configuration files. Developers define a **canonical skill once** under `.agent/` and
Agent Sync **projects** it into agent-specific formats: `AGENTS.md`, `CLAUDE.md`, Cursor
rules, GitHub Copilot instructions, Gemini instructions, and OpenAI/Claude skill folders.
It then detects drift and enforces consistency through Git hooks and CI.

The core problem is **agent instruction drift** — the same guidance copied into many
agent files that slowly diverge. Repository: https://github.com/nova-globen/agent

## Status

- **Stable release.** The core workflow is solid end to end. Current release line: `v0.2.0`
  — first stable release, promoting `v0.2.0-alpha.7`. Adds `toml_agent` (configurable TOML
  projection for a second agent tool alongside Claude), `references/` directory projection
  for skill folders, stale-marker-hash reconciliation, `.agent/backups/` as the default
  sessions backup directory, and `--help` on every command. Target framework: **.NET 10**
  (`net10.0`). The full release history and current-state notes live under
  `.agent/CURRENT_STATE.md` and `.agent/NEXT_STEPS.md`.

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
<!-- agent-sync:end -->

<!-- agent-sync:start id=agent-sync-maintainer target=claude_md hash=sha256:e5b985f249ad72fcf878a4e62fd60ffff2e1027abb30372b3316429197126b86 -->
## Agent Sync Maintainer

How to work on the Agent Sync codebase — invariants you must not break, build/test commands, project layout, key design points, and the release process. Use when implementing features or fixes, updating adapters, adjusting drift detection, or preparing a release.

## When to use

Use this when working on the Agent Sync codebase: implementing features or fixes,
updating adapters, adjusting drift detection, writing docs, or preparing a release.

## Do not accidentally break

- Do not change `agent sync` **write-by-default** behavior without explicit maintainer
  instruction. (`--check` previews and exits non-zero on changes; `--force` overwrites
  manually edited generated sections.)
- Do not retarget away from `net10.0` without explicit maintainer instruction.
- Do not remove `git-agent`; `git agent ...` is a core product requirement. It delegates
  to `AgentSync.Cli.CliRunner`, so keep the two entry points in sync.
- Do not weaken path-traversal protection — `RepoPath` is the central guard (it rejects
  absolute, Windows drive/UNC, and `..`-escaping paths).
- Do not overwrite manually edited generated sections unless `--force` is passed.
- Keep release artifact names stable (`agent-sync-<tag>-<rid>.tar.gz` /
  `...-win-x64.zip`, plus `checksums.txt`).
- Keep install-script URLs on the `master` branch while the default branch is `master`.
- Keep the headless stack — `AgentSync.Cli`, `AgentSync.Core`, `AgentSync.GitAgent`, Git
  hooks, CI, the `dotnet tool` packages, and container images — free of any UI (web host /
  FluentUI) dependency. It must never reference `AgentSync.Ui.Web` or FluentUI.

## Build, test, run

```bash
dotnet build --configuration Release
dotnet test
scripts/release-smoke.sh   # publishes both binaries; checks git-agent delegation
```

- Target framework is `net10.0` (only the .NET 10 SDK/runtime is installed here). Do not
  retarget to net8.0.
- The solution file is `AgentSync.slnx` (the newer XML solution format).
- `nuget.config` pins to nuget.org; the inherited `nova-globen` private feed fails auth in
  this environment (harmless `NU1900` warnings).

## Projects

- `src/AgentSync.Core` — all domain logic (config, skills, projections, adapters, drift,
  services). Includes `Import/` (skill + agent + sub-agent import), `Authoring/` (skill +
  target + sub-agent CRUD writers), `Subagents/` (canonical sub-agents under `.agent/agents/`
  projected to `.claude/agents/<id>.md` via `SubagentProjector`, with their own
  `agents.lock.json`), `Sessions/` (agent session backup/restore: `SessionProviderRegistry`
  + per-agent providers, `PathConversion`/`PathRewriter` for WSL/Windows/Linux path
  translation, `SessionBackupService`/`SessionRestoreService`), `UiLauncher`
  (discovers/launches the external `agent-sync-ui`; no UI reference), `UiInstaller` (installs
  the UI on demand via the `AgentSync.Ui` .NET tool or a release-archive download), and
  `UiSession` (free port + session token).
- `src/AgentSync.Cli` — the `agent` binary (`AssemblyName=agent`); logic lives in the
  public `CliRunner` so tests drive it without spawning a process.
- `src/AgentSync.GitAgent` — the `git-agent` binary (`AssemblyName=git-agent`); its
  `Program` just calls `new CliRunner().Run(args)`, so `git agent <cmd>` == `agent <cmd>`.
- `src/AgentSync.Ui.Abstractions` — UI-independent application service (`AgentSyncApp`)
  over Core, for the UI; no web/UI dependency.
- `src/AgentSync.Ui.Web` — localhost Blazor Web UI host (executable `agent-sync-ui`) using
  Microsoft FluentUI Blazor components; binds `127.0.0.1` with a session token; references
  `AgentSync.Ui.Abstractions`/`AgentSync.Core` only. The CLI never references it.

## Key design points

- Projections: shared files (AGENTS.md, CLAUDE.md, Copilot, Gemini) use `agent-sync`
  markers via `MarkedDocument`; dedicated files (Cursor, OpenAI/Claude skill folders) are
  whole-file with manual-edit detection via the lockfile hash.
- Path safety: every projection read/write resolves through `RepoPath`. `ConfigValidator`
  also flags unsafe target paths (`config.target-unsafe-path`).
- YAML frontmatter (Cursor/skill folders) is emitted via `Yaml.Scalar(...)`, which
  quotes/escapes values that are not safe plain scalars. Do not hand-concatenate.
- Heading strategy: `skill.yaml` owns `name`/`description`/`version`; `SKILL.md` is the
  body only and must not start with a `# Name` heading. Adapters add one heading and
  `SkillContent.StripRedundantHeading` removes a leading `# Name` that would duplicate it.
- Policy (`agent.yaml` `policy:`): `fail_on_missing_projection`,
  `fail_on_outdated_projection`, `fail_on_manual_edit` downgrade the matching drift to a
  reported warning so `agent status --fail-on-drift` does not fail (still listed).
- `agent sync` writes by default; `--check` previews (non-zero on changes), `--force`
  overwrites manually edited generated projections.
- Adapters must produce **deterministic** output (no timestamps/randomness) so hashing,
  drift detection, and golden-file tests stay stable.
- Git hooks must **fail when `agent` is missing** (the scaffolded hooks exit 3 with the
  required message), never silently pass.
- Exit codes: 0 success, 1 drift/validation, 2 invalid usage, 3 environment, 4 unexpected.

## Web UI gotchas (when touching `AgentSync.Ui.Web`)

- Serve static files with `app.MapStaticAssets()`, not `UseStaticFiles()` — the latter
  404s `_framework/blazor.web.js` and the FluentUI `_content/...` assets in a published
  host.
- Build the host with `WebApplicationOptions.ContentRootPath = AppContext.BaseDirectory`,
  not the default current-working-directory root: `agent ui` launches the host inside the
  user's repo, and a CWD content root makes `MapStaticAssets` find no `wwwroot`/manifest and
  serve empty `200`s.
- `App.razor` must link the Blazor CSS-isolation bundle `@Assets["AgentSync.Ui.styles.css"]`
  (it `@import`s the FluentUI component CSS); without it the FluentUI components render
  unstyled. `MainLayout.razor` is a custom app shell styled by `wwwroot/app.css`.
- Do **not** add `<FluentDesignTheme>`: its JS interop (`clearLocalStorage`) crashes the
  Interactive Server circuit and leaves every button dead. Keep the heavy
  `Microsoft.FluentUI.AspNetCore.Components.Icons` package out too.

## Testing expectations

- Add/update tests for every behavior change. Core logic → `tests/AgentSync.Core.Tests`,
  CLI → `tests/AgentSync.Cli.Tests`. Adapter output is covered by golden files under
  `tests/AgentSync.Core.Tests/Golden`.
- Keep generated output deterministic so hashing and golden tests stay stable.
- Test-environment note: a stray `/tmp/.git` in some sandboxes makes temp dirs under
  `/tmp` look like a Git repo; a few tests tolerate an ancestor repo rather than assume its
  absence.

## Releases

- Tag-driven: pushing `v*.*.*` runs `.github/workflows/release.yml`, which publishes
  self-contained `agent`/`git-agent` for linux-x64, linux-arm64, osx-x64, osx-arm64,
  win-x64, generates `checksums.txt`, and creates the GitHub Release via `gh`. Follow
  `RELEASE_CHECKLIST.md`. To cut a release step by step (confirm version, bump it across the
  whole solution, commit), use the **`releasing-agent-sync`** skill.
- .NET tool packages: `src/AgentSync.Cli` packs as `AgentSync` (command `agent`) and
  `src/AgentSync.GitAgent` packs as `AgentSync.Git` (command `git-agent`); the UI packs as
  `AgentSync.Ui` (command `agent-sync-ui`). Pushed to NuGet via Trusted Publishing. The
  dotted `Agent.*` prefix is a reserved NuGet prefix owned by someone else — do not reuse
  it. A synchronous push success does not prove publication; the **Verify packages are
  live** step polls the flat-container and fails the job if a version never becomes
  installable.

## Working conventions

- Run build + tests before and after changes; add tests for behavior changes.
- Prefer small, focused commits. Do not add AI/Claude trailers to commit messages.
- Keep public docs clean: no private conversation URLs and no local machine paths.
- Keep release positioning accurate in any public-facing wording.

## This repository runs Agent Sync on itself

The agent instruction files here — `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`,
`.gemini/GEMINI.md`, and the `.claude/skills/` folders — are **generated projections** of the
skills under `.agent/skills/`. Edit the canonical skill, run `agent sync`, and commit the
canonical change with its regenerated projections. Never hand-edit a generated section; the
Git hooks and the CI drift check (`agent status --fail-on-drift --ci`, run against the repo
root) will block it. Deeper specs and plans live under `.agent/` (`PRODUCT_SPEC.md`,
`ARCHITECTURE.md`, `ADAPTERS.md`, `CLI_CONTRACT.md`, `GIT_AND_CI.md`, `TEST_PLAN.md`,
`features/`, `CURRENT_STATE.md`, `NEXT_STEPS.md`).
<!-- agent-sync:end -->
```

Whole-file targets (Cursor rules, OpenAI/Claude skill folders) are managed in full and
detect manual edits via the lockfile hash instead of markers.
<!-- agent-sync:end -->
```

Whole-file targets (Cursor rules, OpenAI/Claude skill folders) are managed in full and
detect manual edits via the lockfile hash instead of markers.
<!-- agent-sync:end -->
