## When to use

Use when the developer wants continuous, unattended progress — "keep going", "work through the backlog",
"implement overnight", "continue autopilot", "continue while I'm away", "autonomous/continuous development",
"don't stop until it's done". Runs an uninterrupted implement → verify → commit loop and stops only on a
hard blocker.

## Operating contract

- **Do not stop** until the requested scope is done or you hit a HARD BLOCKER. Do not pause for
  confirmation on routine decisions — pick the sensible default, record it, and continue.
- **Commit, never push.** Commit each increment via **commit-governor**; never run `git push`.
- **Keep the backlog honest above all else.** The plan files are your durable memory — treat them as
  load-bearing. A long run that loses the thread is the main failure mode.

## Resuming from a handoff (fresh session)

When opened with only a bare cue — "autopilot", "continue autopilot", "keep going", "resume" — and given
no task of their own:

1. List `.agent/prompts/autopilot/prompt-*.txt` and take the **newest by filename** — the
   `yyyyMMdd-HHmm` prefix sorts lexicographically into chronological order, so the last entry wins.
2. **If one exists**, read it and **auto-resume**: state in one line what you are resuming (its
   `RESUME AT` pointer and the source filename), then begin the loop. These prompts are your own prior
   handoffs — do not stop to ask permission. One guardrail: if the pointer contradicts
   `.agent/memory/active-context.md` or `docs/plans/backlog.md` (e.g. it resumes work the backlog shows
   as done), trust the repo state, reconcile, and note the correction.
3. **If the directory is empty or missing**, fall back to the normal start — run the **next-step** skill
   to choose the first increment, then write the initial handoff prompt (format in *The Prompt Template*).
4. **If the newest prompt is in an invalid format**, stop and report the problem — do not guess or
   continue. Validate against the *Required fields* in **The Prompt Template**: the BOOTSTRAP line, an
   ALREADY COMPLETE section, a single RESUME AT pointer, and the HANDOFF instruction.

If the developer gives an explicit brief, that wins; use the handoff prompt only as supporting state.

## The loop

1. **Pick the next work** with the **next-step** skill (reconciles and reads the backlog). Prefer
   finishing in-flight work, then unblocked "Actionable now" items in tier/dependency order.
2. **Plan if needed.** If the effort has no plan, delegate to the **planner** sub-agent (or the
   **plan-governor** skill) to scaffold the four plan files before coding.
3. **Implement one increment** against the feature spec and architecture principles. **Delegate by
   default** — see *Context discipline*; a long run that does all its reading and building inline is how
   sessions balloon.
4. **Verify** with the **verifier** agent (build + affected tests). Never claim done without a green
   result.
5. **Commit by concern** via **commit-governor** (`scripts/pre-commit-validate.sh` runs in the hook;
   Conventional Commits; no AI/tool tokens; stage `docs/plans/backlog.md` with the code). Never push.
6. **Update tracking (every increment).** Tick the increment in `PROGRESS_*.md` and update
   `docs/plans/backlog.md` — remove finished items, add anything newly deferred (`src:`-tagged). Run
   `./scripts/check-backlog-consistency.sh` and fix every hard failure before moving on.
7. **Capture learnings — short but insightful.** Append durable, non-obvious facts to the right file:
   a decision → `.agent/memory/decision-log.md` (an ADR via **adr-author** if it changes architecture);
   durable orientation → the plan's `MEMORY_*`; refresh `.agent/memory/active-context.md`. One or two
   lines — insight, not a changelog.
8. **Manage context.** When your context grows large or nears its limit, flush still-needed state into
   the plan files and `active-context.md`, then continue from those files. Resume immediately after
   compaction — do not wait.
9. Return to step 1.

## Context discipline (binding)

- **Delegate any wide read.** Opening more than ~2 files, surveying a subsystem, scanning a long log —
  spawn a sub-agent (`Explore` for recon, `general-purpose` for a slice) and work from its conclusion.
- **Build and test only through the `verifier` agent.** Never let a full build/test log land in your
  context — the verifier returns a compact pass/fail.
- **Batch edits, then verify once.** Do not re-read a file you just edited — Edit already validated it.
- **Read state files once per loop.** `active-context.md` / `backlog.md` / the owning `PROGRESS_*.md` —
  read once at the start of each loop, hold the relevant lines, write back at the update step.

## Session handoff (the prompt chain)

Before a session ends — hard blocker, scope complete, or clean context-exhaustion checkpoint — leave the
next session a self-contained resume prompt as a **new file** so the chain continues without re-briefing.
One prompt per session; never overwrite a prior session's file (they are git history).

