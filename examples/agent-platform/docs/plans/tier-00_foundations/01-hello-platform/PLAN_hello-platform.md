# Plan: Hello Platform

**Feature:** 01 — Hello Platform
**Tier:** 00 — Foundations
**Scope decision:** Minimal worked example to prove the autopilot loop. Two files. One commit.
**Date:** 2026-06-26

## Goal

Implement the `Greeter` class and its unit test to demonstrate that the autopilot implement → verify →
commit → handoff cycle works end-to-end in a fresh repository.

## Approach

The implementation is intentionally trivial: a static-pattern class library with one method and one xUnit
test. No dependency injection, no configuration. Build and test via the standard `.NET` gate.

## Ordered Increments

1. **Implement Greeter class**
   - Create `src/Platform.Hello/Greeter.cs`.
   - `public sealed class Greeter { public static string Greet(string name) => $"Hello, {name}!"; }`
   - Verify: `dotnet build Platform.slnx -v q -clp:ErrorsOnly -nologo` passes.

2. **Add unit test**
   - Create `tests/Platform.Hello.Tests/GreeterTests.cs`.
   - One `[Fact]`: `Greeter.Greet("World")` equals `"Hello, World!"`.
   - Verify: `dotnet test tests/Platform.Hello.Tests --no-build -v q --nologo` passes.

Both increments are committed together: `feat(hello-platform): add Greeter class and unit test`.

## Out of Scope

- Internationalization, DI, logging, configuration.
- Any HTTP, persistence, or UI layer.
- Additional test cases (one fact is sufficient to prove the loop).

## Definition of Done

- Build passes (`dotnet build Platform.slnx -v q -clp:ErrorsOnly -nologo`).
- Test passes (`dotnet test tests/Platform.Hello.Tests -v q --nologo`).
- Commit passes all hooks (commit-msg, pre-commit).
- `PROGRESS_hello-platform.md` marked complete.
- `docs/plans/backlog.md` updated (hello-platform item removed).
- `.agent/memory/active-context.md` reflects done.
- Handoff prompt written.
