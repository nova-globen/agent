# Agent Sync

Agent Sync is a Git-native consistency manager for AI-agent skills, instructions,
and configuration files. Define a skill once and mirror it into the formats every
AI coding agent expects — keeping `AGENTS.md`, `CLAUDE.md`, Cursor rules, GitHub
Copilot instructions, Gemini instructions, and OpenAI/Claude skill folders in sync.

The core problem it solves is **agent instruction drift**: the same guidance,
duplicated by hand across many files, slowly diverging until the agents disagree.

## How it works

You author canonical skills under `.agent/`:

```text
.agent/
  agent.yaml            # which targets are enabled and where they live
  lock.json             # last-known-good hashes for every projection
  skills/
    <skill-id>/
      skill.yaml        # id, name, description, version, enabled targets
      SKILL.md          # the canonical instruction body
```

Agent Sync **projects** each skill into the configured targets:

```text
AGENTS.md                                  # managed section
CLAUDE.md                                  # managed section
.cursor/rules/<skill-id>.mdc               # generated file
.github/copilot-instructions.md            # managed section
.gemini/GEMINI.md                          # managed section
.chatgpt/skills/<skill-id>/SKILL.md        # generated file
.claude/skills/<skill-id>/SKILL.md         # generated file
```

Content written into shared files lives between stable markers and is never allowed
to clobber your hand-written prose:

```md
<!-- agent-sync:start id=<skill-id> target=<target-id> hash=sha256:<hash> -->
...generated content...
<!-- agent-sync:end -->
```

A hand-edited section is detected (its content no longer matches the hash) and is
left untouched unless you pass `--force`.

## Install

Agent Sync currently targets **.NET 10** and ships two entry points: `agent` and the
Git extension `git-agent` (so `git agent <command>` works).

```bash
dotnet build --configuration Release
# the build produces 'agent' and 'git-agent' binaries under
# src/AgentSync.Cli/bin/Release and src/AgentSync.GitAgent/bin/Release
```

Put both on your `PATH` to use `agent ...` and `git agent ...`.

## Commands

```bash
agent init            # scaffold .agent/ and .githooks/
agent status          # report state and drift (--json, --fail-on-drift, --ci)
agent sync            # write missing/outdated projections (--check, --write, --force)
agent diff            # show canonical-to-projection differences
agent validate        # validate config and skills
agent install-hooks   # configure core.hooksPath and make hooks executable
agent doctor          # diagnose Git repo, PATH, hooks, and config

# Every command is also available as: git agent <command>
```

### `agent sync` behavior

`agent sync` **writes by default** — it creates missing projections and updates
out-of-date ones. Two flags change that:

- `agent sync` — writes missing/outdated projections. Generated sections that you have
  hand-edited are detected and left untouched (reported, not overwritten).
- `agent sync --check` — previews changes without writing anything; exits non-zero if
  any projection would change or has been manually edited.
- `agent sync --force` — additionally overwrites manually edited generated projections,
  regenerating them from the canonical source.

`--write` is the explicit form of the default and can be passed for clarity.

### Skill content conventions

- `skill.yaml` owns display metadata: `name`, `description`, `version`.
- `SKILL.md` owns the instruction body only — it should **not** start with a
  `# <skill name>` heading. Each adapter adds one target-appropriate heading derived
  from `name`, so a leading heading that repeats the skill name is dropped from
  generated output to avoid duplicate headings.

### Typical workflow

```bash
agent init            # once per repository
agent install-hooks   # wire .githooks via core.hooksPath
# edit .agent/skills/<id>/SKILL.md ...
agent sync            # mirror the change into every target
agent status          # confirm no drift
```

## Drift detection

`agent status` detects missing projections, outdated projections, manually edited
generated sections, invalid config, missing lockfile entries, and orphaned lockfile
entries. For CI:

```bash
agent status --fail-on-drift --ci
```

exits non-zero if drift or invalid state exists. During early development you can use
the build directly:

```bash
dotnet run --project src/AgentSync.Cli -- status --fail-on-drift --ci
```

## Exit codes

```text
0 = success
1 = drift detected or validation failed
2 = invalid usage
3 = tool/environment problem
4 = unexpected error
```

## Git hooks

`agent init` writes `.githooks/pre-commit` and `.githooks/pre-push`, and
`agent install-hooks` points Git at them via `core.hooksPath`. If the hooks are
installed but `agent` is missing, commits and pushes fail with:

```text
Agent Sync is required for this repository.
Install it, then retry.
```

## Example

See [`examples/sample`](examples/sample) for a fully initialized and synced
repository.

## Repository layout

```text
src/
  AgentSync.Cli/        # the 'agent' CLI
  AgentSync.GitAgent/   # the 'git-agent' extension (delegates to the CLI)
  AgentSync.Core/       # config, skills, projections, adapters, drift
tests/
  AgentSync.Core.Tests/
  AgentSync.Cli.Tests/
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). By participating you agree to the
[Code of Conduct](CODE_OF_CONDUCT.md). Security reports: see [SECURITY.md](SECURITY.md).

## License

Agent Sync is licensed under the **GNU Affero General Public License v3.0 or later**
(AGPL-3.0-or-later). See [LICENSE](LICENSE).
