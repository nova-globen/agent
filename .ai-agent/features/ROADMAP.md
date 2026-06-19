# Feature roadmap: imports, CRUD, and UI

> **Status: planned, not implemented.** Milestone-based plan for the next feature wave.
> Implement one milestone at a time, add tests, keep CLI behavior backward compatible,
> and keep the GUI optional (headless CLI must never depend on MAUI). Detailed specs:
> [`IMPORTS.md`](IMPORTS.md), [`CRUD_COMMANDS.md`](CRUD_COMMANDS.md),
> [`UI_MAUI_BLAZOR.md`](UI_MAUI_BLAZOR.md).

Dependency order: **A → B → C** (import), **D → E** (CRUD), **F → F2 → G → H** (UI).
CRUD (D/E) and import (A–C) are independent and can interleave; the UI (F–H) should
come after the import + CRUD services exist, since the GUI reuses them. F2 (OpenMaui
Linux spike) is experimental and never blocks the others or any CLI release.

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

> Architecture is already decided (see `UI_MAUI_BLAZOR.md` → "Decision"): separate
> optional GUI; CLI/git-agent/hooks/CI/`dotnet tool`/containers never depend on
> MAUI/OpenMaui; Windows+macOS official, Linux experimental and non-blocking. This
> milestone *proves out* the architecture and the primary (Windows/macOS) path.

- **Goal:** prove the GUI architecture without tying the CLI to GUI dependencies.
- **Scope:**
  - Decide the exact project split (`AgentSync.Ui.Maui`, optional
    `AgentSync.Ui.Abstractions`).
  - Create the shared UI-independent app-service / view-model boundary if needed.
  - Confirm the `agent ui` launcher strategy (locate + launch external executable).
  - Confirm the GUI executable name: **`agent-sync-ui`**.
  - Confirm the Windows/macOS MAUI Blazor Hybrid path.
  - Confirm `AgentSync.Cli` does **not** reference MAUI/OpenMaui.
- **Files likely touched:** new `src/AgentSync.Ui.Maui/` prototype; optional
  `src/AgentSync.Ui.Abstractions/`; solution wiring (`AgentSync.slnx` + a solution
  filter that excludes UI from the default CI build).
- **Commands added:** none yet (or a stub `agent ui` that reports "GUI not installed"
  and exits 3).
- **Acceptance criteria:**
  - A written architecture decision.
  - The CLI remains buildable/testable without MAUI workloads.
  - The UI project can be excluded from the normal headless build/test path.
  - The `agent ui` launch contract is documented.
- **Risks:** MAUI workload friction in CI; accidental coupling of Core/CLI to MAUI.

## Milestone F2 — OpenMaui Linux spike (experimental)

- **Goal:** evaluate **`open-maui/maui-linux`** for Linux GUI support. Experimental;
  must not gate any CLI release.
- **Scope:**
  - Create a minimal experimental project or branch if appropriate
    (`src/AgentSync.Ui.Linux.OpenMaui/`), kept out of the default build path.
  - Test the OpenMaui template/package.
  - Test X11/Wayland basics.
  - Test whether Blazor Hybrid (or an equivalent Razor-based UI) is realistic.
  - Test calling `AgentSync.Core`.
  - Document build / runtime / packaging constraints (per the "OpenMaui Linux
    evaluation" section of `UI_MAUI_BLAZOR.md`).
- **Commands added:** none (Linux `agent ui` keeps returning the graceful
  experimental/exit-3 message until this proves out).
- **Acceptance criteria:**
  - A clear recommendation: **accept** experimental Linux GUI, **defer**, or **reject**.
  - No production CLI dependency on OpenMaui.
  - No release-workflow dependency on OpenMaui.
  - No Linux GUI support claim unless build **and** runtime are proven.
- **Risks:** OpenMaui maturity/instability; packaging across distros; never claim Linux
  support without a tested artifact; do not let this block CLI releases.

## Milestone G — GUI MVP

- **Goal:** implement the optional desktop GUI for supported platforms.
- **Scope:**
  - Windows/macOS first.
  - Linux **only if** Milestone F2 accepts OpenMaui as an experimental path.
  - Screens: Dashboard, Skills, Imports, Targets, Drift/Status, Diff, Hooks/CI,
    Settings, Logs (wired to Core/CRUD/import services).
- **Files likely touched:** `src/AgentSync.Ui.Maui/` (Razor pages/components), app
  services in `AgentSync.Ui.Abstractions`; tests.
- **Commands added:** `agent ui` launches `agent-sync-ui`; graceful exit-3 when
  absent/headless.
- **Acceptance criteria:**
  - The GUI can open a repo and show status.
  - The GUI can run safe (read-only) operations through shared services.
  - The GUI performs mutations only with explicit confirmation.
  - The CLI remains independent.
  - The headless build/test remains green without UI workloads.
- **Risks:** logic leaking into components; blocking the CLI; platform rendering quirks.

## Milestone H — GUI release packaging

- **Goal:** decide and implement packaging for the GUI app **separately** from the CLI.
- **Scope:**
  - Separate GUI release artifacts.
  - Keep the CLI / `dotnet tool` release independent.
  - Optional: `agent ui` points users to the GUI installer.
  - Linux GUI artifact only if the OpenMaui path is accepted and tested.
- **Files likely touched:** `.github/workflows/release.yml`, `RELEASE_CHECKLIST.md`,
  `README.md` / install docs, `docs/PROMOTION.md`.
- **Acceptance criteria:**
  - The CLI release can ship **without** the GUI.
  - The GUI release can ship **independently**.
  - Install docs clearly distinguish CLI and GUI; CLI artifact names unchanged
    (`agent-sync-<tag>-<rid>.{tar.gz,zip}` + `checksums.txt`).
  - Windows/macOS GUI artifacts are official; any Linux GUI artifact is labeled
    experimental; no official Linux-GUI claim without a tested Linux artifact.
- **Risks:** MAUI/OpenMaui packaging per platform; release pipeline complexity; keeping
  the alpha honest about which platforms have an *official* vs *experimental* GUI.

---

## Cross-cutting acceptance for the whole wave

- All existing tests still pass; new behavior has tests.
- `agent`, `git-agent`, and `git agent ...` unchanged for existing commands.
- `RepoPath`, marker handling, and manual-edit protection unchanged or strengthened.
- The headless stack — CLI, `git-agent`, hooks, CI, `dotnet tool` packages, and
  container images — builds/tests/runs/ships with no MAUI/OpenMaui dependency.
- Docs (`README.md`, `AGENTS.md`, `CLAUDE.md`, `.ai-agent/*`) updated as each milestone
  lands; alpha positioning kept honest.
