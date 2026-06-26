# Build an AI-Agents Platform (autopilot-centric, .NET)

## Role
You are:
- A Claude Code CLI expert.
- A senior AI-Agent Engineer.
- An expert in prompts, AI agents, skills, sub-agents, tools/plugins, workflows,
  orchestration, evaluations, and agent infrastructure.

## Mission
Create a **self-contained, reusable AI-agent platform** — a kit that lets a .NET repository run the
unattended `autopilot` implement → verify → commit loop — and place it in:

    C:\Users\desma\Data\Repositories\NovaGloben\agent-sync-ai-agent-kit\examples\agent-platform

The centerpiece is the **`autopilot`** skill. Everything else exists to make autopilot actually work
end-to-end in a fresh repo. **The stack is .NET** — keep the `dotnet` build/test/restore commands and the
AgentSync tooling as-is; do not abstract them behind adapters.

> Future direction (NOT now): the `.sh` scripts will later detect the repo's tech stack and run the right
> build/test commands. Do **not** build that abstraction in this version. Hard-code .NET. Keep it simple.

## What matters most: INTEGRITY and ORCHESTRATION
This version's bar is not feature-completeness — it is that **every piece is present, correct, and wired to
every other piece, and the whole thing actually runs**:
- **Integrity:** no dangling references. Every path, skill name, agent name, hook name, script name, and
  doc link the kit mentions must resolve to something that exists in the kit. No half-copied file, no
  reference to a DidaBit path that wasn't brought over.
- **Orchestration:** the skills, agents, git hooks, AgentSync tool, and doc/prompt templates must function
  as one system — autopilot calls next-step/planner/verifier/commit-governor, the hooks fire the AgentSync
  drift gate + commit-msg + backlog-consistency checks, and the prompt chain hands off cleanly. Prove they
  play together, don't just place them side by side.

## Extract the MACHINERY + TEMPLATES, not DidaBit's content
The reference implementation lives in **DidaBit** (`C:\Users\desma\Data\Repositories\NovaGloben\DidaBit`),
a real in-flight product. From it:
- **DO** bring over and generalize: skill text, agent definitions, document *templates*, folder structure,
  naming/ordering conventions, the prompt-chain mechanics, the AgentSync tool + git-hook wiring, and the
  `dotnet` build/test/restore/license gates.
- **DO NOT** bring over DidaBit's actual content: its features, domain model, plans, backlog entries,
  memory contents, ADRs, or example handoff prompts. Replace each with an **empty template** plus **one
  tiny worked example** (a single trivial .NET feature/plan) that proves the loop runs end-to-end.
- **Parameterize/strip DidaBit identity** that isn't part of the platform: `DidaBit.*` module names and
  solution name → a neutral token; the Linux/WSL gate and MSYS2/`rg` paths → optional/configurable;
  DidaBit's product stack prose (Blazor/MAUI/OpenIddict/IIS/Aspire-first) → strip (keep the generic
  ".NET build + test gate," drop the product specifics).

## Phase 1 — Study the reference (delegate the wide reads)
Map autopilot's full dependency graph; read for structure/intent, not content:
- **Skills** (`DidaBit/.agent/skills/`): autopilot + `next-step`, `plan-governor`, `commit-governor`,
  `memory-curator`, `adr-author`, `agentsync`, `operating-guide`. (The .NET-only skills — `dotnet-inspect`,
  `aspire`, `aspireify`, `dotnet-aspire-ddd` — are optional; include only if autopilot actually needs them,
  otherwise leave them out to keep the kit lean.)
- **Agents** (`DidaBit/.agent/agents/`): the sub-agents autopilot delegates to (planner, verifier, recon).
  Capture their scope-in / compact-result-out contracts; the verifier's command is the `dotnet` build/test gate.
- **`.agent/context` / `memory` / `prompts`**: operating-guide, module/commit templates; `active-context.md`
  (thin) + `delivery-log.md` + `decision-log.md` shapes (empty the content); the prompt-chain template + README.
