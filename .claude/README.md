# Working on Agent Sync (for Claude)

How to be productive and safe in this repository.

## Start here

Read, in order:

1. `CLAUDE.md` — fast orientation + "do not break" rules.
2. `AGENTS.md` — product goal, architecture, and implementation facts.
3. `.agent/CURRENT_STATE.md` — current release + status snapshot.
4. `.agent/NEXT_STEPS.md` — what to work on next.

For the verified behavior, see `.agent/VALIDATION_LOG.md`.

## Workflow

- Run build + tests **before and after** changes:

  ```bash
  dotnet build --configuration Release
  dotnet test
  scripts/release-smoke.sh   # when touching publish/release/install
  ```

- Prefer small, focused commits.
- For any behavior change, add or update tests (Core tests are in
  `tests/AgentSync.Core.Tests`, CLI tests in `tests/AgentSync.Cli.Tests`; adapter output
  has golden files under `tests/AgentSync.Core.Tests/Golden`).
- Keep logic in `AgentSync.Core`; keep the CLI a thin layer over `CliRunner`.

## Guardrails

- Do **not** add AI/Claude trailers to commit messages.
- Keep public docs clean: no private conversation URLs and no local machine paths.
- Keep alpha positioning honest in any public-facing wording.
- Do not change `agent sync` write-by-default behavior, retarget off `net10.0`, remove
  `git-agent`, weaken `RepoPath` traversal protection, or overwrite manually edited
  generated sections without `--force` — unless a maintainer explicitly asks.
- This repo **dogfoods** Agent Sync: `AGENTS.md`, `CLAUDE.md`, the Copilot/Gemini files,
  and the `.claude/skills/` folders are generated from the canonical skills under
  `.agent/skills/`. Edit the skill and run `agent sync`; never hand-edit a generated
  section (the Git hooks and CI enforce it).

## Handy reusable prompts

See `.claude/commands/` and the maintainer skill — authored canonically at
`.agent/skills/agent-sync-maintainer/SKILL.md` and projected to
`.claude/skills/agent-sync-maintainer/SKILL.md`.
