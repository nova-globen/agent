# OpenMaui Linux GUI spike (Milestone F2)

> **Recommendation: DEFER** (experimental, not adopted). The architecture is ready for a
> Linux GUI host, but the four validation gates (build, package, install, runtime) could
> not be run in this environment, and OpenMaui is third-party / non-official. Re-evaluate
> on a machine with the MAUI workload and OpenMaui packages. **No production CLI, no
> release-workflow, and no headless build/test dependency on OpenMaui was introduced.**

## Goal

Determine whether **OpenMaui / `open-maui/maui-linux`** (`https://github.com/open-maui/maui-linux`)
can host the Agent Sync GUI on Linux, reusing the same Razor components and the
UI-independent `AgentSync.Ui.Abstractions` application service.

## What was evaluated (and the environment limits)

This spike was performed in a headless Linux CI/sandbox:

- `dotnet workload list` → **no MAUI workload installed.**
- `dotnet workload search maui` → only `maui-android`, `maui-tizen`, `maui-windows` are
  offered (no Mac Catalyst on Linux, no general desktop MAUI). Upstream .NET MAUI does
  **not** target desktop Linux, which is the whole reason OpenMaui exists.
- OpenMaui ships via its own packages/feeds and is **not** installed here.

Therefore a real Linux MAUI/OpenMaui app could **not** be built or run in this
environment. The spike is an architecture-readiness assessment plus a documented plan,
not a completed build.

## Architecture readiness (the good news)

The GUI was deliberately decoupled in Milestones F/G so that a Linux host is a
swap-in, not a rewrite:

- **`AgentSync.Ui.Abstractions` (`AgentSyncApp`)** has no MAUI/OpenMaui dependency
  (guarded by a test) and is fully unit-tested without a renderer. Any host — MAUI on
  Windows/macOS, OpenMaui on Linux, or a Blazor web/Photino fallback — drives Agent Sync
  through this one service.
- **Razor components** read state and invoke confirmed actions via `AgentSyncApp`; they
  contain no repository logic, so they are host-agnostic.
- **`agent ui`** launches the GUI by executable name (`agent-sync-ui`) with no
  compile-time UI reference, so a Linux build of the GUI would be discovered the same way
  as the Windows/macOS build.

So the work an OpenMaui Linux host needs is the *host shell* (a Linux MAUI program + app
entry), reusing the existing component tree and `AgentSyncApp`.

## Evaluation criteria — status

| Criterion | Status in this environment |
| --- | --- |
| Minimal MAUI/OpenMaui app builds on Linux | **Not run** (no workload/packages) |
| Runs on Ubuntu/Debian + one RPM distro | **Not run** |
| Hosts the planned Blazor/Razor UI | Plausible — components are host-agnostic; unverified |
| Calls `AgentSync.Core` services | **Yes, by construction** — via `AgentSync.Ui.Abstractions` (verified by tests) |
| Open/select a repository folder | Not run (UI shell not built) |
| Show status + run read-only validation | Logic verified in `AgentSyncApp` tests; not run in a Linux window |
| Packageable (e.g. AppImage) | **Not assessed** |
| Maintenance risk | **Notable** — third-party fork, tracks upstream MAUI; X11/Wayland + SkiaSharp surface |

## Risks

- **Third-party / non-official.** OpenMaui is not Microsoft-supported; cadence and
  long-term maintenance are uncertain.
- **Runtime surface.** Linux desktop rendering (X11/Wayland, SkiaSharp, WebView for
  Blazor Hybrid) is the least-proven part and must be validated on real distros.
- **Packaging.** AppImage/deb/rpm story is unproven for this stack.

## Decision and guardrails

- **Defer.** Keep Linux **CLI-first**. The CLI is fully supported on Linux and is the
  primary interface; Linux users lose no functionality.
- Do **not** advertise Linux GUI support until a tested Linux artifact exists (build +
  package + install + runtime all pass).
- Do **not** let Linux GUI gate any CLI release.
- Guardrails already in place and verified:
  - `AgentSync.Cli` / `AgentSync.Core` have no MAUI/OpenMaui reference (test:
    `Cli_DoesNotReferenceMauiOrOpenMaui`).
  - `AgentSync.Ui.Abstractions` has no MAUI dependency (test:
    `Abstractions_DoesNotReferenceMaui`).
  - `AgentSync.Ui.Maui` is excluded from `AgentSync.slnx`; the headless build/test pass
    with no GUI workload installed.

## When to re-run this spike

On a machine with the MAUI workload and OpenMaui packages available:

1. Add OpenMaui packages/feeds to an experimental copy of `AgentSync.Ui.Maui` (or a
   sibling `AgentSync.Ui.Linux.OpenMaui`), kept out of `AgentSync.slnx`.
2. Build and run on Ubuntu/Debian and one RPM-based distro.
3. Confirm the Razor UI renders, `AgentSyncApp` calls work, a repo folder opens, and
   status/validate run.
4. Attempt an AppImage (or equivalent) package.
5. Record an **accept / defer / reject** verdict with evidence here. Only on **accept**,
   with a tested artifact, may Linux GUI be presented as experimental in docs/releases.

Alternatives if OpenMaui is ultimately rejected: a local Blazor web UI, Avalonia, Uno
Platform, or Photino — all of which can reuse `AgentSync.Ui.Abstractions`.
