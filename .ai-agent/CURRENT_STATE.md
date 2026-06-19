# Current State

Compact handoff for AI sessions. Pair with `.ai-agent/NEXT_STEPS.md` and
`.ai-agent/VALIDATION_LOG.md`.

## Release

- **Latest tagged release:** `v0.2.0-alpha.1` — the first release to ship the import +
  CRUD commands, `agent ui`, the localhost Blazor UI, and the separate UI release
  artifacts. The earlier `v0.1.0-alpha.1…alpha.4` were CLI-focused (`alpha.4` added the
  `AgentSync` / `AgentSync.Git` NuGet tools; `alpha.2` was a version-only retag of
  `alpha.1`).
- **Next intended release:** `v0.2.0-alpha.2` — makes `agent ui` self-installing: it
  installs the optional UI on first run (the `AgentSync.Ui` .NET tool when a `dotnet` SDK is
  present, otherwise by downloading the matching release archive), ships the UI as a
  `dotnet tool`, and fixes the web UI's static-asset 404s (CSS/JS now load) by serving
  assets with `MapStaticAssets`. Tag `v0.2.0-alpha.2` is pushed to cut it.
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
  `app.MapStaticAssets()` (so `_framework/blazor.web.js` and the FluentUI `_content/...`
  assets load instead of 404ing). All screens are wired with mutations; file-writing actions
  use explicit submit buttons and destructive ones (delete, force sync, install hooks)
  require a second confirmation. The CLI never references the UI/web/FluentUI assemblies
  (`UiInstaller` lives in Core; test-guarded). **Separate GUI release packaging is
  implemented** (Milestone UI-3): both the `agent-sync-ui-<tag>-<rid>` archives and the
  `AgentSync.Ui` .NET tool — see "Release automation".
- **Tests:** all suites pass; run `dotnet test` for the current count.
  `dotnet build -c Release` clean. `scripts/release-smoke.sh` also validates UI packaging
  headlessly (publish shape, invalid-args usage, live `/healthz` + `401`).
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
