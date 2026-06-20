<!-- agent-sync:start id=code-review target=gemini hash=sha256:a8a662f5648f865460036839880e4f49840d63dc03cb575f55129e618ee87de3 -->
## Code Review

Reviews changes using repository conventions and flags risky edits. Use when reviewing a pull request, a generated patch, or local changes before committing.

## When to use

Use this skill when reviewing a pull request, a generated patch, or local changes before
you commit.

## Instructions

- Check whether the change follows the repository's conventions and existing patterns.
- Look for correctness, security, maintainability, and test-coverage risks.
- Confirm commit messages follow the project's commit convention.
- Identify generated files that should not be edited by hand.
- Prefer specific, actionable comments over broad criticism.

## Output

Return a concise review with:

- summary
- risks
- required fixes
- optional improvements
<!-- agent-sync:end -->

<!-- agent-sync:start id=conventional-commit target=gemini hash=sha256:a7773b25dcab174d6a487fbb13b294bcbffcc7608b4bec8dcf2a0d6285fde9c4 -->
## Conventional Commits

Write commit messages and PR titles that follow the Conventional Commits specification. Use whenever you author a commit message, amend one, or set a squash-merge title.

## When to use

Use this whenever you write a commit message, amend one, or set a squash-merge / PR title
in this repository.

## Format

Write the subject line as `<type>(<optional scope>): <description>`:

- **type** — one of `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`,
  `ci`, `chore`, `revert`.
- **scope** — an optional area of the codebase in parentheses, e.g. `feat(parser):`.
- **description** — a concise, imperative summary ("add", not "added"), lower case, with no
  trailing period.

Keep the subject under ~72 characters. Leave a blank line, then an optional body that
explains *what* changed and *why* (not how).

## Breaking changes

Signal a breaking change with a `!` after the type/scope (`feat!:` or `feat(api)!:`) and/or
a `BREAKING CHANGE:` footer describing the impact and the migration path.

## Examples

- `feat(auth): add token refresh endpoint`
- `fix: prevent crash on empty config`
- `docs(readme): clarify the install steps`
- `refactor!: drop support for the legacy config format`

## Do not

- Do not use a vague subject like `update`, `fix stuff`, or `wip`.
- Do not capitalize the description or end it with a period.
- Do not bundle unrelated changes into one commit — split them by type/scope.
<!-- agent-sync:end -->
