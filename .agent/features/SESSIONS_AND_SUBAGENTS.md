# Feature: Session backup/restore and canonical sub-agents

Two features added on top of the skill/projection core.

## 1. Session backup/restore (`agent sessions`)

Back up an AI agent's per-project conversation history and restore it into a different
environment — another machine, OS, or project path.

### CLI

```text
agent sessions providers [--json]
agent sessions list [<provider>] [--project <path>] [--json]
agent sessions backup <provider> [--project <path>] [--output <file>] [--json]
agent sessions restore <archive> [--project <path>] [--provider <id>] [--dry-run] [--force] [--json]
```

`<provider>` ∈ `claude`, `codex`, `copilot`, `gemini`, `cursor` (plus aliases). The default
project is the Git repo root (or the working directory); `--project` overrides it and may be a
foreign-OS absolute path (e.g. a Windows `C:\...` path while restoring on WSL).

### Archive format

A zip with a top-level `manifest.json` and session files under `files/`. The manifest records
schema version, Agent Sync version, provider id, and the **source** environment (platform,
path style, home directory, project path, provider store key) plus a SHA-256 per file.

### Providers (`AgentSync.Core/Sessions/Providers/`)

Each `ISessionProvider` knows how to (a) **collect** a project's session files for backup and
(b) **place** an archived file on restore (its absolute destination + whether to rewrite text).

- **Claude Code** — `~/.claude/projects/<encoded-cwd>/`, one folder per project. The folder
  name is the cwd with path separators turned into dashes; backup falls back to matching the
  `cwd` recorded inside the transcripts because the encoding is lossy. Restore recomputes the
  folder for the destination path. Not experimental.
- **Codex** — `~/.codex/sessions/YYYY/MM/DD/rollout-*.jsonl`, date-organised. A project's
  rollouts are matched by the `cwd` in each file's first `session_meta` line. Not experimental.
- **Copilot** — `~/.copilot/session-state/` etc.; per-session UUID dirs matched by the project
  path appearing in their contents (`ContentMatchSessionProvider`). Experimental.
- **Gemini** — `~/.gemini/tmp/<id>/`; matched by the `.project_root` marker (new slug scheme)
  or the legacy SHA-256 directory. Experimental.
- **Cursor** — `…/Cursor/User/workspaceStorage/<hash>/`; matched via `workspace.json`. The
  store is a binary SQLite DB, so contents are not rewritten. Experimental.

### Cross-environment path handling

- `PathConversion` / `LocationPath` decompose an absolute path into an optional drive + segments
  and render it in any style: Unix `/home/...`, WSL `/mnt/c/...`, Windows `C:\...` / `C:/...`.
- `PathRewriter` translates every spelling of the source location to the destination's **native**
  style, JSON-escaping backslashes for `.json`/`.jsonl` content, via a single left-to-right scan
  (so a replacement's output is never itself rewritten). Both the storage location and embedded
  paths (e.g. Claude's `cwd`, Codex's `payload.cwd`) are retargeted.

### Safety

- Restore validates each archive entry path (`RepoPath.IsSafeRelative`) and confines all writes
  under the provider's own root directory in the destination home (zip-slip defence).
- Existing files are never overwritten without `--force`; `--dry-run` previews.

## 2. Canonical sub-agents (`agent subagent`, `import subagent`)

Manage delegate agents (Claude Code `.claude/agents/*.md`) the same canonical-once way as
skills.

- Canonical source: `.agent/agents/<id>/agent.yaml` (id, name, description, optional model and
  `tools` allow-list) + `AGENT.md` (system prompt body).
- `agent sync` projects each enabled sub-agent into `.claude/agents/<id>.md` (frontmatter:
  `name` = id, `description`, optional `tools`/`model`) as a managed whole file; manual edits
  are detected via a dedicated lockfile `.agent/agents.lock.json` and never overwritten without
  `--force`.
- `agent status` reports sub-agent drift (missing / outdated / manually edited / orphan).
- `agent import subagent <path>` imports an existing `.claude/agents/*.md` file (or a folder of
  them) into canonical form, parsing comma-separated or block-list `tools`.

Code: `AgentSync.Core/Subagents/` (`SubagentFiles`, `SubagentProjector`),
`AgentSync.Core/Authoring/SubagentWriter`, `AgentSync.Core/Import/SubagentImporter`.
