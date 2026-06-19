# Feature plan: `agent ui` (.NET MAUI Blazor Hybrid GUI)

> **Status: planned, not implemented.** Implementation-ready spec for a future session.
> The GUI is **optional**: the headless CLI must keep working with zero MAUI
> dependencies. Do not make `AgentSync.Cli` / `AgentSync.Core` depend on MAUI.

`agent ui` opens a desktop GUI that exposes Agent Sync's major features (skills,
imports, targets, sync/status/diff/validate, hooks, version info). It is built with
**.NET MAUI Blazor Hybrid** and reuses `AgentSync.Core` services directly — no
business logic is duplicated in the UI.

## Decision (locked)

The GUI is a **separate, optional product surface**. The following decisions are
settled — implement to them, don't relitigate:

- **The CLI, Git extension, Git hooks, CI, the `dotnet tool` packages, and container
  images must NEVER depend on MAUI, OpenMaui, or any GUI workload.** Headless usage is
  the primary product and must build/test/run/ship with zero GUI dependencies.
- **Primary GUI path (Windows / macOS):** the official **.NET MAUI Blazor Hybrid** app.
- **Linux GUI path:** evaluate **OpenMaui / maui-linux** in a dedicated spike. Treat
  Linux GUI as **experimental** until proven by build, packaging, install, and runtime
  tests.
  - Do **not** block CLI releases on Linux GUI support.
  - Do **not** claim official Linux GUI support until there is a tested release
    artifact (build + package + install + runtime all verified).
- The GUI ships as a **separate publishable app** (`AgentSync.Ui`); `agent ui` launches
  it and fails gracefully when it is absent (it is never bundled into the CLI or the
  `dotnet tool` packages).

## Hard architectural rule

```text
Razor components / pages  ->  application services (view models)  ->  AgentSync.Core
```

- **No repository mutation logic in Razor components.** Components call thin
  application services that wrap `AgentSync.Core` (`SyncService`, `StatusService`,
  `DiffService`, `WorkspaceLoader`, `InitService`, `InstallHooksService`, and the
  planned `SkillWriter`/`TargetWriter`/importers). The UI is a view over the same
  services the CLI uses.
- All the safety invariants (`RepoPath`, marker handling, manual-edit detection,
  `--force` semantics) live in Core and are enforced regardless of caller. The UI must
  not reach around them.

## Proposed solution layout

```text
src/
  AgentSync.Cli/        # existing — `agent`
  AgentSync.Core/       # existing — all domain logic
  AgentSync.GitAgent/   # existing — `git-agent`
  AgentSync.Ui/         # NEW — MAUI Blazor Hybrid app (references AgentSync.Core)
  AgentSync.App/        # NEW (optional) — platform-neutral app services / view models
```

- `AgentSync.Ui` references `AgentSync.Core` and contains the MAUI host + Razor
  components.
- Consider a separate `AgentSync.App` (plain `net10.0` class library, no MAUI) holding
  the view models / application services so they are **unit-testable without a UI
  renderer** and could back a future web or alternative shell. If kept small, these can
  live in `AgentSync.Ui` initially and be extracted later.

## UI goals (feature parity with the CLI)

- Open the current repository (and detect the Git root, like `GitRepository.Discover`).
- Show Agent Sync initialization state (is there a `.agent/agent.yaml`?).
- Show skills; add / edit / delete skills (wraps the CRUD layer — see `CRUD_COMMANDS.md`).
- Import a skill file/folder; import an agent/instruction file/folder (see `IMPORTS.md`).
- Show configured targets; enable / disable / edit targets.
- Run sync (with a clear preview = `--check`, and a guarded `--force`).
- Run status; show drift; show diff (reuse `DiffService` line diffs).
- Run validate; install hooks.
- Show release/version info (`agent --version`, current release).
- Provide a copyable CI command (`agent status --fail-on-drift --ci`).
- Show **safe warnings before destructive actions** (force-overwrite, delete,
  disabling a target) — mirror the CLI's refuse-by-default posture.

## Screens

```text
Dashboard      # repo path, init state, skill/target counts, drift summary, quick actions
Skills         # list; open editor; add/delete
Skill Editor   # name/description/version, target toggles, SKILL.md body editor
Imports        # pick a skill or agent source; dry-run preview; confirm import
Targets        # known targets, enabled/path, edit; drift/lockfile consequences shown
Drift / Status # status report; per-issue severity; "fix" actions (sync / sync --force)
Diff Viewer    # canonical-to-projection diffs (reuse DiffService output)
Hooks / CI     # install hooks; copyable CI command; hook state
Settings       # repo selection, agent.yaml policy toggles
Logs           # output of the last operations (what the CLI would have printed)
```

## `agent ui` launch behavior

The `agent ui` *command* lives in the CLI dispatch but must **launch the separate UI
executable** rather than pull MAUI into the CLI:

- `RunUi(...)` locates the `AgentSync.Ui` executable (alongside the `agent` binary, or
  via a known relative path / config) and starts it with the current repo path as an
  argument, then returns.