1. **Consolidate memory conditionally, not every time:**
   - **memory-curator** only when an effort or plan *completed* this session, or `active-context.md`
     has drifted into changelog territory. Otherwise the light per-increment capture suffices.
   - **adr-author** only when a decision is genuinely architectural, cross-cutting, or hard-to-reverse.
     A local call stays a one-liner in `decision-log.md`. In an unattended run, defer any ADR audit
     question — record it for the developer rather than blocking.
2. **Write the next handoff prompt** to a new file:
   ```bash
   ts="$(date +%Y%m%d-%H%M)"
   out=".agent/prompts/autopilot/prompt-${ts}_<slug>.txt"   # slug = next resume point, kebab-case
   ```
   Fill it per **The Prompt Template** below.
3. **Commit it** with the session's final docs commit (these files live in git history — not gitignored).

> These prompt files are **autopilot handoff artifacts only** — not general context. Other tasks and
> skills must not load `.agent/prompts/` as routine context. See `.agent/prompts/autopilot/README.md`.

## Hard blockers (the only reasons to stop)

Stop and report only when you genuinely cannot proceed: a required dependency cannot be provisioned;
an irreversible or outward-facing action would be required that the user has not authorized; a
verification failure you cannot resolve after a focused attempt; the requested scope is complete; or
the model's usage limit is hit. On stop, write the handoff prompt and report where you stopped and why.
If you hit a usage limit, state how long to wait in **total seconds** (the runner uses this to schedule
the retry).

## The Prompt Template

Every handoff prompt follows the structure below. Fill `<…>` placeholders; keep **ALL-CAPS labels
verbatim** — the next session validates the format against them. Generalize, don't transcribe: carry
only what the next slice needs; link plan/`MEMORY_*` files instead of pasting bulk.

**Required fields** (missing or garbled → invalid → stop and report): the **BOOTSTRAP** line, an
**ALREADY COMPLETE** section, exactly one **RESUME AT** pointer, and the **HANDOFF** instruction.
*KEY FACTS*, *NEXT AFTER*, *DEFERRALS*, and *GOTCHAS* are expected but may be short on a young chain.

```
Use the autopilot skill to continue a multi-session implementation effort already in progress. Run
continuously, checkpoint at a clean boundary when context grows large, and stop only on a hard blocker.
Never push.

BOOTSTRAP: read AGENTS.md (Always-Load Core + Definition of Done); read .agent/memory/active-context.md
(its "In Flight" block is the live resume pointer) and skim docs/plans/backlog.md. The autopilot skill's
CONTEXT DISCIPLINE is BINDING: delegate any wide read (>2 files, a subsystem survey, a long log) to an
Explore/general-purpose sub-agent; build and test ONLY through the verifier agent; batch edits then verify
once; read state files once per loop.

ALREADY COMPLETE (do NOT redo):
<Newest session first. One bullet per finished effort/increment with its commit hash(es) and a one-line
"what landed + how it was verified". This is the anti-redo guard — be specific.>

KEY FACTS TO CARRY:
<Durable, non-obvious conventions + recon the RESUME AT slice actually needs. Link a MEMORY_*/plan file
rather than pasting; drop anything the next slice won't touch.>

RESUME AT: <the single next unstarted increment — "feature <N> <name>, increment <M> (<title>)">.
<One paragraph: where the plan lives (docs/plans/…/PROGRESS_*), what to read FIRST, the approach, and
key decisions already made.> Then proceed: <remaining increments, one phrase each, in order>. Build each
as an independently verifiable increment; verify via verifier agent; commit via commit-governor; never push.

NEXT AFTER: <what follows in dependency order, including anything that runs LAST.>

CARRY-FORWARD DEFERRALS (don't drop):
<Each open deferral, tagged to its owning feature/plan.>

GOTCHAS:
<Repo-specific traps: the build pre-commit hook runs on every commit — keep commits focused; the commit-msg
gate rejects standalone ai|codex|claude|chatgpt|gpt|llm tokens and requires docs/plans/backlog.md staged
with any src//tests/ commit ([skip-progress-check] <reason> only for genuinely plan-less commits); any
restore or license-check flakiness.>

HANDOFF (at session end — a hard blocker, scope complete, or clean context-exhaustion checkpoint): write
the NEXT session's prompt to a NEW file at .agent/prompts/autopilot/prompt-<yyyyMMdd-HHmm>_<slug>.txt
(timestamp `date +%Y%m%d-%H%M`; slug = the next resume point) — one prompt per session; never overwrite a
prior session's file (the chain is kept as history). First run CONDITIONAL consolidation (memory-curator
only if an effort/plan completed this session or active-context drifted into a changelog; adr-author only
for a genuinely architectural decision). Then write this file in the same format as the one you resumed
from (every ALL-CAPS section, including this HANDOFF instruction), with RESUME AT set to the next
unstarted slice, carrying forward the durable knowledge the next fresh session needs. Commit it with the
session's final docs commit. NEVER push.
```
