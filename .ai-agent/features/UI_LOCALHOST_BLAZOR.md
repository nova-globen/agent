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
4. Generate a per-launch session token (`UiSession.NewToken`).
5. Launch:

   ```bash
   agent-sync-ui --repo <repo-path> --port <port> --token <token> [--no-open]
   ```

6. Poll the host's readiness endpoint (`GET /healthz`, unauthenticated, returns `ok`)
   for a few seconds (`IUiReadinessProbe` / `HttpUiReadinessProbe`). If it never becomes
   ready, print a clear error and exit **3** (so there is no confusing "launched" output
   for a host that did not start, e.g. when the chosen port was taken before it bound).
7. Open the browser at `http://127.0.0.1:<port>/?token=<token>` via a small
   `IBrowserLauncher` abstraction (Windows shell-execute, macOS `open`, Linux `xdg-open`).
   - On success, print only the clean URL: `Opened http://127.0.0.1:<port>/`.
   - On failure (or with `--no-open`), print the token URL on **stdout** so the user can
     open it manually. The token is never written to stderr or logs.

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
- **Per-launch session token**, generated per launch and passed to the host; valid for the
  lifetime of the UI process (a session token, not a time-expiring one — the wording is
  kept accurate).
- **Validate the token before any operation** — a middleware (`SessionGate`) rejects
  every request lacking a valid token (query `?token=` on first navigation, then an
  HttpOnly, `SameSite=Strict` cookie). Token comparison is fixed-time (`TokenCheck`).
- **Token is removed from the URL after first use** — on a valid `?token=` request the
  host sets the cookie and **302-redirects to the same path without the token**, so it
  does not stay in the address bar or browser history.
- **No unauthenticated mutation endpoints**; the token gate is global. The only public
  path is `GET /healthz`, which returns a minimal `ok` and exposes no repository data.
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

Current state (UI-2 wired): the host runs **Interactive Server** and the screens drive
`AgentSyncApp` with explicit in-page confirmation before any destructive/file-writing
action:

- **Dashboard** — repo path, init/Git/skill-count/drift state, quick-action links, CI
  command.
- **Skills** — list/show, add, edit (name/description/version/body-file + enable/disable
  targets), delete (confirm; dry-run + force; explains generated sections may remain).
- **Imports** — import skill and import agent, each with dry-run preview, optional
  id/name/target/type/split, and force; renders the import report.
- **Targets** — list known targets with state, add, edit (path/enabled), delete (confirm;
  dry-run + force; explains lockfile/projection implications).
- **Status / Drift** — status, drift items, validation, refresh, and sync / force-sync
  (force is confirmed).
- **Diff** — `AgentSyncApp.Diff()` grouped per target/skill, with an empty state.
- **Hooks / CI** — copyable CI command + confirmed `InstallHooks()`.
- **Settings** — active repo, version, and local-UI security notes.

No repository logic lives in Razor components — they only call `AgentSyncApp` after
confirmation. Remaining: separate GUI release artifacts (Milestone UI-3).

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

- **`agent ui` launcher — implemented + hardened.** Free port + token + repo passed to
  `agent-sync-ui`; polls `/healthz` readiness before opening the browser
  (`IBrowserLauncher`) at the token URL and printing only the clean URL; falls back to the
  token URL on open failure / `--no-open`; exit-3 with install guidance when absent or on
  readiness timeout. No compile-time UI reference in the CLI (test-guarded).
- **`AgentSync.Ui.Abstractions` (`AgentSyncApp`) — implemented + tested** (no UI deps);
  adds `ListTargets`, `InstallHooks`, and `AppVersion` for the UI-2 screens.
- **`AgentSync.Ui.Web` — FluentUI Blazor host (Interactive Server), UI-2 wired.** Binds
  `127.0.0.1`, strict option parsing (`WebOptions.TryParse`), token middleware that
  exchanges the token into an HttpOnly cookie and strips it from the URL, unauthenticated
  `/healthz`; all screens drive `AgentSyncApp` with confirmation before destructive
  actions. Builds with the standard SDK (no special workloads).
- **Remaining:** add separate GUI release artifacts (Milestone UI-3 in `ROADMAP.md`).

## Tests

- `WebOptions.TryParse` strict validation (missing/empty repo, nonexistent repo,
  missing/invalid/out-of-range port, missing/empty token, unknown option, value-less
  option, valid parse) and `TokenCheck.Matches` (`AgentSync.Ui.Web.Tests`) — no browser
  required.
- `SessionGate` decision logic (cookie vs query vs deny, empty-token denies everything),
  `IsPublicPath` (`/healthz`), and `RedirectWithoutToken` (token stripped, other params
  preserved) — pure, no ASP.NET host required.
- `agent ui` launcher (`AgentSync.Cli.Tests/UiCommandTests`): locates `agent-sync-ui`,
  passes repo/port/token, polls readiness on the launched port, opens the browser at the
  token URL but prints only the clean URL, falls back to the token URL on open failure or
  `--no-open`, exits 3 on readiness timeout / launch failure / when absent, and keeps the
  token out of stderr. Browser/readiness are injected fakes (`IBrowserLauncher` /
  `IUiReadinessProbe`) so tests need no real process or socket.
- `AgentSyncApp` capability coverage (`AgentSync.Ui.Abstractions.Tests`), including the
  UI-2 mutation paths each screen calls (`Ui2WiringTests`): `ListTargets`, edit/delete
  skill + target, import skill/agent dry-run, sync dry-run, `InstallHooks`, `AppVersion`.
- Web-host smoke (`AgentSync.Ui.Web.Tests/WebHostSmokeTests`): boots the real
  `agent-sync-ui` process against a throwaway repo and asserts `/healthz` readiness, 401
  without a token, 401 on a wrong token, the valid-token 302 that sets the cookie and
  strips the token from the URL, and that the Dashboard/Skills/Imports/Targets/Status/Diff
  pages render their server-side HTML behind the cookie. No browser required.
- Build isolation: the CLI/Core/GitAgent and tool packages build with no GUI assemblies;
  `AgentSync.Cli` references neither the web host nor FluentUI (test-guarded).
