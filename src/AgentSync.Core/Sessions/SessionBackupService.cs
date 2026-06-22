using System.IO.Compression;
using System.Security.Cryptography;

namespace AgentSync.Core.Sessions;

/// <summary>The outcome of a backup: where it was written and what it contains.</summary>
public sealed record SessionBackupReport(
    string Provider,
    string ProjectPath,
    string? OutputPath,
    int FileCount,
    long TotalBytes,
    bool Experimental,
    IReadOnlyList<string> Files)
{
    public bool IsEmpty => FileCount == 0;
}

/// <summary>
/// Collects an agent's session files for a project and writes them, with a restore manifest,
/// into a single zip archive. When the project has no sessions, nothing is written and an
/// empty report is returned.
/// </summary>
public sealed class SessionBackupService
{
    /// <summary>Builds a default archive file name for a provider.</summary>
    public static string DefaultOutputName(string provider, DateTimeOffset now)
        => $"agent-sessions-{provider}-{now:yyyyMMdd-HHmmss}.zip";

    public SessionBackupReport Run(
        ISessionProvider provider,
        SessionEnvironment env,
        string projectPath,
        string outputPath,
        string agentSyncVersion,
        DateTimeOffset now)
    {
        var collection = provider.Collect(env, projectPath);
        if (collection.Entries.Count == 0)
        {
            return new SessionBackupReport(provider.Id, projectPath, null, 0, 0, provider.Experimental, Array.Empty<string>());
        }

        var manifestEntries = new List<SessionFileEntry>();
        var files = new List<string>();
        long total = 0;

        // Stage the manifest first so we can compute hashes, then write the archive.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var entry in collection.Entries.OrderBy(e => e.ArchivePath, StringComparer.Ordinal))
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(entry.AbsolutePath);
                }
                catch
                {
                    continue; // Skip files that vanished or are locked.
                }

                var zipEntry = archive.CreateEntry(SessionManifest.FilesPrefix + entry.ArchivePath, CompressionLevel.Optimal);
                using (var es = zipEntry.Open())
                {
                    es.Write(bytes, 0, bytes.Length);
                }

                manifestEntries.Add(new SessionFileEntry(entry.ArchivePath, bytes.LongLength, Sha256(bytes)));
                files.Add(entry.ArchivePath);
                total += bytes.LongLength;
            }

            var manifest = new SessionManifest(
                SessionManifest.CurrentSchemaVersion,
                agentSyncVersion,
                provider.Id,
                now.ToUniversalTime().ToString("o"),
                new SessionSource(
                    SessionManifest.PlatformString(env.Platform),
                    env.PathStyle.ToString().ToLowerInvariant(),
                    env.HomeDirectory,
                    projectPath,
                    collection.StoreKey),
                manifestEntries);

            var manifestZip = archive.CreateEntry(SessionManifest.FileName, CompressionLevel.Optimal);
            using var ms = new StreamWriter(manifestZip.Open());
            ms.Write(manifest.ToJson());
        }

        return new SessionBackupReport(provider.Id, projectPath, outputPath, files.Count, total, provider.Experimental, files);
    }

    private static string Sha256(byte[] bytes)
        => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
