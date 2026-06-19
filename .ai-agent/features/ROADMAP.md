# Feature roadmap: imports, CRUD, and UI

> Milestone-based plan for the feature wave. Implement one milestone at a time, add
> tests, keep CLI behavior backward compatible, and keep the GUI optional (the headless
> CLI must never depend on the UI). Detailed specs:
> [`IMPORTS.md`](IMPORTS.md), [`CRUD_COMMANDS.md`](CRUD_COMMANDS.md),
> [`UI_LOCALHOST_BLAZOR.md`](UI_LOCALHOST_BLAZOR.md). Import (A–C) and CRUD (D–E) are
> implemented; the UI is a separate **localhost Blazor Web UI** (milestones UI-1 … UI-3).

Dependency order: **A → B → C** (import), **D → E** (CRUD), **UI-1 → UI-2 → UI-3** (UI).
CRUD and import are independent and can interleave; the UI comes after the import + CRUD
services exist, since it reuses them via `AgentSync.Ui.Abstractions`.

Guardrails for every milestone (from `CLAUDE.md` → "Do not accidentally break"):
keep `net10.0`; keep `git-agent` delegating to `CliRunner`; keep `sync` write-by-default;
don't weaken `RepoPath`; don't overwrite manual edits without `--force`; keep release
artifact names stable; adapters/importers stay deterministic; keep the CLI/`git-agent`/
hooks/CI/`dotnet tool`/containers free of any UI dependency.

---

## Milestone A — Import foundation

- **Goal:** the shared parsing/detection/report plumbing both import commands build on,
  with no user-facing command yet.
- **Files likely touched:** new `src/AgentSync.Core/Import/` (`ImportSource.cs`,
  `SourceDetector.cs`, `SkillFolderReader.cs`, `MarkdownFrontmatter.cs`,
  `HeadingSplitter.cs`, `IdInference.cs`, `ImportReport.cs`); tests in
  `tests/AgentSync.Core.Tests/`.
- **Commands added:** none.
- **Tests:** source detection per path shape; frontmatter/heading parsing round-trips
  against `SkillFolderAdapter` / `CursorAdapter` / `SharedMarkdownAdapter` golden
  output; id inference produces valid kebab-case or rejects.
- **Acceptance:** the foundation parses every shape `sync` can emit; `dotnet build`
  and `dotnet test` green; no CLI surface change.
- **Risks:** lossy heading-splitting; mis-detecting `agent-sync` marker content as
  hand-authored (must be marker-aware from the start).

## Milestone B — `agent import skill`

- **Goal:** import a single skill from a `SKILL.md` or skill folder into `.agent/skills/`.
- **Files likely touched:** `src/AgentSync.Core/Import/SkillImporter.cs`;
  `src/AgentSync.Cli/CliRunner.cs` (dispatch `import` → `skill`); tests.
- **Commands added:** `agent import skill <path>` with `--id`, `--name`, `--target`,
  `--dry-run`, `--force`, `--json` (per `IMPORTS.md` §A).
- **Tests:** file + folder sources; override flags; conflict-without-`--force` skips;
  `--force` overwrites; `--dry-run` writes nothing; imported skill passes
  `SkillValidator`; round-trip (import a `sync`-generated skill folder → `sync` → no
  drift); exit codes per the conflict table.
- **Acceptance:** importing a real `.claude/skills/<id>/SKILL.md` produces a valid
  canonical skill; `--dry-run` reports the plan; validation runs after import.
- **Risks:** id collisions; empty/missing description failing validation post-import
  (report clearly rather than guessing).

## Milestone C — `agent import agent`

- **Goal:** import existing instruction files/folders (AGENTS.md, CLAUDE.md, Copilot,
  Gemini, Cursor rules, skill-folder roots) into canonical skill(s).
- **Files likely touched:** `src/AgentSync.Core/Import/AgentImporter.cs`;
  `CliRunner.cs`; tests.
- **Commands added:** `agent import agent <path>` with `--type`, `--split`, `--id`,
  `--dry-run`, `--force`, `--json` (per `IMPORTS.md` §B).
- **Tests:** each `--type`; `--split file` vs `sections`; marker-awareness (generated
  sections skipped by default); `.cursor/rules/` directory and skill-folder roots
  produce multiple skills; **source files are never modified**.
- **Acceptance:** a repo with a hand-written `AGENTS.md` (no `.agent/`) can adopt Agent
  Sync via one import; originals untouched; report lists every candidate skill.
- **Risks:** ambiguous split boundaries; deciding default target enablement; preserving
  vs dropping Cursor `globs`/`alwaysApply` (document the decision).

## Milestone D — Skill CRUD

- **Goal:** `agent skill add/edit/delete/list/show` (+ `skills` alias).
- **Files likely touched:** new `src/AgentSync.Core/Authoring/SkillWriter.cs`,
  `AuthoringResult.cs`; `CliRunner.cs` (nested `skill` dispatch); tests; docs
  (`README.md` commands section).
- **Commands added:** the skill group in `CRUD_COMMANDS.md`.
- **Tests:** add creates valid manifest+body; add refuses existing dir; edit updates
  each field + body (`--body-file` normalized); enable/disable toggles; delete removes
  dir + lockfile entries; delete blocked when projections exist without `--force`;
  `--dry-run` writes nothing; deterministic `--json`.
- **Acceptance:** `skill add` then `sync` → clean `status`; `skill delete --force` then
  `sync` → no orphan lock entries; exit codes per contract.
- **Risks:** lockfile orphan handling on delete; `agent.yaml`/manifest round-trip
  losing data; the id==folder invariant (no in-place rename).

## Milestone E — Target CRUD

