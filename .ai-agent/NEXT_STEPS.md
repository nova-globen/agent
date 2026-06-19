# Next Steps

Future work, organized by horizon. Keep alpha positioning honest; preserve the core
invariants (see `CLAUDE.md` → "Do not accidentally break").

## Next major feature wave

Planned (not yet implemented) — implementation-ready specs live under
`.ai-agent/features/`. Build milestone by milestone; keep CLI behavior backward
compatible and the GUI optional (headless CLI must not depend on MAUI).

- **Import a skill file/folder** — `agent import skill` (see `features/IMPORTS.md` §A).
- **Import an agent/instruction file/folder** — `agent import agent` for existing
  `AGENTS.md` / `CLAUDE.md` / Copilot / Gemini / Cursor / skill folders
  (`features/IMPORTS.md` §B).
- **Skill CRUD commands** — `agent skill add/edit/delete/list/show`
  (`features/CRUD_COMMANDS.md`).
- **Target CRUD commands** — `agent target add/edit/delete/list/show`
  (`features/CRUD_COMMANDS.md`).
- **`agent ui` launcher command** — a launcher/discovery command that starts a
  *separately installed* GUI executable (`agent-sync-ui`); fails gracefully when the GUI
  isn't installed (`features/UI_MAUI_BLAZOR.md`).
- **Separate, optional GUI app** — GUI code lives in its own project(s); the CLI,
  `git-agent`, hooks, CI, containers, and the `dotnet tool` packages stay GUI-free and
  must not depend on MAUI/OpenMaui.
- **MAUI Blazor Hybrid for Windows/macOS** — the official primary GUI path
  (`AgentSync.Ui.Maui`), reusing `AgentSync.Core` services.
- **OpenMaui Linux spike** — evaluate `open-maui/maui-linux` for an *experimental*
  Linux GUI (Milestone F2); never claimed as supported without a tested build/package/
  runtime; never blocks CLI releases.
- **Separate GUI packaging** — GUI release artifacts ship independently from the CLI /
  `dotnet tool` release.

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
