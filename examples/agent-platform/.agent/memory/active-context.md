# Active Context

<!-- Keep this thin. Current state only — no changelog. Read on every task. -->
<!-- When an effort completes, use memory-curator to rotate narrative into delivery-log.md. -->

## Current State

- Kit scaffolded; worked example (Platform.Hello / Greeter class) is in progress.

## In Flight

- **Autopilot:** pending first run. Starter prompt at `.agent/prompts/autopilot/prompt-20260626-1200_hello-platform-increment-01.txt`.
- **Next:** autopilot picks up the starter prompt and implements `Greeter.Greet(string name)` + unit test.

## Next Priorities

1. Run `dotnet agent autopilot claude` to complete the hello-platform worked example.
2. Verify the full cycle (implement → verify → commit → handoff prompt written).
3. Adapt this kit for your actual project: update `project-brief.md`, `architecture-principles.md`,
   replace the worked example feature, and run `dotnet agent sync`.

## Risks

- None known yet. After adopting, record project-specific risks here.
