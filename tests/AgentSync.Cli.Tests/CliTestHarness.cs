using AgentSync.Cli;

namespace AgentSync.Cli.Tests;

/// <summary>Temporary working directory plus a helper to invoke the CLI in it.</summary>
public sealed class CliTestHarness : IDisposable
{
    public CliTestHarness()
    {
        WorkingDirectory = Path.Combine(
            Path.GetTempPath(),
            "agentsync-cli-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkingDirectory);
    }

    public string WorkingDirectory { get; }

    public string MakeGitRepo()
    {
        Directory.CreateDirectory(Path.Combine(WorkingDirectory, ".git"));
        return WorkingDirectory;
    }

    public CliResult Invoke(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var runner = new CliRunner(stdout, stderr, WorkingDirectory);
        var code = runner.Run(args);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}

public sealed record CliResult(int ExitCode, string StdOut, string StdErr);
