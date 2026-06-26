Use this workflow when the task is commit creation or commit cleanup.

## Inputs

- Changed working tree (staged/unstaged)
- Optional user intent for grouping/scope
- Optional commit count preference

## Minimal Context

1. `AGENTS.md`
2. `.agent/context/git-commit-policy.md`
3. `.agent/context/architecture-principles.md`
4. `docs/governance/oss-license-policy.md`
5. Relevant `docs/adr/*` for touched areas
6. Actual git diff

## Workflow

1. **Inspect and classify changes** — run `git status --short`. Group files by concern.
2. **Validate each commit group** — run `scripts/pre-commit-validate.sh` before each commit.
3. **Create commit(s)** — Conventional Commit format; no AI/tool tokens in messages; split unrelated changes.
4. **Verify history quality** — check `git log --oneline -n <count>` for message quality and grouping.
   If grouping is poor, restage and recommit before finishing.

## Output Contract

- Commits are logically split (one concern per commit; one module boundary when practical).
- Commit messages follow Conventional Commits (`type(scope): subject`).
- Commit messages contain no standalone AI/tool references.
- Repository validation hooks pass.
