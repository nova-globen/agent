# Architecture Principles

These principles govern how code is structured and extended in this repository. When adopting this kit
for your project, replace the placeholder rules below with your project's actual architecture decisions.
For significant decisions, record them as ADRs in `docs/adr/`.

## Core Rules

1. **Domain model purity**
   Domain projects must not depend on infrastructure or UI frameworks. Keep business rules free of
   framework specifics.

2. **Modular boundaries**
   Each logical module/component lives in its own folder and project(s). Avoid direct data access or
   type references across module boundaries; use contracts, interfaces, or integration events.

3. **Dependency direction**
   Domain ← Application ← Infrastructure ← Host/UI. Never let a lower layer depend on a higher one.

4. **Contracts first**
   Cross-module interactions use stable contracts (interfaces, DTOs, events) — not internal types.
   Synchronous cross-module reads use ports owned by the consumed side; async notifications use events.

5. **Dependency and package governance**
   New dependencies must be permissive-license compliant (see `docs/governance/oss-license-policy.md`).
   Central Package Management (`Directory.Packages.props`) governs all versions.

6. **Test strategy**
   - Unit tests for domain logic.
   - Integration tests for persistence, endpoints, and infra-dependent behavior.
   - Architecture tests for dependency-rule enforcement (validate `ProjectReference` edges).
   - Every increment must be individually verifiable — name the test project that proves it.

7. **No silent scope drops**
   Anything not done in an increment moves to **Follow-ups (deferred)** in `PROGRESS_*.md` with a
   one-line reason, and to `docs/plans/backlog.md` with a `src:` tag. Never silently defer.

8. **Deterministic naming**
   Follow the project's naming conventions. When in doubt, check the nearest existing code and mirror
   it. Document any non-obvious naming decision in `DECISIONS_*.md` or `decision-log.md`.

<!-- Add your project-specific principles here. For each significant architectural decision,
     consider creating an ADR in docs/adr/ rather than just listing a bullet here. -->
