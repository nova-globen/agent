# Adapter Specification

## Canonical Skill

Each skill has `.agent/skills/<skill-id>/skill.yaml` and `.agent/skills/<skill-id>/SKILL.md`.

Example:

```yaml
id: code-review
name: Code Review
description: Reviews pull requests using project conventions.
version: 0.1.0
targets:
  agents_md:
    enabled: true
  claude_md:
    enabled: true
  cursor:
    enabled: true
```

Adapters must preserve semantic intent, generate deterministic content, include generated markers where writing into shared files, and avoid overwriting user-authored content.

Initial target formats: AGENTS.md, CLAUDE.md, `.cursor/rules/<skill-id>.mdc`, `.github/copilot-instructions.md`, `.gemini/GEMINI.md`, `.chatgpt/skills/<skill-id>/SKILL.md`, `.claude/skills/<skill-id>/SKILL.md`.
