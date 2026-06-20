# Feature plan: Import commands

> **Status: implemented.** This was the original implementation-ready spec; the import
> commands (`agent import skill` / `agent import agent`) now ship. Kept for historical
> design context. (This repository itself now runs Agent Sync on its own instruction
> files — see `.agent/CURRENT_STATE.md`.)

Import lets a repository that *already has* AI-agent instruction files adopt Agent
Sync without re-typing everything. It reverses the normal data flow: instead of
`canonical skill -> projection`, import reads an existing projection-shaped file and
produces a **canonical skill** under `.agent/skills/<id>/`.

There are two import families:

- **`agent import skill`** — import a single skill (a `SKILL.md`, with or without a
  surrounding skill folder). The source is already skill-shaped.
- **`agent import agent`** — import an existing tool-specific *instruction* file or
  folder (`AGENTS.md`, `CLAUDE.md`, Copilot, Gemini, Cursor rules, or a skill-folder
  root) and turn its content into one or more canonical skills.

Both families end the same way: write canonical skill(s), validate, and tell the user
to run `agent sync`.

## Grounding in the current code

Read these before implementing — the import layer must reuse them, not reinvent them:

- `src/AgentSync.Core/Configuration/Skill.cs` — `SkillManifest` (`Id`, `Name`,
  `Description`, `Version`, `Targets`) and `Skill`.
- `src/AgentSync.Core/Configuration/Validation.cs` + `ConfigValidator.cs` —
  `SkillValidator` rules: id is required and must be lowercase kebab-case
  (`^[a-z0-9]+(?:-[a-z0-9]+)*$`), id must match folder name, `name`/`description`/
  `version` required, `SKILL.md` must be non-empty, targets must be known.
- `src/AgentSync.Core/Adapters/SkillContent.cs` — `StripRedundantHeading(body, name)`:
  the canonical `SKILL.md` body must **not** start with a `# Name` heading.
- `src/AgentSync.Core/Adapters/SkillFolderAdapter.cs` / `CursorAdapter.cs` /
  `SharedMarkdownAdapter.cs` — the exact frontmatter/heading shapes import must parse
  (these are what `sync` *writes*; import must read the same shapes back).
- `src/AgentSync.Core/Configuration/TargetIds.cs` — canonical target ids:
  `agents_md`, `claude_md`, `cursor`, `copilot`, `gemini`, `openai_skill`,
  `claude_skill`.
- `src/AgentSync.Core/Projections/Markers.cs` + `MarkedDocument.cs` — `agent-sync`
  marker parsing. Import must recognize generated sections so it does not re-import
  Agent Sync's own output as if it were hand-authored.
- `src/AgentSync.Core/RepoPath.cs` — every source path and every written canonical
  path must resolve through `RepoPath` (rejects absolute, Windows drive/UNC, and
  `..`-escaping paths).
- `src/AgentSync.Core/RepoLayout.cs` — canonical paths (`.agent/skills/<id>/`).
- `src/AgentSync.Core/Configuration/Yaml.cs` / `Adapters/Yaml.cs` — emit `skill.yaml`
  via `Yaml.Scalar(...)`; never hand-concatenate YAML values.

## Proposed code shape

Keep all logic in `AgentSync.Core` (the CLI stays a thin dispatcher, like the existing
commands in `CliRunner.cs`). Suggested new files:

```text
src/AgentSync.Core/Import/
  ImportSource.cs          # enum/record: detected source kind + path(s)
  SourceDetector.cs        # path -> ImportSource (skill folder, SKILL.md, AGENTS.md, .mdc, ...)
  SkillFolderReader.cs     # parse <dir>/SKILL.md (+ frontmatter) into a draft skill
  MarkdownFrontmatter.cs   # split `---`-delimited YAML frontmatter from a Markdown body
  HeadingSplitter.cs       # split a Markdown doc into sections by heading level
  SkillImporter.cs         # `import skill` orchestration -> ImportReport
  AgentImporter.cs         # `import agent` orchestration -> ImportReport
  ImportReport.cs          # what would be / was created or changed (drives --dry-run + --json)
  IdInference.cs           # derive a safe kebab-case id from filename / heading / frontmatter
```

`ImportReport` should mirror the shape of existing reports (`SyncReport`,
`StatusReport`) so the CLI can render human + `--json` output the same way, and so the
future UI can reuse it. Importers must be **pure planning + apply** (compute the plan,
then optionally write), so `--dry-run` shares the exact code path as a real import
minus the writes — same pattern as `SyncService.Run(force, dryRun)`.

## A. `agent import skill`

### Sources to support

