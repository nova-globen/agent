using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class SubagentCommandTests
{
    private static string AgentDir(CliTestHarness h, string id)
        => Path.Combine(h.WorkingDirectory, ".agent", "agents", id);

    private static string Projection(CliTestHarness h, string id)
        => Path.Combine(h.WorkingDirectory, ".claude", "agents", $"{id}.md");

    [Fact]
    public void Add_CreatesCanonicalFiles()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("subagent", "add", "reviewer", "--description", "Reviews diffs.", "--tools", "Read, Grep");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(AgentDir(h, "reviewer"), "agent.yaml")));
        Assert.True(File.Exists(Path.Combine(AgentDir(h, "reviewer"), "AGENT.md")));
    }

    [Fact]
    public void Add_InvalidId_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var result = h.Invoke("subagent", "add", "Bad Id", "--description", "x");
        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Add_MissingDescription_InvalidUsage()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        var result = h.Invoke("subagent", "add", "reviewer");
        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Add_Duplicate_Rejected()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("subagent", "add", "reviewer", "--description", "x");
        var result = h.Invoke("subagent", "add", "reviewer", "--description", "y");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
        Assert.Contains("already exists", result.StdOut);
    }

    [Fact]
    public void Sync_ProjectsSubagent_AndStatusIsClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "reviewer", "--description", "Reviews diffs.");

        var sync = h.Invoke("sync");
        Assert.Equal(ExitCodes.Success, sync.ExitCode);
        Assert.True(File.Exists(Projection(h, "reviewer")));

        var status = h.Invoke("status", "--fail-on-drift");
        Assert.Equal(ExitCodes.Success, status.ExitCode);
    }

    [Fact]
    public void Status_ReportsOutdatedSubagent()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "reviewer", "--description", "Reviews diffs.");
        h.Invoke("sync");
        h.Invoke("subagent", "edit", "reviewer", "--description", "Changed.");

        var status = h.Invoke("status", "--fail-on-drift");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, status.ExitCode);
        Assert.Contains("Outdated projection", status.StdOut);
    }

    [Fact]
    public void List_And_Show_Work()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "reviewer", "--description", "Reviews diffs.", "--model", "sonnet");

        Assert.Contains("reviewer", h.Invoke("subagent", "list").StdOut);
        Assert.Contains("reviewer", h.Invoke("subagents").StdOut);
        var show = h.Invoke("subagent", "show", "reviewer");
        Assert.Contains("sonnet", show.StdOut);
    }

    [Fact]
    public void Delete_BlockedWithoutForce_ThenForce()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "reviewer", "--description", "Reviews diffs.");
        h.Invoke("sync");

        var blocked = h.Invoke("subagent", "delete", "reviewer");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, blocked.ExitCode);

        var forced = h.Invoke("subagent", "delete", "reviewer", "--force");
        Assert.Equal(ExitCodes.Success, forced.ExitCode);
        Assert.False(Directory.Exists(AgentDir(h, "reviewer")));
    }

    [Fact]
    public void ImportSubagent_FromClaudeAgentFile()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var src = Path.Combine(h.WorkingDirectory, "src.md");
        File.WriteAllText(src,
            "---\nname: debugger\ndescription: Finds bugs.\ntools: Read, Bash\nmodel: opus\n---\n\nYou are a debugger.\n");

        var result = h.Invoke("import", "subagent", src);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(AgentDir(h, "debugger"), "agent.yaml")));
        var show = h.Invoke("subagent", "show", "debugger");
        Assert.Contains("Read, Bash", show.StdOut);
        Assert.Contains("opus", show.StdOut);
    }

    [Fact]
    public void Show_Missing_IsError()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        Assert.Equal(ExitCodes.DriftOrValidationFailed, h.Invoke("subagent", "show", "nope").ExitCode);
    }

    [Fact]
    public void Edit_AbsoluteBodyFile_AppliesAllFlags()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "planner", "--description", "Plans work.");

        var bodyPath = Path.Combine(h.WorkingDirectory, "agent-body.md");
        File.WriteAllText(bodyPath, "## Role\n\nPlan the work carefully.\n");

        // An absolute --body-file used to fail ("path '…' is absolute.") and apply none of the
        // other flags; it should now succeed and apply model, tool, and body together.
        var result = h.Invoke("subagent", "edit", "planner",
            "--model", "opus", "--tool", "Read", "--body-file", bodyPath);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        var yaml = File.ReadAllText(Path.Combine(AgentDir(h, "planner"), "agent.yaml"));
        Assert.Contains("opus", yaml);
        Assert.Contains("Read", yaml);
        var body = File.ReadAllText(Path.Combine(AgentDir(h, "planner"), "AGENT.md"));
        Assert.Contains("Plan the work carefully.", body);
    }

    [Fact]
    public void Add_Help_PrintsUsageAndExitsZero()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("subagent", "add", "--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Usage: agent subagent add", result.StdOut);
        Assert.Contains("--description", result.StdOut);
    }

    [Fact]
    public void Edit_Help_PrintsUsageAndExitsZero()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("subagent", "edit", "--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("--body-file", result.StdOut);
    }

    [Fact]
    public void Add_WithColor_PersistsAndProjects()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("subagent", "add", "reviewer", "--description", "Reviews.", "--color", "cyan");

        var yaml = File.ReadAllText(Path.Combine(AgentDir(h, "reviewer"), "agent.yaml"));
        Assert.Contains("color: cyan", yaml);

        h.Invoke("sync");
        Assert.Contains("color: cyan", File.ReadAllText(Projection(h, "reviewer")));
    }

    [Fact]
    public void ImportSubagent_PreservesColor_RoundTrip()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var src = Path.Combine(h.WorkingDirectory, ".claude", "agents", "verifier.md");
        Directory.CreateDirectory(Path.GetDirectoryName(src)!);
        File.WriteAllText(src, "---\nname: verifier\ndescription: Verifies.\ncolor: green\n---\n\nYou verify.\n");

        var import = h.Invoke("import", "subagent", src);
        Assert.Equal(ExitCodes.Success, import.ExitCode);

        // color survives import into the canonical manifest...
        Assert.Contains("color: green", File.ReadAllText(Path.Combine(AgentDir(h, "verifier"), "agent.yaml")));

        // ...and re-projection back into .claude/agents/<id>.md.
        h.Invoke("sync");
        Assert.Contains("color: green", File.ReadAllText(Projection(h, "verifier")));
    }

    [Fact]
    public void ImportSubagent_OfExistingProjection_StatusIsClean()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync"); // clean baseline (scaffolded skills projected)

        var proj = Path.Combine(h.WorkingDirectory, ".claude", "agents", "verifier.md");
        Directory.CreateDirectory(Path.GetDirectoryName(proj)!);
        File.WriteAllText(proj, "---\nname: verifier\ndescription: Verifies.\ncolor: green\n---\n\nYou verify.\n");

        // Importing one's own existing projection used to flag it as a manual edit; the import
        // now reconciles the projection + lockfile so status stays clean without sync --force.
        Assert.Equal(ExitCodes.Success, h.Invoke("import", "subagent", proj).ExitCode);
        Assert.Equal(ExitCodes.Success, h.Invoke("status", "--fail-on-drift").ExitCode);
    }

    [Fact]
    public void ImportSubagent_NoPath_DiscoversClaudeAgents()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var dir = Path.Combine(h.WorkingDirectory, ".claude", "agents");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "one.md"), "---\nname: one\ndescription: One.\n---\n\nBody one.\n");
        File.WriteAllText(Path.Combine(dir, "two.md"), "---\nname: two\ndescription: Two.\n---\n\nBody two.\n");

        var result = h.Invoke("import", "subagent");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(AgentDir(h, "one")));
        Assert.True(Directory.Exists(AgentDir(h, "two")));
    }

    [Fact]
    public void ImportSubagent_NoPath_NoClaudeAgents_IsUsageError()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("import", "subagent");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }
}
