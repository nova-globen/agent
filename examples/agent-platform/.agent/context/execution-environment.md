# Execution Environment

## Recommended: Linux or WSL

Agent operations are most reliable on Linux or Windows Subsystem for Linux (WSL). Bash scripts
(`scripts/*.sh`) are written for POSIX sh semantics. On WSL, Docker is available if the project uses
containerized infrastructure for integration tests.

## Windows (Git Bash)

All `dotnet` commands work natively on Windows. The Bash scripts in `scripts/` run correctly under
**Git Bash** (ships with Git for Windows). If you are on Windows without WSL:

- Run `scripts/setup-git-hooks.sh` from Git Bash, not PowerShell.
- `dotnet build`, `dotnet test`, `dotnet agent sync` work from any shell.
- Integration tests that require Docker should be run from WSL or a Linux CI agent.

## No hard gate

Unlike some repositories, this kit does **not** hard-stop on non-Linux environments. If your project
adds a mandatory environment gate (e.g. `scripts/require-linux-or-wsl.sh`), document it here and add
the gate call to `AGENTS.md` and the autopilot skill.

## CI

CI agents are expected to be Linux-based. The scripts assume standard POSIX tools (`bash`, `grep`,
`find`, `curl`). The `dotnet` SDK must be installed and in `PATH`.
