# OSS License Policy

This policy governs which open-source licenses are acceptable for dependencies in this repository.

## Allowed Licenses (no exception required)

- MIT
- Apache-2.0
- BSD-2-Clause / BSD-3-Clause
- ISC
- PostgreSQL
- MS-PL

## Allowed via Exception (document rationale + ADR)

- **Weak copyleft** (LGPL-2.1, LGPL-3.0, MPL-2.0): allowed when used as an unmodified, dynamically
  linked library and not redistributed as part of a combined work. Document in an ADR.
- **AGPL-3.0 tooling only**: acceptable for build-time / development tooling that is not shipped
  with the product (e.g. AgentSync). Document in `decision-log.md` or an ADR.

## Not Allowed (without legal review)

- GPL-2.0 / GPL-3.0 (strong copyleft) — not without legal review and an explicit ADR.
- SSPL / BUSL — commercial restrictions; not without legal review.
- Unlicensed / proprietary — not without explicit legal approval.

## Validation Gate

`scripts/check-nuget-licenses.sh` audits all `PackageReference` entries against this policy. It runs in
the pre-commit gate when `Directory.Packages.props` or any `.csproj` adds a new `PackageReference`.

To add a new dependency:
1. Check the license via `scripts/check-nuget-licenses.sh` or by inspecting nuget.org.
2. If it is in the Allowed list, proceed.
3. If it requires an exception, record the decision in an ADR before adding it.
4. If it is Not Allowed, surface it as a blocker — do not add it.
