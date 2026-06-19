# AGENTS.md

This repository builds **Agent Sync**, a Git-native consistency manager for AI-agent skills, instructions, and configuration files.

## Product Goal

Build a command-line tool that works as both:

```bash
agent status
git agent status
```

The tool helps developers define AI-agent skills once and mirror them to multiple AI agent systems such as Claude, ChatGPT/OpenAI Skills, Cursor rules, GitHub Copilot instructions, Gemini instructions, and generic `AGENTS.md`.

The core problem is **agent instruction drift**.

## Required Product Shape

The project must produce:

- A CLI command named `agent`.
- A Git extension command named `git-agent`, so users can run `git agent ...`.
- Support for `agent status` and `git agent status`.
- Repository wiring via `.githooks`.
- CI/pipeline usability.
- Failure behavior when the tool is required but not installed.
- Open-source, copyleft-friendly structure.

The project currently targets **.NET 10** (`net10.0`). The original guidance was
".NET 8 or newer"; .NET 10 is the chosen target. Keep the project files on `net10.0`.

## Required Commands

Minimum MVP commands:

```bash
agent init
agent status
agent sync
agent diff
agent validate
agent doctor
agent install-hooks

git agent init
git agent status
git agent sync
git agent diff
git agent validate
git agent doctor
git agent install-hooks
```

`git-agent` may delegate to `agent`.

## Canonical File Structure

Agent Sync should manage this structure:

```text
.agent/
  agent.yaml
  lock.json
  skills/
    <skill-id>/
      skill.yaml
      SKILL.md
      assets/
      scripts/
```

## Projection Targets

Initial projection targets:

```text
AGENTS.md
CLAUDE.md
.cursor/rules/*.mdc
.github/copilot-instructions.md
.gemini/GEMINI.md
.chatgpt/skills/<skill-id>/SKILL.md
.claude/skills/<skill-id>/SKILL.md
```

Actual paths must be configurable in `.agent/agent.yaml`.

## Drift Detection

`agent status` detects missing projections, outdated projections, manually edited generated projections, missing canonical skills / no skills, invalid config, missing lockfile entries, and orphaned lockfile entries.

For CI usage:

```bash
agent status --fail-on-drift --ci
```

must exit non-zero if drift or invalid state exists.

## Generated Section Markers

Generated sections must include stable markers:

```md
<!-- agent-sync:start id=<skill-id> target=<target-id> hash=sha256:<hash> -->
...
<!-- agent-sync:end -->
```

The tool must not overwrite user-authored content outside managed sections.

---

## Major feature wave (mostly implemented; GUI rendering pending)

Specs live under `.ai-agent/features/`. Status:

- **Import — implemented.** `agent import skill` / `agent import agent` adopt existing
  skill files/folders and instruction files (`AGENTS.md`, `CLAUDE.md`, Copilot, Gemini,
  Cursor, skill folders) into canonical `.agent/skills/`. Logic in
  `src/AgentSync.Core/Import/` (`features/IMPORTS.md`).
- **CRUD — implemented.** `agent skill add/edit/delete/list/show` (+ `skills`) and
  `agent target add/edit/delete/list/show` (+ `targets`). Logic in
  `src/AgentSync.Core/Authoring/` (`features/CRUD_COMMANDS.md`).
- **`agent ui` — implemented as a launcher.** It starts a separately installed GUI
  executable (`agent-sync-ui`) via `AgentSync.Core.UiLauncher` and fails gracefully when
  absent. No compile-time MAUI/OpenMaui reference from the CLI (guarded by a test).
- **GUI app — skeleton + tested service layer.** `AgentSync.Ui.Abstractions`
  (`AgentSyncApp`) is implemented and tested; `AgentSync.Ui.Maui` is a MAUI Blazor Hybrid
  skeleton **excluded from `AgentSync.slnx`**. The rendered MVP + packaging need a
  MAUI-workload build (`features/UI_MAUI_BLAZOR.md`).
- **Linux GUI — deferred**, experimental OpenMaui spike only; do not claim support until
  tested and packaged (`features/OPENMAUI_LINUX_SPIKE.md`).

Guidance for continuing this wave:

