# Contributing to Agent Sync

Thanks for your interest in improving Agent Sync. This document explains how to set
up the project, the conventions we follow, and how to get a change merged.

## Prerequisites

- .NET SDK 8.0 or newer (the repository currently builds and runs on .NET 10).
- Git.

## Build and test

```bash
dotnet restore
dotnet build --configuration Release
dotnet test
```

All tests must pass before a change is merged. New behavior must come with tests.

## Project layout

```text
src/AgentSync.Core/       # domain logic: config, skills, projections, adapters, drift
src/AgentSync.Cli/        # the 'agent' command (thin layer over Core)
src/AgentSync.GitAgent/   # the 'git-agent' extension; delegates to the CLI
tests/                    # xUnit tests (Core + CLI), plus golden files
```

Keep logic in `AgentSync.Core` and keep the CLI a thin adapter that maps results to
exit codes and output. This keeps behavior testable without spawning processes.

## Coding conventions

- Match the style of the surrounding code (naming, nullability, comment density).
- Prefer deterministic output — no timestamps or randomness in generated content,
  so golden-file and hash-based tests stay stable.
- Generated content written into shared files must use the `agent-sync` markers and
  must never overwrite user-authored content outside the managed section.
- Honor the exit-code contract (see `.ai-agent/CLI_CONTRACT.md`).

## Tests

- **Core tests** cover config/skill parsing and validation, hashing, marker parsing,
  section replacement, adapters (golden files), drift detection, and the lockfile.
- **CLI tests** drive `CliRunner` directly with a temporary working directory and
  assert exit codes and output.
- Add golden files under `tests/AgentSync.Core.Tests/Golden/` for new adapters.

## Commit messages

Write clear, imperative commit subjects describing the change and why. Group related
changes into a single commit where it aids review.

## Pull requests

1. Fork and create a topic branch.
2. Make the change with tests; run `dotnet test`.
3. Open a PR describing the motivation and the approach.
4. CI (build, test, and an end-to-end drift check) must be green.

## License of contributions

Agent Sync is licensed under AGPL-3.0-or-later. By contributing, you agree that your
contributions are licensed under the same terms.
