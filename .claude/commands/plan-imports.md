Refine the import feature plan for Agent Sync.

1. Read `.agent/features/IMPORTS.md` and `.agent/features/ROADMAP.md`.
2. Cross-check the plan against the current code (`src/AgentSync.Core/Configuration`,
   `Adapters`, `Projections`, `RepoPath.cs`) so source shapes, frontmatter, and
   validation rules match reality.
3. Tighten the spec: resolve any open questions, add missing edge cases, keep the
   conflict/error table accurate. This is planning only — do not implement.
4. Preserve current behavior and invariants. No AI/Claude trailers in commits.
