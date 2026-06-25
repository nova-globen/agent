# Release Checklist

Releases are driven by Git tags. Pushing a tag matching `v*.*.*` triggers
`.github/workflows/release.yml`, which builds, tests, publishes self-contained
`agent` and `git-agent` binaries for every supported runtime, generates
`checksums.txt`, creates the GitHub Release, and pushes the `AgentSync` /
`AgentSync.Git` .NET tool packages to NuGet.org. `workflow_dispatch` runs the same
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

The optional local web UI ships as **separate** assets on the same release (built by the
`release-ui` job; see "GUI packaging" below), and its checksums are merged into the same
`checksums.txt`:

```text
agent-sync-ui-<tag>-linux-x64.tar.gz
agent-sync-ui-<tag>-linux-arm64.tar.gz
agent-sync-ui-<tag>-osx-x64.tar.gz
agent-sync-ui-<tag>-osx-arm64.tar.gz
agent-sync-ui-<tag>-win-x64.zip
```

## NuGet (.NET tool) packages

The release publishes three .NET tool packages to NuGet.org:

```text
AgentSync       -> command `agent`         (CLI; `release` job)
AgentSync.Git   -> command `git-agent`     (CLI; `release` job)
AgentSync.Ui    -> command `agent-sync-ui` (optional UI; separate `release-ui` job)
```

`AgentSync.Ui` is the optional local web UI, packed and pushed by the **separate
`release-ui` job** (not the CLI `release` job), so the CLI tool packages stay UI-free and
a UI failure never blocks the CLI release. `agent ui` installs it on first use
(`dotnet tool install --global AgentSync.Ui` when a `dotnet` SDK is present, otherwise the
release archive). The Trusted Publishing policy below must also permit the `AgentSync.Ui`
id; the simplest setup is a policy that does not restrict the package id (covers all three).

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

## GUI packaging (separate and optional)

The local web UI (`AgentSync.Ui.Web`, executable `agent-sync-ui`) is a **separate,
optional product** and is **not** part of the CLI release described above:

- The CLI release (binaries above) and the `AgentSync` / `AgentSync.Git` `dotnet tool`
  packages stay **UI-free** — they publish without any web-UI dependency. `AgentSync.Cli`
  references neither `AgentSync.Ui.Web` nor FluentUI.
- The UI also ships as the **`AgentSync.Ui` .NET tool** (command `agent-sync-ui`), packed
  and pushed by the `release-ui` job. The tool package includes its static web assets
  (`wwwroot` + the endpoints manifest), so the installed tool serves CSS/JS correctly.
- The UI ships as separate, self-contained, single-file downloads per runtime, with the
  same RID set and tag as the CLI, but **distinct** artifact names:

  ```text
  agent-sync-ui-<tag>-linux-x64.tar.gz
  agent-sync-ui-<tag>-linux-arm64.tar.gz
  agent-sync-ui-<tag>-osx-x64.tar.gz
  agent-sync-ui-<tag>-osx-arm64.tar.gz
  agent-sync-ui-<tag>-win-x64.zip
  ```

  Each archive contains the `agent-sync-ui` executable **plus its static web assets**
  (`wwwroot/` and the `*.staticwebassets.endpoints.json` manifest), `LICENSE`, and
  `README.md`. Keep the folder together; the executable needs those assets at runtime.

- This is wired in `release.yml` as a **separate `release-ui` job** that `needs: release`.
  Because it runs only after the CLI release job succeeds, a UI build failure shows up as a
  failed (visible, never silent) optional job but **cannot block or alter** the
  already-published CLI release / NuGet packages. The job appends the UI checksums into the
  release's `checksums.txt` without disturbing the CLI entries.

- `agent ui` discovers an installed `agent-sync-ui` on `PATH` (or via `AGENT_SYNC_UI`),
  picks a free port, generates a per-launch session token, polls `/healthz`, and launches
  it bound to `127.0.0.1`; it does not bundle the UI. When the UI is absent it auto-installs
  it (the `AgentSync.Ui` .NET tool, or the release archive into `~/.agent-sync/ui/`). Install
  docs keep the CLI install and the UI install clearly separate.

### Verifying a release

- [ ] CLI artifacts present: `agent-sync-<tag>-<rid>.{tar.gz,zip}` for every RID.
- [ ] UI artifacts present: `agent-sync-ui-<tag>-<rid>.{tar.gz,zip}` for every RID.
- [ ] `checksums.txt` lists **both** the CLI and UI artifacts; `sha256sum -c checksums.txt`
      passes after downloading them all.
- [ ] The `AgentSync` / `AgentSync.Git` `dotnet tool` packages contain **no** UI assemblies.
- [ ] The `AgentSync.Ui` tool installs (`dotnet tool install --global AgentSync.Ui
      --version <version>`) and, once run, serves `/_framework/blazor.web.js` and the
      FluentUI `_content/...` assets with `200` (not `404`).
- [ ] With `agent-sync-ui` on `PATH`, `agent ui` launches it; `agent ui --no-open` prints
      the loopback token URL.
- [ ] `GET /healthz` returns `200 ok`; `GET /` without a token returns `401`; a valid
      `?token=` request 302-redirects to the same path with the token stripped from the URL.
- [ ] No browser is required in CI (`scripts/release-smoke.sh` covers the UI checks
      headlessly).

## Process

1. Ensure `main`/`master` is green (build + test CI passing).
2. Update the version if needed in `Directory.Build.props` (`<Version>`).
3. Create and push the tag. Replace `<tag>` with the release tag (example:
   `v0.2.0`):

   ```bash
   git tag <tag>
   git push origin <tag>
   ```

4. Wait for the **Release** workflow (`release` **and** the optional `release-ui` job) to
   finish.
5. Verify the release exists with all expected assets — both the CLI archives and the
   separate `agent-sync-ui-<tag>-<rid>` archives, plus `checksums.txt` (see "Verifying a
   release" above):

   ```bash
   gh release view <tag>
   ```

6. Verify checksums (download every asset listed in `checksums.txt` — CLI **and** UI —
   first):

   ```bash
   curl -fsSLO https://github.com/nova-globen/agent/releases/download/<tag>/checksums.txt
   # download the archives, then:
   sha256sum -c checksums.txt
   ```

7. Test the install script on at least one Linux/macOS machine and one Windows machine:

   - Linux/macOS: `curl -fsSL .../install.sh | bash -s -- <tag>`
   - Windows: `irm .../install.ps1 | iex`

8. Verify the installed binaries:

   ```bash
   agent --version
   git agent --version
   agent doctor
   ```

9. Verify the NuGet tool packages published and install cleanly (allow a few minutes
   for indexing). NuGet versions have no leading `v`, so tag `<tag>` maps to package
   version `<version>` — for example, tag `v0.2.0` corresponds to package version
   `0.2.0`:

   ```bash
   dotnet tool install --global AgentSync --version <version>
   dotnet tool install --global AgentSync.Git --version <version>
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

## Re-running a release

The Release workflow is safe to re-run on the same tag (e.g. after fixing a secret):
the GitHub Release step creates the release if missing or refreshes notes/assets if it
already exists, and `dotnet nuget push` uses `--skip-duplicate` so already-published
package versions are skipped without failing. Note that a NuGet version, once
published, is permanent — re-runs cannot republish an already-published version with
different content; bump the version for any code change.

## Post-release

- Verify the published artifacts install and run (`agent --version`).
- Open a tracking issue for the next milestone.