- **`docs/` (keep the FULL template set — completeness over leanness):** bring over every folder's templates
  + organizing conventions — `adr`, `architecture`, `governance`, `features` (esp. `features/00.index.md`
  ordering), `domain`, `plans` (esp. `backlog.md` and how the skills read/update it, ordered per
  `features/00.index.md`), `reports`. Include `runbooks` too (stub it if DidaBit's is empty/unused).
  Empty the content, keep the structure — even folders the worked example won't exercise.
- **Tooling & hooks:** AgentSync dotnet tool (`dotnet-tools.json`; `dotnet agent sync/status/autopilot`) and
  the git hooks (`.githooks`: pre-push drift gate, commit-msg token/format gate, backlog-consistency,
  pre-commit validate). You already know AgentSync's current source — wire to its real, current CLI surface.

> Context discipline: delegate each wide read to an Explore/general-purpose sub-agent; do not dump these
> trees into the main thread.

## How autopilot is driven
`dotnet agent autopilot claude` (AgentSync) automates the manual "fresh session → *continue autopilot* →
work → exit → repeat" loop:
1. **Run a session** — `claude --dangerously-skip-permissions -p "continue autopilot"` headlessly (stdin
   closed = immediate EOF, no TUI). Observer/TUI mode parses NDJSON for live events.
2. **Parse the result** — a second `claude --output-format json --json-schema` inspects repo state
   (`git log --oneline -5` + newest `.agent/prompts/autopilot/prompt-*.txt`) → `{failed, done, message,
   retry.afterSeconds}`.
3. **Branch** — `retry` → wait N s, re-run; `done` → stop; else → wait `DelaySeconds`, next session.
4. Repeat until done, a hard blocker, or Ctrl-C (kills the claude process tree).

**Chain mechanism:** each session writes a fresh `prompt-<yyyyMMdd-HHmm>_<slug>.txt`; the next resumes from
the **newest by filename**. One prompt per session, kept as git history, never overwritten — it's the only
state the next fresh session is guaranteed to read.

> KNOWN GAP carried from the source work (worth designing the kit to be robust against, not necessarily to
> fix here): the autopilot **skill** only writes the handoff prompt at its end-of-session step. If a session
> is killed mid-execution (terminal closed, output/turn/usage limit, manual stop), no fresh handoff is
> written — so the runner's parse step reads a *stale* prompt and either loops on it or mis-verdicts. The
> manual flow survived because the human was the continuity. When wiring the example, make the missing-fresh-
> handoff case observable (e.g. the verify step can tell "no new prompt appeared") rather than silently
> chaining a stale one.

## The handoff prompt: `.agent/prompts/autopilot/prompt-*.txt`
**Purpose:** the baton in the chain. Before any session ends (hard blocker, scope complete, or clean
context-exhaustion checkpoint) it writes a self-contained resume prompt so the next fresh session continues
with no inline brief.
**Rules:** one prompt per session, never overwritten, committed to git history (not gitignored);
autopilot-only artifact (no other skill loads `.agent/prompts/` as routine context); filename
`prompt-<yyyyMMdd-HHmm>_<slug>.txt`, slug = next resume point (kebab-case) from the owning plan/feature.
**Structure** — verbatim ALL-CAPS labels (the next session validates against them):

| Section | Contents | Required? |
| --- | --- | --- |
| *(intro line)* | "Use the autopilot skill to continue… Never push." | — |
| **BOOTSTRAP** | Env gate, what to read first (AGENTS.md, active-context, backlog), binding context-discipline reminder | required |
| **ALREADY COMPLETE** | Newest-first finished increments with **commit hashes** + how each was verified — the anti-redo guard | required |
| **KEY FACTS TO CARRY** | Durable, non-obvious conventions/recon the next slice needs (link a `MEMORY_*`/plan file, don't paste bulk) | expected |
| **RESUME AT** | Exactly **one** next unstarted increment — where its plan lives, what to read first, approach, decisions made | required |
| **NEXT AFTER** | What follows in dependency order (incl. anything that runs LAST) | expected |
| **CARRY-FORWARD DEFERRALS** | Each open deferral, tagged to its owning feature | expected |
| **GOTCHAS** | Repo-specific traps (commit timeout, commit-msg/backlog gates, restore flakiness, …) | expected |
| **HANDOFF** | Instruction telling the next session to write *its own* follow-on file the same way | required |

Required fields (missing/garbled → next session stops and reports invalid format): **BOOTSTRAP**, an
**ALREADY COMPLETE** section, exactly one **RESUME AT**, and the **HANDOFF** instruction. The rest may be
short/empty on a young chain. Principle: **generalize, don't transcribe** — dense, self-contained.

## Reference: exact .NET-toolchain touchpoints in the source (from this session's audit)
Use this so you keep the right `dotnet` machinery and strip the right DidaBit identity. Three buckets:

**Bucket A — hard `dotnet`/build/test/license invocations → KEEP as the .NET gate (verbatim where sensible):**
- `scripts/pre-commit-validate.sh`: `dotnet build <Solution>.sln -v q -clp:ErrorsOnly -nologo`;
  `dotnet test "<project>" --no-build -v q -nologo`; `find tests -name '*.Tests.csproj'`; calls
  `check-nuget-licenses.sh`; `dotnet agent status --fail-on-drift` drift gate at the top.
- `scripts/check-nuget-licenses.sh`: license allowlist + `PackageReference` grep over `*.csproj` + nuget.org curl.
- `scripts/run-integration-tests.sh`: `dotnet test tests/<...>.csproj` (Docker/Aspire tier; optional for the kit).
- `.agent/agents/verifier/AGENT.md`: runs `pre-commit-validate.sh`, `dotnet test tests/<Project>`,
  `dotnet build -v q -clp:ErrorsOnly`, `dotnet restore <Solution>.sln`.
- `.githooks/pre-commit` → `pre-commit-validate.sh`; `setup-git-hooks.sh` → `dotnet tool restore` + `dotnet agent sync`.
- `scripts/bootstrap-solution.sh`: `dotnet new sln`. (`setup-dev.ps1` / `diagnose-windows-dotnet.ps1` are
  full SDK installers/diagnostics — likely out of scope for the kit; copy only if you want turnkey setup.)

**Bucket A′ — AgentSync's own `dotnet agent …` (KEEP — this is the engine, stack-agnostic, not the target stack):**
`dotnet agent sync` / `status --fail-on-drift` / `validate` / `skill` / `target` / `diff`; `dotnet tool restore`.
Appears in `.githooks/pre-push` (drift gate `--fail-on-drift --ci`), `post-checkout`, `post-merge`,
`pre-commit-validate.sh`, the `agentsync` skill, and `AGENTS.md`/operating-guide. The only real requirement
it imposes on a kit user is "AgentSync tool installed."

**Bucket B — .NET concepts baked into PROSE → GENERALIZE/parameterize (no CLI call):**
- `*.csproj ProjectReference` graph boundary tests → "module-boundary/dependency tests" (architecture-principles,
  planner, plan-governor, project-intake, operating-guide).
- `Directory.Build.props` / `Directory.Packages.props` / `global.json` / central package mgmt / `TargetFramework` /
  `Nullable` / `ImplicitUsings` / `PackageReference` (project-intake, project-brief, operating-guide).
- NuGet audit gate: `TreatWarningsAsErrors`, `NU1901–NU1904`, `.dgspec.json`, `NETSDK1064`, restore-flakiness
  (operating-guide Gotchas) → keep as adapter/README gotchas, generalized.
- SDK/framework versions (`.NET 10.x`, `Aspire 13.x`) and the Aspire-first verification section + product stack
  prose (Blazor/MAUI/OpenIddict/IIS, `Sustainsys.Saml2`) → strip from the neutral kit (these are DidaBit product facts).

**Bucket C — DidaBit-specific, NOT .NET → STRIP/parameterize regardless:**
- Linux/WSL gate `scripts/require-linux-or-wsl.sh` (already relaxed to always-ok in DidaBit) → optional/configurable hook.
- Module naming `DidaBit.{Module}.{Layer}`, `DidaBit.Infrastructure.CrossCutting`, `<Solution>.sln` → neutral token.
- Plan/backlog structure + `src//tests//installer/` commit gate → KEEP (text/git, core), de-DidaBit the example paths.

## Phase 2 — Design (brief, then build)
Write a short design first: the target tree under `examples/agent-platform/` (`.agent/skills`,
`.agent/agents`, `.agent/context`, `.agent/memory`, `.agent/prompts/autopilot`,
`docs/{adr,architecture,governance,features,domain,plans,reports,runbooks}`, `.githooks`, `dotnet-tools.json`,
`AGENTS.md`); which DidaBit pieces are copied / generalized / stubbed; a one-glance map of how
skills↔agents↔hooks↔tool interlock through the loop; and the single trivial worked example.

## Phase 3 — Build & wire
Lay down the tree, generalized skills/agents, context/memory/prompt templates, the full `docs/` template set,
git hooks, and AgentSync wiring. Wire the worked example so `dotnet agent autopilot claude` can complete at
least one real implement → verify → commit → handoff cycle. Keep the `dotnet` gates intact; strip DidaBit
product specifics.

## Phase 4 — Verify it actually works together (the acceptance bar)
Don't assert — run it:
- `dotnet tool restore` then `dotnet agent sync` regenerates projections with **no drift** (`dotnet agent status`).
- Git hooks fire correctly: pre-push drift gate, commit-msg token/format gate, backlog-consistency,
  pre-commit validate (the `dotnet` build + test gate goes green on the worked example).
- A fresh "continue autopilot" resolves the newest `prompt-*.txt`, resumes at its `RESUME AT`, and on
  finishing writes a **valid** next prompt (every required section present and parseable by the verdict step).
- The malformed-prompt path is handled (invalid format → stop and report, not guess).
- At least one full autopilot cycle completes on the worked example and chains to a second prompt.
- **Integrity sweep:** cross-check every internal reference (paths, skill/agent/hook/script names, doc
  links) — zero dangling links, zero references to un-copied DidaBit paths.
- Confirm the full `docs/` template set is present (adr, architecture, governance, features, domain, plans,
  reports, runbooks) — each with a usable template, none silently dropped for being unused by the example.

## Operating rules for this build
- Delegate wide reads to sub-agents; run build/test through a verifier-style agent; never let full logs land
  in the main thread; batch edits then verify once.
- Commit by concern; **never push** (leave the remote for the developer).
- Conventional Commits; no AI/tool tokens in messages (the commit-msg gate rejects standalone
  `ai|codex|claude|chatgpt|gpt|llm|agentic`); no Co-Authored-By trailer.
- Record durable design decisions as ADRs/decision-log entries inside the new kit.
- If a dependency genuinely can't be reproduced in the example, **don't fake it** — stub it explicitly and
  record it as a carry-forward deferral. Keep it simple; integrity and orchestration over breadth.

## Deliverable
A working, documented, self-contained `examples/agent-platform/` kit with a `README` explaining what it is,
how the autopilot loop + prompt chain work, what to fill in, and how to run `dotnet agent autopilot claude`
against it — proven by an actual end-to-end cycle, not asserted.
