# Next Steps

Future work, organized by horizon. Keep alpha positioning honest; preserve the core
invariants (see `CLAUDE.md` → "Do not accidentally break").

## Feature wave (import + CRUD shipped; UI in progress)

Implementation-ready specs live under `.ai-agent/features/`. Keep CLI behavior backward
compatible and the GUI optional (the headless CLI must not depend on the UI).

- **Import** — `agent import skill` and `agent import agent` (implemented;
  `features/IMPORTS.md`).
- **Skill/target CRUD** — `agent skill …` / `agent target …` (implemented;
  `features/CRUD_COMMANDS.md`).
- **`agent ui` launcher** — launches a *separately installed* `agent-sync-ui` with
  `--repo`/`--port`/`--token`, opens the loopback URL, exit-3 with install guidance when
  absent (implemented; `features/UI_LOCALHOST_BLAZOR.md`).
- **Localhost Blazor Web UI** — `AgentSync.Ui.Web` (executable `agent-sync-ui`) using
  **Microsoft FluentUI Blazor components**, bound to `127.0.0.1` with a random port and a
  short-lived session token. Minimal host implemented (dashboard + read-only screens);
  remaining work: wire mutations with confirmations and finish placeholder screens
  (Milestone UI-2).
- **Separate GUI packaging** — `agent-sync-ui` ships as its own release artifacts,
  independent of the CLI / `dotnet tool` release (Milestone UI-3, not yet done).

> The earlier MAUI/OpenMaui GUI direction was dropped in favour of the localhost web UI;
> the MAUI project and the OpenMaui spike doc were removed.

Milestone breakdown and acceptance criteria: `features/ROADMAP.md`.

## Before wider promotion

- Final README / public-announcement review (clarity, accuracy, alpha framing).
- Verify the Quick Demo in `README.md` works on a clean machine.
- Verify the alpha limitations list is current.
- Verify the GitHub issue templates render and route correctly.
- Verify install docs (`install.sh` / `install.ps1` commands, manual install).
- Verify GitHub release assets exist and checksums validate for `v0.1.0-alpha.2`.

## Near-term product work

- Add a `.gitattributes` recommendation or scaffolding for generated files (e.g. mark
  generated projections so diffs/merges behave sensibly).
- Add symlink escape hardening (resolve real paths; reject symlinked escapes) on top of
  the existing `RepoPath` checks.
- Improve `agent diff` UX (clearer per-target output, summary counts).
- Add more realistic starter skills beyond the default `code-review`.
- Add adapter-specific options (per-target formatting/config knobs).
- Consider an `agent check` alias for `status --fail-on-drift --ci` if it reads better.
- Add CI examples for GitHub Actions, Azure DevOps, and GitLab.

## Distribution

- Homebrew formula (macOS/Linux).
- winget package (Windows).
- `dotnet tool` package, if feasible.
- Container image, if useful for CI usage.

## Longer-term

- MCP config projection (project skills/config into MCP server definitions).
- More agent target adapters (community-requested tools).
- Skill schema versioning and migrations (so v1 can evolve safely).
- Add adapter-version tracking and unsupported-adapter-version drift detection.
- Init templates by ecosystem (language/framework-specific starter skills).
- A proper documentation site.
