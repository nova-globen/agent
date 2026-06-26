# Example: Agent Sync in GitHub Actions

A complete, in-sync repository plus a **GitHub Actions** workflow that fails the build when
any AI-agent instruction file drifts from its canonical skill. Copy the workflow into your
own repository to gate pull requests on agent-instruction consistency.

## What's here

```text
.agent/
  agent.yaml                                  # enabled targets and their paths
  lock.json                                   # recorded hashes for each projection
  skills/
    conventional-commit/{skill.yaml,SKILL.md} # Conventional Commits guidance
    code-review/{skill.yaml,SKILL.md}         # review checklist
.githooks/                                    # pre-commit / pre-push (fail if 'agent' missing)
.github/
  workflows/agent-sync.yml                    # the CI drift gate (the point of this example)
  copilot-instructions.md                     # generated projection

# Other generated projections, all kept in sync with the skills above:
AGENTS.md                                     # managed sections
CLAUDE.md                                     # managed sections
.gemini/GEMINI.md                             # managed sections
.claude/skills/<id>/SKILL.md                  # generated files
```

This example enables five targets (`agents_md`, `claude_md`, `copilot`, `gemini`,
`claude_skill`). Add Cursor or ChatGPT/OpenAI skills with
`agent target add cursor` / `agent target add openai_skill`.

## The skills

- **`conventional-commit`** — how to write commit messages and PR titles that follow the
  [Conventional Commits](https://www.conventionalcommits.org) spec (`feat`, `fix`, scopes,
  breaking-change markers, examples).
- **`code-review`** — a short review checklist (conventions, correctness, security, tests).

Both are authored once under `.agent/skills/<id>/` and projected into every enabled target,
so Claude, Copilot, Gemini, and an `AGENTS.md`-aware agent all get the same guidance.

## The workflow

[`.github/workflows/agent-sync.yml`](.github/workflows/agent-sync.yml) runs on every push to
`main`/`master` and on every pull request:

```yaml
name: Agent Sync

on:
  push:
    branches: [main, master]
  pull_request:

jobs:
  drift:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Install Agent Sync
        run: |
          curl -fsSL https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.sh \
            | bash -s -- v0.3.2
          echo "$HOME/.agent-sync/bin" >> "$GITHUB_PATH"

      - name: Check for agent-instruction drift
        run: agent status --fail-on-drift --ci
```

`agent status --fail-on-drift --ci` exits non-zero — failing the job — if any projection is
**missing**, **outdated**, or **hand-edited**, or if the config or skills are invalid. The
self-contained install needs no .NET runtime on the runner.

## Use it in your repository

1. Commit a `.agent/` with at least one skill (or run `agent init` and `agent sync`).
2. Copy `.github/workflows/agent-sync.yml` into your repository.
3. Pin the install to the release tag you want and bump it when you upgrade.

### Pin to a released version

The example pins `v0.3.2`. Use the tag you have validated; see the
[Releases page](https://github.com/nova-globen/agent/releases). Omitting the version
installs the latest release.

### Alternative: install as a .NET tool

If your runner already has the .NET 10 SDK (or you add `actions/setup-dotnet`), you can use
the NuGet tool instead of the install script:

```yaml
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool install --global AgentSync
      - run: agent status --fail-on-drift --ci
```

### Windows runners

On `windows-latest`, install with PowerShell instead:

```yaml
      - name: Install Agent Sync
        shell: pwsh
        run: |
          irm https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.ps1 | iex
          "$env:USERPROFILE\.agent-sync\bin" | Out-File -FilePath $env:GITHUB_PATH -Append
```

## Local enforcement too

The `.githooks/` here mirror what `agent install-hooks` wires up: a `pre-commit` and
`pre-push` that run the same drift check locally, so contributors catch drift before CI does.
Run `agent install-hooks` in your repository to enable them.

> Note: this example lives inside the Agent Sync repository for documentation. Run the tool
> from a standalone repository of your own — Agent Sync resolves the Git repository root, so
> running it here would target the parent project.
