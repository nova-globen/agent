namespace AgentSync.Core.Sessions;

/// <summary>The operating-system family a backup was captured on or is restored into.</summary>
public enum SessionPlatform
{
    Linux,
    Windows,
    MacOs,
}

/// <summary>
/// The host context a session backup/restore runs in: the user's home directory, the OS
/// family, and the path style absolute paths are written in. Captured into the backup
/// manifest so restore can translate paths even when moving between WSL, Windows, and Linux.
/// </summary>
public sealed record SessionEnvironment(
    string HomeDirectory,
    SessionPlatform Platform,
    PathStyle PathStyle)
{
    /// <summary>Resolves the environment for the current process.</summary>
    public static SessionEnvironment Current()
    {
        var home = ResolveHome();
        var platform = ResolvePlatform();
        var style = platform == SessionPlatform.Windows
            ? PathStyle.Windows
            : IsWsl() ? PathStyle.Wsl : PathStyle.Unix;
        return new SessionEnvironment(home, platform, style);
    }

    private static string ResolveHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            return home;
        }

        home = Environment.GetEnvironmentVariable("HOME");
        return string.IsNullOrEmpty(home) ? Directory.GetCurrentDirectory() : home;
    }

    private static SessionPlatform ResolvePlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return SessionPlatform.Windows;
        }

        return OperatingSystem.IsMacOS() ? SessionPlatform.MacOs : SessionPlatform.Linux;
    }

    /// <summary>Detects whether the current Linux host is the WSL subsystem.</summary>
    public static bool IsWsl()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WSL_INTEROP")))
        {
            return true;
        }

        try
        {
            return File.Exists("/proc/version")
                && File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
