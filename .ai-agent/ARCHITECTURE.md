# Architecture

## Preferred Stack

Use .NET 8 or newer.

Suggested solution layout:

```text
src/
  AgentSync.Cli/
  AgentSync.Core/
  AgentSync.Adapters/
  AgentSync.Git/
tests/
  AgentSync.Core.Tests/
  AgentSync.Cli.Tests/
```

## Components

CLI Layer: argument parsing, command dispatch, exit codes, human-readable output, JSON output.

Core Layer: loading `.agent/agent.yaml`, discovering canonical skills, computing hashes, loading `.agent/lock.json`, detecting drift, producing sync plans.

Adapter Layer: converting canonical skills to target formats.

Git Layer: finding repository root, installing hooks, supporting `git agent ...`.

## Safety Rule

Never overwrite manual edits without explicit user intent.
