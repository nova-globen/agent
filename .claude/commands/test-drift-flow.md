Verify the core drift-detection flow end to end, using a throwaway repo (do not run
Agent Sync on this repository's own files).

Using the local build (`dotnet run --project src/AgentSync.Cli -- ...`) or installed
`agent`:

1. In a fresh temp dir: `git init`, then `agent init` and `agent sync`.
2. `agent status --fail-on-drift --ci` should exit 0 (clean).
3. Hand-edit a generated section inside `AGENTS.md` (between the `agent-sync` markers).
4. `agent status --fail-on-drift --ci` should now exit non-zero and report
   `Manually edited projection AGENTS.md (agents_md)`.
5. `agent sync --force` regenerates; `agent status --fail-on-drift --ci` is clean again.

Report results. This confirms: manual edit -> drift detected -> status fails ->
(with hooks installed) commit blocked.
