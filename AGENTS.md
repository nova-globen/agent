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
  drift, services). Keep behavior here.
- `src/AgentSync.Cli` — the `agent` binary (`AssemblyName=agent`); logic lives in the
  public `CliRunner` so tests drive it without spawning a process.
- `src/AgentSync.GitAgent` — the `git-agent` binary (`AssemblyName=git-agent`); its
  `Program` just calls `new CliRunner().Run(args)`, so `git agent <cmd>` == `agent <cmd>`.

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
`.ai-agent/VALIDATION_LOG.md`. More real-world testing on Linux/macOS is still needed.

### Releases

- Tag-driven: pushing `v*.*.*` runs `.github/workflows/release.yml`, which publishes
  self-contained `agent`/`git-agent` for linux-x64, linux-arm64, osx-x64, osx-arm64,
  win-x64, generates `checksums.txt`, and creates the GitHub Release via `gh`.
  Release commands: `git tag v0.1.0 && git push origin v0.1.0` (see `RELEASE_CHECKLIST.md`).
- Install scripts: `scripts/install.sh` (Linux/macOS) and `scripts/install.ps1`
  (Windows). `scripts/release-smoke.sh` validates naming/mapping and that both binaries
  publish and `git-agent` delegates to `agent`.
- The public repo is `https://github.com/nova-globen/agent`; the default branch in CI
  triggers is both `main` and `master`.

### Test environment gotchas

- There is a stray `/tmp/.git` in this sandbox, so temp dirs under `/tmp` can look like
  a Git repo. A few tests are written to tolerate an ancestor repo rather than assume
  its absence.
- CI's drift check and the committed `examples/sample` are verified by running the tool
  from a standalone repo (Agent Sync resolves the Git root upward, so running it inside
  this repo would target the parent project).
