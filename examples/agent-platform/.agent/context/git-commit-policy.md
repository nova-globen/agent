# Git Commit Policy

This policy defines how agents and humans create commits in this repository.

## Goals

- Keep commits reviewable and architecture-safe.
- Enforce Conventional Commits.
- Keep commit messages focused on the "why" (no AI/tool references).
- Keep commit operations reproducible across agent tools.

## Commit Modes

1. **Standard commit mode** — use the current session; load only commit-relevant context.
2. **Isolated commit mode** — start a fresh session for commit-only work (avoids carrying unrelated
   conversational context into commit decisions). See `docs/runbooks/agentic-commit-workflow.md`.

## Minimum Commit Context

For commit-only work, load only:

1. `AGENTS.md`
2. `.agent/context/git-commit-policy.md`
3. `.agent/context/architecture-principles.md`
4. `docs/governance/oss-license-policy.md`
5. Relevant `docs/adr/*` for changed areas
6. Changed file diffs

Load additional context only when required to validate correctness.

## Required Validation Before Commit

- Run `scripts/pre-commit-validate.sh`.
- Ensure architecture guardrails are not violated.
- Ensure license policy is respected when package metadata changes.

## Conventional Commit Format

```
type(scope): subject
```

Allowed `type` values: `feat`, `fix`, `refactor`, `perf`, `docs`, `test`, `build`, `ci`, `chore`,
`style`, `revert`.

Subject rules: imperative mood; no trailing period; keep summary concise.

## Message Restrictions

Commit messages must not contain standalone AI/tool tokens (word-boundary, case-insensitive):
`ai`, `codex`, `claude`, `chatgpt`, `gpt`, `llm`, `agentic`.

`ai` inside a compound word (email, trail, domain, ai.tokens) is fine — only standalone use is blocked.

## Commit Splitting Rules

- One commit per concern (feature, refactor, docs, tooling).
- Keep mechanical renames/moves separate from behavior changes.
- Keep governance/docs-only changes separate unless required by the code change.
- **Plan-tracking files travel with the code, not in a separate docs commit.**
  `PROGRESS_*.md` and `docs/plans/backlog.md` are committed **in the same commit** as the
  `src/`/`tests/` change that advanced them.

## Plan Progress Tracking (enforced)

Every code change has a corresponding plan-progress change.

- **Rule:** a commit staging anything under `src/` or `tests/` **must** also stage
  `docs/plans/backlog.md` (updated alongside the owning plan's `PROGRESS_*.md`).
- **Enforced by** the `commit-msg` hook (`scripts/validate-commit-message.sh`).
- **Opt out** only for genuinely plan-less commits (a merge, a mechanical mid-increment fixup) by
  adding a justified token to the message: `[skip-progress-check] <reason>`.

## Enforcement

Repository hooks:

- `.githooks/pre-commit` → `scripts/pre-commit-validate.sh`
- `.githooks/commit-msg` → `scripts/validate-commit-message.sh`

Run `scripts/setup-git-hooks.sh` once per clone.
