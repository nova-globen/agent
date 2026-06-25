using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli.Tests;

public sealed class CliRunnerTests
{
    [Fact]
    public void Version_PrintsVersionAndSucceeds()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("--version");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        // Assert the format, not a hardcoded version, so this survives version bumps.
        Assert.Matches(@"^agent \d+\.\d+\.\d+", result.StdOut.Trim());
    }

    [Fact]
    public void NoArgs_PrintsHelpAndSucceeds()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke();

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
    }

    [Fact]
    public void UnknownCommand_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("frobnicate");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("unknown command", result.StdErr);
    }

    [Fact]
    public void UnknownOption_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("status", "--nope");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("unknown option", result.StdErr);
    }

    [Fact]
    public void Init_ScaffoldsStructureAndSucceeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "agent.yaml")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".githooks", "pre-commit")));
    }

    [Fact]
    public void Init_WithSamplesFlag_InstallsSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init", "--with-samples");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "skill.yaml")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "SKILL.md")));
    }

    [Fact]
    public void Init_WithSamplesFlag_InstallsSampleAgents()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init", "--with-samples");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "agents", "planner", "agent.yaml")));
        Assert.True(File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "agents", "verifier", "agent.yaml")));
    }

    [Fact]
    public void Init_NoSamplesFlag_SkipsSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init", "--no-samples");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot")));
    }

    [Fact]
    public void Init_InteractiveYes_InstallsSamples()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke(new StringReader("y\n"), "init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot")));
    }

    [Fact]
    public void Init_InteractiveNo_SkipsSamples()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke(new StringReader("n\n"), "init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot")));
    }

    [Fact]
    public void Status_NotInitialized_NoFailFlag_StillSucceeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("status");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("Initialized: no", result.StdOut);
    }

    [Fact]
    public void Status_NotInitialized_WithFailOnDrift_ReturnsDriftCode()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("status", "--fail-on-drift", "--ci");

        Assert.Equal(ExitCodes.DriftOrValidationFailed, result.ExitCode);
    }

    [Fact]
    public void Status_AfterInit_WithFailOnDrift_Succeeds()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("status", "--fail-on-drift");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("No issues detected.", result.StdOut);
    }

    [Fact]
    public void Status_Json_IsValidJsonWithExpectedFields()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");
        h.Invoke("sync");

        var result = h.Invoke("status", "--json");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.GetProperty("initialized").GetBoolean());
        // init scaffolds two skills: code-review and using-agent-sync.
        Assert.Equal(2, doc.RootElement.GetProperty("skills").GetInt32());
        Assert.False(doc.RootElement.GetProperty("hasProblems").GetBoolean());
    }

    [Fact]
    public void Doctor_Json_ReportsChecks()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();
        h.Invoke("init");

        var result = h.Invoke("doctor", "--json");

        using var doc = JsonDocument.Parse(result.StdOut);
        Assert.True(doc.RootElement.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 4);
    }

    [Fact]
    public void Init_InteractiveYes_InstallsSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        // Simulate user typing "y" at the prompt.
        var result = h.Invoke(new StringReader("y"), "init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(
            File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "SKILL.md")),
            "autopilot should be installed when user types y");
    }

    [Fact]
    public void Init_InteractiveNo_DoesNotInstallSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        // Simulate user typing "n" (or empty) at the prompt.
        var result = h.Invoke(new StringReader("n"), "init");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(
            File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "SKILL.md")),
            "autopilot should not be installed when user types n");
    }

    [Fact]
    public void Init_WithSamples_InstallsSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init", "--with-samples");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(
            File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "SKILL.md")),
            "autopilot SKILL.md should be installed");
        Assert.True(
            File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "agents", "planner", "agent.yaml")),
            "planner agent.yaml should be installed");
        Assert.True(
            File.Exists(Path.Combine(h.WorkingDirectory, ".githooks", "commit-msg")),
            "commit-msg hook should be installed");
    }

    [Fact]
    public void Init_NoSamples_DoesNotInstallSampleSkills()
    {
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        var result = h.Invoke("init", "--no-samples");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.False(
            File.Exists(Path.Combine(h.WorkingDirectory, ".agent", "skills", "autopilot", "SKILL.md")),
            "autopilot SKILL.md should not be installed");
    }

    [Fact]
    public void Autopilot_Help_PrintsUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("autopilot", "--help");

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("claude", result.StdOut);
    }

    [Fact]
    public void Autopilot_NoSubcommand_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("autopilot");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
        Assert.Contains("provider", result.StdErr);
    }

    [Fact]
    public void Autopilot_UnknownProvider_ReturnsInvalidUsage()
    {
        using var h = new CliTestHarness();

        var result = h.Invoke("autopilot", "gemini");

        Assert.Equal(ExitCodes.InvalidUsage, result.ExitCode);
    }

    [Fact]
    public void Autopilot_Claude_MissingCli_ReturnsEnvironmentError()
    {
        // When claude is not on PATH (true in most test environments),
        // the command should report a clean error rather than an exception.
        using var h = new CliTestHarness();
        h.MakeGitRepo();

        // We only run this if claude is genuinely absent to keep the test
        // deterministic without needing to mock the process.
        if (IsClaudeAvailable())
        {
            return;
        }

        var result = h.Invoke("autopilot", "claude");

        Assert.Equal(ExitCodes.EnvironmentProblem, result.ExitCode);
        Assert.Contains("not found on PATH", result.StdErr);
    }

    private static bool IsClaudeAvailable()
    {
        // Mirror ClaudeAutopilotProvider.IsAvailable(): use cmd.exe /c on Windows so that
        // PATHEXT is honoured and .cmd/.exe wrappers are found the same way.
        try
        {
            System.Diagnostics.ProcessStartInfo psi;
            if (OperatingSystem.IsWindows())
            {
                psi = new System.Diagnostics.ProcessStartInfo("cmd.exe")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("claude");
                psi.ArgumentList.Add("--version");
            }
            else
            {
                psi = new System.Diagnostics.ProcessStartInfo("claude")
                {
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
            }

            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
