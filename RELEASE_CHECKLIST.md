# Release Checklist

Releases are driven by Git tags. Pushing a tag matching `v*.*.*` triggers
`.github/workflows/release.yml`, which builds, tests, publishes self-contained
`agent` and `git-agent` binaries for every supported runtime, generates
`checksums.txt`, creates the GitHub Release, and pushes the `Agent.Sync` /
`Agent.Sync.Git` .NET tool packages to NuGet.org. `workflow_dispatch` runs the same
pipeline for testing without creating a release or publishing to NuGet (artifacts,
including the `.nupkg` files, are uploaded to the run).

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

## NuGet (.NET tool) packages

The release also publishes two .NET tool packages to NuGet.org:

```text
Agent.Sync       -> command `agent`
Agent.Sync.Git   -> command `git-agent`
```

Publishing uses **NuGet Trusted Publishing** (GitHub OIDC), so no long-lived API key
is stored. One-time setup, required before the first NuGet publish:

1. On nuget.org, sign in as the account/org that will own the packages, then
   **Account → Trusted Publishing → Add** a policy with:
   - Repository owner: `nova-globen`
   - Repository: `agent`
   - Workflow file: `release.yml` (file name only, no path)
   - Environment: leave empty (the workflow does not use one)
2. Add a repository (or org) Actions secret **`NUGET_USER`** = the nuget.org profile
   name (username, **not** an email). The workflow passes it to `NuGet/login@v1`.
3. First publish: the policy may be "pending" for private repos until the first
   successful push; for the public `nova-globen/agent` it activates on first use.

If a package id does not yet exist on nuget.org, the first push creates it (the
publishing account must be allowed to register that id).

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

9. Verify the NuGet tool packages published and install cleanly (allow a few minutes
   for indexing):

   ```bash
   # NuGet versions have no leading 'v' (tag v0.1.0 -> package version 0.1.0).
   dotnet tool install --global Agent.Sync --version 0.1.0
   dotnet tool install --global Agent.Sync.Git --version 0.1.0
   agent --version
   git agent --version
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
