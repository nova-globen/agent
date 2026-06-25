## When to use

Use this whenever you create, edit, or remove an AI-agent artifact in this repo — a skill
or the generated instruction files — or when `agent status` reports drift. These files are
managed by Agent Sync; the generated projections are not editable by hand.

## The model in one paragraph

The canonical source of truth is `.agent/`. Each skill is a folder `.agent/skills/<id>/`
with `skill.yaml` (metadata: id, name, description, version, and per-target enable flags)
and `SKILL.md` (the instruction body). Agent Sync **projects** each skill into the targets
configured in `.agent/agent.yaml` (`AGENTS.md`, `CLAUDE.md`, Cursor rules, GitHub Copilot,
Gemini, and OpenAI/Claude skill folders) and records a hash for every projection in
`.agent/lock.json`. Generated content sits between `<!-- agent-sync:start ... -->` and
`<!-- agent-sync:end -->` markers in shared files, or is a whole managed file for skill
folders. **Never edit a generated projection by hand** — `lock.json` plus the Git hooks
(`agent status --fail-on-drift`) detect the manual edit and block the commit/push.

## Workflow (every time)

1. Edit the **canonical** source under `.agent/skills/<id>/` — `SKILL.md` for the body,
   `skill.yaml` for metadata and target flags. Do not open the generated `AGENTS.md`,
   `CLAUDE.md`, `.cursor`, `.github`, `.gemini`, or `.claude/skills` files.
2. Run `agent sync` to write the missing/outdated projections and refresh `lock.json`.
3. Run `agent status` and confirm there is no drift (`agent validate` checks config and
   skills).
4. Stage the canonical change **and** its regenerated projections together, then commit.

## Authoring rules

- A `SKILL.md` body must **not** start with a heading that repeats the skill name — Agent
  Sync adds the target-appropriate heading. Start with `## When to use` (or similar).
- The `description` in `skill.yaml` is the trigger text an agent sees; make it specific
  about *when* to use the skill, including the words a user would say. Until the skill
  fires, the description is all that loads.
- Turn a target off **for one skill** in its `skill.yaml` (e.g. keep `agents_md`,
  `claude_md`, and `gemini` off for an on-demand skill so it does not bloat the
  always-loaded files); turn a target off **for the whole repo** in `.agent/agent.yaml`.

## Common commands

- `agent init` — scaffold `.agent/` and the Git hooks.
- `agent skill add <id> --name "<name>" --description "<desc>"` — scaffold a new canonical
  skill; then edit its `skill.yaml` target flags. Also `agent skill list | show <id> |
  edit <id> | delete <id>`.
- `agent import skill <path>` / `agent import agent <path>` — adopt an existing skill
  file/folder or instruction file into `.agent/skills/`.
- `agent subagent add <id> --description "<desc>"` — scaffold a canonical sub-agent. Also
  `agent subagent list | show <id> | edit <id> | delete <id>`, and `agent import subagent
  <path>` to adopt existing `.claude/agents/*.md` files (pass a folder to import all of them).
- `agent sync` (writes by default; `--check` previews, `--force` overwrites a hand-edited
  section), `agent status [--fail-on-drift --ci]`, `agent diff`, `agent validate`,
  `agent doctor`.
- `agent target list | show <id>` — inspect projection destinations (skill targets +
  sub-agent targets) in `agent.yaml`.

## Sub-agents

Sub-agents are a second canonical artifact alongside skills. Each lives in
`.agent/agents/<id>/` with `agent.yaml` (id, name, description, and an optional `model`,
`color`, and `tools` allow-list) and `AGENT.md` (the system-prompt body). `agent sync`
projects each one to a Claude Code sub-agent file at `.claude/agents/<id>.md` and records it
in `.agent/agents.lock.json` — the same canonical → projection → drift flow as skills.
Manage them with `agent subagent add | edit | delete | list | show`, and adopt existing
`.claude/agents/` files with `agent import subagent` (pass a path or omit it to
auto-discover `.claude/agents/`).

### Second-tool sub-agent projection (toml_agent)

By default `agent sync` projects sub-agents only to `.claude/agents/<id>.md`. To also
project them as TOML files for another agent tool, add the `toml_agent` target:

```bash
agent target add toml_agent --path .codex/agents   # configures the output directory
agent sync                                          # emits <id>.toml alongside <id>.md
```

The emitted TOML has `name`, `description`, `model` (if set), `tools` (if set), and
`system_prompt`. Toggle the target off with `agent target edit toml_agent --enabled false`.

As with skills, never hand-edit the generated files; edit `AGENT.md` / `agent.yaml` and
re-run `agent sync`.

Every command also works as `git agent <command>` (for example `git agent sync`).