```text
.chatgpt/skills/<skill-id>/SKILL.md     # OpenAI/ChatGPT skill folder
.claude/skills/<skill-id>/SKILL.md      # Claude skill folder
any folder containing SKILL.md          # generic skill folder
any standalone SKILL.md                 # bare file
```

Detection rules (`SourceDetector`):

- Path is a directory containing `SKILL.md` -> skill folder; the folder name is the
  default id candidate.
- Path is a file named `SKILL.md` (any case) -> bare skill; the *parent* folder name
  is the default id candidate, falling back to inference from frontmatter `name`.
- Path is a directory whose name is `skills` (e.g. `.claude/skills/`) -> this is an
  *agent* import (multiple skills), delegate to `import agent` semantics; print a hint.

### Planned commands

```bash
agent import skill <path>
agent import skill <path> --id <skill-id>
agent import skill <path> --name "<display name>"
agent import skill <path> --target openai_skill
agent import skill <path> --target claude_skill
agent import skill <path> --dry-run
agent import skill <path> --force
agent import skill <path> --json
```

`--target` records which target(s) to enable in the new `skill.yaml`. If omitted,
enable a sensible default set (proposal: all known targets enabled, matching what
`agent init` scaffolds — see open questions). `--target` may be repeated.

### Expected behavior

1. Detect the source shape; error clearly if unsupported.
2. Parse skill frontmatter (`---` block) when present: read `name`, `description`,
   and any `version`. Parse via the same YAML deserializer as `SkillManifest`.
3. Infer the canonical `id`:
   - explicit `--id` wins;
   - else frontmatter `name` slugified to kebab-case;
   - else source folder name;
   - else the file's parent folder name.
   Validate the result against `SkillValidator`'s id pattern; reject (don't silently
   mangle) if it can't be made safe — tell the user to pass `--id`.
4. Infer `name` (`--name` > frontmatter `name` > Title-cased id) and `description`
   (frontmatter `description` > first non-heading paragraph of the body > empty, which
   then fails validation and is reported).
5. Normalize the body: strip the frontmatter, then run `StripRedundantHeading` so a
   leading `# Name` that duplicates the display name is removed. Display metadata lives
   in `skill.yaml`, body lives in `SKILL.md`.
6. Write canonical files:
   - `.agent/skills/<id>/skill.yaml` (via `Yaml.Scalar`, with `version` defaulting to
     `0.1.0` when the source had none),
   - `.agent/skills/<id>/SKILL.md` (normalized body).
7. **Never overwrite an existing canonical skill** at `.agent/skills/<id>/` unless
   `--force`. Without `--force`, report the conflict and exit non-zero.
8. With `--dry-run`, do all of the above except the writes; print exactly what *would*
   be created/changed.
9. After a real import, run validation (`WorkspaceLoader.Load` + report
   `Validation.Messages`) so the user sees any remaining problems immediately.
10. Recommend the next step: `Run 'agent sync' to project this skill into your targets.`

### Import report

The report (human + `--json`) must state, per skill: the resolved `id`, `name`,
`description` (or "inferred"/"missing"), the canonical paths that would be / were
written, the action (`create` / `overwrite` / `skip`), and a validation summary.

## B. `agent import agent`

"Agent file/folder" = an existing tool-specific instruction file a repo already keeps
before adopting Agent Sync. Import reads it and produces canonical skill(s).

### Sources to support

```text
AGENTS.md                      # type: agents_md
CLAUDE.md                      # type: claude_md
.github/copilot-instructions.md# type: copilot
.gemini/GEMINI.md              # type: gemini
.cursor/rules/<name>.mdc       # type: cursor (single rule file)
.cursor/rules/                 # type: cursor (folder of rules -> one skill per .mdc)
.chatgpt/skills/               # delegate to skill import per subfolder
.claude/skills/                # delegate to skill import per subfolder
```

### Planned commands

```bash
agent import agent <path>
agent import agent <path> --type agents_md
agent import agent <path> --type claude_md
agent import agent <path> --type cursor
agent import agent <path> --type copilot
agent import agent <path> --type gemini
agent import agent <path> --split sections     # one skill per top-level heading
agent import agent <path> --split file         # one skill for the whole file (default)
agent import agent <path> --id <skill-id>      # base id (used as-is for --split file)
agent import agent <path> --dry-run
agent import agent <path> --force
agent import agent <path> --json
```

`--type` overrides auto-detection (auto-detect from the filename/path by default).
`--split` selects the strategy (default `file`; see open questions).

### Expected behavior

