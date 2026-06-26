## When to use

Use this when a **durable** architectural or design decision gets made and should be written down:
choosing or rejecting a library, setting a module/aggregate boundary rule, picking a persistence or
auth approach, or resolving a design debate. The goal is that a future agent finds the decision and its
rationale rather than re-litigating it.

## ADR or decision-log?

- **Full ADR (`docs/adr/`):** significant, cross-cutting, or hard-to-reverse — anything that shapes the
  architecture or that someone might later question. These are the source of truth; **the latest ADR wins**
  on conflict.
- **One line in `.agent/memory/decision-log.md`:** smaller, local calls (a naming choice, a split
  strategy, a pinned transitive version). When unsure, prefer an ADR.

## Writing an ADR

1. Pick the next zero-padded number: list `docs/adr/`, take the highest `NNNN` + 1.
2. Create `docs/adr/NNNN-kebab-case-title.md`, following this format exactly:

   ```markdown
   # ADR NNNN: <Title in Title Case>

   - Status: Proposed | Accepted | Superseded by ADR-XXXX
   - Date: YYYY-MM-DD

   ## Context
   <The forces and constraints. Why a decision is needed now.>

   ## Decision
   <The choice, stated plainly in active voice.>

   ## Consequences
   - <What gets better.>
   - <What it costs / what discipline it now requires.>
   - <Follow-ups or risks.>
   ```

3. If this ADR supersedes an earlier one, set the old ADR's Status to `Superseded by ADR-NNNN` in the
   same change.

## Writing a decision-log entry

Append under the right `## YYYY-MM-DD` heading in `.agent/memory/decision-log.md`:

```
- <Decision>. Rationale: <why>. <Governing policy/ADR reference, if any>.
```

## Keeping the ADR set healthy (count gate)

When you add a new ADR, count them: `ls docs/adr/[0-9]*.md | wc -l`.
**At or above 20**, the set is large enough to warrant a review:
- **Interactive session:** ask the developer whether to audit, consolidate, and revise the ADRs. If yes,
  read the set, mark superseded ADRs, merge overlapping ones, fix stale status/dates. If no, proceed.
- **Unattended / autopilot run:** do **not** block. Record the suggestion in `active-context.md` **Risks**
  and in the session handoff prompt, then continue. The audit is the developer's call to authorize.

The threshold is a floor for *considering* a cleanup, not a cap — never withhold a warranted ADR to stay
under it.

## After recording

- Reference the ADR from the code or docs it governs when that helps discovery.
- If the decision closes out an effort, fold its narrative into `.agent/memory/delivery-log.md` — do not
  duplicate the full rationale into `active-context.md`.
- Commit the ADR with the change it justifies (or as its own `docs(adr): …` commit).
