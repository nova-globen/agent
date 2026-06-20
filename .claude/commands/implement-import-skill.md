Implement **only Milestone B (`agent import skill`)** from the import plan.

1. Read `.agent/features/IMPORTS.md` (§A) and `.agent/features/ROADMAP.md`
   (Milestone B). Milestone A (import foundation) must exist first — if it doesn't,
   implement A before B.
2. Put logic in `AgentSync.Core` (`Import/`), keep `CliRunner` a thin dispatcher, reuse
   `SkillValidator`, `SkillContent.StripRedundantHeading`, `Yaml.Scalar`, and `RepoPath`.
3. Add tests for every behavior in §A (file/folder sources, overrides, conflict/force,
   dry-run, validation, round-trip). Stop when `dotnet build -c Release` and
   `dotnet test` pass.
4. Do not implement other milestones. Preserve current behavior and invariants. No
   AI/Claude trailers in commits.
