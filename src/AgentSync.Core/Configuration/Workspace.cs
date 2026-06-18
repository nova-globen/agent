namespace AgentSync.Core.Configuration;

/// <summary>
/// The loaded canonical state of a repository: config, skills, and validation messages.
/// </summary>
public sealed class Workspace
{
    public required string RepoRoot { get; init; }

    public AgentConfig? Config { get; init; }

    public IReadOnlyList<Skill> Skills { get; init; } = Array.Empty<Skill>();

    public required ValidationResult Validation { get; init; }

    public bool IsValid => Validation.IsValid;
}

/// <summary>Loads and validates the <c>.agent/</c> tree from disk.</summary>
public static class WorkspaceLoader
{
    public static Workspace Load(string repoRoot)
    {
        var layout = new RepoLayout(repoRoot);
        var validation = new ValidationResult();

        var config = LoadConfig(layout, validation);
        var skills = LoadSkills(layout, validation);

        return new Workspace
        {
            RepoRoot = layout.RepoRoot,
            Config = config,
            Skills = skills,
            Validation = validation,
        };
    }

    private static AgentConfig? LoadConfig(RepoLayout layout, ValidationResult validation)
    {
        if (!File.Exists(layout.ConfigFile))
        {
            validation.AddError(
                "config.missing",
                $"Missing configuration file {RepoLayout.AgentDirName}/{RepoLayout.ConfigFileName}. Run 'agent init'.",
                "agent.yaml");
            return null;
        }

        AgentConfig config;
        try
        {
            config = AgentConfig.Parse(File.ReadAllText(layout.ConfigFile));
        }
        catch (ConfigParseException ex)
        {
            validation.AddError("config.parse-error", ex.Message, "agent.yaml");
            return null;
        }

        ConfigValidator.Validate(config, validation);
        return config;
    }

    private static IReadOnlyList<Skill> LoadSkills(RepoLayout layout, ValidationResult validation)
    {
        var skills = new List<Skill>();
        if (!Directory.Exists(layout.SkillsDir))
        {
            return skills;
        }

        var seenIds = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var dir in Directory.EnumerateDirectories(layout.SkillsDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var dirName = Path.GetFileName(dir);
            var manifestPath = Path.Combine(dir, "skill.yaml");
            var bodyPath = Path.Combine(dir, "SKILL.md");
            var source = $"skills/{dirName}";

            if (!File.Exists(manifestPath))
            {
                validation.AddError(
                    "skill.manifest-missing",
                    $"Skill directory '{dirName}' has no skill.yaml.",
                    source);
                continue;
            }

            SkillManifest manifest;
            try
            {
                manifest = SkillManifest.Parse(File.ReadAllText(manifestPath));
            }
            catch (ConfigParseException ex)
            {
                validation.AddError("skill.parse-error", $"{dirName}: {ex.Message}", source);
                continue;
            }

            var body = File.Exists(bodyPath) ? File.ReadAllText(bodyPath) : string.Empty;

            var skill = new Skill
            {
                Manifest = manifest,
                Body = body,
                DirectoryName = dirName,
                DirectoryPath = dir,
            };

            SkillValidator.Validate(skill, validation);

            if (!string.IsNullOrWhiteSpace(manifest.Id))
            {
                if (seenIds.TryGetValue(manifest.Id, out var firstDir))
                {
                    validation.AddError(
                        "skill.duplicate-id",
                        $"Duplicate skill id '{manifest.Id}' in '{dirName}' (already defined in '{firstDir}').",
                        source);
                }
                else
                {
                    seenIds[manifest.Id] = dirName;
                }
            }

            skills.Add(skill);
        }

        return skills;
    }
}
