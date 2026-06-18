# Release Checklist

Releases are driven by Git tags. Pushing a tag matching `v*.*.*` triggers
`.github/workflows/release.yml`, which builds, tests, publishes self-contained
`agent` and `git-agent` binaries for every supported runtime, generates
`checksums.txt`, and creates the GitHub Release. `workflow_dispatch` runs the same
pipeline for testing without creating a release (artifacts are uploaded to the run).

## Supported runtimes / assets

```text
agent-sync-<tag>-linux-x64.tar.gz
agent-sync-<tag>-linux-arm64.tar.gz
agent-sync-<tag>-osx-x64.tar.gz
agent-sync-<tag>-osx-arm64.tar.gz
agent-sync-<tag>-win-x64.zip
checksums.txt
```

Each archive contains `agent`, `git-agent`, `LICENSE`, and `README.md`
(`agent.exe` / `git-agent.exe` on Windows).

## Process

1. Ensure `main`/`master` is green (build + test CI passing).
2. Update the version if needed in `Directory.Build.props` (`<Version>`).
3. Create and push the tag:

   ```bash
   git tag v0.1.0
   git push origin v0.1.0
   ```

4. Wait for the **Release** workflow to finish.
5. Verify the release exists with all expected assets:

   ```bash
   gh release view v0.1.0
   ```

6. Verify checksums:

   ```bash
   curl -fsSLO https://github.com/nova-globen/agent/releases/download/v0.1.0/checksums.txt
   # download the archives, then:
   sha256sum -c checksums.txt
   ```

7. Test the install script on at least one Linux/macOS machine and one Windows machine:

   - Linux/macOS: `curl -fsSL .../install.sh | bash -s -- v0.1.0`
   - Windows: `irm .../install.ps1 | iex`

8. Verify the installed binaries:

   ```bash
   agent --version
   git agent --version
   agent doctor
   ```

## Pre-release sanity (optional, local)

- `dotnet restore && dotnet build --configuration Release && dotnet test` is green.
- End-to-end in a fresh repo: `init` → `install-hooks` → `sync` →
  `status --fail-on-drift --ci` exits `0`.
- `examples/sample` reports no drift (`agent status` is clean).
- Local publish smoke test for the current runtime:

  ```bash
  dotnet publish src/AgentSync.Cli -c Release -r linux-x64 --self-contained true
  dotnet publish src/AgentSync.GitAgent -c Release -r linux-x64 --self-contained true
  ```

## Post-release

- Verify the published artifacts install and run (`agent --version`).
- Open a tracking issue for the next milestone.
