# Autopilot Handoff Prompts

The files in this directory are **autopilot artifacts only** — the baton in the multi-session chain.

## Purpose

Each session of `dotnet agent autopilot claude` writes one fresh file here before it ends, so the next
fresh session can resume with full context and no inline brief. The file is the only thing the next
session is guaranteed to read; keep it dense and self-contained.

## Rules

- **One file per session.** Never overwrite a prior session's file — they are git history.
- **Filename:** `prompt-<yyyyMMdd-HHmm>_<slug>.txt` where `slug` is the next resume point (kebab-case).
- **Read order:** the autopilot skill takes the **newest by filename** (the `yyyyMMdd-HHmm` prefix sorts
  lexicographically into chronological order; the last entry wins).
- **Format:** the file must contain the ALL-CAPS required sections (see the autopilot skill); a missing
  or garbled BOOTSTRAP, ALREADY COMPLETE, RESUME AT, or HANDOFF causes the next session to stop and
  report — not guess.

## Scope

These prompt files are **not** general context. Other tasks, skills, and agents must not load
`.agent/prompts/` as routine context — it is intentionally absent from the Task → Context Map in
`AGENTS.md`. Only the autopilot resume flow reads them.

## Mid-session kill gap

**Known behavior:** the autopilot skill writes the handoff prompt at session end. If a session is killed
mid-execution (terminal closed, output/turn/usage limit, manual stop), no fresh handoff is written —
the next session reads the *previous* prompt and may resume from a stale position.

To detect this, the verifier's parse step checks whether a **fresh** prompt appeared since the last run.
If no new prompt was written, the verdict reports "no new handoff found — verify before continuing."
When this happens: check `git log --oneline -3` to see what was committed, then manually inspect the
newest `prompt-*.txt` to confirm its `RESUME AT` still reflects the correct next step before running
autopilot again.
