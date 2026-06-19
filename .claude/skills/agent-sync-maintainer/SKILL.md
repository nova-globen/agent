---
name: Agent Sync Maintainer
description: Maintain the Agent Sync repository — implement changes, run tests, and ship releases while preserving the core invariants.
---

## When to use

Use this when working on the Agent Sync codebase: implementing features or fixes,
updating adapters, adjusting drift detection, writing docs, or preparing a release.

## Product intent

Agent Sync is a Git-native consistency manager. Developers define a **canonical skill**
once under `.agent/`; Agent Sync **projects** it into agent-specific formats (AGENTS.md,
CLAUDE.md, Cursor rules, Copilot/Gemini instructions, OpenAI/Claude skill folders) and
enforces consistency via Git hooks and CI. Current state: public **alpha**
(`v0.1.0-alpha.3`), targeting **.NET 10**.

## Key invariants (do not break without maintainer sign-off)

- `agent sync` **writes by default**; `--check` previews, `--force` overwrites manual edits.
- Manually edited generated sections are detected and **never overwritten** unless `--force`.
- `RepoPath` is the central path-safety guard (rejects absolute / Windows-drive / UNC /
  `..`-escaping paths). Don't weaken it.
- Two entry points: `agent` and `git-agent`; `git agent ...` must keep working
  (`git-agent` delegates to `AgentSync.Cli.CliRunner`).
- Stay on `net10.0`. Keep release artifact names stable. Install-script URLs use `master`.
- Adapters produce **deterministic** output (no timestamps/randomness).
- Hooks **fail when `agent` is missing** (exit 3), never silently pass.

## Common commands

```bash
dotnet build --configuration Release
dotnet test
scripts/release-smoke.sh
```

Product CLI: `agent init | sync | status | diff | validate | doctor | install-hooks`
(each also as `git agent ...`).

## Drift-detection behavior

- Shared files (AGENTS.md, CLAUDE.md, Copilot, Gemini) use `agent-sync` start/end
  markers; hand edits are detected by comparing the section's content hash to the marker.
- Whole-file targets (Cursor, OpenAI/Claude skill folders) detect manual edits via the
  lockfile hash.
- `agent status --fail-on-drift --ci` exits non-zero on drift; the pre-commit hook uses
  it to block commits.
- Policy flags in `agent.yaml` (`fail_on_missing_projection`, `fail_on_outdated_projection`,
  `fail_on_manual_edit`) can downgrade a drift type to a warning (still reported, not failing).

## Testing expectations

- Add/update tests for every behavior change. Core logic → `tests/AgentSync.Core.Tests`,
  CLI → `tests/AgentSync.Cli.Tests`. Adapter output is covered by golden files under
  `tests/AgentSync.Core.Tests/Golden`.
- Keep generated output deterministic so hashing and golden tests stay stable.

## Release workflow

- Tag-driven: `git tag vX.Y.Z && git push origin vX.Y.Z` triggers
  `.github/workflows/release.yml` (build, test, publish self-contained binaries for all
  runtimes, checksums, GitHub Release). Follow `RELEASE_CHECKLIST.md`.

## Documentation tone

- Honest alpha positioning. No private conversation URLs or local machine paths in public
  docs. No AI/Claude trailers in commit messages. Prefer small commits.
