# Agent Sync

[![CI](https://github.com/nova-globen/agent/actions/workflows/agent-sync-check.yml/badge.svg?branch=master)](https://github.com/nova-globen/agent/actions/workflows/agent-sync-check.yml)
[![Latest release](https://img.shields.io/github/v/release/nova-globen/agent?include_prereleases&sort=semver)](https://github.com/nova-globen/agent/releases)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/license-AGPL--3.0--or--later-blue.svg)](LICENSE)

Agent Sync is a Git-native consistency manager for AI-agent skills, instructions,
and configuration files. Define a skill once and mirror it into the formats every
AI coding agent expects — keeping `AGENTS.md`, `CLAUDE.md`, Cursor rules, GitHub
Copilot instructions, Gemini instructions, and OpenAI/Claude skill folders in sync.

The core problem it solves is **agent instruction drift**: the same guidance,
duplicated by hand across many files, slowly diverging until the agents disagree.

## Status: alpha (developer preview)

Agent Sync is an **alpha / developer preview**. The core workflow — author canonical
skills, project them to every target, and catch drift via Git hooks and CI — works
today and has been used end to end. The fundamentals are solid; the surface is still
settling, so expect some sharp edges and a few breaking changes before a stable v1.

Current limitations:

- Adapters are MVP-level and may evolve.
- The canonical skill schema may change before stable v1.
- Manually validated on Windows; needs more real-world testing on Linux and macOS.
- Install scripts are new and should be tested across more environments.
- Symlink escape hardening is not yet implemented.
- Published as a .NET tool on NuGet (`AgentSync` / `AgentSync.Git`); other
  package managers (Homebrew, winget, etc.) are not available yet.
- Generated output conventions may change based on feedback.

Feedback from real repositories is the most useful thing right now — see
[Contributing](#contributing) and the issue templates.

## Who this is for

- Developers using multiple AI coding agents in one repository.
- Teams maintaining `AGENTS.md`, `CLAUDE.md`, Cursor rules, Copilot instructions,
  Gemini instructions, or skill folders.
- Teams that want Git hooks and CI to catch AI-instruction drift automatically.

## What Agent Sync is not

- Not a replacement for Git.
- Not an AI agent runtime.
- Not a prompt optimizer.
- Not a hosted SaaS.
- Not a package registry.

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

## Installation

Agent Sync ships two entry points: `agent` and the Git extension `git-agent` (so
`git agent <command>` works). Releases include self-contained builds, so no .NET
runtime is required to run them.

The CLI is the primary, fully supported interface. The optional local web UI is a
**separate download** (see [Optional local web UI](#optional-local-web-ui)); installing
the CLI never pulls in the UI, and the `dotnet tool` packages are CLI-only.

### Recommended: install from GitHub Releases

Linux/macOS:

```bash
curl -fsSL https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.sh | bash
```

Install a specific version:

```bash
curl -fsSL https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.sh | bash -s -- v0.1.0-alpha.4
```

By default this installs into `$HOME/.agent-sync/bin`. Override with
`AGENT_SYNC_INSTALL_DIR=/custom/bin`. The script prints how to add the directory to
your `PATH` if needed.

Windows PowerShell:

```powershell
irm https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.ps1 | iex
```

Prefer to review the script before running it (recommended):

```powershell
irm https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.ps1 -OutFile install.ps1
# review install.ps1, then:
.\install.ps1            # or: .\install.ps1 -Version v0.1.0-alpha.4
```

Override the Windows install directory with `$env:AGENT_SYNC_INSTALL_DIR`. Installs
into `%USERPROFILE%\.agent-sync\bin` by default.

### Install as a .NET tool

Agent Sync is published on NuGet as a .NET tool, which is the easiest way to pin a
version per repository. Unlike the self-contained GitHub Releases above, this path
requires the **.NET 10 runtime** on the machine.

Two packages are published:

- [`AgentSync`](https://www.nuget.org/packages/AgentSync) — the `agent` command.
- [`AgentSync.Git`](https://www.nuget.org/packages/AgentSync.Git) — the `git-agent`
  command, so `git agent <command>` works.

**Per-repository (recommended): a tool manifest.** One developer adds the dependency
and commits the manifest; everyone else restores it:

```bash
# once per repo, by whoever adds the dependency
dotnet new tool-manifest          # creates .config/dotnet-tools.json
dotnet tool install AgentSync
dotnet tool install AgentSync.Git

# every other developer, after cloning
dotnet tool restore
```

This produces a committed `.config/dotnet-tools.json`:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "agentsync": { "version": "0.1.0-alpha.4", "commands": ["agent"] },
    "agentsync.git": { "version": "0.1.0-alpha.4", "commands": ["git-agent"] }
  }
}
```

Run a manifest (local) tool through `dotnet`:

```bash
dotnet agent status
dotnet tool run agent -- status   # equivalent
```

> Note: local manifest tools are invoked via `dotnet agent ...`; their shims are not
> placed on `PATH`, so the bare `agent` and `git agent ...` forms are not available
> from a manifest install. For those, use a global install or the GitHub Releases
> binaries.

**Global install.** This puts `agent` and `git-agent` on your `PATH` (via
`~/.dotnet/tools`), so both `agent ...` and `git agent ...` work:

```bash
dotnet tool install --global AgentSync
dotnet tool install --global AgentSync.Git

agent --version
git agent --version
```

### Manual install

1. Go to the [GitHub Releases](https://github.com/nova-globen/agent/releases) page.
2. Download the archive for your OS/architecture, e.g.
   `agent-sync-v0.1.0-alpha.4-linux-x64.tar.gz` (or `...-win-x64.zip` on Windows).
3. Extract it.
4. Put both `agent` and `git-agent` (or `agent.exe` and `git-agent.exe`) on your `PATH`.
5. Verify:

   ```bash
   agent --version
   git agent --version
   ```

Optionally verify the download against `checksums.txt`:

```bash
sha256sum -c checksums.txt
```

### Build from source

Agent Sync currently targets **.NET 10** (`net10.0`).

```bash
git clone https://github.com/nova-globen/agent.git
cd agent
dotnet build --configuration Release
dotnet test
```

The build produces the executables at:

```text
src/AgentSync.Cli/bin/Release/net10.0/agent
src/AgentSync.GitAgent/bin/Release/net10.0/git-agent
```

Put both on your `PATH` to use `agent ...` and `git agent ...`.

## Quick demo

Scaffold a repo, sync the default `code-review` skill into every target, and wire the
hooks:

```bash
mkdir agent-sync-demo
cd agent-sync-demo
git init

agent init
agent sync
agent status --fail-on-drift --ci
agent install-hooks
```

Now simulate drift by hand-editing a generated section:

```bash
# Edit AGENTS.md inside the generated agent-sync marker section
# (the block between <!-- agent-sync:start ... --> and <!-- agent-sync:end -->).
agent status --fail-on-drift --ci
```

Agent Sync reports a **manually edited projection** and exits non-zero. With the hooks
installed, Git blocks the commit:

```bash
git commit --allow-empty -m "Should fail because of drift"
# pre-commit runs 'agent status --fail-on-drift' and aborts the commit
```

Restore the generated content and you're green again:

```bash
agent sync --force
agent status --fail-on-drift --ci
```

## Commands

```bash
agent init            # scaffold .agent/ and .githooks/
agent status          # report state and drift (--json, --fail-on-drift, --ci)
agent sync            # write missing/outdated projections (--check, --write, --force)
agent diff            # show canonical-to-projection differences
agent validate        # validate config and skills
agent import skill    # import an existing SKILL.md / skill folder into .agent/skills
agent skill           # manage canonical skills: add | edit | delete | list | show
agent target          # manage projection targets: add | edit | delete | list | show
agent ui              # launch the optional local web UI (separate install)
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

## Importing existing skills

If you already have a skill folder or a standalone `SKILL.md` (for example under
`.claude/skills/` or `.chatgpt/skills/`), import it into the canonical `.agent/` layout:

```bash
agent import skill .claude/skills/code-review        # a skill folder
agent import skill path/to/SKILL.md                  # a standalone file
agent import skill path/to/SKILL.md --id my-skill --name "My Skill"
agent import skill path/to/SKILL.md --dry-run        # preview; writes nothing
agent import skill path/to/SKILL.md --force          # overwrite an existing canonical skill
```

Import parses any YAML frontmatter, infers `id`/`name`/`description` when possible,
writes `.agent/skills/<id>/skill.yaml` and `SKILL.md`, and validates the result. It
never overwrites an existing canonical skill unless you pass `--force`. After importing,
run `agent sync` to project the skill into your targets. JSON output is available with
`--json`.

If you already have tool-specific instruction files (an `AGENTS.md`, `CLAUDE.md`,
Copilot/Gemini instructions, or Cursor rules) but no `.agent/`, import them into
canonical skills:

```bash
agent import agent AGENTS.md                       # one skill from the whole file
agent import agent AGENTS.md --split sections       # one skill per top-level heading
agent import agent .cursor/rules                    # one skill per .mdc rule
agent import agent .claude/skills                    # delegates to skill import per folder
agent import agent legacy.md --type agents_md        # force the source type
agent import agent AGENTS.md --dry-run               # preview; writes nothing
```

`import agent` reads the source only — it never modifies your original files. Generated
`agent-sync` sections are skipped by default (pass `--include-generated` to include
them). Each imported skill enables the matching target; run `agent sync` afterwards.

## Managing skills

Create, edit, and remove canonical skills without hand-editing `.agent/skills/`:

```bash
agent skill add docs-review --name "Docs Review" --description "Reviews documentation."
agent skill list                       # or: agent skills
agent skill show docs-review           # add --json for machine-readable output
agent skill edit docs-review --description "Reviews docs and examples."
agent skill edit docs-review --body-file path/to/SKILL.md
agent skill edit docs-review --enable cursor --disable gemini
agent skill delete docs-review         # refused if projections exist; preview with --dry-run
agent skill delete docs-review --force # also prunes the skill's lockfile entries
```

Every mutation re-validates the workspace and reminds you to run `agent sync`.
`skill delete` refuses to remove a skill that has already been projected unless you pass
`--force`; generated sections in shared files are left in place for you to clean up or
re-sync.

## Managing targets

Configure which projection targets are enabled and where they live, without editing
`.agent/agent.yaml` by hand:

```bash
agent target list                       # or: agent targets
agent target show cursor                # add --json for machine-readable output
agent target add gemini --path .gemini/GEMINI.md
agent target edit cursor --path .cursor/rules --enabled true
agent target delete gemini              # refused if projections exist; preview with --dry-run
agent target delete gemini --force      # also prunes the target's lockfile entries
```

Target ids must be known adapter ids (`agents_md`, `claude_md`, `cursor`, `copilot`,
`gemini`, `openai_skill`, `claude_skill`) and paths must stay inside the repository.
Edits round-trip `agent.yaml` through the parser, so hand-written comments in that file
are not preserved.

## Optional local web UI

Agent Sync is a CLI first. An optional GUI is a **separate, independent product**: a
local web UI you run on your own machine. The CLI, the Git extension, the Git hooks, CI
usage, the container images, and the `dotnet tool` packages never depend on it or on any
UI assemblies — the CLI still works fully if the UI is not installed.

```bash
agent ui    # locates and launches the separately installed web UI (agent-sync-ui)
```

`agent ui` discovers the `agent-sync-ui` executable, picks a free port, generates a
short-lived session token, launches the host, and opens your browser at
`http://127.0.0.1:<port>/?token=<token>` (it also prints that URL). If the UI is not
installed, it says so and exits without affecting the CLI.

The UI is a Blazor web app built with Microsoft FluentUI Blazor components. It binds to
**`127.0.0.1`** only (never `0.0.0.0`), uses a random port, and gates access with the
session token. It ships as **separate release artifacts** on its own cadence — the CLI
release and the `dotnet tool` packages never include it. See
`.ai-agent/features/UI_LOCALHOST_BLAZOR.md` and `RELEASE_CHECKLIST.md`.

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
Maintainers cutting a release: see [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

**AI-agent maintainers:** for project context and guardrails, start with
[CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md), and
[.ai-agent/CURRENT_STATE.md](.ai-agent/CURRENT_STATE.md).

## License

Agent Sync is licensed under the **GNU Affero General Public License v3.0 or later**
(AGPL-3.0-or-later). See [LICENSE](LICENSE).
