# Example: a synced Agent Sync repository

This directory is a snapshot of a repository after running `agent init` followed by
`agent sync`. It shows the canonical source and every generated projection in sync.

## What's here

```text
.agent/
  agent.yaml                              # enabled targets and their paths
  lock.json                               # recorded hashes for each projection
  skills/example-skill/
    skill.yaml                            # skill metadata + enabled targets
    SKILL.md                              # the canonical instruction body (no leading # heading)
.githooks/                               # pre-commit / pre-push (fail if 'agent' missing)

# Generated projections (kept in sync with the skill above):
AGENTS.md                                 # managed section between agent-sync markers
CLAUDE.md                                 # managed section
.github/copilot-instructions.md           # managed section
.gemini/GEMINI.md                         # managed section
.cursor/rules/example-skill.mdc           # generated file
.chatgpt/skills/example-skill/SKILL.md    # generated file
.claude/skills/example-skill/SKILL.md     # generated file
```

## Try it yourself

From an empty directory that is its own Git repository:

```bash
git init
agent init
agent install-hooks
agent sync
agent status --fail-on-drift   # exits 0 — no drift
```

Then edit `.agent/skills/example-skill/SKILL.md` and run `agent diff` to see what
would change, or `agent sync` to mirror the edit into every target.

> Note: this example lives inside the Agent Sync repository for documentation. Run
> the tool from a standalone repository of your own — Agent Sync resolves the Git
> repository root, so running it here would target the parent project.
