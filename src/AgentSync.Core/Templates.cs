namespace AgentSync.Core;

/// <summary>
/// Canonical file contents written by <c>agent init</c>. Kept as constants so the
/// CLI has no runtime dependency on the repository's <c>templates/</c> directory.
/// </summary>
public static class Templates
{
    public const string AgentYaml =
"""
version: 1

targets:
  agents_md:
    enabled: true
    path: AGENTS.md

  claude_md:
    enabled: true
    path: CLAUDE.md

  cursor:
    enabled: true
    path: .cursor/rules

  copilot:
    enabled: true
    path: .github/copilot-instructions.md

  gemini:
    enabled: true
    path: .gemini/GEMINI.md

  openai_skill:
    enabled: true
    path: .chatgpt/skills

  claude_skill:
    enabled: true
    path: .claude/skills

policy:
  fail_on_missing_projection: true
  fail_on_outdated_projection: true
  fail_on_manual_edit: true
  allow_target_specific_overrides: true
""";

    /// <summary>Directory name (and id) of the skill scaffolded by <c>agent init</c>.</summary>
    public const string DefaultSkillId = "code-review";

    public const string DefaultSkillYaml =
"""
id: code-review
name: Code Review
description: Reviews changes using repository conventions and flags risky edits.
version: 0.1.0

targets:
  agents_md:
    enabled: true
  claude_md:
    enabled: true
  cursor:
    enabled: true
  copilot:
    enabled: true
  gemini:
    enabled: true
  openai_skill:
    enabled: true
  claude_skill:
    enabled: true
""";

    // The canonical SKILL.md holds the instruction body only. Display metadata
    // (name, description, version) lives in skill.yaml, and adapters add a single
    // target-appropriate heading, so this file must not start with a "# Name" heading.
    public const string DefaultSkillMarkdown =
"""
## When to use

Use this skill when reviewing pull requests, generated patches, or local changes.

## Instructions

- Check whether the change follows repository conventions.
- Look for correctness, security, maintainability, and test coverage risks.
- Identify generated files that should not be edited by hand.
- Prefer actionable comments over broad criticism.

## Output

Return a concise review with:
- summary
- risks
- required fixes
- optional improvements
""";

    public const string LockJson =
"""
{
  "version": 1,
  "projections": {}
}
""";

    public const string PreCommitHook =
"""
#!/usr/bin/env bash
set -euo pipefail

if ! command -v agent >/dev/null 2>&1; then
  echo "Agent Sync is required for this repository."
  echo "Install it, then retry."
  echo ""
  echo "Expected command:"
  echo "  agent status --fail-on-drift"
  exit 3
fi

agent status --fail-on-drift
""";

    public const string PrePushHook =
"""
#!/usr/bin/env bash
set -euo pipefail

if ! command -v agent >/dev/null 2>&1; then
  echo "Agent Sync is required for this repository."
  echo "Install it, then retry."
  echo ""
  echo "Expected command:"
  echo "  agent status --fail-on-drift --ci"
  exit 3
fi

agent status --fail-on-drift --ci
""";
}
