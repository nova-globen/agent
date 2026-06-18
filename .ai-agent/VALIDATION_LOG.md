# Validation Log

Manually verified end-to-end scenario for `v0.1.0-alpha.1`, run on **Windows** with the
globally installed binaries (`agent` and `git-agent` on `PATH`). This is the canonical
proof of the core product behavior:

```text
manual edit -> drift detected -> CI/status fails -> Git commit blocked
```

## Scenario (PowerShell)

```powershell
agent --version
git agent --version

git init
agent init
agent sync
agent status --fail-on-drift --ci
git agent status
agent install-hooks

git add .
git commit -m "Test Agent Sync initialization"

notepad AGENTS.md          # hand-edit inside a generated agent-sync marker section
agent status --fail-on-drift --ci
git commit --allow-empty -m "Should fail because of drift"
```

## What each step proves

| Step | Proves |
| --- | --- |
| `agent --version` / `git agent --version` | Both entry points are installed and report `agent 0.1.0-alpha.1`; `git-agent` delegates to the same CLI. |
| `git init` | Starting from a fresh repository (the normal user starting point). |
| `agent init` | Scaffolds `.agent/` (default `code-review` skill) and `.githooks/`. |
| `agent sync` | Projects the canonical skill into every enabled target (AGENTS.md, CLAUDE.md, Cursor, Copilot, Gemini, OpenAI/Claude skill folders). |
| `agent status --fail-on-drift --ci` | Reports a clean state (exit 0) right after sync — the CI-facing command works. |
| `git agent status` | The Git extension produces the same result as `agent status`. |
| `agent install-hooks` | Sets `core.hooksPath=.githooks` and makes the hooks executable. |
| `git add .` + `git commit` | A normal commit succeeds while everything is in sync. The initial commit added the scaffolded project files (large file count — output summarized, not reproduced here). |
| `notepad AGENTS.md` (hand edit in a marker section) | Simulates instruction drift: a human edits generated content between the `agent-sync:start`/`end` markers. |
| `agent status --fail-on-drift --ci` (after edit) | Detects the manual edit and **fails** (exit non-zero) with:<br>`[ERROR] Manually edited projection AGENTS.md (agents_md). Run 'agent sync --force' to regenerate.` |
| `git commit --allow-empty -m "Should fail because of drift"` | The pre-commit hook runs Agent Sync and **blocks the commit** because drift exists. |

## Result

All steps behaved as expected. The drift→status-failure→commit-block chain is confirmed
on Windows.

> Note: the large initial commit's per-file output is intentionally summarized here, not
> pasted in full.

## Still to validate

- The same scenario on **Linux** and **macOS** with installed release binaries.
- The install scripts (`install.sh` / `install.ps1`) across more environments.
