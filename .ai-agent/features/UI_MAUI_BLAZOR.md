# Feature plan: `agent ui` (separate GUI) — MAUI Blazor Hybrid + OpenMaui Linux spike

> **Status: planned, not implemented.** Implementation-ready spec for a future session.
> The GUI is a **separate, optional product surface**. The headless CLI must keep
> working with zero GUI dependencies. Do not make `AgentSync.Cli`, `AgentSync.GitAgent`,
> or the `dotnet tool` packages depend on MAUI / OpenMaui / any desktop-UI workload.

`agent ui` is a **launcher/discovery command** that starts a *separately installed*
desktop GUI. The GUI exposes Agent Sync's major features (skills, imports, targets,
sync/status/diff/validate, hooks, version info) and reuses `AgentSync.Core` services —
no business logic is duplicated in the UI, and no CLI code references the UI.

## Implementation status

- **`agent ui` launcher — implemented.** It discovers and launches an external
  `agent-sync-ui` executable via `AgentSync.Core.UiLauncher` (env override
  `AGENT_SYNC_UI`, then next to the binary, then `PATH`) and fails gracefully when the
  GUI is absent. `AgentSync.Cli` has **no** compile-time MAUI/OpenMaui reference (guarded
  by a test).
- **`AgentSync.Ui.Abstractions` — implemented.** A UI-independent application service
  (`AgentSyncApp`) over `AgentSync.Core`, unit-tested without a renderer. The GUI binds
  to this; no repository logic lives in Razor components.
- **`AgentSync.Ui.Maui` — skeleton.** A MAUI Blazor Hybrid project (executable
  `agent-sync-ui`) **excluded from `AgentSync.slnx`** so the headless build/test never
  need the MAUI workload. Build it separately (`dotnet build src/AgentSync.Ui.Maui`).
  Full screens are Milestone H.
- **OpenMaui Linux — evaluated (deferred).** See
  [`OPENMAUI_LINUX_SPIKE.md`](OPENMAUI_LINUX_SPIKE.md).

## Decision

The CLI and GUI are **separate, independent products/surfaces**. These decisions are
settled — implement to them, don't relitigate:

- **CLI and GUI are separate.** The CLI stays headless and GUI-free.
- **The GUI is optional.** Everything Agent Sync does is available from the CLI; the
  GUI is a convenience layer on top.
- **No GUI dependency in the headless stack.** `AgentSync.Cli`, `AgentSync.GitAgent`,
  Git hooks, CI usage, container images, and the `dotnet tool` distribution must
  **never** depend on MAUI, OpenMaui, desktop-UI packages, or platform-specific UI
  packaging. They must build/test/run/ship with no GUI workload installed.
- **`agent ui` is a launcher/discovery command only.** It locates and starts an
  external GUI executable; it must **not** add a compile-time reference from
  `AgentSync.Cli` to MAUI/OpenMaui/UI projects. If the GUI isn't installed, it fails
  gracefully and tells the user how to install/launch it.
- **GUI business operations call shared services.** All repository operations go through
  `AgentSync.Core` (or a UI-independent application-service layer such as
  `AgentSync.Ui.Abstractions`). **Razor components must not contain repository mutation
  logic.**
- **Primary GUI path (Windows / macOS):** the official **.NET MAUI Blazor Hybrid** app.
- **Linux GUI path:** evaluate **OpenMaui / `open-maui/maui-linux`** in a dedicated
  spike. Promising but third-party / non-official. Linux GUI stays **experimental**,
  must **not** block CLI releases, and must **not** be claimed as supported until it has
  a tested build, package, and runtime flow.

## Hard architectural rule

```text
Razor components / pages  ->  application services / view models  ->  AgentSync.Core
```

- **No repository mutation logic in Razor components.** Components call thin application
  services that wrap `AgentSync.Core` (`SyncService`, `StatusService`, `DiffService`,
  `WorkspaceLoader`, `InitService`, `InstallHooksService`, and the planned
  `SkillWriter`/`TargetWriter`/importers). The UI is a view over the same services the
  CLI uses; it must not duplicate CLI logic.
- All safety invariants (`RepoPath`, marker handling, manual-edit detection, `--force`
  semantics) live in Core and are enforced regardless of caller. The UI must not reach
  around them.

## Recommended architecture

