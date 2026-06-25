Review Agent Sync for release readiness.

1. Read `.agent/CURRENT_STATE.md`, `RELEASE_CHECKLIST.md`, and `.github/workflows/release.yml`.
2. Run `dotnet build --configuration Release`, `dotnet test`, and `scripts/release-smoke.sh`.
3. Verify: artifact names match `agent-sync-<tag>-<rid>.{tar.gz,zip}` + `checksums.txt`;
   both `agent` and `git-agent` are published; install-script URLs use `master`.
4. Confirm the version/tag story is consistent (`v0.2.0-alpha.7` is the next release;
   `agent --version`).
5. Report a short pass/fail checklist and any blockers. Do not change product behavior
   unless asked. No AI/Claude trailers in commits.
