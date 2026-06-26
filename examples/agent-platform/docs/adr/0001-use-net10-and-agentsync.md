# ADR 0001: Use .NET 10 and AgentSync v0.3.2 for the Platform Kit

- Status: Accepted
- Date: 2026-06-26

## Context

The platform kit needs a stable, long-term-supported runtime and a consistent agent-instruction
management tool. The kit must work on Windows (Git Bash / native dotnet) and Linux/WSL. AgentSync
manages skill drift detection and projection — enforcing that AGENTS.md, CLAUDE.md, and `.claude/skills/`
stay in sync with canonical `.agent/skills/` sources.

## Decision

Use .NET 10 (`net10.0`) as the target framework with `TreatWarningsAsErrors=true` and nullable
reference types enabled. Use AgentSync v0.3.2 (local dotnet tool) for agent-instruction management.

## Consequences

- .NET 10 SDK (or later, within the `latestFeature` roll-forward band) must be installed.
- AgentSync is installed locally via `dotnet tool restore`; no global install required.
- All projects in the solution inherit `net10.0`, `Nullable`, and `TreatWarningsAsErrors` from
  `Directory.Build.props` — new projects get these settings automatically.
- Pre-push hook enforces `dotnet agent status --fail-on-drift`; hand-editing generated projections
  blocks the push.
