using AgentSync.Core.Configuration;

namespace AgentSync.Core.Import;

/// <summary>
/// A proposed canonical skill produced by import, before it is written to disk. Holds the
/// resolved id/name/description/version, the normalized body, the enabled target ids, and
/// the source it came from.
/// </summary>
public sealed record SkillDraft(
    string Id,
    string Name,
    string Description,
    string Version,
    string Body,
    IReadOnlyList<string> Targets,
    string SourceRelativePath)
{
    /// <summary>Builds the <see cref="SkillManifest"/> this draft would write.</summary>
    public SkillManifest ToManifest()
    {
        var manifest = new SkillManifest
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Version = Version,
        };

        foreach (var target in Targets)
        {
            manifest.Targets[target] = new SkillTargetSetting { Enabled = true };
        }

        return manifest;
    }
}

/// <summary>Validates a <see cref="SkillDraft"/> using the canonical <see cref="SkillValidator"/> rules.</summary>
public static class DraftValidation
{
    /// <summary>
    /// Runs the same validation a written skill would face (id format, required fields,
    /// non-empty body, known targets). The draft's id is treated as its folder name so
    /// the id==folder invariant is satisfied.
    /// </summary>
    public static ValidationResult Validate(SkillDraft draft)
    {
        var result = new ValidationResult();
        var skill = new Skill
        {
            Manifest = draft.ToManifest(),
            Body = draft.Body,
            DirectoryName = draft.Id,
            DirectoryPath = draft.Id,
        };

        SkillValidator.Validate(skill, result);
        return result;
    }
}