```text
src/
  AgentSync.Core/                  # existing — all domain logic (no UI)
  AgentSync.Cli/                   # existing — `agent` (headless, GUI-free)
  AgentSync.GitAgent/              # existing — `git-agent` (headless, GUI-free)
  AgentSync.Ui.Abstractions/       # optional: UI-independent view models / app-service
                                   #   contracts (plain net10.0, no MAUI). Unit-testable
                                   #   without a renderer; could back a web/alt shell.
  AgentSync.Ui.Maui/               # MAUI Blazor Hybrid app for Windows/macOS (the GUI)
  AgentSync.Ui.Linux.OpenMaui/     # optional EXPERIMENTAL spike — only if Milestone F2
                                   #   accepts OpenMaui. Kept out of the default build.
```

- `AgentSync.Ui.Abstractions` is **optional at planning stage** — if it feels like too
  much up front, the view models / application services may start inside
  `AgentSync.Ui.Maui` and be extracted later. The non-negotiable rule is that the UI
  **reuses Core / application services and does not duplicate CLI logic**.
- The GUI executable is named **`agent-sync-ui`** (distinct from the `agent` /
  `git-agent` CLI binaries). `AgentSync.Ui.Maui` produces it for Windows/macOS; the
  OpenMaui spike, if accepted, would produce the Linux build of the same app.
- None of the UI projects are referenced by `AgentSync.Cli` / `AgentSync.GitAgent`. They
  are excluded from the default headless build/test path (separate solution filter or
  build flag); the current solution is `AgentSync.slnx`.

## Launch model: `agent ui`

`agent ui` lives in the CLI dispatch but is a thin **launcher** — it must not pull MAUI
or OpenMaui into `AgentSync.Cli`. It should:

- **Locate** an installed GUI executable named `agent-sync-ui` (on `PATH`, alongside the
  `agent` binary, or via a known/config location).
- **Pass the current repository path** to the GUI (e.g. `agent-sync-ui --repo <path>`),
  resolving the Git root like `GitRepository.Discover` first.
- **Fail gracefully** if no GUI is installed: print install instructions / a download
  URL and exit with code `3` (environment). Never hang, never require a display.
- **Not reference MAUI/OpenMaui assemblies** from `AgentSync.Cli` — discovery is by
  executable name/process launch only.

Example behavior:

```text
agent ui
```

If the GUI is installed:

```text
Launching Agent Sync UI for <repo-path>...
```

If the GUI is not installed:

```text
Agent Sync UI is not installed.
The headless CLI is working.
Install the GUI package or download the desktop app from GitHub Releases.
```

(On Linux, until the OpenMaui spike is accepted, `agent ui` reports that the Linux GUI
is experimental / not yet available and points the user at the CLI — same graceful,
exit-3 path.)

## Platform support matrix

```text
Windows: primary GUI target — MAUI Blazor Hybrid — supported.
macOS:   primary GUI target — MAUI Blazor Hybrid — supported.
Linux:   experimental only — evaluate OpenMaui / maui-linux (Milestone F2).
CLI:     supported everywhere .NET runs — no GUI dependency, on every platform.
```

Notes:

- Official Microsoft **.NET MAUI does not currently list desktop Linux as an official
  target** (its desktop targets are Windows via WinUI 3 and macOS via Mac Catalyst).
- **OpenMaui / `open-maui/maui-linux`** may provide Linux desktop support (e.g. via
  SkiaSharp with X11/Wayland). It is third-party and must be evaluated before any Linux
  GUI commitment.
- **Linux GUI must not be required for alpha promotion**, and Linux users retain **full
  CLI functionality** regardless of GUI status.
- Avoid wording anywhere that says official MAUI supports Linux desktop, and avoid
  wording that says a Linux GUI is impossible. Preferred phrasing:
  *"Linux GUI support is experimental and pending OpenMaui evaluation."*

## OpenMaui Linux evaluation

**Goal:** determine whether OpenMaui can host the Agent Sync GUI on Linux.

**Inputs:**