- If the UI executable is not present (e.g. CLI-only / self-contained release without
  the GUI), fail **gracefully** with a clear message: how to install the UI, or that
  the GUI isn't available on this platform. Exit code `3` (environment).
- If not inside a Git repo, the UI offers to open a folder or initialize the current
  folder (`InitService`), matching `agent init`'s fall-back-to-cwd behavior.
- If no `.agent/agent.yaml` exists, the UI shows a setup wizard (wraps `init`).
- In a headless/CI environment (no display), `agent ui` must not hang — detect and exit
  with a clear error. **CLI commands must never block on or require the UI.**

## Platform constraints (decide scope in the spike — Milestone F)

.NET MAUI's supported targets are **Windows (WinUI 3), macOS (Mac Catalyst), iOS, and
Android**. **MAUI does not target desktop Linux.** Blazor Hybrid renders via a
`BlazorWebView` inside the MAUI host on those platforms.

Implications:

- **Windows + macOS** (primary, official): MAUI Blazor Hybrid is the path. Good fit.
- **Linux desktop** (experimental): upstream MAUI does not target desktop Linux. Per
  the locked decision, the Linux path is a **dedicated OpenMaui / maui-linux spike**
  (Milestone F-Linux). It stays **experimental** until build, packaging, install, and
  runtime tests all pass, and Linux GUI support must **never block a CLI release**. On
  Linux until then, `agent ui` returns a clear "GUI is experimental / not available on
  Linux yet; use the CLI" message and exits gracefully (exit 3).
  - The Razor components and view models (ideally in `AgentSync.App`) are shared, so an
    OpenMaui Linux host reuses the same UI; a Blazor Server / Photino host remains a
    fallback option to evaluate if OpenMaui doesn't pan out.
  - Do not advertise official Linux GUI support anywhere (README, PROMOTION, release
    notes) until a tested Linux release artifact exists.

## Technical concerns

- **MAUI / OpenMaui workload**: `AgentSync.Ui` needs the MAUI workload
  (`dotnet workload install maui`, plus OpenMaui/maui-linux bits for the Linux spike)
  and platform SDKs to build. This must **not** be required to build/test `AgentSync.Cli`,
  `AgentSync.GitAgent`, or `AgentSync.Core`, nor to build the `dotnet tool` packages or
  the container images. Keep `AgentSync.Ui` out of the default `dotnet build`/
  `dotnet test` path used by CI (separate solution filter, or a build flag), so
  contributors and CI without the GUI workload aren't broken. The current solution is
  `AgentSync.slnx`.
- **Packaging per platform**: MSIX/unpackaged `.exe` (Windows), `.app`/`.pkg`
  (macOS via Mac Catalyst). Separate from the existing self-contained
  `agent`/`git-agent` archives.
- **Release artifacts**: decide whether the UI ships in the same GitHub Release (extra
  per-platform assets) or as a separate download / channel. The CLI release naming
  (`agent-sync-<tag>-<rid>.tar.gz` / `...-win-x64.zip` + `checksums.txt`) must stay
  stable — add UI assets, don't rename CLI ones.
- **`dotnet tool` distribution**: the `AgentSync` / `AgentSync.Git` tool packages are
  small framework-dependent CLIs. A MAUI app is **not** a good fit for `dotnet tool`
  packaging. Proposal: the tool's `agent ui` *launches* a separately-installed UI; it
  does not bundle it. Document that `agent ui` requires installing the GUI app.
- **Cross-platform limitations**: see Linux above; also the WebView engine differs per
  OS (WebView2 on Windows, WKWebView on macOS) — keep the Razor UI to standard,
  engine-agnostic HTML/CSS.
- **Testing UI logic separately from rendering**: put all logic in application services
  / view models (ideally in `AgentSync.App`) and unit-test those with the existing
  test setup. Razor components stay declarative. Optionally add bUnit component tests
  later, but the **acceptance bar is that view models are tested without a renderer**.

## Architecture decisions (settled — see "Decision (locked)" above)

- **Separate publishable app** (`AgentSync.Ui`) that `agent ui` launches — **decided**,
  not bundled into the CLI or the `dotnet tool` packages. This keeps the CLI small and
  dependency-free, keeps `dotnet tool` packaging viable, lets the GUI ship on its own
  cadence, and avoids forcing the MAUI/OpenMaui workload on CLI contributors and CI.
- Still to define during the spike (Milestone F): the exact launch contract (`agent ui`
  -> locate + start `AgentSync.Ui` with the repo path; graceful exit-3 when absent), and
  the OpenMaui Linux viability outcome.
- The Windows/macOS/Linux support matrix is fixed by the locked decision: Windows/macOS
  official; Linux experimental and non-blocking for CLI releases.

## Tests required

- Application services / view models (in `AgentSync.App` or `AgentSync.Ui` logic):
  cover dashboard state, skill add/edit/delete flows, import dry-run preview, target
  edits, sync/status/diff/validate orchestration — all without a renderer.
- `agent ui` CLI handler: locates the UI executable; graceful, exit-3 failure when the
  UI is absent or the environment is headless; never blocks other CLI commands.
- Build isolation: a CI build/test of Core + CLI + GitAgent succeeds **without** the
  MAUI workload installed.
