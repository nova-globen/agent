namespace AgentSync.Core;

/// <summary>A curated skill, agent, or hook bundled with the sample pack.</summary>
public sealed record SampleSkill(string Id, string SkillYaml, string SkillMd);

public sealed record SampleAgent(string Id, string AgentYaml, string AgentMd);

public sealed record SampleHook(string Name, string Content, bool Executable = true);

/// <summary>
/// Returns a curated set of skills, sub-agents, and Git hooks sourced from a reference repository.
/// Installed by <see cref="InitService"/> when the user opts in during <c>agent init</c>.
/// </summary>
public static class SamplePack
{
    public static IEnumerable<SampleSkill> GetSkills() =>
    [
        new("adr-author", AdrAuthorYaml, AdrAuthorMd),
        new("agentsync", AgentSyncYaml, AgentSyncMd),
        new("autopilot", AutopilotYaml, AutopilotMd),
        new("commit-governor", CommitGovernorYaml, CommitGovernorMd),
        new("dotnet-inspect", DotnetInspectYaml, DotnetInspectMd),
        new("memory-curator", MemoryCuratorYaml, MemoryCuratorMd),
        new("next-step", NextStepYaml, NextStepMd),
        new("operating-guide", OperatingGuideYaml, OperatingGuideMd),
        new("plan-governor", PlanGovernorYaml, PlanGovernorMd),
    ];

    public static IEnumerable<SampleAgent> GetAgents() =>
    [
        new("planner", PlannerAgentYaml, PlannerAgentMd),
        new("verifier", VerifierAgentYaml, VerifierAgentMd),
        new("git-ops-executor", GitOpsExecutorAgentYaml, GitOpsExecutorAgentMd),
    ];

    public static IEnumerable<SampleHook> GetHooks() =>
    [
        new("pre-commit", PreCommitHook, Executable: true),
        new("commit-msg", CommitMsgHook, Executable: true),
        new("pre-push", PrePushHook, Executable: true),
        new("post-checkout", PostCheckoutHook, Executable: true),
        new("post-merge", PostMergeHook, Executable: true),
    ];

    // ── adr-author ────────────────────────────────────────────────────────────

    private const string AdrAuthorYaml = """
        id: adr-author
        name: adr-author
        description: "Use when a durable architectural or design decision is made — choosing a library, setting a module/aggregate boundary rule, picking a persistence or auth approach, or resolving a design debate. Records it as a numbered ADR in docs/adr (or a line in decision-log.md for smaller calls), in the repo's established format. Use whenever the answer to 'should we write this down?' is yes."
        version: 0.1.0

        # Real skill: projects ONLY to .claude/skills (loaded on-demand). Kept OUT of the always-loaded
        # instruction files to avoid context bloat.
        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string AdrAuthorMd = """
        ## When to use

        Use this when a **durable** architectural or design decision gets made and should be written down:
        choosing or rejecting a library, setting a module/aggregate boundary rule, picking a persistence/auth/
        deployment approach, or resolving a design debate. The goal is that a future agent finds the decision and
        its rationale instead of re-litigating it.

        ## ADR or decision-log?

        - **Full ADR (`docs/adr/`):** significant, cross-cutting, or hard-to-reverse — anything that shapes the
          architecture or that someone might later question. These are the source of truth; **the latest ADR wins**
          on conflict.
        - **One line in `.agent/memory/decision-log.md`:** smaller, local calls (a naming choice, a split
          strategy, a pinned transitive version). When unsure, prefer an ADR.

        ## Writing an ADR

        1. Pick the next zero-padded number: list `docs/adr/`, take the highest `NNNN` + 1.
        2. Create `docs/adr/NNNN-kebab-case-title.md`, following the naming conventions
           (`docs/naming-conventions.md`) for the slug. Match the existing house format exactly:

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

        3. If this ADR supersedes an earlier one, set the old ADR's Status to `Superseded by ADR-NNNN` in the same
           change. Keep implementation terms out of domain language per the operating guide.

        ## Writing a decision-log entry

        Append under the right `## YYYY-MM-DD` heading in `.agent/memory/decision-log.md`:

        ```
        - <Decision>. Rationale: <why>. <Governing policy/ADR reference, if any>.
        ```

        ## Keeping the ADR set healthy (count gate)

        ADRs are read on demand, not always-loaded, so a large set costs little in tokens — the real risk is
        **signal dilution**: overlapping, stale, or superseded ADRs that make the authoritative decision harder to
        find. Guard the set size when you add a new ADR:

        1. Count them: `ls docs/adr/[0-9]*.md | wc -l`.
        2. **At or above the threshold of 20**, the set is large enough to warrant a review:
           - **Interactive session:** ask the developer whether to **audit, consolidate, and revise** the ADRs.
             If they say **yes**, do the work now — read the set, mark genuinely superseded ADRs
             `Superseded by ADR-NNNN`, merge overlapping ones, fix stale status/dates, and confirm each remaining
             ADR still reflects the code. If **no**, proceed and don't ask again this session.
           - **Unattended / autopilot run:** do **not** block on a prompt. Record the suggestion for the developer
             — a line in `active-context.md` **Risks** and in the session handoff prompt — and continue.

        The threshold is a floor for *considering* a cleanup, not a cap on ADRs.

        ## After recording

