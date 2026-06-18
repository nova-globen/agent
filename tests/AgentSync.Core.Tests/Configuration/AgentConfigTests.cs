using AgentSync.Core.Configuration;

namespace AgentSync.Core.Tests.Configuration;

public sealed class AgentConfigTests
{
    [Fact]
    public void Parse_ReadsTargetsAndPolicy()
    {
        var config = AgentConfig.Parse(Templates.AgentYaml);

        Assert.Equal(1, config.Version);
        Assert.True(config.Targets[TargetIds.AgentsMd].Enabled);
        Assert.Equal("AGENTS.md", config.Targets[TargetIds.AgentsMd].Path);
        Assert.Equal(".cursor/rules", config.Targets[TargetIds.Cursor].Path);
        Assert.True(config.Policy.FailOnManualEdit);
        Assert.True(config.Policy.AllowTargetSpecificOverrides);
    }

    [Fact]
    public void Parse_MalformedYaml_Throws()
    {
        Assert.Throws<ConfigParseException>(() => AgentConfig.Parse("version: 1\n  bad: : :"));
    }

    [Fact]
    public void Validate_DefaultTemplate_IsValid()
    {
        var config = AgentConfig.Parse(Templates.AgentYaml);
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_MissingVersion_ReportsError()
    {
        var config = AgentConfig.Parse("targets:\n  agents_md:\n    enabled: true\n    path: AGENTS.md\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, m => m.Code == "config.version-missing");
    }

    [Fact]
    public void Validate_UnsupportedVersion_ReportsError()
    {
        var config = AgentConfig.Parse("version: 99\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.Contains(result.Messages, m => m.Code == "config.version-unsupported");
    }

    [Fact]
    public void Validate_UnknownTarget_ReportsError()
    {
        var config = AgentConfig.Parse("version: 1\ntargets:\n  not_a_target:\n    enabled: true\n    path: x\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.Contains(result.Messages, m => m.Code == "config.unknown-target");
    }

    [Fact]
    public void Validate_EnabledTargetWithoutPath_ReportsError()
    {
        var config = AgentConfig.Parse("version: 1\ntargets:\n  agents_md:\n    enabled: true\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.Contains(result.Messages, m => m.Code == "config.target-missing-path");
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../../escape.md")]
    [InlineData("C:\\\\Windows\\\\x.md")]
    public void Validate_UnsafeTargetPath_ReportsError(string unsafePath)
    {
        var config = AgentConfig.Parse($"version: 1\ntargets:\n  agents_md:\n    enabled: true\n    path: \"{unsafePath}\"\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.False(result.IsValid);
        Assert.Contains(result.Messages, m => m.Code == "config.target-unsafe-path");
    }

    [Fact]
    public void Validate_RelativeTargetPath_IsAccepted()
    {
        var config = AgentConfig.Parse("version: 1\ntargets:\n  agents_md:\n    enabled: true\n    path: docs/AGENTS.md\n");
        var result = new ValidationResult();

        ConfigValidator.Validate(config, result);

        Assert.DoesNotContain(result.Messages, m => m.Code == "config.target-unsafe-path");
    }
}
