## When to use

Use when the developer asks what to do next or where things stand — "what's next", "next step", "what
should I work on", "where were we", "what's left to do", "pick up the next task". This skill makes the
cross-plan backlog trustworthy **before** answering, then recommends the next work.

## Steps

1. **Verify the backlog is current** — run `./scripts/check-backlog-consistency.sh`.
   - On **hard failure** (completed items left in `docs/plans/backlog.md`, ticked checkboxes, or a
     dangling `src:` reference), the backlog is stale — reconcile it first: open each implicated plan's
     `PROGRESS_*.md`, remove finished items from `backlog.md`, and add any newly deferred ones (with a
     `src:` tag), per the **plan-governor** skill. Re-run the check until clean.
   - Resolve advisory warnings where cheap.
2. **Read the current state** — `docs/plans/backlog.md` (the open-items rollup) and
   `.agent/memory/active-context.md` (current state, in-flight, next priorities, risks). Skim
   `docs/plans/README.md` for the tier map.
   - **If an autopilot run is in flight**, the newest `.agent/prompts/autopilot/prompt-*.txt` (latest by
     filename) carries the most specific `RESUME AT` pointer — read it and treat it as the precise next
     increment, but **reconcile against the backlog**: if it names work the backlog shows as done, the
     repo state wins (the prompt is stale) — recommend from the reconciled state, not the prompt.
3. **Recommend the next steps**, in tier/dependency order (see `docs/features/00.index.md` for the tier
   map). Prefer, in order:
   - finishing in-flight work over starting new work;
   - "Actionable now (no blocker)" items before "Blocked" ones;
   - the next tier's first feature when the current tier is complete.
   For each suggestion, cite its plan (`src:` folder) and the **first verifiable increment** from its
   `PLAN_*`/`PROGRESS_*`, and name any blocker. For a large effort, hand off to the **planner** sub-agent
   or the **plan-governor** skill to scaffold or resume the plan; for autonomous continuous execution use
   the **autopilot** skill.

## Output

A short, ordered list of recommended next steps — each with its plan reference, the concrete first
increment, and any blocker — plus a one-line note on whether the backlog needed reconciling. Keep it
skimmable; do not dump whole plan files.
