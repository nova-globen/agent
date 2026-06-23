# Current State

Compact handoff for AI sessions. Pair with `.agent/NEXT_STEPS.md` and
`.agent/VALIDATION_LOG.md`.

## Release

- **Latest tagged release:** `v0.2.0-alpha.5` — adds **`agent sessions`** (back up / restore
  an AI agent's per-project session history for Claude Code, Codex, Copilot, Gemini, and
  Cursor; manifest-driven zip archives; cross-environment restore that relocates the store and
  translates absolute paths across WSL / Windows / Linux — `/mnt/c/...` ⇄ `C:\...` — and a
  changed project path, keeping JSON valid; zip-slip-safe, never overwrites without `--force`)
  and **canonical sub-agents** (`agent subagent add/edit/delete/list/show`, `agent import
  subagent`; projected by `agent sync` into `.claude/agents/<id>.md` with drift in
  `.agent/agents.lock.json`). Copilot/Gemini/Cursor session support is experimental. The
  earlier `v0.2.0-alpha.4` made the repository **run Agent Sync on itself** and added the
  GitHub Actions / Azure Pipelines CI examples and the `releasing-agent-sync` skill;
  `v0.2.0-alpha.3` made the web UI usable; `v0.2.0-alpha.2` made `agent ui` self-installing;
  `v0.2.0-alpha.1` first shipped import + CRUD, `agent ui`, and the localhost Blazor UI;
  `v0.1.0-alpha.1…alpha.4` were CLI-focused.
- **Next intended release:** `v0.2.0-alpha.6` — **sub-agent and authoring polish** driven by
  dogfooding feedback. Adds **per-subcommand `--help`** across the `skill`/`target`/`subagent`/
  `import` subcommands; **`skill`/`subagent edit --body-file` now accepts absolute (and
  repo-external) paths** instead of rejecting them as unsafe (a body file is read-only input;
  the `RepoPath` guard on projection writes is unchanged); **`target list` surfaces the
  sub-agent destination** (`claude_agent -> .claude/agents/<id>.md`); **sub-agent `--color`**
  is captured on `import`, stored in `agent.yaml`, and re-emitted on projection (settable via
  `subagent add/edit --color`); and **`import subagent` with no path discovers `.claude/agents/`**
  and reconciles the sub-agent lockfile so importing one's own existing projection no longer
  registers as a manual edit. The `using-agent-sync` scaffolded guidance now documents
  sub-agents. Push tag `v0.2.0-alpha.6` to cut it.
- **Release type:** public alpha / developer preview
- **Repository:** https://github.com/nova-globen/agent (default branch `master`)

## Technical status

- **Target framework:** `.NET 10` (`net10.0`). Solution: `AgentSync.slnx`.
- **Entry points:** `agent` (`src/AgentSync.Cli`) and `git-agent`
  (`src/AgentSync.GitAgent`, delegates to `AgentSync.Cli.CliRunner`).
- **Commands implemented:** `init`, `sync`, `status`, `diff`, `validate`, `doctor`,
  `install-hooks`, `import skill`, `import agent`, `import subagent`,
  `skill add/edit/delete/list/show`, `target add/edit/delete/list/show`,
  `subagent add/edit/delete/list/show`, `sessions backup/restore/list/providers`, and `ui` —
  all available via both `agent` and `git agent`.
- **Sub-agents:** canonical sub-agents under `.agent/agents/<id>/` (`agent.yaml` + `AGENT.md`)
  projected by `agent sync` into `.claude/agents/<id>.md`; drift tracked in
  `.agent/agents.lock.json` (`AgentSync.Core/Subagents/`).
- **Session backup/restore:** `AgentSync.Core/Sessions/` — provider registry (Claude, Codex,
  Copilot, Gemini, Cursor), `PathConversion`/`PathRewriter` translate absolute paths across
  WSL/Windows/Linux on restore, manifest-driven archives, zip-slip-safe extraction confined to
  the agent's home directory.
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
  and PRs, runs the drift gate against this repository itself (the dogfood check below),
  and also runs an end-to-end drift check in a throwaway repo.
- **This repo now dogfoods Agent Sync.** Its agent instruction files — `AGENTS.md`,
  `CLAUDE.md`, `.github/copilot-instructions.md`, `.gemini/GEMINI.md`, and the
  `.claude/skills/` folders — are generated projections of the canonical skills under
  `.agent/skills/` (`agent-sync-overview`, `agent-sync-maintainer`, `using-agent-sync`,
  `releasing-agent-sync`).
  Edit the skill, run `agent sync`, and commit the regenerated projections together; never
  hand-edit a generated section (the Git hooks and CI enforce it). The planning/spec docs
  (this file, `NEXT_STEPS.md`, `PRODUCT_SPEC.md`, `ARCHITECTURE.md`, `features/`, `prompts/`,
  …) also live under `.agent/` now — moved from the former `.ai-agent/`.

## Validated workflows (Windows, v0.1.0-alpha.1)

The full chain was verified manually on Windows with globally installed binaries:

```text
manual edit -> drift detected -> CI/status fails -> Git commit blocked
```

Covered: install, `agent --version` / `git agent --version`, `init`, `sync`,
`status --fail-on-drift --ci`, `git agent status`, `install-hooks`, pre-commit hook
running Agent Sync, manual-edit drift detection in `AGENTS.md`, and a commit being
blocked on drift. Details in `.agent/VALIDATION_LOG.md`.

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

See `.agent/NEXT_STEPS.md`. Highest-leverage now: real-world Linux/macOS validation,
symlink escape hardening, and broader install-script testing before wider promotion.
