# Project Brief

<!-- Fill in the sections below when adopting this kit for your project. -->

## What the product is

<!-- One paragraph: what this repository builds, who uses it, what problem it solves. -->

**Example (replace this):** Platform.Hello is a trivial .NET 10 class library used as the worked example
for the AI-agent platform kit. It demonstrates the autopilot loop: implement → verify → commit → handoff.

## Tech stack

- **Runtime:** .NET 10 (`net10.0`)
- **Solution:** `Platform.slnx`
- **Test framework:** xUnit 2.9.x
- **AgentSync:** v0.3.2 (manages agent skills/projections)

<!-- Add your stack: web framework, database, messaging, auth, deployment targets, etc. -->

## Deployment

<!-- Where the product runs: SaaS, on-prem, both, cloud provider, containers, etc. -->

## Key constraints

<!-- License policy, compliance requirements, performance targets, API compatibility requirements, etc. -->
- OSS dependencies must be permissive-license compliant (see `docs/governance/oss-license-policy.md`).
- Commits must follow Conventional Commits; no AI/tool tokens in messages.
- Never push from an autopilot session — leave the remote for the developer to review.
