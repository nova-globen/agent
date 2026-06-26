## When to use

Use this when you touch the agent memory files — when an effort or plan increment closes, or when
`.agent/memory/active-context.md` is drifting toward a changelog. The job: keep `active-context.md` a
thin working-state pointer that is cheap to load on **every** task, and move durable history elsewhere.

## The memory layout

- **`active-context.md`** — thin and current only: **Current State, In Flight, Next Priorities, Risks.**
  It is read on every task; treat each line as a recurring token cost. No feature-by-feature history.
- **`delivery-log.md`** — append-only narrative of delivered capabilities. Not part of the always-load core.
- **`decision-log.md`** / **`docs/adr/`** — durable decisions and their rationale (use **adr-author**
  skill). The authoritative decision record.

## Curation procedure

When an effort completes:

1. **Summarize, don't append.** In `active-context.md`, replace the in-flight bullet with a one-or-two-line
   statement of the new current state; refresh **Next Priorities** and **Risks**.
2. **Rotate the narrative.** Move the detailed "what we built and how" into `delivery-log.md` (append a
   bullet/section). **Move, don't copy** — the same paragraph must not live in both files.
3. **Capture decisions.** Any durable decision goes to `docs/adr/` or `decision-log.md` via **adr-author**,
   not into `active-context.md`. When minting a new ADR, **adr-author**'s count gate may flag that the ADR
   set warrants a review — honour it (ask the developer when interactive; defer with a note when unattended).
4. **Convert relative dates.** Write absolute dates (`YYYY-MM-DD`), never "today"/"last week".
5. **Prune.** Delete entries that are now wrong or fully superseded rather than annotating them in place.

## Smell tests

- If `active-context.md` reads like a release changelog, it is too fat — rotate to `delivery-log.md`.
- If you are about to paste a 200-word feature recap you also wrote into a plan `PROGRESS_*` doc, link
  the doc instead.
- If a section answers "what did we do" rather than "what is true now / what's next", it belongs in the
  delivery log.
