Implement **only Milestone E (target CRUD)** from the CRUD plan.

1. Read `.ai-agent/features/CRUD_COMMANDS.md` (target commands) and
   `.ai-agent/features/ROADMAP.md` (Milestone E).
2. Put logic in `AgentSync.Core` (`Authoring/TargetWriter.cs` / `ConfigEditor.cs`), add
   a nested `target` dispatch in `CliRunner`. Target ids must be known
   (`TargetIds.IsKnown`); paths must pass `RepoPath`; round-trip `agent.yaml` without
   losing `policy:` or other targets; surface drift/lockfile consequences on
   disable/delete.
3. Add tests for every behavior (add/edit/delete/list/show, unsafe-path and
   unknown-target rejection, config round-trip, deterministic `--json` ordered by
   `TargetIds.Ordered`). Stop when `dotnet build -c Release` and `dotnet test` pass.
4. Do not implement skill CRUD or the UI here. Preserve current behavior and
   invariants. No AI/Claude trailers in commits.
