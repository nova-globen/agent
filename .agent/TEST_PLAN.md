# Test Plan

## Unit Tests

Test config parsing, skill parsing, hashing, marker parsing, generated section replacement, drift detection, and adapter output.

## Integration Tests

Create temporary Git repositories and test `agent init`, `agent status`, `agent sync`, and `agent install-hooks`.

## CLI Tests

Verify exit codes, JSON output, human-readable output, missing config behavior, missing skill behavior, and manual edit detection.

## Golden Files

Use golden snapshot tests for adapter outputs.
