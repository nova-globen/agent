# Feature roadmap: imports, CRUD, and UI

> **Status: planned, not implemented.** Milestone-based plan for the next feature wave.
> Implement one milestone at a time, add tests, keep CLI behavior backward compatible,
> and keep the GUI optional (headless CLI must never depend on MAUI). Detailed specs:
> [`IMPORTS.md`](IMPORTS.md), [`CRUD_COMMANDS.md`](CRUD_COMMANDS.md),
> [`UI_MAUI_BLAZOR.md`](UI_MAUI_BLAZOR.md).

Dependency order: **A → B → C** (import), **D → E** (CRUD), **F → G → H** (UI). CRUD
(D/E) and import (A–C) are independent and can interleave; the UI (F–H) should come
after the import + CRUD services exist, since the GUI reuses them.

Guardrails for every milestone (from `CLAUDE.md` → "Do not accidentally break"):
keep `net10.0`; keep `git-agent` delegating to `CliRunner`; keep `sync` write-by-default;
don't weaken `RepoPath`; don't overwrite manual edits without `--force`; keep release
artifact names stable; adapters/importers stay deterministic.

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

## Milestone F — UI architecture spike

- **Goal:** prove MAUI Blazor Hybrid viability and lock in the architecture decisions
  (separate app vs bundled; Linux scope; build isolation).
- **Files likely touched:** new `src/AgentSync.Ui/` prototype; optional
  `src/AgentSync.App/` (view models / app services); solution wiring (`AgentSync.slnx`
  + a solution filter that excludes UI from the default CI build).
- **Commands added:** none yet (or a stub `agent ui` that reports "GUI not installed").
- **Tests:** a CI build/test of Core + CLI + GitAgent passes **without** the MAUI
  workload; any view-model logic added is unit-tested without a renderer.
- **Acceptance:** documented decision on launch model + platform matrix + Linux alpha
  scope (per `UI_MAUI_BLAZOR.md`); a minimal MAUI Blazor Hybrid window renders on at
  least one of Windows/macOS and can read the current workspace via `AgentSync.Core`.
- **Risks:** MAUI workload friction in CI; Linux not supported by MAUI; accidental
  coupling of Core/CLI to MAUI.

## Milestone G — GUI MVP

- **Goal:** the screens in `UI_MAUI_BLAZOR.md` wired to Core/CRUD/import services.
- **Files likely touched:** `src/AgentSync.Ui/` (Razor pages/components), app services
  in `AgentSync.App`; tests.
- **Commands added:** `agent ui` launches the UI executable; graceful exit-3 when
  absent/headless.
- **Tests:** application services / view models cover dashboard, skill add/edit/delete,
  import dry-run preview, target edits, sync/status/diff/validate orchestration — all
  without a renderer; `agent ui` handler locates the exe and fails gracefully when
  missing.
- **Acceptance:** the GUI performs every CLI-equivalent action through shared services
  with no mutation logic in Razor components; destructive actions show warnings.
- **Risks:** logic leaking into components; blocking the CLI; platform rendering quirks.

## Milestone H — Release packaging

- **Goal:** ship the UI without destabilizing the CLI release.
- **Files likely touched:** `.github/workflows/release.yml`, `RELEASE_CHECKLIST.md`,
  `README.md`/install docs, `docs/PROMOTION.md`.
- **Commands added:** none.
- **Tests:** UI smoke test where feasible (build the UI app per supported platform);
  existing release-smoke (`scripts/release-smoke.sh`) still passes for the CLI.
- **Acceptance:** decision recorded on whether the UI ships in the same Release or
  separately; CLI artifact names unchanged (`agent-sync-<tag>-<rid>.{tar.gz,zip}` +
  `checksums.txt`); install docs cover installing + launching the GUI; `dotnet tool`
  packages remain CLI-only and `agent ui` documents the GUI as a separate install.
- **Risks:** MAUI packaging per platform; release pipeline complexity; keeping the
  alpha honest about which platforms have a GUI.

---

## Cross-cutting acceptance for the whole wave

- All existing tests still pass; new behavior has tests.
- `agent`, `git-agent`, and `git agent ...` unchanged for existing commands.
- `RepoPath`, marker handling, and manual-edit protection unchanged or strengthened.
- Headless CLI builds/tests/runs with no MAUI dependency.
- Docs (`README.md`, `AGENTS.md`, `CLAUDE.md`, `.ai-agent/*`) updated as each milestone
  lands; alpha positioning kept honest.
