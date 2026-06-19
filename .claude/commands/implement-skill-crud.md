Implement **only Milestone D (skill CRUD)** from the CRUD plan.

1. Read `.ai-agent/features/CRUD_COMMANDS.md` (skill commands) and
   `.ai-agent/features/ROADMAP.md` (Milestone D).
2. Put logic in `AgentSync.Core` (`Authoring/SkillWriter.cs`), add a nested `skill`
   dispatch in `CliRunner`, reuse `SkillValidator`, `Yaml.Scalar`, `RepoPath`, and the
   lockfile (`Projections/Lockfile.cs`) so deletes don't leave orphans.
3. Add tests for every behavior (add/edit/delete/list/show, refuse-overwrite, delete
   blocked by existing projections without `--force`, dry-run, deterministic `--json`).
   Stop when `dotnet build -c Release` and `dotnet test` pass.
4. Do not implement target CRUD or the UI here. Preserve current behavior and
   invariants. No AI/Claude trailers in commits.
