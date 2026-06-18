# Next Steps

Future work, organized by horizon. Keep alpha positioning honest; preserve the core
invariants (see `CLAUDE.md` → "Do not accidentally break").

## Before wider promotion

- Final README / public-announcement review (clarity, accuracy, alpha framing).
- Verify the Quick Demo in `README.md` works on a clean machine.
- Verify the alpha limitations list is current.
- Verify the GitHub issue templates render and route correctly.
- Verify install docs (`install.sh` / `install.ps1` commands, manual install).
- Verify GitHub release assets exist and checksums validate for `v0.1.0-alpha.1`.

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
