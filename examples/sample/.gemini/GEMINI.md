<!-- agent-sync:start id=code-review target=gemini hash=sha256:56cb5e1f447edefa38a1db62733a32ae1b62e480eee74f65b4b99c71ef50cd71 -->
## Code Review

Reviews changes using repository conventions and flags risky edits.

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
<!-- agent-sync:end -->
