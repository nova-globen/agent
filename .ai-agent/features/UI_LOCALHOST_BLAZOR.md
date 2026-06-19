# Feature plan: `agent ui` — localhost Blazor Web UI

> **Status: launcher + minimal web host implemented; feature wiring in progress.** The
> GUI is a **separate, optional product surface** — a localhost Blazor web host
> (`agent-sync-ui`) using Microsoft FluentUI Blazor components. The headless CLI keeps
> working with zero GUI dependencies.

`agent ui` is a **launcher/discovery command** that starts a separately installed local
web host. The host serves a browser UI on `127.0.0.1`, gated by a short-lived session
token, and drives Agent Sync through `AgentSync.Ui.Abstractions` (`AgentSyncApp`) — no
business logic is duplicated, and no CLI code references the web host.

> **History:** an earlier plan targeted .NET MAUI Blazor Hybrid (Windows/macOS) with an
> experimental OpenMaui Linux spike. That direction was **dropped** in favour of this
> single cross-platform localhost web UI. The MAUI project and the OpenMaui spike doc
> have been removed.

## Decision

- The primary GUI is a **separate, optional localhost Blazor Web UI**.
- GUI executable name: **`agent-sync-ui`**.
- `agent ui` is a **launcher/discovery command only** (no UI rendering in the CLI).
- The CLI and Git extension stay **headless and GUI-free**; hooks, CI, containers, and
  the `dotnet tool` packages never depend on the UI or on any web/UI assemblies.
- `AgentSync.Cli` must not reference `AgentSync.Ui.Web` (no compile-time UI dependency);
  it knows only the executable name and the launch protocol.
- The `dotnet tool` package remains CLI-only unless explicitly changed later.
- The GUI is **independently packaged** and shipped on its own cadence.
- The GUI uses **`AgentSync.Ui.Abstractions`** and **`AgentSync.Core`**.
- UI rendering is separated from repository mutation logic — **Razor components contain
  no repository mutation logic**; they call `AgentSyncApp`.
- The UI is built with **Microsoft FluentUI Blazor components**
  (`Microsoft.FluentUI.AspNetCore.Components`).

## Architecture

```text
src/
  AgentSync.Core/                  # all domain logic (no UI)
  AgentSync.Cli/                   # `agent` (headless, GUI-free)
  AgentSync.GitAgent/              # `git-agent` (headless, GUI-free)
  AgentSync.Ui.Abstractions/       # UI-independent application service (AgentSyncApp); no web/UI deps
  AgentSync.Ui.Web/                # ASP.NET Core + Blazor host -> executable `agent-sync-ui`
```

Optional later — add **only if useful**, do not add unnecessary projects:

```text
src/AgentSync.Ui.Shared/           # shared Razor components / view models, if reused beyond one host
```

`AgentSync.Ui.Web` is an ASP.NET Core / Blazor host (Razor Components, server-side
rendering) using FluentUI components. It references `AgentSync.Ui.Abstractions` (and
`AgentSync.Core` for shared types), never the reverse.

```text
Razor components (FluentUI)  ->  AgentSyncApp (AgentSync.Ui.Abstractions)  ->  AgentSync.Core
```

## Launch model

`agent ui` (in `AgentSync.Cli`, via `AgentSync.Core.UiLauncher` / `UiSession`):

1. Resolve the current repository path (`GitRepository.Discover`, falling back to cwd).
2. Locate `agent-sync-ui` (`AGENT_SYNC_UI` override → next to the `agent` binary →
   `PATH`).
3. Choose a free loopback port (`UiSession.FindFreePort`).
4. Generate a short-lived session token (`UiSession.NewToken`).
5. Launch:

   ```bash
   agent-sync-ui --repo <repo-path> --port <port> --token <token>
   ```

6. Open the browser at:

   ```text
   http://127.0.0.1:<port>/?token=<token>
   ```

7. Always print that URL so the user can open it if the browser does not.

If the GUI is missing, `agent ui` exits with code **3** and prints:

```text
Agent Sync UI is not installed.
The headless CLI is working.
Install the Agent Sync UI package or download the local web UI from GitHub Releases.
```