- Work **milestone-based** — see `features/ROADMAP.md`. Land one milestone at a time
  with tests.
- **Keep existing CLI behavior backward compatible.** These are additive commands;
  don't change current command semantics or exit codes.
- **Future UI work must be milestone-based** (see `features/ROADMAP.md`, Milestones
  F/F2/G/H) — land one milestone at a time with tests.
- **The UI must not make the CLI depend on MAUI/OpenMaui.** The headless stack —
  `AgentSync.Cli`, `AgentSync.Core`, `AgentSync.GitAgent`, Git hooks, CI, the
  `dotnet tool` packages, and container images — must build, test, run, and ship without
  any GUI workload and must **never** reference MAUI, OpenMaui, or desktop-UI packages.
- **`agent ui` should launch an external GUI executable** (`agent-sync-ui`) and fail
  gracefully with install guidance when it is absent — it is a launcher, not a UI host.
- **Linux GUI is experimental** pending the OpenMaui (`open-maui/maui-linux`)
  evaluation; do not claim Linux GUI support until it is tested and packaged.
- **The CLI remains the primary supported interface** on every platform; the GUI is an
  optional convenience layer on top.
- Preserve the core invariants below and in `CLAUDE.md` (path safety, marker handling,
  manual-edit protection, `net10.0`, `git-agent` delegation).

## Notes for future sessions

This section captures non-obvious implementation facts to speed up future work. It is
maintained by hand (this repository does not run Agent Sync on itself — `AGENTS.md`
and `CLAUDE.md` here are hand-authored, not generated).

### Build, test, run

- Target framework is `net10.0` (only the .NET 10 SDK/runtime is installed here).
  Do not retarget to net8.0.
- `dotnet build --configuration Release` and `dotnet test` from the repo root.
- Solution file is `AgentSync.slnx` (the newer XML solution format).
- `nuget.config` pins to nuget.org; the inherited `nova-globen` private feed fails
  auth in this environment (harmless `NU1900` warnings).

### Projects

- `src/AgentSync.Core` — all domain logic (config, skills, projections, adapters,
  drift, services). Keep behavior here. Includes `Import/` (skill + agent import) and
  `Authoring/` (skill + target CRUD writers), and `UiLauncher` (discovers/launches the
  external GUI; no MAUI reference).
- `src/AgentSync.Cli` — the `agent` binary (`AssemblyName=agent`); logic lives in the
  public `CliRunner` so tests drive it without spawning a process.
- `src/AgentSync.GitAgent` — the `git-agent` binary (`AssemblyName=git-agent`); its
  `Program` just calls `new CliRunner().Run(args)`, so `git agent <cmd>` == `agent <cmd>`.
- `src/AgentSync.Ui.Abstractions` — UI-independent application service (`AgentSyncApp`)
  over Core, for the GUI; no MAUI dependency; in `AgentSync.slnx` and unit-tested.
- `src/AgentSync.Ui.Maui` — MAUI Blazor Hybrid GUI skeleton (executable `agent-sync-ui`),
  **excluded from `AgentSync.slnx`** (build separately; needs the MAUI workload).

### Key design points

- Projections: shared files (AGENTS.md, CLAUDE.md, copilot, gemini) use
  `agent-sync` markers via `MarkedDocument`; dedicated files (cursor, openai/claude
  skill folders) are whole-file with manual-edit detection via the lockfile hash.
- Path safety: every projection read/write resolves through `RepoPath` (rejects
  absolute, Windows drive/UNC, and `..`-escaping paths). `ConfigValidator` also flags
  unsafe target paths (`config.target-unsafe-path`).
- YAML frontmatter (cursor/skill folders) is emitted via `Yaml.Scalar(...)`, which
  quotes/escapes values that are not safe plain scalars. Do not hand-concatenate.
- Heading strategy: `skill.yaml` owns `name`/`description`/`version`; `SKILL.md` is the
  body only and must not start with a `# Name` heading. Adapters add one heading and
  `SkillContent.StripRedundantHeading` removes a leading `# Name` that would duplicate it.
