Implement **only Milestone C (`agent import agent`)** from the import plan.

1. Read `.agent/features/IMPORTS.md` (§B) and `.agent/features/ROADMAP.md`
   (Milestone C). Milestones A and B must exist first — implement them first if not.
2. Put logic in `AgentSync.Core` (`Import/AgentImporter.cs`), keep `CliRunner` thin, and
   be marker-aware (`MarkedDocument`) so generated sections aren't re-imported. Never
   modify the source files.
3. Add tests for every behavior in §B (each `--type`, `--split file`/`sections`,
   marker-awareness, `.cursor/rules/` and skill-folder directories, originals
   untouched). Stop when `dotnet build -c Release` and `dotnet test` pass.
4. Do not implement other milestones. Preserve current behavior and invariants. No
   AI/Claude trailers in commits.
