# Decision Log

<!-- Thin index of durable cross-cutting decisions that do not warrant their own ADR.
     One line per decision: decision · rationale · reference.
     For significant architectural decisions, create an ADR in docs/adr/ instead. -->

## ADR Index

- ADR 0001: Use .NET 10 and AgentSync v0.3.2 for the platform kit. See `docs/adr/0001-use-net10-and-agentsync.md`.

## Cross-Cutting Decisions

### 2026-06-26

- Adopt Conventional Commits with backlog-staging gate. Rationale: enforces plan honesty and
  keeps commit history reviewable without a separate tracking system. Governing: git-commit-policy.md.
- Use xUnit 2.9.x (permissive MIT license) as the test framework for the worked example. Rationale:
  industry standard for .NET; simple API; no external dependencies beyond the runner. ADR: none (local call).
