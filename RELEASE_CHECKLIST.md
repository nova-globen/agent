# Release Checklist

Use this checklist when cutting a public release of Agent Sync.

## Pre-release

- [ ] `dotnet restore` succeeds from a clean checkout.
- [ ] `dotnet build --configuration Release` produces `agent` and `git-agent` with
      zero warnings.
- [ ] `dotnet test` is fully green (Core + CLI).
- [ ] `agent --version` reports the intended version (see `Directory.Build.props`).
- [ ] End-to-end smoke test in a fresh repo: `init` → `install-hooks` → `sync` →
      `status --fail-on-drift --ci` exits `0`.
- [ ] `examples/sample` is regenerated and in sync (`agent status` reports no drift).

## Documentation and governance

- [ ] `README.md` reflects the current command set and behavior.
- [ ] `LICENSE` contains the full AGPL-3.0-or-later text.
- [ ] `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, and `SECURITY.md` are present and
      current.
- [ ] `CHANGELOG`/release notes summarize user-visible changes.

## Versioning

- [ ] Bump `<Version>` in `Directory.Build.props` following semantic versioning.
- [ ] Tag the release commit: `git tag vX.Y.Z` and push the tag.

## Publish

- [ ] CI is green on the release commit.
- [ ] Build release artifacts for the supported platforms.
- [ ] Create the GitHub release with notes; attach artifacts.

## Post-release

- [ ] Verify the published artifacts install and run (`agent --version`).
- [ ] Open a tracking issue for the next milestone.
