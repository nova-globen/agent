namespace AgentSync.Core.Projections;

/// <summary>
/// Applies a generated section to a file on disk, preserving all user-authored content
/// outside the managed markers and refusing to clobber hand-edited sections.
/// </summary>
public static class SectionWriter
{
    /// <summary>
    /// Reads <paramref name="filePath"/> (if present), upserts the section for
    /// (<paramref name="skillId"/>, <paramref name="targetId"/>) with <paramref name="body"/>,
    /// and writes back only when something changed.
    /// </summary>
    public static UpsertResult Apply(
        string filePath,
        string skillId,
        string targetId,
        string body,
        bool force = false)
    {
        var existing = File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
        var doc = MarkedDocument.Parse(existing);
        var result = doc.Upsert(skillId, targetId, body, force);

        if (result.Wrote)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var content = doc.Render();
            if (!content.EndsWith('\n'))
            {
                content += "\n";
            }

            File.WriteAllText(filePath, content);
        }

        return result;
    }
}
