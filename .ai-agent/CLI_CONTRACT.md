# CLI Contract

## Binary Names

The project must produce two command entry points:

```bash
agent
git-agent
```

`git-agent` may delegate to `agent`, enabling `agent status` and `git agent status`.

## Exit Codes

```text
0 = success
1 = drift detected or validation failed
2 = invalid usage
3 = tool/environment problem
4 = unexpected error
```

## Commands

### agent init

Creates `.agent/agent.yaml`, `.agent/skills/`, `.agent/lock.json`, `.githooks/pre-commit`, and `.githooks/pre-push`. Must not overwrite existing files unless `--force` is provided.

### agent status

Reports projection state. Options: `--fail-on-drift`, `--json`, `--ci`.

### agent sync

Writes missing or outdated projections. Options: `--check`, `--write`, `--force`. Default should be safe.

### agent diff

Shows canonical-to-projection differences.

### agent validate

Validates config and skills.

### agent doctor

Checks Git repo, PATH, hooks, config, and projections.

### agent install-hooks

Runs `git config core.hooksPath .githooks` and verifies hooks are executable on Unix-like systems.