The CLI has **no** compile-time reference to the UI; discovery/launch is by executable
name only (guarded by a test).

## Security

- **Bind to `127.0.0.1` by default** — loopback only (`builder.WebHost.UseUrls`). Never
  bind `0.0.0.0`; that would require a future explicit, reviewed flag.
- **Random/free port by default.**
- **Short-lived session token**, generated per launch and passed to the host.
- **Validate the token before any operation** — a middleware (`SessionGate`) rejects
  every request lacking a valid token (query `?token=` on first navigation, then an
  HttpOnly, `SameSite=Strict` cookie). Token comparison is fixed-time (`TokenCheck`).
- **No unauthenticated mutation endpoints**; the token gate is global.
- **Explicit confirmation for destructive operations** (force-overwrite, delete,
  disable a target) before calling `AgentSyncApp`.
- **Avoid logging tokens** — the token is never written to logs or stderr; it appears
  only in the access URL printed to the user's own terminal.
- **CSRF / same-origin:** antiforgery is enabled (`UseAntiforgery`) and the cookie is
  `SameSite=Strict`.
- **One repository per session** — the host is scoped to the single `--repo` path; the
  active repo path is shown clearly in the UI.

## Feature scope

The UI should eventually support (all through `AgentSyncApp`):

- Dashboard + repository initialization state
- Skills: list / show / add / edit / delete
- Import skill; import agent/instruction file
- Targets: list / show / add / edit / delete
- Status / drift
- Diff viewer
- Sync; validate; hooks / CI
- Settings / logs
- Version / release info

Current state: Dashboard, Skills (list), Targets (list), Status/Drift are wired
read-only; Imports, Diff, Hooks/CI, Settings are placeholders. Mutations are not yet
wired into the UI (they exist and are tested on `AgentSyncApp`); they will be added with
explicit confirmation dialogs.

## Packaging

- The **CLI release remains independent** and GUI-free; CLI artifact names
  (`agent-sync-<tag>-<rid>.{tar.gz,zip}` + `checksums.txt`) are unchanged.
- The **`dotnet tool` packages stay CLI-only.**
- `agent-sync-ui` ships **separately**, as a self-contained executable per OS/runtime,
  as an optional archive in GitHub Releases, and possibly a future package-manager
  package. Proposed (future) names:

  ```text
  agent-sync-ui-<version>-<rid>.tar.gz
  agent-sync-ui-<version>-win-x64.zip
  ```

- `agent ui` tells users how to install the GUI when it is missing.
- A GUI release must not break or block the CLI release.

## Implementation status

- **`agent ui` launcher — implemented.** Free port + token + repo passed to
  `agent-sync-ui`; prints the loopback URL; exit-3 with install guidance when absent.
  No compile-time UI reference in the CLI (test-guarded).
- **`AgentSync.Ui.Abstractions` (`AgentSyncApp`) — implemented + tested** (no UI deps).
- **`AgentSync.Ui.Web` — minimal FluentUI Blazor host implemented.** Binds `127.0.0.1`,
  token middleware, dashboard + read-only screens + placeholders. Builds with the
  standard SDK (no special workloads); verified at runtime (401 without token, 200 +
  FluentUI dashboard with token).
- **Remaining:** wire mutations with confirmations, finish placeholder screens, and add
  separate GUI release artifacts (Milestones UI-2 / UI-3 in `ROADMAP.md`).

## Tests

- `WebOptions.Parse` (repo/port/token/`--no-open`) and `TokenCheck.Matches`
  (`AgentSync.Ui.Web.Tests`) — no browser required.
- `agent ui` launcher: locates `agent-sync-ui`, passes repo/port/token, prints the
  loopback URL, keeps the token out of stderr, exit-3 when absent
  (`AgentSync.Cli.Tests/UiCommandTests`).
- `AgentSyncApp` capability coverage (`AgentSync.Ui.Abstractions.Tests`).
- Build isolation: the CLI/Core/GitAgent and tool packages build with no GUI assemblies;
  `AgentSync.Cli` references neither the web host nor FluentUI (test-guarded).