        - Reference the ADR/decision from the code or docs it governs when that helps discovery.
        - If the decision closes out an effort, fold its narrative into `.agent/memory/delivery-log.md`.
        - Commit the ADR with the change it justifies (or as its own `docs(adr): …` commit).
        """;

    // ── agentsync ─────────────────────────────────────────────────────────────

    private const string AgentSyncYaml = """
        id: agentsync
        name: agentsync
        description: "Use when creating, editing, or removing an AI-agent skill or the operating guide in this repo, or when AGENTS.md/CLAUDE.md/.gemini/.claude/skills show drift. Covers the AgentSync workflow: edit canonical .agent/ sources, project with agent sync, and verify with status/validate. Use whenever you touch agent artifacts; never hand-edit generated files."
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string AgentSyncMd = """
        ## When to use

        Use this whenever you create, edit, or remove an **AI-agent artifact** in this repo — a skill or the
        operating guide — or when `agent status` reports drift in any generated file. Agent artifacts are
        managed by **Agent Sync** (the `agent` CLI); the generated files are not editable by hand.

        ## The model in one paragraph

        The canonical store is **`.agent/`**. Each skill is a folder `.agent/skills/<id>/` with `skill.yaml`
        (metadata: id, name, description, version, per-target enable flags) and `SKILL.md` (the instruction body).
        The operating guide is the special skill `operating-guide`. Agent Sync **projects** these into targets
        configured in `.agent/agent.yaml` and records a hash for each projection in `.agent/lock.json`. Generated
        content sits between `<!-- agent-sync:start … -->` / `<!-- agent-sync:end -->` markers. **Never edit a
        generated file** — `lock.json` + the pre-push hook (`agent status --fail-on-drift`) will flag the
        manual edit and block the push.

        ## Targets (who gets what)

        - **`operating-guide`** → `AGENTS.md`, `CLAUDE.md`, `.gemini/GEMINI.md` (always-on base instructions).
          It is deliberately **not** a `.claude/skills` entry — it must not be duplicated as a loadable skill.
        - **Every other (real) skill** → `.claude/skills/<id>/SKILL.md` only (Claude loads it progressively/on
          demand). Keep `agents_md`/`claude_md`/`gemini` **disabled** for real skills so they don't bloat the
          always-loaded files.

        ## Workflow (every time)

        1. Edit the **canonical** source under `.agent/skills/<id>/` — `SKILL.md` for body, `skill.yaml` for
           metadata/targets. Do **not** open `AGENTS.md`/`CLAUDE.md`/`.gemini`/`.claude/skills`.
        2. `agent sync` — writes missing/outdated projections and updates `lock.json`.
        3. `agent status` — confirm "No issues detected" (no drift). `agent validate` checks config + skill validity.
        4. Stage the canonical change **and** its regenerated projections together, then commit.

        ## Common commands

        - `agent skill add <id> --name "<name>" --description "<desc>"` — scaffold a new skill. It enables
          **all** targets by default; immediately edit `skill.yaml` down to `claude_skill` only for a real skill.
        - `agent skill list | show <id> | edit <id> | delete <id>` — manage canonical skills.
        - `agent sync --check` — preview projections without writing. `--force` overwrites a
          manually-edited (drifted) section — use only when intentionally discarding a hand-edit.
        - `agent status [--fail-on-drift --ci]` — drift report. `agent diff` shows canonical-to-projection differences.
        - `agent target list | show <id>` — inspect projection destinations defined in `agent.yaml`.

        ## Authoring rules

        - A `SKILL.md` body must **not** start with a heading that repeats the skill name — adapters generate the
          target-appropriate heading. Start with `## When to use` (or similar).
        - The `description` in `skill.yaml` is the trigger text the agent sees; make it specific about *when* to
          use the skill. It is the only part loaded until the skill fires.
        - Context and memory docs (`.agent/context/*`, `.agent/memory/*`) are **not** Agent Sync artifacts — edit
          them directly.
        """;

    // ── autopilot ─────────────────────────────────────────────────────────────

    private const string AutopilotYaml = """
        id: autopilot
        name: autopilot
        description: "Use when the developer asks for continuous, unattended, or overnight development — 'keep going', 'work through the backlog', 'implement overnight', 'continue while I'm away', 'autonomous/continuous development', 'don't stop until done'. Runs a non-stop implement -> verify -> commit loop across the planned backlog, keeps plan files current, compacts context as it grows, delegates to sub-agents and other skills, and stops only on a hard blocker. Commits but never pushes."
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string AutopilotMd = """
        ## When to use

        Use when the developer wants continuous, unattended progress — "keep going", "work through the backlog",
        "implement this overnight", "continue while I'm away", "autonomous/continuous development", "don't stop
        until it's done". You will run an uninterrupted implement → verify → commit loop over the planned work
        and stop only on a hard blocker.

        ## Operating contract

        - **Do not stop** until the requested scope is done or you hit a HARD BLOCKER (see below). Do not pause
          for confirmation on routine decisions — pick the sensible default, record it, and continue.
        - **Commit, never push.** Commit each increment via **commit-governor**; never run `git push`. (Leave the
          remote untouched so the developer reviews before anything leaves the machine.)
        - **Keep the backlog honest above all else.** You will do a lot in one run and losing the thread is the
          main failure mode. The plan files are your durable memory — treat them as load-bearing.

        ## Resuming from a handoff prompt (fresh session)