- Policy (`agent.yaml` `policy:`): `fail_on_missing_projection`,
  `fail_on_outdated_projection`, `fail_on_manual_edit` downgrade the matching drift to a
  reported warning so `agent status --fail-on-drift` does not fail (still listed).
- `agent sync` writes by default; `--check` previews (non-zero on changes), `--force`
  overwrites manually edited generated projections.
- Adapters must produce **deterministic** output (no timestamps/randomness) so hashing,
  drift detection, and golden-file tests stay stable.
- Git hooks must **fail when `agent` is missing** (the scaffolded hooks exit 3 with the
  required message), never silently pass.
- `git-agent` delegates to `AgentSync.Cli.CliRunner`; keep the two entry points in sync.
- Exit codes: 0 success, 1 drift/validation, 2 invalid usage, 3 environment, 4 unexpected.

### Current validated state (v0.1.0-alpha.1)

Manually validated on Windows with the globally installed binaries: `agent --version`
and `git agent --version` (both `agent 0.1.0-alpha.1`), `agent init`, `agent sync`,
`agent status --fail-on-drift --ci`, `git agent status`, `agent install-hooks`, the
pre-commit hook running Agent Sync, manual-edit drift detection in `AGENTS.md`, and a
commit being **blocked** by the hook when drift exists. See
`.ai-agent/VALIDATION_LOG.md`. The current release `v0.1.0-alpha.2` is a version-only
retag of `alpha.1` (no code changes), so this validation carries over. More real-world
testing on Linux/macOS is still needed.

### Releases

- Tag-driven: pushing `v*.*.*` runs `.github/workflows/release.yml`, which publishes
  self-contained `agent`/`git-agent` for linux-x64, linux-arm64, osx-x64, osx-arm64,
  win-x64, generates `checksums.txt`, and creates the GitHub Release via `gh`.
  Release commands: `git tag v0.1.0 && git push origin v0.1.0` (see `RELEASE_CHECKLIST.md`).
- Install scripts: `scripts/install.sh` (Linux/macOS) and `scripts/install.ps1`
  (Windows). `scripts/release-smoke.sh` validates naming/mapping and that both binaries
  publish and `git-agent` delegates to `agent`.
- .NET tool packages: `src/AgentSync.Cli` packs as `AgentSync` (command `agent`) and
  `src/AgentSync.GitAgent` packs as `AgentSync.Git` (command `git-agent`); tool/package
  metadata lives in the two csproj plus shared bits in `Directory.Build.props`
  (`IsPackable` is off by default and opted into per tool project). The release workflow
  packs both and `dotnet nuget push`es them to NuGet.org via **Trusted Publishing**
  (`NuGet/login@v1`, job permission `id-token: write`, secret `NUGET_USER`, one-time
  nuget.org policy — see `RELEASE_CHECKLIST.md`). Tools are framework-dependent, so they
  need the .NET 10 runtime (unlike the self-contained release binaries). Description
  fields must XML-escape `<` / `>` (use `&lt;`/`&gt;`).
  - **Package ids are `AgentSync` / `AgentSync.Git`** (command names stay `agent` /
    `git-agent`). The dotted `Agent.*` prefix is a **reserved NuGet prefix** owned by
    someone else — `Agent.Sync` pushes are accepted synchronously but silently rejected
    by async validation (never publish, re-push returns 409). Do not reuse `Agent.*`.
  - The push step keeps `--skip-duplicate` (idempotent re-runs) but a synchronous push
    success does **not** prove publication; the **Verify packages are live** step polls
    the flat-container (`v3-flatcontainer/<id-lowercased>/<version>/...nupkg`) and fails
    the job if a version never becomes installable. If you change a package id, update
    the lowercased ids in that step.
- The public repo is `https://github.com/nova-globen/agent`; the default branch in CI
  triggers is both `main` and `master`.

### Test environment gotchas

- There is a stray `/tmp/.git` in this sandbox, so temp dirs under `/tmp` can look like
  a Git repo. A few tests are written to tolerate an ancestor repo rather than assume
  its absence.
- CI's drift check and the committed `examples/sample` are verified by running the tool
  from a standalone repo (Agent Sync resolves the Git root upward, so running it inside
  this repo would target the parent project).
