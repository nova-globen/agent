# Agentic Commit Workflow

How AI agents should create commits in this repository. Follows `.agent/context/git-commit-policy.md`.

## Quick reference

```bash
# Before any commit:
bash scripts/pre-commit-validate.sh

# Commit format:
git commit -m "type(scope): subject"

# Check the last N commits:
git log --oneline -5
```

## Isolated commit mode

For commit-only work (no implementation), start a fresh agent session and load only the Minimum Commit
Context defined in `.agent/context/git-commit-policy.md`:

1. `AGENTS.md`
2. `.agent/context/git-commit-policy.md`
3. `.agent/context/architecture-principles.md`
4. `docs/governance/oss-license-policy.md`
5. Relevant `docs/adr/*` for changed areas
6. `git diff --staged` output

## Validation checklist (per commit)

- [ ] `git status --short` — confirm staged files match the commit's concern.
- [ ] `scripts/pre-commit-validate.sh` — all gates pass.
- [ ] Commit message: `type(scope): subject` format.
- [ ] No standalone AI tokens (`ai`, `codex`, `claude`, `chatgpt`, `gpt`, `llm`, `agentic`).
- [ ] `docs/plans/backlog.md` is staged if `src/` or `tests/` are staged.
- [ ] Splits: one commit per concern; mechanical changes separate from behavior changes.

## Conventional Commit examples

```
feat(hello-platform): add Greeter class and unit test
fix(hello-platform): handle null name in Greeter.Greet
refactor(hello-platform): extract greeting format constant
docs(adr): record decision to use xUnit for testing
chore: update AgentSync to 0.3.2
```

## Never

- `git push` — leave the remote for the developer to review.
- Amend published commits.
- Skip hooks (`--no-verify`).
- Include AI/tool tokens in commit messages.
