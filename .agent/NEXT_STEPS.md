# Next Steps

Future work, organized by horizon. Preserve the core invariants
(see `CLAUDE.md` → "Do not accidentally break").

## Feature wave (import + CRUD + UI shipped)

Implementation-ready specs live under `.agent/features/`. Keep CLI behavior backward
compatible and the GUI optional (the headless CLI must not depend on the UI).

- **Import** — `agent import skill` and `agent import agent` (implemented;
  `features/IMPORTS.md`).
- **Skill/target CRUD** — `agent skill …` / `agent target …` (implemented;
  `features/CRUD_COMMANDS.md`).
- **`agent ui` launcher + installer** — launches `agent-sync-ui` with
  `--repo`/`--port`/`--token` (`--no-open`), waits for its `/healthz` readiness endpoint,
  and opens the browser at the token URL (printing only the clean URL). When the UI is
  absent it **auto-installs** it (the `AgentSync.Ui` .NET tool when `dotnet` is present,
  otherwise the matching release archive into `~/.agent-sync/ui/`) and only prints install
  guidance / exits 3 if that fails (implemented; `features/UI_LOCALHOST_BLAZOR.md`).
- **Localhost Blazor Web UI** — `AgentSync.Ui.Web` (executable `agent-sync-ui`) using
  **Microsoft FluentUI Blazor components**, bound to `127.0.0.1` with a random port and a
  per-launch session token (exchanged into an HttpOnly cookie and stripped from the URL on
  first use; unauthenticated `/healthz`). UI-2 wired: the host runs Interactive Server and
  all screens (Dashboard, Skills, Imports, Targets, Status/Drift, Diff, Hooks/CI, Settings)
  drive `AgentSyncApp` via view-models (`AgentSync.Ui.Web/ViewModels/*`); file-writing
  actions use explicit submit buttons and destructive ones (delete, force sync, install
  hooks) require a second confirmation step (Milestone UI-2 done).
- **Separate GUI packaging** — `agent-sync-ui` ships both as its own
  `agent-sync-ui-<tag>-<rid>` release artifacts **and** as the `AgentSync.Ui` .NET tool
  (command `agent-sync-ui`), via a separate `release-ui` job (`needs: release`), independent
  of the CLI / CLI-`dotnet tool` release; UI checksums are merged into `checksums.txt` and a
  UI build failure never blocks the CLI release (Milestone UI-3 done).

> The earlier MAUI/OpenMaui GUI direction was dropped in favour of the localhost web UI;
> the MAUI project and the OpenMaui spike doc were removed.

Milestone breakdown and acceptance criteria: `features/ROADMAP.md`.

## Before the next release

- Verify the Quick Demo in `README.md` works on a clean machine.
- Verify the GitHub issue templates render and route correctly.
- Verify install docs (`install.sh` / `install.ps1` commands, manual install).
- Verify GitHub release assets (CLI + UI archives + the `AgentSync.Ui` tool) exist and
  checksums validate.
- Manually confirm the installed UI loads CSS/JS in a real browser when launched via
  `agent ui` from inside a repo.

## Near-term product work

- Add a `.gitattributes` recommendation or scaffolding for generated files (e.g. mark
  generated projections so diffs/merges behave sensibly).
- Add symlink escape hardening (resolve real paths; reject symlinked escapes) on top of
  the existing `RepoPath` checks.
- Improve `agent diff` UX (clearer per-target output, summary counts).
- Expand the `--with-samples` starter pack with ecosystem-specific skill sets (Node.js, Python, etc.).
- Add adapter-specific options (per-target formatting/config knobs).
- Consider an `agent check` alias for `status --fail-on-drift --ci` if it reads better.
- Add a GitLab CI example (`examples/github-actions` and `examples/azure-pipelines` ship;
  GitLab still to do).

## Distribution

- Homebrew formula (macOS/Linux).
- winget package (Windows).
- `dotnet tool` packages — done (`AgentSync`, `AgentSync.Git`, and `AgentSync.Ui`).
- Container image, if useful for CI usage.

## Longer-term

- MCP config projection (project skills/config into MCP server definitions).
- More agent target adapters (community-requested tools).
- Skill schema versioning and migrations (so v1 can evolve safely).
- Add adapter-version tracking and unsupported-adapter-version drift detection.
- Init templates by ecosystem (language/framework-specific starter skills).
- A proper documentation site.
