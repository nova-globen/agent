using System.Reflection;
using System.Text.Json;
using AgentSync.Core;

namespace AgentSync.Cli;

/// <summary>
/// Parses arguments, dispatches commands, and maps results to exit codes.
/// Kept free of <see cref="Console"/> coupling so it can be driven from tests.
/// </summary>
public sealed class CliRunner
{
    private readonly TextWriter _out;
    private readonly TextWriter _err;
    private readonly string _workingDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CliRunner(TextWriter? output = null, TextWriter? error = null, string? workingDirectory = null)
    {
        _out = output ?? Console.Out;
        _err = error ?? Console.Error;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    public int Run(string[] args)
    {
        try
        {
            return Dispatch(args);
        }
        catch (Exception ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }
    }

    private int Dispatch(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        var command = args[0];
        var rest = args.Skip(1).ToArray();

        return command switch
        {
            "--version" or "-v" or "version" => RunVersion(),
            "--help" or "-h" or "help" => RunHelp(),
            "init" => RunInit(rest),
            "status" => RunStatus(rest),
            "doctor" => RunDoctor(rest),
            _ => UnknownCommand(command),
        };
    }

    private int UnknownCommand(string command)
    {
        _err.WriteLine($"error: unknown command '{command}'.");
        _err.WriteLine("Run 'agent --help' for usage.");
        return ExitCodes.InvalidUsage;
    }

    private int RunVersion()
    {
        _out.WriteLine($"agent {GetVersion()}");
        return ExitCodes.Success;
    }

    private int RunHelp()
    {
        PrintHelp();
        return ExitCodes.Success;
    }

    // --- init -----------------------------------------------------------------

    private int RunInit(string[] args)
    {
        var force = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--force":
                    force = true;
                    break;
                default:
                    return UnknownOption("init", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory);
        var fellBack = root is null;
        root ??= _workingDirectory;

        if (fellBack)
        {
            _out.WriteLine("note: not inside a Git repository; scaffolding in the current directory.");
        }

        var result = new InitService(root).Run(force);

        foreach (var file in result.Files)
        {
            var label = file.Action switch
            {
                FileAction.Created => "created",
                FileAction.Overwritten => "overwritten",
                _ => "skipped (exists)",
            };
            _out.WriteLine($"  {label,-18} {file.RelativePath}");
        }

        if (!force && result.Files.Any(f => f.Action == FileAction.Skipped))
        {
            _out.WriteLine();
            _out.WriteLine("Some files already existed and were left untouched. Use --force to overwrite.");
        }

        _out.WriteLine();
        _out.WriteLine("Agent Sync initialized. Next: run 'agent install-hooks', then 'agent status'.");
        return ExitCodes.Success;
    }

    // --- status ---------------------------------------------------------------

    private int RunStatus(string[] args)
    {
        var json = false;
        var failOnDrift = false;
        var ci = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json":
                    json = true;
                    break;
                case "--fail-on-drift":
                    failOnDrift = true;
                    break;
                case "--ci":
                    ci = true;
                    break;
                default:
                    return UnknownOption("status", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var report = new StatusService(root).Run();

        if (json)
        {
            WriteStatusJson(root, report);
        }
        else
        {
            WriteStatusHuman(root, report, ci);
        }

        return failOnDrift && report.HasProblems
            ? ExitCodes.DriftOrValidationFailed
            : ExitCodes.Success;
    }

    private void WriteStatusHuman(string root, StatusReport report, bool ci)
    {
        _out.WriteLine("Agent Sync status");
        _out.WriteLine();
        _out.WriteLine($"Repository:  {root}");
        _out.WriteLine($"Initialized: {(report.Initialized ? "yes" : "no")}");
        _out.WriteLine($"Skills:      {report.SkillCount}");
        _out.WriteLine();

        if (report.Issues.Count == 0)
        {
            _out.WriteLine("No issues detected.");
            return;
        }

        foreach (var issue in report.Issues)
        {
            var marker = issue.Severity switch
            {
                IssueSeverity.Error => "ERROR",
                IssueSeverity.Warning => "WARN",
                _ => "INFO",
            };
            _out.WriteLine($"  [{marker}] {issue.Message}");
        }

        if (ci && report.HasProblems)
        {
            _out.WriteLine();
            _out.WriteLine("Drift or invalid state detected.");
        }
    }

    private void WriteStatusJson(string root, StatusReport report)
    {
        var payload = new
        {
            repository = root,
            initialized = report.Initialized,
            skills = report.SkillCount,
            hasProblems = report.HasProblems,
            issues = report.Issues.Select(i => new
            {
                code = i.Code,
                severity = i.Severity.ToString().ToLowerInvariant(),
                message = i.Message,
            }),
        };
        _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    // --- doctor ---------------------------------------------------------------

    private int RunDoctor(string[] args)
    {
        var json = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json":
                    json = true;
                    break;
                default:
                    return UnknownOption("doctor", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory);
        var hooksPath = root is null ? null : GitRepository.GetHooksPath(root);
        var agentOnPath = EnvironmentProbe.IsOnPath("agent");

        var report = new DoctorService().Run(new DoctorInput(root, hooksPath, agentOnPath));

        if (json)
        {
            var payload = new
            {
                ok = report.AllOk,
                checks = report.Checks.Select(c => new { name = c.Name, ok = c.Ok, detail = c.Detail }),
            };
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            _out.WriteLine("Agent Sync doctor");
            _out.WriteLine();
            foreach (var check in report.Checks)
            {
                var mark = check.Ok ? "OK " : "FAIL";
                _out.WriteLine($"  [{mark}] {check.Name}: {check.Detail}");
            }
            _out.WriteLine();
            _out.WriteLine(report.AllOk ? "All checks passed." : "Some checks failed.");
        }

        return report.AllOk ? ExitCodes.Success : ExitCodes.EnvironmentProblem;
    }

    // --- helpers --------------------------------------------------------------

    private int UnknownOption(string command, string option)
    {
        _err.WriteLine($"error: unknown option '{option}' for '{command}'.");
        _err.WriteLine("Run 'agent --help' for usage.");
        return ExitCodes.InvalidUsage;
    }

    private void PrintHelp()
    {
        _out.WriteLine($"agent {GetVersion()} — Git-native consistency manager for AI-agent skills.");
        _out.WriteLine();
        _out.WriteLine("Usage:");
        _out.WriteLine("  agent <command> [options]");
        _out.WriteLine();
        _out.WriteLine("Commands:");
        _out.WriteLine("  init                Scaffold .agent/ and .githooks/ (use --force to overwrite).");
        _out.WriteLine("  status              Report Agent Sync state (--json, --fail-on-drift, --ci).");
        _out.WriteLine("  doctor              Diagnose Git repo, PATH, hooks, and config (--json).");
        _out.WriteLine();
        _out.WriteLine("Global:");
        _out.WriteLine("  --version, -v       Print version.");
        _out.WriteLine("  --help, -h          Print this help.");
    }

    private static string GetVersion()
    {
        var info = typeof(CliRunner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }

        return typeof(CliRunner).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