1. Detect or accept (`--type`) the source kind.
2. **Marker awareness:** parse the file with `MarkedDocument`. Content inside
   `agent-sync:start/end` markers is *already generated by Agent Sync* — by default,
   skip it (don't re-import generated content as new canonical content). Only import
   marked sections when an explicit flag (proposal: `--include-generated`) is passed.
   Import the hand-authored prose outside the markers.
3. For **shared Markdown** files (AGENTS.md, CLAUDE.md, Copilot, Gemini):
   - `--split file` (default): one skill from the whole hand-authored body; id from
     `--id` or inferred from the filename (`agents-md`, `claude-md`, ...).
   - `--split sections`: split by top-level (`##`/`#`) headings via `HeadingSplitter`;
     each section becomes a candidate skill (heading -> `name` -> inferred id; body ->
     `SKILL.md`). Report each candidate.
4. For **Cursor `.mdc`** files: parse the YAML frontmatter (`description`, `globs`,
   `alwaysApply`) and the body. `description` -> skill `description`; the `# Name`
   heading (CursorAdapter writes one) -> `name`; the rest -> body. A `.cursor/rules/`
   *directory* imports each `.mdc` as its own skill.
5. For **skill folders** (`.chatgpt/skills/`, `.claude/skills/`): enumerate subfolders
   and delegate each to `import skill` behavior.
6. **Preserve the original files.** Import only *reads* them; it never edits or deletes
   the source. (Running `agent sync` later may write managed sections into the same
   files — that is a separate, explicit step.)
7. Normalize bodies (`StripRedundantHeading`), write canonical skill(s), respect
   `--force` for existing ids, support `--dry-run`, validate after, and recommend
   `agent sync`.
8. Produce a clear multi-skill import report.

## Conflict & error behavior (both families)

Each row is a defined outcome, not an exception that escapes to exit code 4.

| Situation | Behavior | Exit code |
| --- | --- | --- |
| Canonical skill id already exists | Report conflict; skip unless `--force` | 1 (drift/validation) |
| Inferred id is invalid (not kebab-case, can't be slugified) | Report; tell user to pass `--id` | 2 (invalid usage) |
| Unsupported source shape | Report what shapes are supported | 2 |
| `SKILL.md` missing in a folder source | Report; skip that source | 1 |
| Malformed YAML frontmatter | Report the YAML error (reuse `ConfigParseException` message); skip | 1 |
| Duplicate detected skill (two sources -> same id in one run) | Import first; report the rest as conflicts (or require `--force`) | 1 |
| Unknown `--target` / `--type` value | Reject; list known values | 2 |
| Unsafe source/destination path (`RepoPath` rejects) | Report the `RepoPath` error; abort | 3 (environment) or 2 |
| Source outside the repo root | Reject via `RepoPath` | 2 |

Always: do not partially write a skill (write `skill.yaml` + `SKILL.md` together, or
neither). Never overwrite without `--force`. Always show exactly what changed.

## Open questions (decide during implementation, record the decision)

- **One skill per file or one per heading?** Proposal: default `--split file`;
  `--split sections` opt-in. Section-splitting is lossy for prose that isn't
  cleanly headed, so make it explicit.
- **Preserve original target-specific metadata?** Cursor's `globs`/`alwaysApply` have
  no home in the canonical schema today. Proposal: drop them in alpha and note it in
  the report; revisit per-adapter options later (see `NEXT_STEPS.md`).
- **Create disabled targets by default?** Proposal for `import skill`: enable the
  target the source came from plus the `init` default set; for `import agent`: enable
  the matching target by default. Make `--target` authoritative when present.
- **How to handle existing `agent-sync` markers / comments?** Proposal: skip generated
  sections by default; `--include-generated` to opt in. Never import a marker block as
  hand-authored prose.
- **How aggressive should inference be?** Proposal: conservative — infer id/name/
  description only from clear signals (frontmatter, filename, first heading/paragraph);
  when ambiguous, leave `description` empty and let validation flag it rather than
  guessing.

## Tests required

- `SourceDetector`: every supported path shape maps to the right `ImportSource`;
  unsupported shapes are rejected.
- `MarkdownFrontmatter` / `HeadingSplitter`: round-trip against the exact output of
  `SkillFolderAdapter`, `CursorAdapter`, and `SharedMarkdownAdapter` (golden files).
- `IdInference`: slugification produces valid kebab-case; invalid inputs are rejected,
  not mangled.
- `SkillImporter`: file + folder sources; `--id`/`--name`/`--target` overrides;
  conflict-without-`--force` skips; `--force` overwrites; `--dry-run` writes nothing;
  imported skill passes `SkillValidator`.
- `AgentImporter`: each `--type`; `--split file` vs `sections`; marker-awareness
  (generated sections skipped); `.cursor/rules/` and skill-folder directories produce
  multiple skills; originals are untouched.
- CLI: exit codes per the table above; `--json` output is deterministic.
- Round-trip property: `import skill` of a `sync`-generated skill folder, then `sync`,
  reproduces equivalent projections (no drift) — the strongest correctness check.
