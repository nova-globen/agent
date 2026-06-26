# AI-Agent Platform Kit

A self-contained, reusable kit that lets a .NET repository run the unattended
**`autopilot`** implement → verify → commit loop via [AgentSync](https://github.com/nova-globen/agent).

## What this is

This kit wires together:

- **7 canonical skills** (`autopilot`, `next-step`, `plan-governor`, `commit-governor`, `memory-curator`,
  `adr-author`, `operating-guide`) managed by AgentSync — defined once in `.agent/skills/`, projected
  into `AGENTS.md`, `CLAUDE.md`, and `.claude/skills/`.
- **3 sub-agents** (`planner`, `verifier`, `git-ops-executor`) that the autopilot loop delegates to.
- **5 git hooks** (pre-commit, commit-msg, post-checkout, post-merge, pre-push) enforcing drift
  detection, Conventional Commits, build/test gates, and backlog consistency.
- **5 scripts** (`pre-commit-validate.sh`, `validate-commit-message.sh`, `check-nuget-licenses.sh`,
  `check-backlog-consistency.sh`, `setup-git-hooks.sh`) implementing the gates.
- **A full docs/ template set** (`adr`, `architecture`, `domain`, `features`, `governance`, `plans`,
  `reports`, `runbooks`) — empty templates + one worked example.
- **A worked example** (`Platform.Hello`) — a trivial .NET class library that proves the loop runs.

## How the autopilot loop works

```
dotnet agent autopilot claude
  │
  ├─ reads newest .agent/prompts/autopilot/prompt-*.txt
  │
  ├─ loop:
  │   ├─ next-step skill        → reconciles backlog.md, picks next increment
  │   ├─ planner agent          → scaffolds/resumes docs/plans/<tier>/<NN>-<slug>/
  │   ├─ [implement increment]  → main agent writes source + test files
  │   ├─ verifier agent         → dotnet build + dotnet test → compact PASS/FAIL
  │   ├─ commit-governor skill  → Conventional Commit; pre-commit gate fires
  │   └─ update PROGRESS_*.md + backlog.md
  │
  └─ write next .agent/prompts/autopilot/prompt-<ts>_<slug>.txt (the handoff)
```

The **handoff prompt** is the chain mechanism: each session writes one file before it ends, so the next
fresh session resumes with full context and no inline brief.

**Known gap:** if a session is killed mid-execution (terminal closed, usage limit hit), no handoff is
written and the next session reads the previous (stale) prompt. Check `git log --oneline -3` to confirm
what was committed before re-running.

## Quick start

```bash
# 1. Install tools
dotnet tool restore

# 2. Wire git hooks (Git Bash / WSL / Linux)
bash scripts/setup-git-hooks.sh

# 3. Verify AgentSync projections (AGENTS.md, CLAUDE.md, .claude/skills/)
dotnet agent status

# 4. Verify the build
dotnet build Platform.slnx -v q -clp:ErrorsOnly -nologo

# 5. Run the first autopilot session (implements the worked example)
dotnet agent autopilot claude
```

See `docs/runbooks/setup-guide.md` for detailed setup instructions and troubleshooting.

## Autopilot handoff prompt format

Every `.agent/prompts/autopilot/prompt-*.txt` must contain these **ALL-CAPS required sections**
(a missing or garbled section causes the next session to stop and report — not guess):

| Section | Required? | Purpose |
|---------|-----------|---------|
| `BOOTSTRAP` | yes | What to read first; context discipline reminder |
| `ALREADY COMPLETE` | yes | Anti-redo guard with commit hashes |
| `KEY FACTS TO CARRY` | expected | Durable conventions the next slice needs |
| `RESUME AT` | yes (exactly one) | The single next unstarted increment |
| `NEXT AFTER` | expected | What follows in dependency order |
| `CARRY-FORWARD DEFERRALS` | expected | Open deferrals tagged to their feature |
| `GOTCHAS` | expected | Repo-specific traps |
| `HANDOFF` | yes | Instruction to write the next session's prompt |

## Adopting for your project

1. **Update** `.agent/context/project-brief.md` — your product description and tech stack.
2. **Update** `.agent/context/architecture-principles.md` — your architecture rules.
3. **Replace** the worked example: remove `src/Platform.Hello/`, `tests/Platform.Hello.Tests/`;
   add your projects to `Platform.slnx`; create your feature specs in `docs/features/` and plans
   in `docs/plans/`.
4. **Run** `dotnet agent sync` after any changes to `.agent/skills/` or `.agent/agents/`.
5. **Write** an initial handoff prompt and run `dotnet agent autopilot claude`.

## Key files

| Path | Purpose |
|------|---------|
| `.agent/skills/autopilot/SKILL.md` | The core loop skill |
| `.agent/skills/operating-guide/SKILL.md` | Always-on agent instructions (→ AGENTS.md) |
| `.agent/prompts/autopilot/` | Session handoff chain |
| `.agent/memory/active-context.md` | Thin working-state pointer |
| `docs/plans/backlog.md` | Cross-plan open-work rollup |
| `scripts/pre-commit-validate.sh` | Multi-gate build/test/drift/license validator |
| `docs/runbooks/setup-guide.md` | Step-by-step setup |
