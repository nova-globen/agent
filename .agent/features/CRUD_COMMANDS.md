# Feature plan: CLI CRUD commands

> **Status: planned, not implemented.** Implementation-ready spec for a future session.
> All current command behavior must be preserved; these are additive subcommands.

CRUD commands let users create, edit, and remove canonical Agent Sync items
(skills and targets) from the CLI instead of hand-editing `.agent/skills/<id>/skill.yaml`,
`SKILL.md`, and `.agent/agent.yaml`. They are the scripted, validated counterpart to
editing those files by hand.

## Grounding in the current code

- Dispatch lives in `src/AgentSync.Cli/CliRunner.cs` — a `switch` on `args[0]`, each
  command in a `RunX(string[] rest)` method that parses its own flags and returns an
  exit code. CRUD adds command *groups* (`skill`, `target`), so introduce a small
  nested dispatch (e.g. `RunSkill(rest)` switches on `rest[0]`). Keep `CliRunner` thin;
  put logic in `AgentSync.Core`.
- Canonical model: `SkillManifest` / `Skill` (`Skill.cs`), `AgentConfig` /
  `TargetSetting` / `AgentPolicy` (`AgentConfig.cs`), `TargetIds` (known adapter ids).
- Validation: `SkillValidator` (id pattern `^[a-z0-9]+(?:-[a-z0-9]+)*$`, id==folder,
  required name/description/version/body, known targets) and `ConfigValidator`
  (known targets, enabled-needs-path, `config.target-unsafe-path` via `RepoPath`).
- Loading: `WorkspaceLoader.Load(repoRoot)` returns config + skills + validation.
- Lockfile: `Projections/Lockfile.cs` — entries keyed `"<skill>/<target>"`; deletes
  must remove the matching entries or they become **orphans** (`DriftDetector` already
  reports orphaned lock entries).
- YAML emission: always via `Yaml.Scalar(...)`; never hand-concatenate.
- Path safety: every path argument goes through `RepoPath`.

## Proposed code shape

```text
src/AgentSync.Core/Authoring/
  SkillWriter.cs       # create/update/delete .agent/skills/<id>/ (manifest + body)
  TargetWriter.cs      # mutate .agent/agent.yaml targets safely (round-trip preserving)
  ConfigEditor.cs      # load -> mutate -> serialize agent.yaml without losing user keys
  AuthoringResult.cs   # what changed (drives human/--json output + dry-run)
```

`agent.yaml` round-trip matters: editing a single target must not drop the user's
`policy:` block, comments aside. Prefer mutating the deserialized `AgentConfig` and
re-serializing, and document that comments in `agent.yaml` may not survive an edit
(call this out in the command output, or do a targeted text edit if feasible).

## Command groups

```bash
agent skill add | edit | delete | list | show
agent target add | edit | delete | list | show
agent import skill | agent import agent     # see IMPORTS.md
```

Aliases (optional, low cost — map to the same handlers):

```bash
agent skills    # alias for `agent skill list`
agent targets   # alias for `agent target list`
```

## Skill commands

```bash
agent skill add <id> --name "<name>" --description "<description>"
agent skill add <id> --name "<name>" --description "<desc>" --target cursor --target claude_md
agent skill edit <id>
agent skill edit <id> --name "<name>"
agent skill edit <id> --description "<description>"
agent skill edit <id> --version <version>
agent skill edit <id> --body-file path/to/SKILL.md
agent skill edit <id> --enable <target> | --disable <target>
agent skill delete <id>
agent skill delete <id> --force
agent skill list   [--json]
agent skill show <id>   [--json]
```

### Rules

- **`skill add`** creates `.agent/skills/<id>/skill.yaml` and `.agent/skills/<id>/SKILL.md`.
  - `<id>` must pass the kebab-case pattern; reject otherwise (exit 2).
  - Refuse if the skill directory already exists (exit 1) — there is no `--force` on
    `add`; use `edit` to change an existing skill.
  - `--target` (repeatable) sets enabled targets; default to the `init` set when none
    are given. Reject unknown target ids (exit 2).
  - Seed `SKILL.md` with a minimal body (a `## When to use` stub like the `init`
    template) so the new skill validates; `version` defaults to `0.1.0`.
- **`skill edit`** updates the manifest and/or body safely.
  - `--body-file` reads a path (through `RepoPath`) and writes it as the new `SKILL.md`,
    after `StripRedundantHeading`.
  - `--enable`/`--disable` toggle a target in the skill's `targets:` map.
  - Editing `id` is **not** supported via `edit` (id==folder invariant); document that
    renaming = `delete` + `add`/`import`, or a future `skill rename`.
