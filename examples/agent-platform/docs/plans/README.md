# Plans Directory

This directory contains the work plans for all features in this repository.

## Structure

```
docs/plans/
├── README.md                      ← this file
├── backlog.md                     ← cross-plan rollup of ALL open work
├── tier-00_foundations/           ← Tier 0 feature plans
│   └── 01-hello-platform/         ← Worked example plan
│       ├── PLAN_hello-platform.md
│       ├── DECISIONS_hello-platform.md
│       ├── PROGRESS_hello-platform.md
│       └── MEMORY_hello-platform.md
└── other_cross-cutting/           ← Plans that span multiple tiers
```

## How Plans Work

Each feature gets a **folder of four living documents**, following the **plan-governor** skill:

| File | Purpose |
|------|---------|
| `PLAN_<slug>.md` | Stable intent: goal, scope decision, approach, ordered increments, out-of-scope. |
| `DECISIONS_<slug>.md` | Durable decisions with rationale — one bullet per decision, bold lead phrase. |
| `PROGRESS_<slug>.md` | Living tracker: status, checklist, verification bullets, deferred follow-ups, dated log. |
| `MEMORY_<slug>.md` | Quick durable cross-reference for future agents. Wikilinks to related plans. |

## The Cross-Plan Backlog (`backlog.md`)

`backlog.md` is the **single source of truth for all remaining work** across all plans. Open items only.
When an item is done, remove it from `backlog.md` — never tick or annotate as "done." Each item cites
its source plan (`src: tier-NN_name/NN-slug`).

## Naming

Plans mirror the feature file: `docs/features/01.hello-platform.md` → `tier-00_foundations/01-hello-platform/`.
`<NN>` is the two-digit feature number; `<slug>` is kebab-case. Cross-cutting work lives under
`other_cross-cutting/<slug>/`.

## Adding a New Plan

1. Create the tier folder if it doesn't exist: `docs/plans/tier-NN_name/`.
2. Create the plan folder: `NN-slug/`.
3. Scaffold the four files using the plan-governor skill (see templates in any existing plan folder).
4. Add open items to `backlog.md` under the right category with `src:` tag.
5. Add an "IN PROGRESS" entry to `.agent/memory/active-context.md`.