- **Goal:** `agent target add/edit/delete/list/show` (+ `targets` alias).
- **Files likely touched:** new `src/AgentSync.Core/Authoring/TargetWriter.cs`,
  `ConfigEditor.cs`; `CliRunner.cs` (nested `target` dispatch); tests; docs.
- **Commands added:** the target group in `CRUD_COMMANDS.md`.
- **Tests:** add/edit/delete round-trips `agent.yaml` without losing `policy:` or other
  targets; unsafe paths rejected (`config.target-unsafe-path`); unknown targets
  rejected; disabling/deleting surfaces drift/lockfile consequences; resulting config
  passes `ConfigValidator`; deterministic `--json` ordered by `TargetIds.Ordered`.
- **Acceptance:** target edits change only the intended keys; disabling a target then
  `status` reports the now-orphaned projections clearly.
- **Risks:** YAML round-trip dropping comments; orphaned generated files after disable.

## Milestone UI-1 — Localhost UI host  ✅ implemented

- **Goal:** a separate, optional localhost Blazor Web UI host driven by the launcher.
- **Scope:**
  - Add `AgentSync.Ui.Web` (ASP.NET Core + Blazor, FluentUI components) →
    executable `agent-sync-ui`.
  - Bind to `127.0.0.1`; accept `--repo`, `--port`, `--token` (optional `--no-open`).
  - Session-token middleware (`SessionGate` / `TokenCheck`); deny without a valid token.
  - Show the dashboard + read-only status/skills/targets through `AgentSyncApp`.
  - `agent ui` launcher: free port + token + repo → `agent-sync-ui`; print loopback URL;
    exit-3 with install guidance when absent. No CLI compile-time UI reference.
- **Files touched:** `src/AgentSync.Ui.Web/`, `src/AgentSync.Core/UiLauncher.cs`
  (port/token/`UiSession`), `src/AgentSync.Cli/CliRunner.cs`, `AgentSync.slnx`, tests.
- **Acceptance criteria (met):** headless build/test green with **no** GUI workload;
  the CLI references neither `AgentSync.Ui.Web` nor FluentUI (test-guarded); host binds
  loopback and rejects missing/invalid tokens; launcher passes repo/port/token.

## Milestone UI-2 — UI feature wiring  ✅ implemented

- **Goal:** wire the remaining screens to `AgentSyncApp` with confirmations.
- **Scope:** Skills CRUD; Imports (dry-run preview then confirm); Targets CRUD;
  Status/drift + sync / force-sync; Diff viewer; Hooks (confirmed install-hooks).
  Destructive actions require explicit confirmation; no repository logic in Razor
  components.
- **Done:** host runs Interactive Server; Dashboard, Skills, Imports, Targets,
  Status/Drift, Diff, Hooks/CI, Settings all drive `AgentSyncApp` via view-models in
  `AgentSync.Ui.Web/ViewModels/*`. File-writing actions (add/edit/import/sync) use
  explicit submit buttons; destructive ones (delete skill/target, force sync, install
  hooks) require a second confirmation step. `AgentSyncApp` gained `ListTargets`,
  `InstallHooks`, `AppVersion`. The launch path was also hardened (readiness probe,
  browser open, strict option parsing, token→cookie redirect).
- **Acceptance criteria (met):** each action goes through `AgentSyncApp`; destructive
  mutations confirmed; headless build/test stays green; logic tested at the view-model /
  `AgentSyncApp` layer (`Ui2WiringTests`, `ConfirmationSemanticsTests`) plus a real
  web-host smoke test (`WebHostSmokeTests`).

## Milestone UI-3 — GUI packaging  ✅ implemented

- **Goal:** ship `agent-sync-ui` as **separate** release artifacts.
- **Done:** `release.yml` has a separate **`release-ui` job** (`needs: release`) that
  publishes self-contained, single-file `agent-sync-ui-<tag>-<rid>.tar.gz` /
  `...-win-x64.zip` (each carrying the executable + its `wwwroot`/static-web-assets
  manifest + LICENSE/README) for every CLI RID, merges their checksums into the release's
  `checksums.txt`, and (on manual runs) uploads them as workflow artifacts. Because it runs
  only after the CLI release job succeeds, a UI build failure is visible but cannot block or
  alter the CLI release / NuGet packages. The CLI artifact names and the CLI-only
  `dotnet tool` packages are unchanged. `agent ui`'s missing-UI message points to the UI
  archives. `scripts/release-smoke.sh` validates the UI publish shape, invalid-args usage,
  and a live `/healthz`/`401` check headlessly. README + `RELEASE_CHECKLIST.md` document
  the optional UI install.
- **Acceptance criteria (met):** the CLI release ships without the GUI; the GUI ships
  independently; CLI artifact names unchanged; a GUI build never blocks a CLI release.

> **Historical (rejected):** an earlier plan used .NET MAUI Blazor Hybrid (Windows/macOS)
> plus an experimental OpenMaui Linux spike. That direction was dropped in favour of the
> single cross-platform localhost web UI above; the MAUI project and OpenMaui spike doc
> were removed.

---

## Cross-cutting acceptance for the whole wave

- All existing tests still pass; new behavior has tests.
- `agent`, `git-agent`, and `git agent ...` unchanged for existing commands.
- `RepoPath`, marker handling, and manual-edit protection unchanged or strengthened.
- The headless stack — CLI, `git-agent`, hooks, CI, `dotnet tool` packages, and
  container images — builds/tests/runs/ships with no UI (web host / FluentUI) dependency.
- Docs (`README.md`, `AGENTS.md`, `CLAUDE.md`, `.ai-agent/*`) updated as each milestone
  lands; alpha positioning kept honest.
