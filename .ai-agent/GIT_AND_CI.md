# Git and CI Integration

## Local Hooks

The repo contains `.githooks/pre-commit` and `.githooks/pre-push`. Developers must run `agent install-hooks` or manually `git config core.hooksPath .githooks`.

## Missing Tool Behavior

If hooks are installed but `agent` is missing, commits and pushes must fail. The error must say:

```text
Agent Sync is required for this repository.
Install it, then retry.
```

## CI

Pipelines must run `agent status --fail-on-drift --ci` or, during early development, `dotnet run --project src/AgentSync.Cli -- status --fail-on-drift --ci`.

CI is the real enforcement layer because Git cannot force hooks to run on every developer machine until hooks are configured locally.
