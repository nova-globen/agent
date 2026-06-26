# autopilot-test

A minimal test repository for `agent autopilot claude` and the streaming TUI spike.

## What this repo does

The autopilot loop runs five sessions. Each session reads a handoff prompt from
`.agent/prompts/autopilot/`, completes one small writing task, commits, and
either writes the next handoff or stops when all five tasks are done.

## How to run

### Step 0 — One-time setup (REQUIRED — do this before running autopilot)

> **Important:** skip this and the autopilot will commit to the parent repository
> instead of the test repo, polluting its history.

Run these commands **from inside** the `autopilot-test` folder to create its own
independent git repository:

```bash
cd examples/autopilot-test
git init
git add .
git commit -m "chore: initial autopilot test setup"
```

Then trust the workspace — either open `claude` interactively here once and
accept the trust dialog, **or** add an entry to `~/.claude.json`:

```json
"projects": {
  "C:/path/to/examples/autopilot-test": { "hasTrustDialogAccepted": true }
}
```

### Prerequisites

- `claude` CLI on PATH (Claude Code).
- `agent` CLI on PATH — or run via `dotnet run --project ../../src/AgentSync.Cli`.

### Run with the standard CLI

```bash
cd examples/autopilot-test
agent autopilot claude
```

### Run with the streaming TUI spike

From the repository root:

```bash
dotnet run --project spike/AutopilotTui -- \
  --repo "$(pwd)/examples/autopilot-test" \
  --delay 3
```

## Expected outcome

After five sessions:
- `haiku.md`, `git-tips.md`, `testing-tips.md`, `clean-code.md`, `retrospective.md`
  are all present with content.
- `TODO.md` shows all five tasks checked (`[x]`).
- `git log --oneline` shows ~10 commits (5 task commits + 4 handoff commits + 1 initial).
- `.agent/prompts/autopilot/` contains `prompt-001.txt` through `prompt-005.txt`.
  No `prompt-006.txt` should exist — that's the stop signal.

## Resetting

To run again from scratch:

```bash
git checkout .
git clean -fd
```
