namespace AgentSync.Core.Authoring;

/// <summary>
/// Resolves a <c>--body-file</c> argument (used by <c>skill edit</c> and
/// <c>subagent edit</c>) to an absolute path.
/// <para>
/// A body file is an <em>input</em> the user explicitly points at: it is read once and its
/// contents are written into the canonical skill/sub-agent, which is itself repo-confined.
/// The body file is never written to, so — unlike a projection path — it is deliberately not
/// run through <c>RepoPath</c>. Absolute paths and paths outside the repo are accepted, so a
/// perfectly valid source file no longer fails with "path '…' is absolute." The
/// path-traversal guard on projection reads/writes is unchanged.
/// </para>
/// </summary>
internal static class BodyFile
{
    public static string Resolve(string repoRoot, string bodyFile)
    {
        var trimmed = bodyFile.Trim();
        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(repoRoot, trimmed));
    }
}
