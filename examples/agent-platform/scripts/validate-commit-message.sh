#!/usr/bin/env bash
# Enforce Conventional Commits format and reject standalone AI-tool tokens.
set -euo pipefail

commit_msg_file="${1:-}"
if [[ -z "${commit_msg_file}" ]]; then
  echo "Usage: $0 <commit-msg-file>" >&2
  exit 2
fi

msg="$(cat "${commit_msg_file}")"

# Strip comment lines (lines beginning with #)
cleaned="$(echo "${msg}" | grep -v '^#')"

# Conventional Commits: type(scope): subject  OR  type: subject
cc_pattern='^(feat|fix|refactor|perf|docs|test|build|ci|chore|style|revert)(\([a-zA-Z0-9._-]+\))?: .+'
if ! echo "${cleaned}" | head -1 | grep -qE "${cc_pattern}"; then
  echo "[commit-msg] Commit message does not follow Conventional Commits." >&2
  echo "  Expected: type(scope): subject  (e.g. feat(hello-platform): add Greeter class)" >&2
  echo "  Got: $(echo "${cleaned}" | head -1)" >&2
  exit 1
fi

# Reject standalone AI/tool tokens (word-boundary, case-insensitive).
# Allows 'ai' inside words (domain, email, trail, ai.tokens) but blocks bare 'ai', 'claude', etc.
if echo "${cleaned}" | grep -qiE '\b(ai|codex|claude|chatgpt|gpt|llm|agentic)\b'; then
  echo "[commit-msg] Commit message must not contain standalone AI/tool tokens." >&2
  echo "  (ai, codex, claude, chatgpt, gpt, llm, agentic are rejected as standalone words)" >&2
  exit 1
fi

# Enforce: a commit touching src/ or tests/ must also stage docs/plans/backlog.md,
# unless the message contains [skip-progress-check] with a reason.
if echo "${cleaned}" | grep -q '\[skip-progress-check\]'; then
  exit 0
fi

staged="$(git diff --cached --name-only 2>/dev/null || true)"
has_src=$(echo "${staged}" | grep -E '^(src|tests)/' | head -1 || true)
has_backlog=$(echo "${staged}" | grep -F 'docs/plans/backlog.md' | head -1 || true)

if [[ -n "${has_src}" && -z "${has_backlog}" ]]; then
  echo "[commit-msg] A commit touching src/ or tests/ must also stage docs/plans/backlog.md." >&2
  echo "  Update the cross-plan backlog alongside the code, or add [skip-progress-check] <reason>." >&2
  exit 1
fi

exit 0
