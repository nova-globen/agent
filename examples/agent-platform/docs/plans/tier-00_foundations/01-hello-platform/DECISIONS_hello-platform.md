# Decisions: Hello Platform

<!-- Durable decisions with rationale. One bullet per decision, bold lead phrase.
     For architectural decisions, create an ADR in docs/adr/ instead. -->

- **Use a static method for Greet.** Rationale: no state, no dependencies — simplest possible
  implementation that proves the build/test cycle without introducing DI or configuration concerns.
  Scope: worked example only.

- **Commit both src and test together.** Rationale: the test is the verification of the class; separating
  them would leave a passing build with no verification in between. Per plan-governor: plan-tracking files
  travel with the code.

- **Use [skip-progress-check] on the initial commit.** Rationale: the backlog item exists in backlog.md
  from kit setup, but the commit-msg gate requires backlog.md staged when src/ is staged. Since we do
  stage backlog.md (removing the hello-platform item), no override is actually needed — stage it normally.