- **`skill delete`** removes `.agent/skills/<id>/`.
  - **Refuse by default when projections still exist on disk** for that skill
    (detected via the planner/lockfile). Print the safe-delete plan: which projection
    files/sections and which lockfile entries would be affected.
  - `--force` (or an accepted plan) proceeds: delete the skill dir, and **remove the
    skill's lockfile entries** so they don't become orphans. Note that managed
    sections already written into shared files (AGENTS.md etc.) are *not* auto-removed
    by delete in alpha — report them as now-orphaned content the user should clean up
    (or define `agent sync` follow-up behavior; see open questions).
- **`skill list`** prints id, name, enabled-target count; `--json` is an array of
  `{ id, name, description, version, targets }`.
- **`skill show <id>`** prints the manifest fields + body length (or full body);
  `--json` emits the full manifest + body.

## Target commands

```bash
agent target list   [--json]
agent target show <target-id>   [--json]
agent target add <target-id> --path <path>
agent target add <target-id> --path <path> --enabled false
agent target edit <target-id> --path <path>
agent target edit <target-id> --enabled true|false
agent target delete <target-id>
agent target delete <target-id> --force
```

### Rules

- `<target-id>` must be a **known adapter id** (`TargetIds.IsKnown`): `agents_md`,
  `claude_md`, `cursor`, `copilot`, `gemini`, `openai_skill`, `claude_skill`. Reject
  unknown ids (exit 2) — targets are not arbitrary, they map to adapters.
- `--path` must pass `RepoPath.IsSafeRelative`; reject absolute/UNC/`..`-escaping paths
  (this is what `ConfigValidator` already flags as `config.target-unsafe-path`).
- `target add` writes a `targets.<id>` block in `agent.yaml` with `enabled` (default
  true) and `path`. Refuse if the target is already configured (use `edit`).
- `target edit` changes `path` and/or `enabled` for an existing target.
- **Disabling or deleting a target has drift/lockfile consequences** — make them
  explicit:
  - Disabling/removing a target means its projections are no longer planned, so their
    lockfile entries become **orphans** (`DriftDetector` reports them). `target delete`
    should offer to prune those lockfile entries (proposal: prune by default on
    `delete`, keep on `edit --enabled false` and just warn).
  - Already-generated files/sections for that target are left on disk; report them so
    the user can remove or `sync --force` as appropriate.
- `target list` shows each known target with enabled/path/configured state; `--json`
  is deterministic (ordered by `TargetIds.Ordered`).
- All target config lives in `.agent/agent.yaml`; never touch other files.

## UX rules (apply to every command)

- **Never silently overwrite.** `add` refuses to clobber; destructive `edit`/`delete`
  require `--force` or an accepted plan.
- **Always show what changed** — list created/updated/deleted paths and lockfile
  entries, like `agent sync`'s outcome list.
- **Prefer dry-run / check for destructive ops** — support `--dry-run` on `delete`
  (and ideally on `edit`) to preview without writing.
- **Correct exit codes** (match the existing contract):
  `0` success · `1` validation/drift problem (e.g. delete blocked by existing
  projections, invalid resulting config) · `2` invalid usage (bad id, unknown target,
  bad flags) · `3` environment (not a repo / path resolution) · `4` unexpected.
- **Validate after every mutation** — run `WorkspaceLoader.Load` and surface any new
  validation errors; if a mutation would make the workspace invalid, report it.
- **Recommend `agent sync`** after any change that affects projections.
- **`--json` everywhere it's useful**, and deterministic (stable key order, sorted
  collections) so tests and the UI can rely on it.
- **Add tests for each behavior** (see below).

## Open questions

- Should `skill delete` also strip the skill's managed sections from shared files
  (AGENTS.md etc.), or only delete the canonical skill + lockfile entries and leave
  cleanup to `sync`? Proposal: alpha removes canonical + lockfile, reports orphaned
  generated content; a later `sync --prune` removes generated sections for deleted
  skills/targets.
- Do we want `agent skill rename <old> <new>`? Cleaner than delete+add given the
  id==folder invariant. Proposal: defer past the first CRUD milestone.
- Should `agent.yaml` edits preserve comments? YAML round-trip via the current
  deserializer will not. Proposal: document the limitation, or do a minimal targeted
  text patch for single-key edits.

## Tests required

- `SkillWriter`: add creates valid manifest+body (passes `SkillValidator`); add refuses
  existing dir; edit updates each field and the body; `--body-file` is normalized;
  enable/disable toggles targets; delete removes dir and lockfile entries; delete is
  blocked when projections exist without `--force`; `--dry-run` writes nothing.
- `TargetWriter`/`ConfigEditor`: add/edit/delete round-trips `agent.yaml` without
  losing `policy:` or other targets; unsafe paths rejected; unknown targets rejected;
  resulting config passes `ConfigValidator`.
- CLI: nested dispatch (`skill`/`target` subcommands), exit codes per the contract,
  deterministic `--json`, aliases (`skills`/`targets`) map correctly.
- Integration: after `skill add` + `sync`, `status` is clean; after `skill delete
  --force` + `sync`, no orphan lock entries remain.