- `https://github.com/open-maui/maui-linux`
- OpenMaui templates / packages
- required Linux system dependencies
- X11 / Wayland behavior
- WebView / Blazor Hybrid compatibility (if applicable to OpenMaui's rendering)
- packaging feasibility (e.g. AppImage, if available)

**Evaluation criteria (questions the spike must answer):**

- Can a minimal MAUI/OpenMaui app **build** on Linux?
- Can it **run** on Ubuntu/Debian and on one RPM-based distro (e.g. Fedora)?
- Can it host the planned **Blazor UI** (or an equivalent Razor-based UI)?
- Can it call **`AgentSync.Core`** services?
- Can it **open / select a repository folder**?
- Can it **show status** and run a **read-only validation** operation?
- Can it be **packaged** into a user-installable artifact?
- Does it introduce **unacceptable maintenance risk**?

**Result categories:**

- **Accepted** as an experimental Linux GUI path.
- **Deferred.**
- **Rejected for now** — Linux stays CLI-first, with possible future alternatives
  (local Blazor web UI, Avalonia, Uno Platform, Photino, or another Linux-capable
  shell).

## UI goals (feature parity with the CLI)

- Open the current repository (detect the Git root, like `GitRepository.Discover`).
- Show Agent Sync initialization state (is there a `.agent/agent.yaml`?).
- Show skills; add / edit / delete skills (wraps the CRUD layer — see `CRUD_COMMANDS.md`).
- Import a skill file/folder; import an agent/instruction file/folder (see `IMPORTS.md`).
- Show configured targets; enable / disable / edit targets.
- Run sync (with a clear preview = `--check`, and a guarded `--force`).
- Run status; show drift; show diff (reuse `DiffService` line diffs).
- Run validate; install hooks.
- Show release/version info; provide a copyable CI command
  (`agent status --fail-on-drift --ci`).
- Show **safe warnings before destructive actions** (force-overwrite, delete, disabling
  a target) — mirror the CLI's refuse-by-default posture; mutations require explicit
  confirmation.

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

## Technical concerns

- **GUI workloads stay out of the headless build.** `AgentSync.Ui.Maui` needs the MAUI
  workload (`dotnet workload install maui`, plus platform SDKs); the Linux spike needs
  OpenMaui/maui-linux bits. None of this may be required to build/test `AgentSync.Cli`,
  `AgentSync.GitAgent`, `AgentSync.Core`, the `dotnet tool` packages, or the container
  images. Keep the UI projects out of the default `dotnet build`/`dotnet test` path
  (separate solution filter or build flag).
- **Packaging per platform**: MSIX / unpackaged `.exe` (Windows), `.app` / `.pkg`
  (macOS via Mac Catalyst); AppImage or similar for Linux *only if* OpenMaui is
  accepted. Separate from the self-contained `agent` / `git-agent` archives.
- **Release artifacts**: GUI ships as **separate** artifacts from the CLI. The CLI
  release naming (`agent-sync-<tag>-<rid>.tar.gz` / `...-win-x64.zip` + `checksums.txt`)
  must stay stable — add UI assets, don't rename CLI ones. CLI releases must be able to
  ship without the GUI.
- **`dotnet tool` distribution**: the `AgentSync` / `AgentSync.Git` tool packages are
  small framework-dependent CLIs; a MAUI app is not a fit for `dotnet tool` packaging.
  `agent ui` *launches* a separately-installed `agent-sync-ui`; it never bundles it.
- **Cross-platform rendering**: the WebView engine differs per OS (WebView2 on Windows,
  WKWebView on macOS, and whatever OpenMaui uses on Linux) — keep the Razor UI to
  standard, engine-agnostic HTML/CSS.
- **Testing UI logic separately from rendering**: put all logic in application services
  / view models (ideally in `AgentSync.Ui.Abstractions`) and unit-test those with the
  existing test setup. Razor components stay declarative. bUnit component tests are
  optional later; the **acceptance bar is that view models are tested without a
  renderer**.

## Tests required

- Application services / view models (in `AgentSync.Ui.Abstractions` or the UI app's
  logic layer): cover dashboard state, skill add/edit/delete flows, import dry-run
  preview, target edits, sync/status/diff/validate orchestration — all without a
  renderer.
- `agent ui` CLI handler: locates `agent-sync-ui`; passes the repo path; graceful,
  exit-3 failure with install guidance when the GUI is absent or the environment is
  headless; never blocks other CLI commands; **no MAUI/OpenMaui reference compiled into
  `AgentSync.Cli`**.
- Build isolation: a CI build/test of Core + CLI + GitAgent (and the tool packages /
  containers) succeeds **without** any GUI workload installed.
