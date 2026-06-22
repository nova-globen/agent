using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AgentSync.Core.Sessions;
using AgentSync.Core.Sessions.Providers;

namespace AgentSync.Core.Tests.Sessions;

public sealed class SessionBackupRestoreTests
{
    private static SessionEnvironment Env(string home, SessionPlatform platform, PathStyle style)
        => new(home, platform, style);

    private static string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Claude_RoundTrip_WslToWindows_RelocatesAndRewrites()
    {
        using var src = new TempDir();
        using var dst = new TempDir();
        using var work = new TempDir();

        const string project1 = "/mnt/c/work/app";
        const string project2 = "C:\\work\\app2";
        var env1 = Env(src.Path, SessionPlatform.Linux, PathStyle.Wsl);
        var env2 = Env(dst.Path, SessionPlatform.Windows, PathStyle.Windows);

        var key1 = SessionProviderSupport.EncodePathKey(project1);
        Write(Path.Combine(src.Path, ".claude", "projects", key1, "s.jsonl"),
            "{\"type\":\"mode\"}\n{\"cwd\":\"/mnt/c/work/app\",\"p\":\"/mnt/c/work/app/src/a.cs\"}\n");

        var zip = Path.Combine(work.Path, "claude.zip");
        var provider = new ClaudeSessionProvider();
        var backup = new SessionBackupService().Run(provider, env1, project1, zip, "test", DateTimeOffset.UnixEpoch);

        Assert.Equal(1, backup.FileCount);
        Assert.False(backup.IsEmpty);

        var report = new SessionRestoreService().Run(zip, env2, project2, force: false, dryRun: false);

        Assert.Equal(1, report.Written);
        var key2 = SessionProviderSupport.EncodePathKey(project2);
        var restored = Path.Combine(dst.Path, ".claude", "projects", key2, "s.jsonl");
        Assert.True(File.Exists(restored));

        var text = File.ReadAllText(restored);
        Assert.Contains("\"cwd\":\"C:\\\\work\\\\app2\"", text);
        Assert.DoesNotContain("/mnt/c/work/app", text);
        // Every line still parses as JSON.
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var _ = JsonDocument.Parse(line);
        }
    }

    [Fact]
    public void Codex_BackupMatchesByCwd_AndRestoreRewrites()
    {
        using var src = new TempDir();
        using var dst = new TempDir();
        using var work = new TempDir();

        const string project1 = "/mnt/c/work/app";
        const string project2 = "/home/u/app";
        var env1 = Env(src.Path, SessionPlatform.Linux, PathStyle.Wsl);
        var env2 = Env(dst.Path, SessionPlatform.Linux, PathStyle.Unix);

        // One rollout for our project, one for a different project (must be excluded).
        Write(Path.Combine(src.Path, ".codex", "sessions", "2026", "06", "20", "rollout-a.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"cwd\":\"/mnt/c/work/app\"}}\n{\"p\":\"/mnt/c/work/app/x\"}\n");
        Write(Path.Combine(src.Path, ".codex", "sessions", "2026", "06", "20", "rollout-b.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"cwd\":\"/mnt/c/other\"}}\n");

        var zip = Path.Combine(work.Path, "codex.zip");
        var backup = new SessionBackupService().Run(new CodexSessionProvider(), env1, project1, zip, "test", DateTimeOffset.UnixEpoch);
        Assert.Equal(1, backup.FileCount);

        var report = new SessionRestoreService().Run(zip, env2, project2, force: false, dryRun: false);
        Assert.Equal(1, report.Written);

        var restored = Path.Combine(dst.Path, ".codex", "sessions", "2026", "06", "20", "rollout-a.jsonl");
        Assert.True(File.Exists(restored));
        var text = File.ReadAllText(restored);
        Assert.Contains("\"cwd\":\"/home/u/app\"", text);
        Assert.DoesNotContain("/mnt/c/work/app", text);
    }

    [Fact]
    public void Backup_NoSessions_ReturnsEmptyAndWritesNothing()
    {
        using var src = new TempDir();
        using var work = new TempDir();
        var env = Env(src.Path, SessionPlatform.Linux, PathStyle.Unix);
        var zip = Path.Combine(work.Path, "out.zip");

        var report = new SessionBackupService().Run(new ClaudeSessionProvider(), env, "/home/u/empty", zip, "test", DateTimeOffset.UnixEpoch);

        Assert.True(report.IsEmpty);
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void Restore_DryRun_WritesNothing()
    {
        using var src = new TempDir();
        using var dst = new TempDir();
        using var work = new TempDir();
        var env1 = Env(src.Path, SessionPlatform.Linux, PathStyle.Unix);
        var env2 = Env(dst.Path, SessionPlatform.Linux, PathStyle.Unix);

        var key = SessionProviderSupport.EncodePathKey("/home/u/app");
        Write(Path.Combine(src.Path, ".claude", "projects", key, "s.jsonl"), "{\"cwd\":\"/home/u/app\"}\n");
        var zip = Path.Combine(work.Path, "c.zip");
        new SessionBackupService().Run(new ClaudeSessionProvider(), env1, "/home/u/app", zip, "test", DateTimeOffset.UnixEpoch);

        var report = new SessionRestoreService().Run(zip, env2, "/home/u/app", force: false, dryRun: true);

        Assert.Equal(1, report.Written); // counted as "would write"
        Assert.False(Directory.Exists(Path.Combine(dst.Path, ".claude")));
    }

    [Fact]
    public void Restore_SkipsExisting_UnlessForce()
    {
        using var src = new TempDir();
        using var dst = new TempDir();
        using var work = new TempDir();
        var env1 = Env(src.Path, SessionPlatform.Linux, PathStyle.Unix);
        var env2 = Env(dst.Path, SessionPlatform.Linux, PathStyle.Unix);

        var key = SessionProviderSupport.EncodePathKey("/home/u/app");
        Write(Path.Combine(src.Path, ".claude", "projects", key, "s.jsonl"), "{\"cwd\":\"/home/u/app\"}\n");
        var zip = Path.Combine(work.Path, "c.zip");
        new SessionBackupService().Run(new ClaudeSessionProvider(), env1, "/home/u/app", zip, "test", DateTimeOffset.UnixEpoch);

        // Pre-existing file at the destination.
        var dest = Path.Combine(dst.Path, ".claude", "projects", key, "s.jsonl");
        Write(dest, "OLD");

        var skipped = new SessionRestoreService().Run(zip, env2, "/home/u/app", force: false, dryRun: false);
        Assert.True(skipped.AnyBlocked);
        Assert.Equal("OLD", File.ReadAllText(dest));

        var forced = new SessionRestoreService().Run(zip, env2, "/home/u/app", force: true, dryRun: false);
        Assert.Equal(1, forced.Written);
        Assert.NotEqual("OLD", File.ReadAllText(dest));
    }

    [Fact]
    public void Restore_RejectsZipSlipEntries()
    {
        using var dst = new TempDir();
        using var work = new TempDir();
        var env = Env(dst.Path, SessionPlatform.Linux, PathStyle.Unix);

        var zip = Path.Combine(work.Path, "evil.zip");
        using (var stream = new FileStream(zip, FileMode.Create))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var manifest = new SessionManifest(
                SessionManifest.CurrentSchemaVersion, "test", "claude",
                DateTimeOffset.UnixEpoch.ToString("o"),
                new SessionSource("linux", "unix", "/home/u", "/home/u/app", "key"),
                new[] { new SessionFileEntry("../escape.txt", 4, "sha256:00") });
            using (var mw = new StreamWriter(archive.CreateEntry(SessionManifest.FileName).Open()))
            {
                mw.Write(manifest.ToJson());
            }

            using var ew = new StreamWriter(archive.CreateEntry(SessionManifest.FilesPrefix + "../escape.txt").Open());
            ew.Write("evil");
        }

        var report = new SessionRestoreService().Run(zip, env, "/home/u/app", force: true, dryRun: false);

        Assert.All(report.Items, i => Assert.Equal(RestoreAction.SkippedUnsafe, i.Action));
        Assert.False(File.Exists(Path.Combine(work.Path, "escape.txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dst.Path)!, "escape.txt")));
    }

    [Fact]
    public void Restore_UnknownArchive_Throws()
    {
        using var dst = new TempDir();
        var env = Env(dst.Path, SessionPlatform.Linux, PathStyle.Unix);
        Assert.Throws<SessionException>(() =>
            new SessionRestoreService().Run(Path.Combine(dst.Path, "missing.zip"), env, "/home/u/app", false, false));
    }
}
