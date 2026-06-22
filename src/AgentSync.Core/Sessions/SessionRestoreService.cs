using System.IO.Compression;
using System.Text;

namespace AgentSync.Core.Sessions;

/// <summary>What happened to a single file during restore.</summary>
public enum RestoreAction
{
    Written,
    Overwritten,
    SkippedExists,
    SkippedUnsafe,
    SkippedNoPlacement,
}

/// <summary>One file's restore outcome.</summary>
public sealed record SessionRestoreItem(string ArchivePath, string DestPath, RestoreAction Action, bool Rewritten);

/// <summary>The outcome of a restore operation.</summary>
public sealed record SessionRestoreReport(
    string Provider,
    string DestProjectPath,
    SessionSource Source,
    bool DryRun,
    bool PathsTranslated,
    IReadOnlyList<SessionRestoreItem> Items)
{
    public int Written => Items.Count(i => i.Action is RestoreAction.Written or RestoreAction.Overwritten);
    public int Skipped => Items.Count(i => i.Action is RestoreAction.SkippedExists or RestoreAction.SkippedUnsafe or RestoreAction.SkippedNoPlacement);
    public bool AnyBlocked => Items.Any(i => i.Action == RestoreAction.SkippedExists);
}

/// <summary>
/// Restores a session backup archive into the current environment. It reads the manifest,
/// retargets each file's location for the destination project path / OS via the provider, and
/// rewrites absolute paths embedded inside text session files so sessions resume cleanly even
/// when the project moved between WSL, Windows, and Linux. Writes are confined to the
/// provider's own directory under the user's home (defends against zip-slip).
/// </summary>
public sealed class SessionRestoreService
{
    private readonly SessionProviderRegistry _registry;

    public SessionRestoreService(SessionProviderRegistry? registry = null)
        => _registry = registry ?? SessionProviderRegistry.Default;

    public SessionRestoreReport Run(
        string archivePath,
        SessionEnvironment destEnv,
        string destProjectPath,
        bool force,
        bool dryRun,
        string? providerOverride = null)
    {
        if (!File.Exists(archivePath))
        {
            throw new SessionException($"Archive '{archivePath}' does not exist.");
        }

        using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry(SessionManifest.FileName)
            ?? throw new SessionException("Archive is not an Agent Sync session backup (no manifest.json).");

        SessionManifest manifest;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            manifest = SessionManifest.Parse(reader.ReadToEnd());
        }

        var providerId = providerOverride ?? manifest.Provider;
        var provider = _registry.Resolve(providerId)
            ?? throw new SessionException($"Unknown session provider '{providerId}'.");

        var rewriter = PathRewriter.Build(
            manifest.Source.ProjectPath,
            destProjectPath,
            manifest.Source.HomeDirectory,
            destEnv.HomeDirectory);

        var providerRoot = Path.GetFullPath(provider.Root(destEnv));
        var items = new List<SessionRestoreItem>();

        foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            if (!entry.FullName.StartsWith(SessionManifest.FilesPrefix, StringComparison.Ordinal)
                || entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var archiveRel = entry.FullName[SessionManifest.FilesPrefix.Length..];
            if (!RepoPath.IsSafeRelative(archiveRel))
            {
                items.Add(new SessionRestoreItem(archiveRel, string.Empty, RestoreAction.SkippedUnsafe, false));
                continue;
            }

            var placement = provider.Place(manifest, destEnv, destProjectPath, archiveRel);
            if (placement is null)
            {
                items.Add(new SessionRestoreItem(archiveRel, string.Empty, RestoreAction.SkippedNoPlacement, false));
                continue;
            }

            var destFull = Path.GetFullPath(placement.AbsoluteDestPath);
            if (!IsUnder(providerRoot, destFull))
            {
                items.Add(new SessionRestoreItem(archiveRel, destFull, RestoreAction.SkippedUnsafe, false));
                continue;
            }

            byte[] bytes;
            using (var es = entry.Open())
            using (var ms = new MemoryStream())
            {
                es.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var rewritten = false;
            if (placement.RewriteText && !rewriter.IsIdentity && TryDecodeUtf8(bytes, out var text))
            {
                var jsonEscaped = archiveRel.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || archiveRel.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
                var updated = rewriter.Apply(text!, jsonEscaped);
                if (!string.Equals(updated, text, StringComparison.Ordinal))
                {
                    bytes = new UTF8Encoding(false).GetBytes(updated);
                    rewritten = true;
                }
            }

            var exists = File.Exists(destFull);
            if (exists && !force)
            {
                items.Add(new SessionRestoreItem(archiveRel, destFull, RestoreAction.SkippedExists, rewritten));
                continue;
            }

            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFull)!);
                File.WriteAllBytes(destFull, bytes);
            }

            items.Add(new SessionRestoreItem(
                archiveRel,
                destFull,
                exists ? RestoreAction.Overwritten : RestoreAction.Written,
                rewritten));
        }

        return new SessionRestoreReport(
            provider.Id,
            destProjectPath,
            manifest.Source,
            dryRun,
            !rewriter.IsIdentity,
            items);
    }

    /// <summary>Reads a manifest from an archive without restoring (for inspection/listing).</summary>
    public static SessionManifest ReadManifest(string archivePath)
    {
        using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(SessionManifest.FileName)
            ?? throw new SessionException("Archive is not an Agent Sync session backup (no manifest.json).");
        using var reader = new StreamReader(entry.Open());
        return SessionManifest.Parse(reader.ReadToEnd());
    }

    private static bool IsUnder(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootSep = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return string.Equals(candidate, root, comparison) || candidate.StartsWith(rootSep, comparison);
    }

    private static bool TryDecodeUtf8(byte[] bytes, out string? text)
    {
        text = null;
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return false; // Contains NUL: treat as binary.
        }

        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