        Each autopilot run writes a handoff prompt for the next one (see *Session handoff* below), so a fresh
        session can pick up the chain with no inline brief. When the developer opens a run with only a bare cue —
        "autopilot", "go on your own", "continue", "keep going", "resume" — and gives no task of their own:

        1. List `.agent/prompts/autopilot/prompt-*.txt` and take the **newest by filename** — the
           `yyyyMMdd-HHmm` prefix sorts lexicographically into chronological order, so the last entry wins.
        2. **If one exists**, read it and **auto-resume**: state in one line what you are resuming (its
           `RESUME AT` pointer and the source filename), then begin the loop at that increment. These prompts are
           your own prior handoffs — do not stop to ask permission. The one guardrail: if the prompt's pointer
           contradicts `active-context.md` / `backlog.md` (e.g. it resumes work the backlog shows as done),
           trust the repo state, reconcile, and note the correction — don't blindly follow a stale prompt.
        3. **If the directory is empty or missing**, there is no chain yet: fall back to the normal start — read
           `.agent/memory/active-context.md` and run the **next-step** skill to choose the first increment.

        If the developer gives an explicit brief, that brief wins; use the handoff prompt only as supporting state.

        ## The loop

        1. **Pick the next work** with the **next-step** skill (it reconciles and reads the backlog). Take the
           next individually-verifiable increment in tier/dependency order; prefer finishing in-flight work, then
           unblocked "Actionable now" items.
        2. **Plan if needed.** If the effort has no plan or needs resuming, delegate to the **planner** sub-agent
           (or the **plan-governor** skill) to scaffold/resume the four plan files before coding.
        3. **Implement one increment** against its bounded context, naming conventions, and the module template;
           keep module boundaries intact. **Delegate by default** — see *Context discipline* below.
        4. **Verify** with the **verifier** agent (build + affected tests). Never claim done without a green result.
        5. **Commit by concern** via **commit-governor**. **Never push.**
        6. **Update tracking (every increment).** Tick the increment in the owning `PROGRESS_*.md`, and update
           `docs/plans/backlog.md` — remove finished items, add anything newly deferred (`src:`-tagged).
        7. **Capture learnings — short but insightful.** Append durable, non-obvious facts to the right
           ai-agent file: a decision → `.agent/memory/decision-log.md`; durable orientation → the plan's `MEMORY_*`;
           refresh the thin `.agent/memory/active-context.md`. One or two lines — insight, not a changelog.
        8. **Manage context.** When your context grows large, drifts off-task, or nears its limit, COMPACT:
           first flush all still-needed state into the plan files / `active-context.md` (so nothing is lost),
           then continue working from those files, keeping only the context relevant to the remaining todo items.
           After compaction, resume immediately at the next increment — do not wait.
        9. Return to step 1.

        ## Context discipline (binding, not optional)

        Over a long unattended run, the main context is your scarcest resource:

        - **Delegate any wide read.** If answering a question means opening more than ~2 files, surveying a
          subsystem, scanning a long log, or chasing naming/usage across the tree — spawn a sub-agent (`Explore`
          for recon, `general-purpose`/`planner` for a slice) and work from its conclusion.
        - **Build and test only through the `verifier` agent.** Do not run raw build/test commands on the main
          thread and never let a full build/test log land in your context — the verifier returns a compact pass/fail.
        - **Batch edits, then verify once.** Make the related edits to a file in one pass before building. Do
          **not** re-read a file you just edited to "check" it.
        - **Read state files once per loop.** `active-context.md` / `backlog.md` / the owning `PROGRESS_*.md`
          are small; read once at step 1, hold the relevant lines, and write back at steps 6–7 rather than re-reading.

        ## Session handoff (the prompt chain)

        Before a session ends — whether you hit a hard blocker, run the scope to completion, or checkpoint at a
        clean boundary because context is nearly exhausted — leave the next session a self-contained resume prompt
        as a **file**, so the chain continues without the developer re-briefing:

        1. **Consolidate memory first — but conditionally, not every time.**
           - **memory-curator** when an effort or plan **completed** this session, or `active-context.md` has
             drifted into changelog territory.
           - **adr-author** when a decision made this session is genuinely **architectural, cross-cutting, or
             hard-to-reverse**.
        2. **Write the handoff prompt to a file.** Stamp the name from the clock and a short slug:

           ```
           .agent/prompts/autopilot/prompt-<yyyyMMdd-HHmm>_<slug>.txt
           ```

           Include: what you **completed** this session (with commit hashes), an updated **"Already complete
           (do NOT redo)"** list, a new **`RESUME AT`** naming the next unstarted slice, the carried-forward
           **deferrals**, and a closing instruction telling that session to write *its* follow-on file the same way.
        3. **Commit it** with the session's final docs commit. Never push.

        ## Hard blockers (the only reasons to stop)

        Stop and report only when you genuinely cannot proceed: a required dependency cannot be provisioned;
        an irreversible or outward-facing action would be required that the user has not authorized; a
        verification failure you cannot resolve after a focused attempt; or the requested scope is complete.
        On stop, leave the backlog and plan files current, **write the handoff prompt (above)**, and report
        where you stopped and why.

        ## Use the rest of the toolbox

