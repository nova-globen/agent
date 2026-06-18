# Security Policy

## Supported Versions

Agent Sync is pre-1.0. Security fixes are applied to the `main` branch and the latest
released version.

## Reporting a Vulnerability

Please report security issues privately. **Do not open a public issue for a
vulnerability.**

- Email **desmati@gmail.com** with a description of the issue and steps to reproduce.
- If available, use GitHub's private vulnerability reporting ("Report a vulnerability"
  under the repository's Security tab).

You can expect an acknowledgement within a few business days. We will work with you to
understand and resolve the issue, and will credit reporters who wish to be credited
once a fix is available.

## Scope and considerations

Agent Sync reads and writes files in your repository and runs `git` to read and set
`core.hooksPath`. When assessing impact, note that:

- Agent Sync writes only inside the repository working tree (canonical `.agent/` and
  the configured projection targets) and `.githooks/`.
- Generated content in shared files is confined to `agent-sync` marker regions;
  user-authored content outside those markers is never modified.
- The installed Git hooks fail closed: if the `agent` tool is missing, commits and
  pushes are blocked rather than silently skipped.

Examples of issues we consider in scope: path traversal when resolving projection
paths, overwriting user content outside managed sections, or hook scripts that fail
open when the tool is absent.
