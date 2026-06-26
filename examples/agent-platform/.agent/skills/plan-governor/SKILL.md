Use this workflow to **create**, **execute**, and **document** a plan for any effort that does not fit in
a single commit. A plan is a **folder of four living documents** under `docs/plans/`, kept in sync with
the agent memory files. The plan is not written once and abandoned — it is updated as each increment lands.

## When to use

- Starting a new feature, capability, or module — anything spanning more than one commit.
- Resuming work where a `docs/plans/<NN>-<slug>/` folder already exists.
- Any task the user frames as "plan X", "scaffold a plan for X", or "track X".

Do **not** spin up a plan folder for a one-file fix or a single-commit change — record those in
`.agent/memory/decision-log.md` if they carry a durable decision, and use **commit-governor**.

## Plan layout and naming

Plans are grouped by tier: `docs/plans/tier-<TT>_<tier-name>/<NN>-<slug>/`
(e.g. `docs/plans/tier-00_foundations/01-hello-platform/`). `<NN>` and `<slug>` match the feature file
(`01.hello-platform.md` → `01-hello-platform`). Cross-cutting work lives under
`docs/plans/other_cross-cutting/<slug>/`. See `docs/plans/README.md` for the current map.

A plan folder contains exactly four files:

| File | Purpose |
| --- | --- |
| `PLAN_<slug>.md` | Stable intent: goal, scope decision, approach, ordered increments, out-of-scope. |
| `DECISIONS_<slug>.md` | Durable decisions **with rationale** — one bullet per decision, bold lead phrase. |
| `PROGRESS_<slug>.md` | Living tracker: status, task checklist with verification sub-bullets, deferred follow-ups, dated log with commit hashes. |
| `MEMORY_<slug>.md` | Quick durable cross-reference for future agents. Link to related plans with `[[other-plan-memory]]` wikilinks. |

**Naming rules:** `<NN>` is the two-digit feature number from `docs/features/`; `<slug>` is kebab-case
and identical across the folder and all four filenames. Dates in plan documents are absolute
(`YYYY-MM-DD`), never relative.

## The cross-plan backlog (`docs/plans/backlog.md`)

`backlog.md` is the **single, always-current rollup of everything remaining, deferred, or to follow up
across all plans** — the one place to look for open work without opening every `PROGRESS_*`.

Rules:
- **Open items only** — `[ ]` not started / `[~]` partial, grouped under **Actionable now** /
  **Blocked** / **Intentional / out-of-band**. Never list completed work — once done it is *removed*.
- Every item cites its source plan (`src: tier-<TT>/<NN>-<slug>`).
- **Mandatory to update at every increment and at plan close-out**: remove finished items, add anything
  newly deferred. A stale backlog defeats its purpose.

## Workflow

### Phase A — Create (or locate) the plan

1. Read `.agent/context/architecture-principles.md` and the relevant `docs/features/NN.*.md` spec.
2. Check `docs/plans/` for an existing folder. **If it exists, resume it** (skip to Phase B).
3. Pick `<NN>` and a kebab `<slug>`. Scaffold the four files (use the structure from any existing plan
   as a template):
   - `PLAN_*` — goal, scope decision, approach, **ordered increments each individually verifiable**, explicit out-of-scope.
   - `DECISIONS_*` — seed with decisions known at planning time.
   - `PROGRESS_*` — status `Not started`, increments as an unchecked `- [ ]` list, empty follow-ups, a `## Log` with the dated creation entry.
   - `MEMORY_*` — durable orientation a future agent needs, with wikilinks to related plans.
4. Add a short "IN PROGRESS" entry to `.agent/memory/active-context.md`.

### Phase B — Execute, one increment at a time

For **each** increment in `PLAN_*`, in order:

5. Implement the increment against the feature spec and architecture principles.
6. **Verify before claiming done**: build and run the relevant tests.
7. **Commit by concern** using **commit-governor** (Conventional Commits; `scripts/pre-commit-validate.sh`
   per commit). Plan-tracking files (`PROGRESS_*`, `docs/plans/backlog.md`) are **committed in the same
   commit as the `src/`/`tests/` change they describe** — they are not separate documentation commits.
8. **Update the plan in the same commit as the code**:
   - Tick the task in `PROGRESS_*`; add a verification sub-bullet.
   - Append a dated `## Log` entry naming the commit hash.
   - Update `docs/plans/backlog.md`: remove completed items, add anything newly deferred with `src:` tag.
   - Record durable decisions in `DECISIONS_*` and (for architecture/governance decisions)
     `.agent/memory/decision-log.md`.

### Phase C — Close out

9. Set `PROGRESS_*` status to complete; ensure follow-ups list everything deferred; confirm
   `active-context.md` reflects done. **Reconcile `backlog.md`**: remove every item this plan closed;
   ensure each remaining Follow-up is in `backlog.md` with `src:` tag.
10. Confirm the **Definition of Done** (AGENTS.md): builds, tests pass, architecture respected, decisions
    recorded, commits pass hooks.

## Keeping it honest

- The `PROGRESS_*` checklist is the source of truth — never mark `[x]` without verifying.
- Do not silently drop scope: anything not done moves to **Follow-ups (deferred)** with a one-line reason,
  and the still-open ones are mirrored in `backlog.md`.
- `DECISIONS_*` records *why*, not just *what* — a future agent reverses a decision only by reading its rationale.