        Lean on the existing skills/agents rather than re-deriving: **next-step** (what's next), **planner** /
        **plan-governor** (plans), **verifier** (gate), **commit-governor** (commits), **memory-curator**
        (rotate history out of active-context), **adr-author** (durable decisions).
        """;

    // ── commit-governor ───────────────────────────────────────────────────────

    private const string CommitGovernorYaml = """
        id: commit-governor
        name: commit-governor
        description: Use this skill when the user asks to create commit(s), split commits, or validate commit quality before committing.
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string CommitGovernorMd = """
        Use this workflow when the task is commit creation or commit cleanup.

        ## Inputs

        - Changed working tree (staged/unstaged)
        - Optional user intent for grouping/scope
        - Optional commit count preference

        ## Workflow

        1. **Inspect and classify changes**
           - Run `git status --short`.
           - Group files by concern and module boundary.

        2. **Validate each commit group**
           - Run any pre-commit validation scripts configured in this repo before each commit.
           - Ensure architecture constraints are not violated.

        3. **Create commit(s)**
           - Use Conventional Commit format: `type(scope): subject`.
           - Do not mention AI tools, models, or agent names in commit messages.
           - Split unrelated changes into separate commits.

        4. **Verify history quality**
           - Check `git log --oneline -n <count>` for message quality and grouping.
           - If grouping is poor, restage and recommit before finishing.

        ## Output Contract

        - Commits are logically split.
        - Commit messages follow Conventional Commits.
        - Commit messages contain no AI/tool references.
        - Repository validation hooks pass.
        """;

    // ── dotnet-inspect ────────────────────────────────────────────────────────

    private const string DotnetInspectYaml = """
        id: dotnet-inspect
        name: dotnet-inspect
        description: Query .NET APIs across NuGet packages, platform libraries, and local files. Search for types, list API surfaces, compare and diff versions, find extension methods and implementors. Use whenever you need to answer questions about .NET library contents.
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string DotnetInspectMd = """
        Query .NET library APIs — the same commands work across NuGet packages, platform libraries
        (System.*, Microsoft.AspNetCore.*), and local .dll/.nupkg files.

        ## Quick Decision Tree

        - **Code broken?** → `diff --package Foo@old..new` first, then `member --oneline`
        - **Need API surface?** → `member Type --package Foo --oneline` (token-efficient)
        - **Need signatures?** → `member Type --package Foo -m Method` (default shows full signatures + docs)
        - **Need source/IL?** → `member Type --package Foo -m Method -v:d` (adds Source, Lowered C#, IL)
        - **Need constructors?** → `member 'Type<T>' --package Foo -m .ctor` (use `<T>` not `<>`)
        - **Need all overloads?** → `member Type --package Foo --select` (shows `Name:N` indices)

        ## When to Use This Skill

        - **"What types are in this package?"** — `type` discovers types, `find` searches by pattern
        - **"What's the API surface?"** — `type` for discovery, `member` for detailed inspection
        - **"What changed between versions?"** — `diff` classifies breaking/additive changes
        - **"This code uses an old API — fix it"** — `diff` the old..new version, then `member --oneline`
        - **"What extends this type?"** — `extensions` finds extension methods/properties
        - **"What implements this interface?"** — `implements` finds concrete types
        - **"What does this type depend on?"** — `depends` walks the type hierarchy upward

        ## Key Patterns

        Use `--oneline` as the default for scanning:

        ```bash
        dnx dotnet-inspect -y -- member JsonSerializer --package System.Text.Json --oneline
        dnx dotnet-inspect -y -- type --package System.Text.Json --oneline
        dnx dotnet-inspect -y -- diff --package System.CommandLine@2.0.0-beta4.22272.1..2.0.3 --oneline
        ```

        Use `diff` first when fixing broken code:

        ```bash
        dnx dotnet-inspect -y -- diff --package System.CommandLine@old..new --oneline   # what changed?
        dnx dotnet-inspect -y -- diff -t Command --package System.CommandLine@old..new  # detail on Command
        dnx dotnet-inspect -y -- member Command --package System.CommandLine@new --oneline  # new API surface
        ```

        ## Command Reference

        | Command | Purpose |
        | ------- | ------- |
        | `type` | **Discover types** — terse output, no docs |
        | `member` | **Inspect members** — docs on by default |
        | `find` | Search for types by glob pattern |
        | `diff` | Compare API surfaces between versions |
        | `extensions` | Find extension methods/properties for a type |
        | `implements` | Find types implementing an interface |
        | `depends` | Walk the type dependency hierarchy upward |
        | `package` | Package metadata, files, versions, dependencies |

        ## Key Syntax

        - Generic types need quotes: `'Option<T>'`, `'IEnumerable<T>'`
        - Use `<T>` not `<>` for generic types
        - Diff ranges use `..`: `--package System.Text.Json@9.0.0..10.0.0`

        ## Installation

        Use `dnx` (like `npx`). Always use `-y` and `--` to prevent interactive prompts:

        ```bash
        dnx dotnet-inspect -y -- <command>
        ```

        ## Full Documentation

        ```bash
        dnx dotnet-inspect -y -- llmstxt
        ```
        """;

    // ── memory-curator ────────────────────────────────────────────────────────

    private const string MemoryCuratorYaml = """
        id: memory-curator
        name: memory-curator
        description: "Use when updating the AI-agent memory files — when an effort or plan increment completes, or when active-context.md is growing into a changelog. Keeps active-context.md thin (current state, in-flight, next, risks) and rotates completed-effort narrative into delivery-log.md and durable decisions into docs/adr or decision-log.md. Use whenever you would otherwise append history to active-context."
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string MemoryCuratorMd = """
        ## When to use

        Use this when you touch the AI-agent memory files — when an effort or plan increment closes, or when
        `.agent/memory/active-context.md` is drifting toward a changelog. The job: keep `active-context.md` a thin
        working-state pointer that is cheap to load on **every** task, and move durable history elsewhere.

        ## The memory layout

        - **`active-context.md`** — thin and current only: **Current State, In Flight, Next Priorities, Risks.**
          It is read on every task; treat each line as a recurring token cost. No feature-by-feature history.
        - **`delivery-log.md`** — append-only narrative of delivered capabilities. The history that used to live
          inline in active-context. Not part of the always-load core.
        - **`decision-log.md`** / **`docs/adr/`** — durable decisions and their rationale (use the `adr-author`
          skill). The authoritative decision record.

        ## Curation procedure

        When an effort completes:

        1. **Summarize, don't append.** In `active-context.md`, replace the in-flight bullet with a one-or-two-line
           statement of the new current state, and refresh **Next Priorities** and **Risks**.
        2. **Rotate the narrative.** Move the detailed "what we built and how" into `delivery-log.md` (append a
           bullet/section). **Move, don't copy** — the same paragraph must not live in both files.
        3. **Capture decisions.** Any durable decision goes to `docs/adr/` or `decision-log.md` via `adr-author`,
           not into active-context.
        4. **Convert relative dates.** Write absolute dates (`YYYY-MM-DD`), never "today"/"last week".
        5. **Prune.** Delete entries that are now wrong or fully superseded rather than annotating them in place.

        ## Smell tests

        - If `active-context.md` reads like a release changelog, it is too fat — rotate to `delivery-log.md`.
        - If you are about to paste a 200-word feature recap you also wrote into a plan `PROGRESS` doc, link the
          doc instead.
        - If a section answers "what did we do" rather than "what is true now / what's next", it belongs in the
          delivery log.
        """;

    // ── next-step ─────────────────────────────────────────────────────────────

    private const string NextStepYaml = """
        id: next-step
        name: next-step
        description: "Use when the developer asks what to do next or where things stand — 'what's next', 'next step', 'what should I work on', 'where were we', 'what is left'. Reviews docs/plans/backlog.md (runs any consistency check to ensure it is current and reconciles it if stale), then recommends the next actionable work in tier/dependency order, citing each item's plan and first verifiable increment."
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string NextStepMd = """
        ## When to use

        Use when the developer asks what to do next or where things stand — "what's next", "next step", "what
        should I work on", "where were we", "what's left to do", "pick up the next task". This skill makes the
        cross-plan backlog trustworthy **before** answering, then recommends the next work.

        ## Steps

        1. **Verify the backlog is current** — run any backlog-consistency check script configured in this repo.
           - On **hard failure** (completed items still in `docs/plans/backlog.md`, ticked checkboxes, or dangling
             `src:` references), the backlog is stale — reconcile it first: open each implicated plan's
             `PROGRESS_*.md`, remove finished items from `backlog.md`, and add any newly-deferred ones (with a
             `src:` tag). Re-run the check until clean. Never recommend next steps from a stale backlog.
        2. **Read the current state** — `docs/plans/backlog.md` (the open-items rollup) and
           `.agent/memory/active-context.md` (current state, in-flight, next priorities, risks).
           - **If an autopilot run is in flight**, the newest `.agent/prompts/autopilot/prompt-*.txt` (latest by
             filename) carries the most specific `RESUME AT` pointer — read it and treat it as the precise next
             increment, but **reconcile against the backlog/active-context**: if it names work the backlog shows
             as done, the repo state wins. Skip this when the prompt directory is empty.
        3. **Recommend the next steps**, in implementation/dependency order. Prefer, in order:
           - finishing in-flight work over starting new work;
           - "Actionable now (no blocker)" items before "Blocked" ones.
           For each suggestion cite its plan (`src:`/folder) and the **first verifiable increment** from its
           `PLAN_*`/`PROGRESS_*`, and name any blocker. For a large effort, hand off to the **planner** sub-agent
           (or the **plan-governor** skill) to scaffold or resume the plan; for autonomous continuous execution
           use the **autopilot** skill.

        ## Output

        A short, ordered list of recommended next steps — each with its plan reference, the concrete first
        increment, and any blocker — plus a one-line note on whether the backlog needed reconciling. Keep it
        skimmable; do not dump whole plan files.
        """;

    // ── operating-guide ───────────────────────────────────────────────────────

    private const string OperatingGuideYaml = """
        id: operating-guide
        name: operating-guide
        description: "Base operating instructions for AI coding agents in this repository. Always-loaded — defines mandatory practices, context-loading strategy, token discipline, commit conventions, and the definition of done. Customize for your project."
        version: 0.1.0

        targets:
          agents_md:
            enabled: true
          claude_md:
            enabled: true
          gemini:
            enabled: true
          claude_skill:
            enabled: false
        """;

    private const string OperatingGuideMd = """
        This file defines how AI coding agents should operate in this repository.

        > **Customize this file for your project.** The content below is a starting template — edit the
        > canonical source at `.agent/skills/operating-guide/SKILL.md`, then run `agent sync`.

        ## Start Here

        1. Load the **Always-Load Core** below — these short docs apply to every task.
        2. Consult the **Task → Context Map** and load *only* the rows that match the task in front of you.
           Do not read the whole canonical set up front; most tasks touch a single area.

        ## Always-Load Core (Read on Every Task)

        These apply everywhere — adapt the list to your project's actual files:

        1. Your project brief / README — what the product is.
        2. Your naming conventions document — every naming decision defers to this.
        3. `.agent/memory/active-context.md` — current state, in-flight work, next priorities. (Past-delivery
           detail lives in `.agent/memory/delivery-log.md`; read it only when you need history.)

        ## Task → Context Map (Load Only What Matches)

        | When the task is… | Also load |
        | --- | --- |
        | Implementing/changing a feature | The matching domain/feature spec; architecture principles |
        | Adding/upgrading a dependency | License policy; `dotnet-inspect` skill |
        | Commits only | git commit policy (Conventional Commits, no AI references) |
        | Editing agent skills | The `agentsync` skill |

        ## Token & Context Discipline

        - **Summarize noisy command output.** Never let a full build/restore/test log land in the transcript.
          Build quietly and filter; have gate scripts report pass/fail, not the whole run.
        - **Delegate broad reading.** When a task spans more than ~2 docs or a wide search, launch a subagent
          and work from its conclusion.
        - **Read once, scoped.** Read a file once before a batch of edits; never re-read a file you just edited.

        ## Mandatory Practices

        - Follow your naming conventions for all naming decisions.
        - Add/adjust tests for behavior changes.
        - Update docs when architecture or conventions change.
        - When starting a non-trivial, multi-step effort, use the `plan-governor` skill to create a plan, execute
          it in verifiable increments, and keep it and the memory files current.
        - Keep `.agent/memory/active-context.md` thin — current state, in-flight work, next priorities, risks.
          When an effort completes, use the `memory-curator` skill to move its narrative into
          `.agent/memory/delivery-log.md` and durable decisions into `docs/adr` or `.agent/memory/decision-log.md`.
        - When asked to create commits, follow the `commit-governor` skill.
        - When editing any agent artifact, follow the `agentsync` skill.
        - Use Conventional Commits (`type(scope): subject`) and keep messages free of AI/tool references.
        - Split unrelated changes into multiple commits by concern.

        ## Definition of Done (for AI-generated changes)

        - Builds locally.
        - Relevant tests pass.
        - Architecture constraints are respected.
        - New decisions are recorded in `docs/adr` or `.agent/memory/decision-log.md`.
        - If commit(s) were requested: each commit passes repository hooks.
        """;

    // ── plan-governor ─────────────────────────────────────────────────────────

    private const string PlanGovernorYaml = """
        id: plan-governor
        name: plan-governor
        description: "Use this skill when starting a non-trivial, multi-step effort (a feature, capability, module, migration, or refactor that spans more than one commit) and you need to create a plan, execute it in verifiable increments, and keep it documented in docs/plans/ and the AI-agent memory files. Use it again when resuming or updating an existing plan."
        version: 0.1.0

        targets:
          agents_md:
            enabled: false
          claude_md:
            enabled: false
          gemini:
            enabled: false
          claude_skill:
            enabled: true
        """;

    private const string PlanGovernorMd = """
        Use this workflow to **create**, **execute**, and **document** a plan for any effort that does not fit in a
        single commit. A plan in this repository is a **folder of living documents** under `docs/plans/`, kept in
        sync with the AI-agent memory files.

        ## When to use

        - Starting a new feature/capability/module, a migration, or a broad refactor.
        - Resuming work where a `docs/plans/<slug>/` folder already exists.
        - Any task the user frames as "plan X", "scaffold a plan for X", or "track X".

        Do **not** spin up a plan folder for a one-file fix or a single-commit change — record those directly in
        `.agent/memory/decision-log.md` if they carry a durable decision, and use `commit-governor`.

        ## Plan layout

        A plan folder contains:

        | File | Purpose |
        | --- | --- |
        | `PLAN_<slug>.md` | The stable intent: goal, scope decision, approach, ordered increments, out-of-scope. |
        | `DECISIONS_<slug>.md` | Durable decisions **with rationale** — one bullet per decision. |
        | `PROGRESS_<slug>.md` | The living tracker: status, task checklist with verification sub-bullets, deferred follow-ups, dated log. |
        | `MEMORY_<slug>.md` | Quick durable cross-reference for future agents. |

        ## Workflow

        ### Phase A — Create (or locate) the plan

        1. Check `docs/plans/` for an existing folder for this area. **If it exists, resume it** rather than
           creating a duplicate.
        2. Pick a kebab `<slug>`. Scaffold the four files:
           - `PLAN_*` — goal, scope decision, approach, **ordered increments each of which is independently verifiable**, explicit out-of-scope.
           - `DECISIONS_*` — seed with the decisions known at planning time.
           - `PROGRESS_*` — status `Not started`, the increments as an unchecked `- [ ]` task list, an empty
             follow-ups section, and a `## Log` with the dated creation entry.
           - `MEMORY_*` — the durable orientation a future agent needs.
        3. Reflect the new plan in `.agent/memory/active-context.md` (a short "IN PROGRESS" entry).

        ### Phase B — Execute, one increment at a time

        For **each** increment in `PLAN_*`, in order:

        4. Implement the increment. Keep module boundaries intact.
        5. **Verify before claiming done**: build and run the relevant tests. Add/adjust tests for behavior changes.
           If verification is not possible, say so explicitly rather than implying success.
        6. **Commit by concern** using `commit-governor`. Split by module/concern. Stage the plan-tracking files
           (`PROGRESS_*`, `backlog.md`) in the **same commit as the code they describe**.
        7. **Update the plan in the same commit as the code**:
           - Tick the task in `PROGRESS_*` and add a verification sub-bullet.
           - Append a dated `## Log` entry naming the commit.
           - **Update `docs/plans/backlog.md`**: remove any backlog items this increment completed, add anything
             newly deferred.
           - Record any durable decision in `DECISIONS_*`, and mirror architecture/governance-level decisions into
             `.agent/memory/decision-log.md`.

        ### Phase C — Close out

        8. When all increments are done, set the `PROGRESS_*` status to complete, ensure the follow-ups section
           lists everything deferred, and confirm `active-context.md` reflects "done/verified". Reconcile
           `docs/plans/backlog.md`: remove every backlog item this plan closed.

        ## Keeping it honest

        - The `PROGRESS_*` checklist and `## Log` are the source of truth for "where are we" — never mark a task
          `[x]` you have not verified.
        - Do not silently drop scope. Anything not done moves to **Follow-ups (deferred)** with a one-line reason.
        - `docs/plans/backlog.md` is the cross-plan source of truth for remaining work; it must stay current
          (open items only, removed when done).
        - `DECISIONS_*` records *why*, not just *what*.
        """;

    // ── Sub-agent: planner ────────────────────────────────────────────────────

    private const string PlannerAgentYaml = """
        id: planner
        name: planner
        description: "Architect and maintain implementation plans for large, multi-step efforts. Use when starting or resuming a feature/capability/migration/refactor that spans more than one commit, when asked to plan/scaffold/track work, or when the cross-plan backlog (docs/plans/backlog.md) needs reconciling. Follows the plan-governor skill: creates four plan-governor files per feature, keeps PROGRESS_*.md and backlog.md in sync."
        model: claude-opus-4-8[1m]
        tools:
          - Read
          - Write
          - Edit
          - Bash
          - Grep
          - Glob
          - Skill
        """;

    private const string PlannerAgentMd = """
        ## Role

        You are the planning architect for this solution — turn a feature/capability/migration/refactor into a
        verifiable, well-sequenced plan and keep the plan documents and the cross-plan backlog honest as work lands.
        You do **not** implement production code; you produce and maintain plans, and you read code/specs to ground them.

        ## What you produce

        - A plan folder per feature under `docs/plans/<slug>/` with the four plan-governor files — `PLAN_`,
          `DECISIONS_`, `PROGRESS_`, `MEMORY_` — matching the headings in the plan-governor skill.
        - Increments that are ordered and **individually verifiable** (each names the test project that proves it).
          Explicit out-of-scope, durable decisions with rationale.

        ## Keep the backlog honest (non-negotiable)

        - `docs/plans/backlog.md` holds **open items only** — never completed work, never a changelog. When an
          increment lands, **remove** the items it finished and **add** anything newly deferred (with a `src:` tag).
          Completed items are removed, not ticked or annotated as "DONE".
        - Mirror durable architecture/governance decisions into `.agent/memory/decision-log.md` (open an ADR if
          it changes architecture). Keep `.agent/memory/active-context.md` thin and current.

        ## Scope discipline

        - Resume an existing plan rather than duplicating it. Capture user scope decisions verbatim with an
          absolute date.
        - Think deeply and exhaustively — a shallow plan costs more than the planning. Prefer many small,
          verifiable increments over a few broad ones.

        ## Output

        Return a compact summary: the plan folder(s) touched, the ordered increment titles, the deferred
        follow-ups. The plan files are the deliverable — do not restate them as prose in the reply.
        """;

    // ── Sub-agent: verifier ───────────────────────────────────────────────────

    private const string VerifierAgentYaml = """
        id: verifier
        name: verifier
        description: "Use to verify a change by running the repository's test tiers and reporting a COMPACT pass/fail. Runs the local gate (build + unit tests). Returns only the verdict, per-suite counts, and failing tests with minimal excerpts — never full build logs. Use after implementing an increment, before a commit, or whenever asked to run the gate or tests."
        model: sonnet
        tools:
          - Bash
          - Glob
          - Grep
          - Read
        """;

    private const string VerifierAgentMd = """
        You are a verification agent. Your entire value is running the repository's checks in an isolated
        context and returning a **small, high-signal** result — the caller must never receive a raw build or
        test log. Operate independently; gather context fresh.

        ## What to run

        Pick the narrowest tier that covers the change unless the caller asks for more:

        - **Local gate (default):** build the solution and run unit tests quietly:
          `dotnet build -v q -clp:ErrorsOnly && dotnet test --nologo | tail -n 5`
        - **Targeted unit tests:** when the caller names projects, run just those.

        ## Output hygiene (the whole point)

        - Build/test **quietly** and filter: `dotnet build -v q -clp:ErrorsOnly`, `… | tail -n 5`.
          Never echo a full restore/build/test log into your output.

        ## Report format (return exactly this, nothing more)

        ```
        VERDICT: PASS | FAIL
        Local gate: <pass/fail> (<N>/<N> unit tests, <N> projects)
        Failures:
          - <Project.TestName> — <one-line cause>
          ...
        Notes: <anything notable>
        ```

        If everything passes, the Failures block is `none`. Keep causes to one line each; if a failure needs
        real diagnosis, name the test and the assertion — do not paste the stack trace. Do not edit code; you
        verify and report only.
        """;

    // ── Sub-agent: git-ops-executor ───────────────────────────────────────────

    private const string GitOpsExecutorAgentYaml = """
        id: git-ops-executor
        name: git-ops-executor
        description: Use for git operations (commit/push/pull/branch/merge/rebase/tag/stash/cherry-pick) with repository policy enforcement.
        model: sonnet
        tools:
          - Bash
          - Glob
          - Grep
          - Read
          - Skill
        """;

    private const string GitOpsExecutorAgentMd = """
        You are a policy-driven Git operations agent. Operate independently and gather context fresh each time.

        ## Commit Procedure

        1. Analyze changes using:
           - `git status --short`
           - `git diff --stat`
           - `git diff --cached --stat` (if staged changes exist)

        2. Determine commit strategy:
           - Single commit only for one cohesive concern/module.
           - Split commits by concern/module when changes are broad.

        3. Commit messages:
           - Must follow Conventional Commits.
           - Must not contain AI/tool/model references.

        4. Execute commit(s), then verify:
           - `git log --oneline -n <count>`

        ## Other Git Tasks

        For push/pull/branch/merge/rebase/tag/stash/cherry-pick:
        - Follow the repository's commit policy.
        - Explain plan briefly before execution.
        - Report results and issues clearly.

        ## Decision Logging

        When a significant git decision is made (e.g. split strategy or conflict resolution), append to
        `.agent/memory/decision-log.md` with date, decision, rationale, and policy reference.

        ## Absolute Rules

        - Never include AI/tool/model terms in commit messages.
        - Always use Conventional Commits.
        """;

    // ── Git hooks ─────────────────────────────────────────────────────────────

    private const string PreCommitHook = """
        #!/usr/bin/env bash
        set -euo pipefail

        # pre-commit: run your project's validation before each commit.
        # Customize this hook for your repository's build/lint/test scripts.
        #
        # Example: uncomment and adapt the lines below.
        # repo_root="$(git rev-parse --show-toplevel)"
        # cd "${repo_root}"
        # dotnet build -v q -clp:ErrorsOnly --no-restore
        # dotnet test --no-build --nologo | tail -n 5
        """;

    private const string CommitMsgHook = """
        #!/usr/bin/env bash
        set -euo pipefail

        # commit-msg: validate commit message format.
        # This template enforces Conventional Commits and rejects AI-tool references.
        #
        # Customize or remove if you use a different convention.

        msg_file="$1"
        msg="$(cat "${msg_file}")"

        # Reject AI/tool references in the subject line.
        subject="$(head -n1 "${msg_file}")"
        if echo "${subject}" | grep -qiE '\b(claude|chatgpt|gpt|copilot|codex|gemini|llm|agentic)\b'; then
          echo "commit-msg: AI/tool references are not allowed in commit messages."
          exit 1
        fi

        # Optional: enforce Conventional Commit format (uncomment to enable).
        # if ! echo "${subject}" | grep -qE '^(feat|fix|docs|style|refactor|perf|test|chore|ci|build|revert)(\([^)]+\))?: .+'; then
        #   echo "commit-msg: Subject must follow Conventional Commits: type(scope): subject"
        #   exit 1
        # fi
        """;

    private const string PrePushHook = """
        #!/usr/bin/env bash
        set -euo pipefail

        # pre-push: ensure AgentSync projections are current before pushing.
        # Supports both a local dotnet tool install and a global PATH install.
        repo_root="$(git rev-parse --show-toplevel)"
        cd "${repo_root}"

        if dotnet tool run agent --version >/dev/null 2>&1; then
          dotnet agent status --fail-on-drift --ci
        elif command -v agent >/dev/null 2>&1; then
          agent status --fail-on-drift --ci
        else
          echo "Agent Sync is required for this repository."
          echo "Install globally: dotnet tool install -g AgentSync"
          echo "Or as a local tool: dotnet tool restore"
          exit 3
        fi
        """;

    private const string PostCheckoutHook = """
        #!/usr/bin/env bash
        set -euo pipefail

        # post-checkout: refresh AgentSync projections after a branch switch.
        # Args: <prev-HEAD> <new-HEAD> <branch-checkout-flag>
        branch_checkout_flag="${3:-0}"
        if [ "${branch_checkout_flag}" != "1" ]; then
          exit 0
        fi

        repo_root="$(git rev-parse --show-toplevel)"
        cd "${repo_root}"

        if dotnet tool run agent --version >/dev/null 2>&1; then
          ( dotnet agent sync ) >/dev/null 2>&1 || echo "[post-checkout] agent sync skipped (run 'agent sync' manually)"
        elif command -v agent >/dev/null 2>&1; then
          ( agent sync ) >/dev/null 2>&1 || echo "[post-checkout] agent sync skipped (run 'agent sync' manually)"
        fi
        """;

    private const string PostMergeHook = """
        #!/usr/bin/env bash
        set -euo pipefail

        # post-merge: refresh AgentSync projections after a merge or pull.
        repo_root="$(git rev-parse --show-toplevel)"
        cd "${repo_root}"

        if dotnet tool run agent --version >/dev/null 2>&1; then
          ( dotnet agent sync ) >/dev/null 2>&1 || echo "[post-merge] agent sync skipped (run 'agent sync' manually)"
        elif command -v agent >/dev/null 2>&1; then
          ( agent sync ) >/dev/null 2>&1 || echo "[post-merge] agent sync skipped (run 'agent sync' manually)"
        fi
        """;
}
