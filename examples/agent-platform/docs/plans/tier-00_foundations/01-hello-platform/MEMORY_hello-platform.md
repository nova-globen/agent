# Memory: Hello Platform

<!-- Quick durable cross-reference for future agents picking up this plan. -->

## What this plan is

The "Hello Platform" worked example for the AI-agent platform kit. Proves the autopilot loop in a
minimal, reproducible way. Once complete, the feature is done forever — replace it with your first real
feature when adopting the kit.

## Key files

- Spec: `docs/features/01.hello-platform.md`
- Source: `src/Platform.Hello/Greeter.cs` (to be created by autopilot)
- Test: `tests/Platform.Hello.Tests/GreeterTests.cs` (to be created by autopilot)
- Plan: `docs/plans/tier-00_foundations/01-hello-platform/PLAN_hello-platform.md`

## Orientation for the next agent

The `Greeter` class is intentionally static and dependency-free. The test is a single xUnit `[Fact]`.
No configuration, no DI, no persistence. The goal is to prove the loop, not to build production software.

## Related plans

*(none — this is the only plan in the worked example)*
