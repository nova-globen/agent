# Current State

Compact handoff for AI sessions. Pair with `.ai-agent/NEXT_STEPS.md` and
`.ai-agent/VALIDATION_LOG.md`.

## Release

- **Latest tagged release:** `v0.2.0-alpha.2` — made `agent ui` self-installing (installs
  the optional UI on first run: the `AgentSync.Ui` .NET tool when a `dotnet` SDK is present,
  otherwise the matching release archive), shipped the UI as a `dotnet tool`, and switched
  the host to `MapStaticAssets`. That cleared the asset `404`s but **not** the empty asset
  bodies, so the UI still came up with no CSS/JS. The earlier `v0.2.0-alpha.1` first shipped
  the import + CRUD commands, `agent ui`, the localhost Blazor UI, and the separate UI
  release artifacts; `v0.1.0-alpha.1…alpha.4` were CLI-focused (`alpha.4` added the
  `AgentSync` / `AgentSync.Git` NuGet tools; `alpha.2` was a version-only retag of
  `alpha.1`).
- **Next intended release:** `v0.2.0-alpha.3` — makes the web UI usable. (a) The host pins
  its content root to `AppContext.BaseDirectory` (the executable's own directory) instead of
  the current working directory, so `MapStaticAssets` finds `wwwroot`/the static-web-assets
  manifest even though `agent ui` launches the host inside the user's repo (the default CWD
  content root made it serve empty `200`s — assets returned but blank). (b) `App.razor` links
  the FluentUI CSS-isolation bundle and `MainLayout.razor` adds a custom app shell
  (`wwwroot/app.css`), so the UI — previously bare, unstyled HTML — now renders styled. (c) It
  removes `<FluentDesignTheme>`, whose JS interop crashed the Interactive Server circuit and
  left every button dead. It also makes `agent init` scaffold a second skill, `using-agent-sync`
  (a `claude_skill`-only guide teaching AI agents how to handle an Agent Sync repo). Push tag
  `v0.2.0-alpha.3` to cut it.
- **Release type:** public alpha / developer preview
- **Repository:** https://github.com/nova-globen/agent (default branch `master`)

## Technical status

- **Target framework:** `.NET 10` (`net10.0`). Solution: `AgentSync.slnx`.
- **Entry points:** `agent` (`src/AgentSync.Cli`) and `git-agent`
  (`src/AgentSync.GitAgent`, delegates to `AgentSync.Cli.CliRunner`).
- **Commands implemented:** `init`, `sync`, `status`, `diff`, `validate`, `doctor`,
  `install-hooks`, `import skill`, `import agent`, `skill add/edit/delete/list/show`,
  `target add/edit/delete/list/show`, and `ui` — all available via both `agent` and
  `git agent`.
- **Local web UI (optional, separate):** `AgentSync.Ui.Web` (executable `agent-sync-ui`)
  is a localhost Blazor host (Interactive Server) using Microsoft FluentUI Blazor
  components, driving Agent Sync through `AgentSync.Ui.Abstractions` (`AgentSyncApp`).
  `agent ui` launches it: free port + per-launch session token, `/healthz` readiness
  poll, opens the browser at the token URL (prints only the clean URL; falls back to the
  token URL on open failure / `--no-open`). When the UI is absent, `agent ui`
  **auto-installs** it via `AgentSync.Core.UiInstaller` (the `AgentSync.Ui` .NET tool when a
  `dotnet` SDK is present, otherwise the matching `agent-sync-ui-v<version>-<rid>` release
  archive extracted under `~/.agent-sync/ui/`), only exiting 3 with install guidance when
  installation fails. The host binds `127.0.0.1`, exchanges the token into an HttpOnly
  cookie and strips it from the URL on first use, and serves static assets with
  `app.MapStaticAssets()` while pinning its content root to `AppContext.BaseDirectory` (not
  the CWD) — so `_framework/blazor.web.js` and the FluentUI `_content/...` assets load with
  real bytes instead of 404ing or returning empty `200`s, even though `agent ui` runs the
  host inside the user's repo. `App.razor` links the FluentUI CSS-isolation bundle
  (`AgentSync.Ui.styles.css`) and `MainLayout.razor` is a custom app shell styled by a global
  `wwwroot/app.css` (dark header/nav rail, light content, live drift-status pill), so the
  components render styled (no `FluentDesignTheme` — its JS interop crashed the circuit; no
  Icons package — kept lean). All screens are wired with mutations; file-writing actions
  use explicit submit buttons and destructive ones (delete, force sync, install hooks)
  require a second confirmation. The CLI never references the UI/web/FluentUI assemblies
  (`UiInstaller` lives in Core; test-guarded). **Separate GUI release packaging is
  implemented** (Milestone UI-3): both the `agent-sync-ui-<tag>-<rid>` archives and the
  `AgentSync.Ui` .NET tool — see "Release automation".
- **Tests:** all suites pass; run `dotnet test` for the current count.
  `dotnet build -c Release` clean. `scripts/release-smoke.sh` also validates UI packaging
  headlessly (publish shape, invalid-args usage, live `/healthz` + `401`, and — launching
  the UI from a foreign working directory — a **non-empty** static-asset body and a rendered
  page that **links the FluentUI CSS bundle**, guarding the content-root and styling fixes).
- **`agent init` scaffolds two skills:** `code-review` (all targets) and `using-agent-sync`
  (`claude_skill`-only). Templates in `src/AgentSync.Core/Templates.cs`.
- **Release automation:** `.github/workflows/release.yml` (tag `v*.*.*`) publishes
  self-contained CLI binaries for linux-x64/arm64, osx-x64/arm64, win-x64, plus
  `checksums.txt`; `scripts/install.sh` and `scripts/install.ps1` install from releases.
  The CLI `dotnet tool` packages stay CLI-only. A **separate `release-ui` job** (`needs:
  release`) publishes the optional `agent-sync-ui-<tag>-<rid>` archives, merges their
  checksums in, **and** packs/pushes the `AgentSync.Ui` .NET tool; it can fail independently
  without affecting the CLI release.
- **CI:** `.github/workflows/agent-sync-check.yml` builds/tests on push (`main`/`master`)
  and PRs, and runs an end-to-end drift check in a throwaway repo.
- This repo does **not** dogfood Agent Sync on its own hand-authored `AGENTS.md` /
  `CLAUDE.md` (no root `.agent/`).

## Validated workflows (Windows, v0.1.0-alpha.1)

The full chain was verified manually on Windows with globally installed binaries:

```text
manual edit -> drift detected -> CI/status fails -> Git commit blocked
```

Covered: install, `agent --version` / `git agent --version`, `init`, `sync`,
`status --fail-on-drift --ci`, `git agent status`, `install-hooks`, pre-commit hook
running Agent Sync, manual-edit drift detection in `AGENTS.md`, and a commit being
blocked on drift. Details in `.ai-agent/VALIDATION_LOG.md`.

## Known limitations

- Alpha release, not a stable v1.
- Needs more real-world testing on Linux and macOS.
- Symlink escape hardening is not yet implemented.
- Package-manager install is not yet available.
- Adapters are MVP-level and may evolve.
- Canonical skill schema may change before v1.
- Install scripts need broader environment testing.
- Generated output conventions may evolve based on user feedback.

## Next recommended work

See `.ai-agent/NEXT_STEPS.md`. Highest-leverage now: real-world Linux/macOS validation,
symlink escape hardening, and broader install-script testing before wider promotion.
