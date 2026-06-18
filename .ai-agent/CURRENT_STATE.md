# Current State

Compact handoff for AI sessions. Pair with `.ai-agent/NEXT_STEPS.md` and
`.ai-agent/VALIDATION_LOG.md`.

## Release

- **Current release:** `v0.1.0-alpha.2` (version-only retag of `alpha.1`; no code
  changes between the two tags)
- **Release type:** public alpha / developer preview
- **Repository:** https://github.com/nova-globen/agent (default branch `master`)

## Technical status

- **Target framework:** `.NET 10` (`net10.0`). Solution: `AgentSync.slnx`.
- **Entry points:** `agent` (`src/AgentSync.Cli`) and `git-agent`
  (`src/AgentSync.GitAgent`, delegates to `AgentSync.Cli.CliRunner`).
- **Commands implemented:** `init`, `sync`, `status`, `diff`, `validate`, `doctor`,
  `install-hooks` — all available via both `agent` and `git agent`.
- **Tests:** Core and CLI test suites passing as of v0.1.0-alpha.1; run `dotnet test`
  for the current count. `dotnet build -c Release` clean.
- **Release automation:** `.github/workflows/release.yml` (tag `v*.*.*`) publishes
  self-contained binaries for linux-x64/arm64, osx-x64/arm64, win-x64, plus
  `checksums.txt`; `scripts/install.sh` and `scripts/install.ps1` install from releases.
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
