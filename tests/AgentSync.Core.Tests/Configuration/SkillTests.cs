using AgentSync.Core.Configuration;

namespace AgentSync.Core.Tests.Configuration;

public sealed class SkillTests
{
    private static Skill MakeSkill(string dirName, SkillManifest manifest, string body = "# Body\n")
        => new()
        {
            Manifest = manifest,
            Body = body,
            DirectoryName = dirName,
            DirectoryPath = "/tmp/" + dirName,
        };

    [Fact]
    public void Parse_ReadsManifestFields()
    {
        var manifest = SkillManifest.Parse(Templates.DefaultSkillYaml);

        Assert.Equal("code-review", manifest.Id);
        Assert.Equal("Code Review", manifest.Name);
        Assert.True(manifest.Targets[TargetIds.ClaudeMd].Enabled);
    }

    [Fact]
    public void Validate_ValidSkill_IsValid()
    {
        var manifest = SkillManifest.Parse(Templates.DefaultSkillYaml);
        var skill = MakeSkill("code-review", manifest);
        var result = new ValidationResult();

        SkillValidator.Validate(skill, result);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingRequiredFields_ReportsErrors()
    {
        var skill = MakeSkill("broken", new SkillManifest { Id = "broken" }, body: "");
        var result = new ValidationResult();

        SkillValidator.Validate(skill, result);

        Assert.Contains(result.Messages, m => m.Code == "skill.name-missing");
        Assert.Contains(result.Messages, m => m.Code == "skill.description-missing");
        Assert.Contains(result.Messages, m => m.Code == "skill.version-missing");
        Assert.Contains(result.Messages, m => m.Code == "skill.body-empty");
    }

    [Fact]
    public void Validate_IdNotMatchingFolder_ReportsError()
    {
        var manifest = new SkillManifest
        {
            Id = "other-id",
            Name = "X",
            Description = "Y",
            Version = "0.1.0",
        };
        var skill = MakeSkill("folder-name", manifest);
        var result = new ValidationResult();

        SkillValidator.Validate(skill, result);

        Assert.Contains(result.Messages, m => m.Code == "skill.id-folder-mismatch");
    }

    [Theory]
    [InlineData("Bad_Id")]
    [InlineData("UPPER")]
    [InlineData("-leading")]
    [InlineData("with space")]
    public void Validate_BadIdFormat_ReportsError(string id)
    {
        var manifest = new SkillManifest { Id = id, Name = "X", Description = "Y", Version = "0.1.0" };
        var skill = MakeSkill(id, manifest);
        var result = new ValidationResult();

        SkillValidator.Validate(skill, result);

        Assert.Contains(result.Messages, m => m.Code == "skill.id-format");
    }

    [Fact]
    public void Validate_UnknownTarget_ReportsError()
    {
        var manifest = new SkillManifest
        {
            Id = "s",
            Name = "X",
            Description = "Y",
            Version = "0.1.0",
            Targets = new() { ["nope"] = new SkillTargetSetting { Enabled = true } },
        };
        var skill = MakeSkill("s", manifest);
        var result = new ValidationResult();

        SkillValidator.Validate(skill, result);

        Assert.Contains(result.Messages, m => m.Code == "skill.unknown-target");
    }
}
