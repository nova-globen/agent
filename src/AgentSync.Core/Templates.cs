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

    public const string ExampleSkillYaml =
"""
id: example-skill
name: Example Skill
description: Describe what this skill helps the agent do.
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

    public const string ExampleSkillMarkdown =
"""
# Example Skill

## When to use

Use this skill when the developer asks for this workflow.

## Instructions

Follow the repository conventions.

## Output

Return clear, actionable results.
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
