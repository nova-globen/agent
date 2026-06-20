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
