<#
.SYNOPSIS
    Install Agent Sync (agent + git-agent) from GitHub Releases on Windows.

.DESCRIPTION
    Downloads the win-x64 release archive, extracts agent.exe and git-agent.exe,
    and installs them into a user-writable directory.

.PARAMETER Version
    The release tag to install (e.g. v0.1.0). Defaults to the latest release.

.EXAMPLE
    .\install.ps1

.EXAMPLE
    .\install.ps1 -Version v0.1.0

.EXAMPLE
    irm https://raw.githubusercontent.com/nova-globen/agent/master/scripts/install.ps1 | iex

.NOTES
    Override the install directory:
        $env:AGENT_SYNC_INSTALL_DIR = "C:\Tools\agent-sync"
        .\install.ps1
#>
[CmdletBinding()]
param(
    [string]$Version = "latest"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repo = "nova-globen/agent"

$installDir = $env:AGENT_SYNC_INSTALL_DIR
if ([string]::IsNullOrWhiteSpace($installDir)) {
    $installDir = Join-Path $env:USERPROFILE ".agent-sync\bin"
}

# Resolve "latest" to a concrete tag.
if ($Version -eq "latest") {
    Write-Host "Resolving latest release..."
    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{ "User-Agent" = "agent-sync-installer" }
        $tag = $release.tag_name
    }
    catch {
        throw "Could not determine the latest release tag: $($_.Exception.Message)"
    }
}
else {
    $tag = $Version
}

if ([string]::IsNullOrWhiteSpace($tag)) {
    throw "Could not resolve a release tag."
}

$rid = "win-x64"
$archive = "agent-sync-$tag-$rid.zip"
$url = "https://github.com/$repo/releases/download/$tag/$archive"

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("agent-sync-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

try {
    $zipPath = Join-Path $tmp $archive
    Write-Host "Downloading $archive ..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -Headers @{ "User-Agent" = "agent-sync-installer" }
    }
    catch {
        throw "Could not download $url. Check that release $tag has a $rid asset. ($($_.Exception.Message))"
    }

    Write-Host "Extracting ..."
    Expand-Archive -Path $zipPath -DestinationPath $tmp -Force

    $agentExe = Join-Path $tmp "agent.exe"
    $gitAgentExe = Join-Path $tmp "git-agent.exe"
    if (-not (Test-Path $agentExe)) { throw "Archive did not contain agent.exe." }
    if (-not (Test-Path $gitAgentExe)) { throw "Archive did not contain git-agent.exe." }

    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item -Path $agentExe -Destination (Join-Path $installDir "agent.exe") -Force
    Copy-Item -Path $gitAgentExe -Destination (Join-Path $installDir "git-agent.exe") -Force

    Write-Host ""
    Write-Host "Installed Agent Sync $tag to $installDir :"
    Write-Host "  $installDir\agent.exe"
    Write-Host "  $installDir\git-agent.exe"

    $onPath = ($env:PATH -split ';') -contains $installDir
    if (-not $onPath) {
        Write-Host ""
        Write-Host "Add it to your PATH for the current session:"
        Write-Host "  `$env:PATH = `"$installDir;`$env:PATH`""
        Write-Host ""
        Write-Host "Or persist it for your user account:"
        Write-Host "  [Environment]::SetEnvironmentVariable('PATH', `"$installDir;`" + [Environment]::GetEnvironmentVariable('PATH','User'), 'User')"
    }

    Write-Host ""
    Write-Host "Then verify with: agent --version  ;  git agent --version"
}
finally {
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
