namespace AgentSync.Core;

/// <summary>Probes the host environment (PATH lookups).</summary>
public static class EnvironmentProbe
{
    /// <summary>
    /// Returns true if a command with the given <paramref name="name"/> can be found
    /// on the current PATH (honoring PATHEXT on Windows).
    /// </summary>
    public static bool IsOnPath(string name)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return false;
        }

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : new[] { string.Empty };

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
