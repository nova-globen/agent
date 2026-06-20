using AgentSync.Core;
using AgentSync.Core.Import;
using AgentSync.Ui.Abstractions;

namespace AgentSync.Ui.Abstractions.Tests;

public sealed class AgentSyncAppTests : IDisposable
{
    private readonly string _dir;

    public AgentSyncAppTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "agentsync-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(Path.Combine(_dir, ".git"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private AgentSyncApp Init()
    {
        new InitService(_dir).Run();
        return new AgentSyncApp(_dir);
    }

    [Fact]
    public void GetState_Uninitialized()
    {
        var app = new AgentSyncApp(_dir);

        var state = app.GetState();

        Assert.True(state.IsGitRepository);
        Assert.False(state.Initialized);
        Assert.Equal(0, state.SkillCount);
    }

    [Fact]
    public void GetState_AfterInit_Initialized()
    {
        var app = Init();

        var state = app.GetState();

        Assert.True(state.Initialized);
        Assert.Equal(2, state.SkillCount);
    }

    [Fact]
    public void ListSkills_ReturnsCanonicalSkills()
    {
        var app = Init();

        var skills = app.ListSkills();

        // init scaffolds two skills: code-review and using-agent-sync (ordered by id).
        Assert.Equal(2, skills.Count);
        Assert.Equal("code-review", skills[0].Id);
        Assert.Equal("using-agent-sync", skills[1].Id);
    }

    [Fact]
    public void Validate_DefaultWorkspace_IsValid()
    {
        var app = Init();

        Assert.True(app.Validate().IsValid);
    }

    [Fact]
    public void Sync_ThenStatus_IsClean()
    {
        var app = Init();

        var sync = app.Sync();
        var status = app.Status();

        Assert.True(sync.ConfigValid);
        Assert.False(status.HasProblems);
    }

    [Fact]
    public void AddSkill_ThenListed()
    {
        var app = Init();

        var result = app.AddSkill("docs-review", "Docs Review", "Reviews docs.", null, null);

        Assert.Equal(Core.Authoring.AuthoringStatus.Ok, result.Status);
        Assert.Contains(app.ListSkills(), s => s.Id == "docs-review");
    }

    [Fact]
    public void ImportSkill_ThroughApp_CreatesSkill()
    {
        var app = Init();
        var srcDir = Path.Combine(_dir, "incoming", "triage");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "SKILL.md"),
            "---\nname: Triage\ndescription: Triage.\n---\n\n## Use\n\nx\n");

        var report = app.ImportSkill("incoming/triage", new SkillImportOptions());

        Assert.Equal(ImportStatus.Ok, report.Status);
        Assert.Contains(app.ListSkills(), s => s.Id == "triage");
    }

    [Fact]
    public void DeleteTarget_BeforeSync_RemovesFromConfig()
    {
        var app = Init();

        var result = app.DeleteTarget("gemini", force: false, dryRun: false);

        Assert.Equal(Core.Authoring.AuthoringStatus.Ok, result.Status);
        Assert.False(app.GetConfig()!.Targets.ContainsKey("gemini"));
    }

    [Fact]
    public void Abstractions_DoesNotReferenceMaui()
    {
        foreach (var referenced in typeof(AgentSyncApp).Assembly.GetReferencedAssemblies())
        {
            var name = referenced.Name ?? string.Empty;
            Assert.DoesNotContain("maui", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
