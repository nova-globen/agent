---
name: Code Review
description: Reviews changes using repository conventions and flags risky edits. Use when reviewing a pull request, a generated patch, or local changes before committing.
---

## When to use

Use this skill when reviewing a pull request, a generated patch, or local changes before
you commit.

## Instructions

- Check whether the change follows the repository's conventions and existing patterns.
- Look for correctness, security, maintainability, and test-coverage risks.
- Confirm commit messages follow the project's commit convention.
- Identify generated files that should not be edited by hand.
- Prefer specific, actionable comments over broad criticism.

## Output

Return a concise review with:

- summary
- risks
- required fixes
- optional improvements
