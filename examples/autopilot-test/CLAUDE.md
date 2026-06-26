# Autopilot Test Repository

This repository exists to test the `agent autopilot claude` command and the
streaming TUI spike (`spike/AutopilotTui`). It has no real production purpose.

---

## Autopilot Contract

Every session starts with `claude -p "continue autopilot"`. Follow this protocol:

### Step 1 — Find your task

Read the newest file matching `.agent/prompts/autopilot/prompt-NNN.txt`.
That file describes what this session must accomplish.

### Step 2 — Do the work

Complete exactly the work described. Do not start tasks from future prompts.
Keep each session focused and commit-sized.

### Step 3 — Commit

Stage all changed/created files and commit with a short, descriptive message.
Example: `feat: write haiku about software development`

### Step 4 — Hand off or finish

- If `TODO.md` still has unchecked tasks (`- [ ]`):
  Write the next prompt to `.agent/prompts/autopilot/prompt-NNN.txt`
  where NNN is the previous prompt number + 1 (zero-padded to 3 digits).
  Commit this file: `chore: write autopilot handoff for task N`

- If ALL tasks in `TODO.md` are checked (`- [x]`):
  Do NOT write another prompt file. The autopilot loop will detect this and stop.

### Important rules

- Do not skip tasks or do multiple tasks in one session.
- Always mark the completed task as `[x]` in `TODO.md` before committing.
- The handoff prompt must describe the NEXT task clearly enough to execute without
  reading this file — future sessions have limited context.
- Write clean, simple content. The quality of the output is not what's being tested;
  the autopilot loop mechanics are.
