using System.Text.RegularExpressions;

namespace AgentSync.Core.Configuration;

/// <summary>Validates an <see cref="AgentConfig"/> document.</summary>
public static partial class ConfigValidator
{
    public static void Validate(AgentConfig config, ValidationResult result, string source = "agent.yaml")
    {
        if (config.Version == 0)
        {
            result.AddError("config.version-missing", "Config 'version' is missing or zero.", source);
        }
        else if (config.Version > AgentConfig.SupportedVersion)
        {
            result.AddError(
                "config.version-unsupported",
                $"Config version {config.Version} is newer than the supported version {AgentConfig.SupportedVersion}.",
                source);
        }

        if (config.Targets.Count == 0)
        {
            result.AddWarning("config.no-targets", "No projection targets are configured.", source);
        }

        foreach (var (id, setting) in config.Targets)
        {
            if (!TargetIds.IsKnown(id))
            {
                result.AddError("config.unknown-target", $"Unknown projection target '{id}'.", source);
                continue;
            }

            if (setting.Enabled && string.IsNullOrWhiteSpace(setting.Path))
            {
                result.AddError(
                    "config.target-missing-path",
                    $"Target '{id}' is enabled but has no 'path'.",
                    source);
            }
        }
    }
}

/// <summary>Validates a single skill (manifest + body).</summary>
public static partial class SkillValidator
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SkillIdPattern();

    public static void Validate(Skill skill, ValidationResult result)
    {
        var source = $"skills/{skill.DirectoryName}";
        var manifest = skill.Manifest;

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            result.AddError("skill.id-missing", "Skill 'id' is required.", source);
        }
        else
        {
            if (!SkillIdPattern().IsMatch(manifest.Id))
            {
                result.AddError(
                    "skill.id-format",
                    $"Skill id '{manifest.Id}' must be lowercase kebab-case (a-z, 0-9, hyphens).",
                    source);
            }

            if (!string.Equals(manifest.Id, skill.DirectoryName, StringComparison.Ordinal))
            {
                result.AddError(
                    "skill.id-folder-mismatch",
                    $"Skill id '{manifest.Id}' does not match its folder name '{skill.DirectoryName}'.",
                    source);
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            result.AddError("skill.name-missing", "Skill 'name' is required.", source);
        }

        if (string.IsNullOrWhiteSpace(manifest.Description))
        {
            result.AddError("skill.description-missing", "Skill 'description' is required.", source);
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            result.AddError("skill.version-missing", "Skill 'version' is required.", source);
        }

        if (string.IsNullOrWhiteSpace(skill.Body))
        {
            result.AddError("skill.body-empty", "SKILL.md is missing or empty.", source);
        }

        foreach (var id in manifest.Targets.Keys)
        {
            if (!TargetIds.IsKnown(id))
            {
                result.AddError("skill.unknown-target", $"Unknown projection target '{id}'.", source);
            }
        }
    }
}
