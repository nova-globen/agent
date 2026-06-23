using System.Reflection;
using System.Text.Json;
using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;
using AgentSync.Core.Import;
using AgentSync.Core.Projections;
using AgentSync.Core.Sessions;
using AgentSync.Core.Subagents;

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
    private readonly IUiLauncher _uiLauncher;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly IUiReadinessProbe _readinessProbe;
    private readonly IUiInstaller _uiInstaller;

    /// <summary>How long <c>agent ui</c> waits for the web host to become ready.</summary>
    private static readonly TimeSpan UiReadyTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CliRunner(
        TextWriter? output = null,
        TextWriter? error = null,
        string? workingDirectory = null,
        IUiLauncher? uiLauncher = null,
        IBrowserLauncher? browserLauncher = null,
        IUiReadinessProbe? readinessProbe = null,
        IUiInstaller? uiInstaller = null)
    {
        _out = output ?? Console.Out;
        _err = error ?? Console.Error;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        _uiLauncher = uiLauncher ?? new UiLauncher();
        _browserLauncher = browserLauncher ?? new BrowserLauncher();
        _readinessProbe = readinessProbe ?? new HttpUiReadinessProbe();
        _uiInstaller = uiInstaller ?? new UiInstaller(_uiLauncher);
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
            "sync" => RunSync(rest),
            "diff" => RunDiff(rest),
            "validate" => RunValidate(rest),
            "import" => RunImport(rest),
            "skill" => RunSkill(rest),
            "skills" => RunSkillList(rest),
            "target" => RunTarget(rest),
            "targets" => RunTargetList(rest),
            "subagent" => RunSubagent(rest),
            "subagents" => RunSubagentList(rest),
            "sessions" or "session" => RunSessions(rest),
            "ui" => RunUi(rest),
            "install-hooks" => RunInstallHooks(rest),
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
        var subDrift = new SubagentProjector(root).Detect();

        if (json)
        {
            WriteStatusJson(root, report, subDrift);
        }
        else
        {
            WriteStatusHuman(root, report, ci);
            WriteSubagentStatusHuman(subDrift);
        }

        return failOnDrift && (report.HasProblems || subDrift.Count > 0)
            ? ExitCodes.DriftOrValidationFailed
            : ExitCodes.Success;
    }

    private void WriteSubagentStatusHuman(IReadOnlyList<SubagentDrift> drift)
    {
        if (drift.Count == 0)
        {
            return;
        }

        _out.WriteLine();
        _out.WriteLine("Sub-agents:");
        foreach (var d in drift)
        {
            var message = d.Kind switch
            {
                SubagentDriftKind.Missing => $"Missing projection {d.Path} (subagent {d.Id}). Run 'agent sync'.",
                SubagentDriftKind.Outdated => $"Outdated projection {d.Path} (subagent {d.Id}). Run 'agent sync'.",
                SubagentDriftKind.ManualEdit => $"Manually edited projection {d.Path} (subagent {d.Id}). Run 'agent sync --force'.",
                _ => $"Lockfile references a sub-agent no longer defined: {d.Id} ({d.Path}).",
            };
            _out.WriteLine($"  [ERROR] {message}");
        }
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

    private void WriteStatusJson(string root, StatusReport report, IReadOnlyList<SubagentDrift> subDrift)
    {
        var payload = new
        {
            repository = root,
            initialized = report.Initialized,
            skills = report.SkillCount,
            hasProblems = report.HasProblems || subDrift.Count > 0,
            issues = report.Issues.Select(i => new
            {
                code = i.Code,
                severity = i.Severity.ToString().ToLowerInvariant(),
                message = i.Message,
            }),
            subagentDrift = subDrift.Select(d => new
            {
                id = d.Id,
                path = d.Path,
                kind = d.Kind.ToString(),
            }),
        };
        _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    // --- install-hooks --------------------------------------------------------

    private int RunInstallHooks(string[] args)
    {
        foreach (var arg in args)
        {
            return UnknownOption("install-hooks", arg);
        }

        var root = GitRepository.Discover(_workingDirectory);
        if (root is null)
        {
            _err.WriteLine("error: not inside a Git repository.");
            return ExitCodes.EnvironmentProblem;
        }

        var result = new InstallHooksService(root).Run();

        if (result.GitConfigured)
        {
            _out.WriteLine($"core.hooksPath set to '{result.HooksPath}'.");
        }

        foreach (var hook in result.Hooks)
        {
            var state = !hook.Present ? "missing" : hook.Executable ? "ready" : "not executable";
            _out.WriteLine($"  {state,-15} {hook.Name}");
        }

        if (!result.Success)
        {
            _out.WriteLine();
            _out.WriteLine(result.Error ?? "Hooks were not fully installed.");
            return ExitCodes.EnvironmentProblem;
        }

        _out.WriteLine();
        _out.WriteLine("Git hooks installed.");
        return ExitCodes.Success;
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

    // --- sessions -------------------------------------------------------------

    private int RunSessions(string[] args)
    {
        if (args.Length == 0)
        {
            _err.WriteLine("error: 'sessions' requires a subcommand: backup | restore | list | providers.");
            _err.WriteLine("Run 'agent --help' for usage.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "backup" => RunSessionsBackup(rest),
            "restore" => RunSessionsRestore(rest),
            "list" or "ls" => RunSessionsList(rest),
            "providers" => RunSessionsProviders(rest),
            _ => UnknownSubcommand("sessions", sub),
        };
    }

    private int RunSessionsBackup(string[] args)
    {
        string? provider = null;
        string? project = null;
        string? output = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--project":
                    if (!TryValue(args, ref i, "--project", out project)) return ExitCodes.InvalidUsage;
                    break;
                case "--output":
                case "-o":
                    if (!TryValue(args, ref i, "--output", out output)) return ExitCodes.InvalidUsage;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("sessions backup", arg);
                    if (provider is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    provider = arg;
                    break;
            }
        }

        if (provider is null)
        {
            _err.WriteLine("error: 'sessions backup' requires a <provider>. Run 'agent sessions providers' to list them.");
            return ExitCodes.InvalidUsage;
        }

        var resolved = SessionProviderRegistry.Default.Resolve(provider);
        if (resolved is null)
        {
            return UnknownProvider(provider);
        }

        var env = SessionEnvironment.Current();
        var projectPath = ResolveProjectPath(project);
        var now = DateTimeOffset.Now;
        var outputPath = Path.GetFullPath(output ?? SessionBackupService.DefaultOutputName(resolved.Id, now), _workingDirectory);

        SessionBackupReport report;
        try
        {
            report = new SessionBackupService().Run(resolved, env, projectPath, outputPath, GetVersion(), now);
        }
        catch (Exception ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(new
            {
                provider = report.Provider,
                projectPath = report.ProjectPath,
                output = report.OutputPath,
                fileCount = report.FileCount,
                totalBytes = report.TotalBytes,
                experimental = report.Experimental,
                files = report.Files,
            }, JsonOptions));
        }
        else if (report.IsEmpty)
        {
            _out.WriteLine($"No {resolved.DisplayName} sessions found for {projectPath}.");
            if (resolved.Experimental)
            {
                _out.WriteLine($"note: {resolved.DisplayName} support is experimental; its on-disk layout may differ.");
            }
        }
        else
        {
            _out.WriteLine($"Backed up {report.FileCount} {resolved.DisplayName} session file(s) ({FormatBytes(report.TotalBytes)})");
            _out.WriteLine($"  project: {projectPath}");
            _out.WriteLine($"  archive: {report.OutputPath}");
            if (resolved.Experimental)
            {
                _out.WriteLine($"  note: {resolved.DisplayName} support is experimental; verify the archive contents.");
            }
        }

        return ExitCodes.Success;
    }

    private int RunSessionsRestore(string[] args)
    {
        string? archive = null;
        string? project = null;
        string? provider = null;
        var dryRun = false;
        var force = false;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--project":
                    if (!TryValue(args, ref i, "--project", out project)) return ExitCodes.InvalidUsage;
                    break;
                case "--provider":
                    if (!TryValue(args, ref i, "--provider", out provider)) return ExitCodes.InvalidUsage;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("sessions restore", arg);
                    if (archive is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    archive = arg;
                    break;
            }
        }

        if (archive is null)
        {
            _err.WriteLine("error: 'sessions restore' requires an <archive>.");
            return ExitCodes.InvalidUsage;
        }

        if (provider is not null && SessionProviderRegistry.Default.Resolve(provider) is null)
        {
            return UnknownProvider(provider);
        }

        var env = SessionEnvironment.Current();
        var projectPath = ResolveProjectPath(project);
        var archivePath = Path.GetFullPath(archive, _workingDirectory);

        SessionRestoreReport report;
        try
        {
            report = new SessionRestoreService().Run(archivePath, env, projectPath, force, dryRun, provider);
        }
        catch (SessionException ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitCodes.InvalidUsage;
        }
        catch (Exception ex)
        {
            _err.WriteLine($"error: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(new
            {
                provider = report.Provider,
                destProjectPath = report.DestProjectPath,
                dryRun = report.DryRun,
                pathsTranslated = report.PathsTranslated,
                written = report.Written,
                skipped = report.Skipped,
                source = new
                {
                    platform = report.Source.Platform,
                    pathStyle = report.Source.PathStyle,
                    projectPath = report.Source.ProjectPath,
                    homeDirectory = report.Source.HomeDirectory,
                },
                items = report.Items.Select(it => new
                {
                    path = it.ArchivePath,
                    dest = it.DestPath,
                    action = it.Action.ToString(),
                    rewritten = it.Rewritten,
                }),
            }, JsonOptions));
            return ExitCodes.Success;
        }

        _out.WriteLine(report.DryRun ? "Agent Sync sessions restore (dry-run)" : "Agent Sync sessions restore");
        _out.WriteLine();
        _out.WriteLine($"  provider: {report.Provider}");
        _out.WriteLine($"  from:     {report.Source.ProjectPath} ({report.Source.Platform}/{report.Source.PathStyle})");
        _out.WriteLine($"  to:       {report.DestProjectPath}");
        if (report.PathsTranslated)
        {
            _out.WriteLine("  paths:    embedded paths translated for this environment");
        }

        _out.WriteLine();
        foreach (var item in report.Items)
        {
            var verb = item.Action switch
            {
                RestoreAction.Written => report.DryRun ? "would write" : "written",
                RestoreAction.Overwritten => report.DryRun ? "would overwrite" : "overwritten",
                RestoreAction.SkippedExists => "skipped (exists)",
                RestoreAction.SkippedUnsafe => "skipped (unsafe)",
                _ => "skipped",
            };
            var tag = item.Rewritten ? " [rewritten]" : string.Empty;
            _out.WriteLine($"  {verb,-18} {item.ArchivePath}{tag}");
        }

        _out.WriteLine();
        _out.WriteLine($"{report.Written} written, {report.Skipped} skipped.");
        if (report.AnyBlocked && !force)
        {
            _out.WriteLine("Some files already existed and were left untouched. Re-run with --force to overwrite.");
        }

        return ExitCodes.Success;
    }

    private int RunSessionsList(string[] args)
    {
        string? only = null;
        string? project = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--project":
                    if (!TryValue(args, ref i, "--project", out project)) return ExitCodes.InvalidUsage;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("sessions list", arg);
                    if (only is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    only = arg;
                    break;
            }
        }

        if (only is not null && SessionProviderRegistry.Default.Resolve(only) is null)
        {
            return UnknownProvider(only);
        }

        var env = SessionEnvironment.Current();
        var projectPath = ResolveProjectPath(project);
        var providers = only is null
            ? SessionProviderRegistry.Default.Providers
            : new[] { SessionProviderRegistry.Default.Resolve(only)! };

        var rows = providers.Select(p =>
        {
            var count = SafeCount(p, env, projectPath);
            return (p, count);
        }).ToList();

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(rows.Select(r => new
            {
                id = r.p.Id,
                name = r.p.DisplayName,
                experimental = r.p.Experimental,
                sessions = r.count,
            }), JsonOptions));
            return ExitCodes.Success;
        }

        _out.WriteLine("Agent Sync sessions");
        _out.WriteLine();
        _out.WriteLine($"Project: {projectPath}");
        _out.WriteLine();
        foreach (var (p, count) in rows)
        {
            var flag = p.Experimental ? " (experimental)" : string.Empty;
            var summary = count < 0 ? "n/a" : $"{count} file(s)";
            _out.WriteLine($"  {p.Id,-10} {summary,-14} {p.DisplayName}{flag}");
        }

        return ExitCodes.Success;
    }

    private int RunSessionsProviders(string[] args)
    {
        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else return UnknownOption("sessions providers", arg);
        }

        var providers = SessionProviderRegistry.Default.Providers;
        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(providers.Select(p => new
            {
                id = p.Id,
                name = p.DisplayName,
                aliases = p.Aliases,
                experimental = p.Experimental,
            }), JsonOptions));
            return ExitCodes.Success;
        }

        _out.WriteLine("Supported session providers");
        _out.WriteLine();
        foreach (var p in providers)
        {
            var flag = p.Experimental ? " (experimental)" : string.Empty;
            _out.WriteLine($"  {p.Id,-10} {p.DisplayName}{flag}");
        }

        return ExitCodes.Success;
    }

    private static int SafeCount(ISessionProvider provider, SessionEnvironment env, string projectPath)
    {
        try
        {
            return provider.Collect(env, projectPath).Entries.Count;
        }
        catch
        {
            return -1;
        }
    }

    private string ResolveProjectPath(string? project)
    {
        if (!string.IsNullOrEmpty(project))
        {
            // An explicit project may be a foreign-OS absolute path (e.g. a Windows C:\... path
            // supplied while restoring on WSL); use it verbatim rather than resolving it against
            // the working directory. Only genuinely relative paths are made absolute.
            return LocationPath.Parse(project) is not null
                ? project
                : Path.GetFullPath(project, _workingDirectory);
        }

        return GitRepository.Discover(_workingDirectory) ?? Path.GetFullPath(_workingDirectory);
    }

    private int UnknownProvider(string provider)
    {
        var known = string.Join(", ", SessionProviderRegistry.Default.Providers.Select(p => p.Id));
        _err.WriteLine($"error: unknown session provider '{provider}'. Known providers: {known}.");
        return ExitCodes.InvalidUsage;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }

    // --- sync -----------------------------------------------------------------

    private int RunSync(string[] args)
    {
        var force = false;
        var check = false;
        var json = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--force":
                    force = true;
                    break;
                case "--check":
                    check = true;
                    break;
                case "--write":
                    check = false;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    return UnknownOption("sync", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var report = new SyncService(root).Run(force, dryRun: check);
        var subReport = new SubagentProjector(root).Sync(force, dryRun: check);

        if (json)
        {
            WriteSyncJson(report, subReport);
        }
        else
        {
            WriteSyncHuman(report, check);
            WriteSubagentSyncHuman(subReport, check);
        }

        if (!report.ConfigValid)
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        // In check mode, pending changes or manual edits are a non-zero (drift) result.
        if (check && (report.AnyChanges || report.AnyManualEdits || subReport.AnyChanges || subReport.AnySkippedManualEdits))
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        // In write mode, manual edits we refused to overwrite are a problem. Edits that
        // --force rewrote are not — the projection is back in sync.
        if (!check && (report.AnySkippedManualEdits || subReport.AnySkippedManualEdits))
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        return ExitCodes.Success;
    }

    private void WriteSubagentSyncHuman(SubagentSyncReport report, bool check)
    {
        if (report.Outcomes.Count == 0)
        {
            return;
        }

        _out.WriteLine();
        _out.WriteLine("Sub-agents:");
        foreach (var o in report.Outcomes)
        {
            var verb = o.Change switch
            {
                SubagentChange.Created => check ? "would create" : "created",
                SubagentChange.Updated => check ? "would update" : "updated",
                SubagentChange.SkippedManualEdit => "manual edit (skipped)",
                _ => "up to date",
            };
            _out.WriteLine($"  {verb,-22} {o.Path}");
        }

        if (report.AnySkippedManualEdits)
        {
            _out.WriteLine("  Some sub-agent files were manually edited and left untouched. Use --force to overwrite.");
        }
    }

    private void WriteSyncHuman(SyncReport report, bool check)
    {
        _out.WriteLine(check ? "Agent Sync sync (check)" : "Agent Sync sync");
        _out.WriteLine();

        if (!report.ConfigValid)
        {
            foreach (var m in report.Validation.Messages.Where(m => m.Severity == ValidationSeverity.Error))
            {
                _out.WriteLine($"  [ERROR] {m.Message}");
            }
            _out.WriteLine();
            _out.WriteLine("Cannot sync: configuration is invalid. Run 'agent validate'.");
            return;
        }

        if (report.Outcomes.Count == 0)
        {
            _out.WriteLine("No projections configured.");
            return;
        }

        foreach (var o in report.Outcomes)
        {
            var verb = o.Change switch
            {
                ProjectionChange.Created => check ? "would create" : "created",
                ProjectionChange.Updated => check ? "would update" : "updated",
                ProjectionChange.SkippedManualEdit => "manual edit (skipped)",
                _ => "up to date",
            };
            _out.WriteLine($"  {verb,-22} {o.Projection.RelativePath} ({o.Projection.TargetId})");
        }

        if (report.AnySkippedManualEdits)
        {
            _out.WriteLine();
            _out.WriteLine("Some projections were manually edited and left untouched. Use --force to overwrite.");
        }
    }

    private void WriteSyncJson(SyncReport report, SubagentSyncReport subReport)
    {
        var payload = new
        {
            configValid = report.ConfigValid,
            dryRun = report.DryRun,
            anyChanges = report.AnyChanges || subReport.AnyChanges,
            anyManualEdits = report.AnyManualEdits,
            anySkippedManualEdits = report.AnySkippedManualEdits || subReport.AnySkippedManualEdits,
            outcomes = report.Outcomes.Select(o => new
            {
                skill = o.Projection.SkillId,
                target = o.Projection.TargetId,
                path = o.Projection.RelativePath,
                change = o.Change.ToString(),
                manualEdit = o.ManualEditDetected,
            }),
            subagents = subReport.Outcomes.Select(o => new
            {
                id = o.Id,
                path = o.Path,
                change = o.Change.ToString(),
                manualEdit = o.ManualEditDetected,
            }),
        };
        _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    // --- diff -----------------------------------------------------------------

    private int RunDiff(string[] args)
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
                    return UnknownOption("diff", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var report = new DiffService(root).Run();

        if (json)
        {
            var payload = new
            {
                configValid = report.ConfigValid,
                hasDifferences = report.HasDifferences,
                entries = report.Entries.Select(e => new
                {
                    skill = e.SkillId,
                    target = e.TargetId,
                    path = e.RelativePath,
                    kind = e.Kind.ToString(),
                    diff = e.Diff,
                }),
            };
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else if (!report.ConfigValid)
        {
            _out.WriteLine("Cannot diff: configuration is invalid. Run 'agent validate'.");
        }
        else if (!report.HasDifferences)
        {
            _out.WriteLine("No differences. All projections are in sync.");
        }
        else
        {
            foreach (var e in report.Entries)
            {
                _out.WriteLine($"# {e.RelativePath} ({e.TargetId}) — {e.Kind}");
                _out.Write(e.Diff);
                _out.WriteLine();
            }
        }

        if (!report.ConfigValid)
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        return report.HasDifferences ? ExitCodes.DriftOrValidationFailed : ExitCodes.Success;
    }

    // --- validate -------------------------------------------------------------

    private int RunValidate(string[] args)
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
                    return UnknownOption("validate", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var workspace = WorkspaceLoader.Load(root);
        var messages = workspace.Validation.Messages;

        if (json)
        {
            var payload = new
            {
                valid = workspace.IsValid,
                skills = workspace.Skills.Count,
                messages = messages.Select(m => new
                {
                    code = m.Code,
                    severity = m.Severity.ToString().ToLowerInvariant(),
                    message = m.Message,
                    source = m.Source,
                }),
            };
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            _out.WriteLine("Agent Sync validate");
            _out.WriteLine();
            _out.WriteLine($"Skills: {workspace.Skills.Count}");
            _out.WriteLine();

            if (messages.Count == 0)
            {
                _out.WriteLine("Configuration and skills are valid.");
            }
            else
            {
                foreach (var m in messages)
                {
                    var marker = m.Severity == ValidationSeverity.Error ? "ERROR" : "WARN";
                    var where = string.IsNullOrEmpty(m.Source) ? "" : $"{m.Source}: ";
                    _out.WriteLine($"  [{marker}] {where}{m.Message}");
                }

                _out.WriteLine();
                _out.WriteLine(workspace.IsValid
                    ? "Valid (with warnings)."
                    : "Validation failed.");
            }
        }

        return workspace.IsValid ? ExitCodes.Success : ExitCodes.DriftOrValidationFailed;
    }

    // --- import ---------------------------------------------------------------

    private int RunImport(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h") return SubUsage("import");

        if (args.Length == 0)
        {
            _err.WriteLine("error: 'import' requires a subcommand: skill | agent | subagent.");
            _err.WriteLine("Run 'agent --help' for usage.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "skill" => RunImportSkill(rest),
            "agent" => RunImportAgent(rest),
            "subagent" => RunImportSubagent(rest),
            _ => UnknownSubcommand("import", sub),
        };
    }

    private int RunImportSkill(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("import skill");

        string? path = null;
        string? id = null;
        string? name = null;
        var targets = new List<string>();
        var force = false;
        var dryRun = false;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--id":
                    if (!TryValue(args, ref i, "--id", out id)) return ExitCodes.InvalidUsage;
                    break;
                case "--name":
                    if (!TryValue(args, ref i, "--name", out name)) return ExitCodes.InvalidUsage;
                    break;
                case "--target":
                    if (!TryValue(args, ref i, "--target", out var t)) return ExitCodes.InvalidUsage;
                    targets.Add(t!);
                    break;
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        return UnknownOption("import skill", arg);
                    }

                    if (path is not null)
                    {
                        _err.WriteLine($"error: unexpected argument '{arg}'.");
                        return ExitCodes.InvalidUsage;
                    }

                    path = arg;
                    break;
            }
        }

        if (path is null)
        {
            _err.WriteLine("error: 'import skill' requires a <path>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var options = new SkillImportOptions(id, name, targets.Count > 0 ? targets : null, force, dryRun);
        var report = new SkillImporter(root).Import(path, options);

        return RenderImport(report, json);
    }

    private int RunImportAgent(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("import agent");

        string? path = null;
        string? type = null;
        string? id = null;
        var split = "file";
        var includeGenerated = false;
        var force = false;
        var dryRun = false;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--type":
                    if (!TryValue(args, ref i, "--type", out type)) return ExitCodes.InvalidUsage;
                    break;
                case "--split":
                    if (!TryValue(args, ref i, "--split", out split)) return ExitCodes.InvalidUsage;
                    break;
                case "--id":
                    if (!TryValue(args, ref i, "--id", out id)) return ExitCodes.InvalidUsage;
                    break;
                case "--include-generated":
                    includeGenerated = true;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("import agent", arg);
                    if (path is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    path = arg;
                    break;
            }
        }

        if (path is null)
        {
            _err.WriteLine("error: 'import agent' requires a <path>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var options = new AgentImportOptions(type, split!, id, includeGenerated, force, dryRun);
        var report = new AgentImporter(root).Import(path, options);
        return RenderImport(report, json);
    }

    private int RenderImport(ImportReport report, bool json)
    {
        if (json)
        {
            WriteImportJson(report);
        }
        else
        {
            WriteImportHuman(report);
        }

        return ImportExitCode(report.Status);
    }

    private void WriteImportHuman(ImportReport report)
    {
        _out.WriteLine(report.DryRun ? "Agent Sync import (dry-run)" : "Agent Sync import");
        _out.WriteLine();

        if (report.Items.Count == 0)
        {
            _out.WriteLine($"  [ERROR] {report.Message}");
            return;
        }

        foreach (var item in report.Items)
        {
            var verb = item.Action switch
            {
                ImportAction.Create => report.DryRun ? "would create" : "created",
                ImportAction.Overwrite => report.DryRun ? "would overwrite" : "overwritten",
                _ => "skipped",
            };
            _out.WriteLine($"  {verb,-16} {item.Id} ({item.Name})");
            _out.WriteLine($"    from {item.SourceRelativePath}");
            _out.WriteLine($"    -> {item.SkillYamlPath}");
            _out.WriteLine($"    -> {item.SkillMdPath}");
            if (item.Note is not null)
            {
                _out.WriteLine($"    note: {item.Note}");
            }

            foreach (var m in item.Validation)
            {
                var marker = m.Severity == ValidationSeverity.Error ? "ERROR" : "WARN";
                _out.WriteLine($"    [{marker}] {m.Message}");
            }
        }

        _out.WriteLine();
        if (report.AnyWritten && !report.DryRun)
        {
            _out.WriteLine("Run 'agent sync' to project the imported skill(s) into your targets.");
        }
        else if (report.DryRun && report.Items.Any(i => i.Action != ImportAction.Skip))
        {
            _out.WriteLine("Dry run: nothing was written. Re-run without --dry-run to import.");
        }
    }

    private void WriteImportJson(ImportReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            dryRun = report.DryRun,
            message = report.Message,
            items = report.Items.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                description = i.Description,
                action = i.Action.ToString(),
                source = i.SourceRelativePath,
                skillYaml = i.SkillYamlPath,
                skillMd = i.SkillMdPath,
                note = i.Note,
                validation = i.Validation.Select(m => new
                {
                    code = m.Code,
                    severity = m.Severity.ToString().ToLowerInvariant(),
                    message = m.Message,
                }),
            }),
        };
        _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static int ImportExitCode(ImportStatus status) => status switch
    {
        ImportStatus.Ok => ExitCodes.Success,
        ImportStatus.Problem => ExitCodes.DriftOrValidationFailed,
        ImportStatus.UnsafePath => ExitCodes.EnvironmentProblem,
        _ => ExitCodes.InvalidUsage,
    };

    // --- skill CRUD -----------------------------------------------------------

    private int RunSkill(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h") return SubUsage("skill");

        if (args.Length == 0)
        {
            _err.WriteLine("error: 'skill' requires a subcommand: add | edit | delete | list | show.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "add" => RunSkillAdd(rest),
            "edit" => RunSkillEdit(rest),
            "delete" => RunSkillDelete(rest),
            "list" => RunSkillList(rest),
            "show" => RunSkillShow(rest),
            _ => UnknownSubcommand("skill", sub),
        };
    }

    private int RunSkillAdd(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("skill add");

        string? id = null;
        string? name = null;
        string? description = null;
        string? version = null;
        var targets = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--name":
                    if (!TryValue(args, ref i, "--name", out name)) return ExitCodes.InvalidUsage;
                    break;
                case "--description":
                    if (!TryValue(args, ref i, "--description", out description)) return ExitCodes.InvalidUsage;
                    break;
                case "--version":
                    if (!TryValue(args, ref i, "--version", out version)) return ExitCodes.InvalidUsage;
                    break;
                case "--target":
                    if (!TryValue(args, ref i, "--target", out var t)) return ExitCodes.InvalidUsage;
                    targets.Add(t!);
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("skill add", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'skill add' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var result = new SkillWriter(root).Add(id, name, description, version, targets.Count > 0 ? targets : null);
        return RenderAuthoring($"skill add {id}", result);
    }

    private int RunSkillEdit(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("skill edit");

        string? id = null;
        string? name = null;
        string? description = null;
        string? version = null;
        string? bodyFile = null;
        var enable = new List<string>();
        var disable = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--name":
                    if (!TryValue(args, ref i, "--name", out name)) return ExitCodes.InvalidUsage;
                    break;
                case "--description":
                    if (!TryValue(args, ref i, "--description", out description)) return ExitCodes.InvalidUsage;
                    break;
                case "--version":
                    if (!TryValue(args, ref i, "--version", out version)) return ExitCodes.InvalidUsage;
                    break;
                case "--body-file":
                    if (!TryValue(args, ref i, "--body-file", out bodyFile)) return ExitCodes.InvalidUsage;
                    break;
                case "--enable":
                    if (!TryValue(args, ref i, "--enable", out var en)) return ExitCodes.InvalidUsage;
                    enable.Add(en!);
                    break;
                case "--disable":
                    if (!TryValue(args, ref i, "--disable", out var di)) return ExitCodes.InvalidUsage;
                    disable.Add(di!);
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("skill edit", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'skill edit' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var result = new SkillWriter(root).Edit(id, name, description, version, bodyFile,
            enable.Count > 0 ? enable : null, disable.Count > 0 ? disable : null);
        return RenderAuthoring($"skill edit {id}", result);
    }

    private int RunSkillDelete(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("skill delete");

        string? id = null;
        var force = false;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("skill delete", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'skill delete' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var result = new SkillWriter(root).Delete(id, force, dryRun);
        return RenderAuthoring($"skill delete {id}", result);
    }

    private int RunSkillList(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("skill list");

        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else return UnknownOption("skill list", arg);
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var workspace = WorkspaceLoader.Load(root);
        var skills = workspace.Skills.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();

        if (json)
        {
            var payload = skills.Select(s => new
            {
                id = s.Id,
                name = s.Manifest.Name,
                description = s.Manifest.Description,
                version = s.Manifest.Version,
                targets = s.EnabledTargets.OrderBy(t => t, StringComparer.Ordinal).ToArray(),
            });
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            _out.WriteLine("Agent Sync skills");
            _out.WriteLine();
            if (skills.Count == 0)
            {
                _out.WriteLine("No skills defined. Add one with 'agent skill add <id> --name ... --description ...'.");
            }
            else
            {
                foreach (var s in skills)
                {
                    _out.WriteLine($"  {s.Id,-24} {s.Manifest.Name}  ({s.EnabledTargets.Count()} target(s))");
                }
            }
        }

        return ExitCodes.Success;
    }

    private int RunSkillShow(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("skill show");

        string? id = null;
        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else if (arg.StartsWith('-')) return UnknownOption("skill show", arg);
            else if (id is null) id = arg;
            else { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'skill show' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var workspace = WorkspaceLoader.Load(root);
        var skill = workspace.Skills.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

        if (skill is null)
        {
            _err.WriteLine($"error: skill '{id}' not found.");
            return ExitCodes.DriftOrValidationFailed;
        }

        if (json)
        {
            var payload = new
            {
                id = skill.Id,
                name = skill.Manifest.Name,
                description = skill.Manifest.Description,
                version = skill.Manifest.Version,
                targets = skill.EnabledTargets.OrderBy(t => t, StringComparer.Ordinal).ToArray(),
                body = skill.Body,
            };
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            _out.WriteLine($"Skill: {skill.Id}");
            _out.WriteLine($"  name:        {skill.Manifest.Name}");
            _out.WriteLine($"  description: {skill.Manifest.Description}");
            _out.WriteLine($"  version:     {skill.Manifest.Version}");
            _out.WriteLine($"  targets:     {string.Join(", ", skill.EnabledTargets.OrderBy(t => t, StringComparer.Ordinal))}");
            _out.WriteLine($"  body:        {skill.Body.Length} chars");
        }

        return ExitCodes.Success;
    }

    private int RenderAuthoring(string title, AuthoringResult result)
    {
        _out.WriteLine(result.DryRun ? $"Agent Sync {title} (dry-run)" : $"Agent Sync {title}");
        _out.WriteLine();

        foreach (var change in result.Changes)
        {
            _out.WriteLine($"  {change}");
        }

        foreach (var m in result.Validation)
        {
            var marker = m.Severity == ValidationSeverity.Error ? "ERROR" : "WARN";
            _out.WriteLine($"  [{marker}] {m.Message}");
        }

        if (result.Message is not null)
        {
            if (result.Changes.Count > 0) _out.WriteLine();
            _out.WriteLine(result.Message);
        }

        if (result.Status == AuthoringStatus.Ok && result.RecommendSync && !result.DryRun)
        {
            _out.WriteLine();
            _out.WriteLine("Run 'agent sync' to update your projections.");
        }

        return AuthoringExitCode(result.Status);
    }

    private static int AuthoringExitCode(AuthoringStatus status) => status switch
    {
        AuthoringStatus.Ok => ExitCodes.Success,
        AuthoringStatus.InvalidUsage => ExitCodes.InvalidUsage,
        AuthoringStatus.UnsafePath => ExitCodes.EnvironmentProblem,
        _ => ExitCodes.DriftOrValidationFailed,
    };

    // --- ui (launcher) --------------------------------------------------------

    private int RunUi(string[] args)
    {
        var noOpen = false;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--no-open":
                    noOpen = true;
                    break;
                default:
                    return UnknownOption("ui", arg);
            }
        }

        var root = GitRepository.Discover(_workingDirectory);
        var repoPath = root ?? _workingDirectory;
        if (root is null)
        {
            _out.WriteLine("note: not inside a Git repository; the UI will open the current folder.");
        }

        var executable = _uiLauncher.Locate();
        if (executable is null)
        {
            // First run: install the optional local web UI on the user's behalf rather than
            // making them download and place it by hand.
            _out.WriteLine("Agent Sync UI is not installed; setting it up now...");
            executable = _uiInstaller.Install(GetVersion(), _out, _err);
        }

        if (executable is null)
        {
            _out.WriteLine("Agent Sync UI is not installed.");
            _out.WriteLine("The headless CLI is working.");
            _out.WriteLine("Install the optional local web UI as a .NET tool:");
            _out.WriteLine("  dotnet tool install --global AgentSync.Ui");
            _out.WriteLine("or download agent-sync-ui from GitHub Releases and put it on PATH:");
            _out.WriteLine("  https://github.com/nova-globen/agent/releases");
            return ExitCodes.EnvironmentProblem;
        }

        var port = UiSession.FindFreePort();
        var token = UiSession.NewToken();

        _out.WriteLine($"Launching Agent Sync UI for {repoPath}...");

        if (!_uiLauncher.Launch(new UiLaunchRequest(executable, repoPath, port, token)))
        {
            _err.WriteLine("error: failed to launch the Agent Sync UI.");
            return ExitCodes.EnvironmentProblem;
        }

        // Confirm the host actually started (the chosen port may have been taken before it
        // bound, or the process may have failed) before pointing the user at it.
        if (!_readinessProbe.WaitUntilReady(port, UiReadyTimeout))
        {
            _err.WriteLine("error: the Agent Sync UI did not become ready in time.");
            _err.WriteLine("The port may be in use, or the UI failed to start. Try again.");
            return ExitCodes.EnvironmentProblem;
        }

        // The token URL is what authenticates the first navigation; the host then exchanges
        // it into an HttpOnly cookie and strips it from the address bar.
        var tokenUrl = UiSession.Url(port, token);

        if (!noOpen && _browserLauncher.Open(tokenUrl))
        {
            // The browser already carries the token; print only the clean loopback URL.
            _out.WriteLine($"Opened {UiSession.BaseUrl(port)}");
        }
        else
        {
            // Fall back to printing the token URL (on stdout, never stderr) so the user can
            // open it manually.
            _out.WriteLine($"Open {tokenUrl}");
        }

        return ExitCodes.Success;
    }

    // --- target CRUD ----------------------------------------------------------

    private int RunTarget(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h") return SubUsage("target");

        if (args.Length == 0)
        {
            _err.WriteLine("error: 'target' requires a subcommand: add | edit | delete | list | show.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "add" => RunTargetAdd(rest),
            "edit" => RunTargetEdit(rest),
            "delete" => RunTargetDelete(rest),
            "list" => RunTargetList(rest),
            "show" => RunTargetShow(rest),
            _ => UnknownSubcommand("target", sub),
        };
    }

    private int RunTargetAdd(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("target add");

        string? id = null;
        string? path = null;
        var enabled = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--path":
                    if (!TryValue(args, ref i, "--path", out path)) return ExitCodes.InvalidUsage;
                    break;
                case "--enabled":
                    if (!TryValue(args, ref i, "--enabled", out var e)) return ExitCodes.InvalidUsage;
                    if (!TryParseBool(e!, out enabled)) return ExitCodes.InvalidUsage;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("target add", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'target add' requires a <target-id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        return RenderAuthoring($"target add {id}", new TargetWriter(root).Add(id, path, enabled));
    }

    private int RunTargetEdit(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("target edit");

        string? id = null;
        string? path = null;
        bool? enabled = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--path":
                    if (!TryValue(args, ref i, "--path", out path)) return ExitCodes.InvalidUsage;
                    break;
                case "--enabled":
                    if (!TryValue(args, ref i, "--enabled", out var e)) return ExitCodes.InvalidUsage;
                    if (!TryParseBool(e!, out var b)) return ExitCodes.InvalidUsage;
                    enabled = b;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("target edit", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'target edit' requires a <target-id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        return RenderAuthoring($"target edit {id}", new TargetWriter(root).Edit(id, path, enabled));
    }

    private int RunTargetDelete(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("target delete");

        string? id = null;
        var force = false;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("target delete", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'target delete' requires a <target-id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        return RenderAuthoring($"target delete {id}", new TargetWriter(root).Delete(id, force, dryRun));
    }

    // The sub-agent projection target. Unlike the entries in TargetIds.Ordered it is not
    // configured per-target in agent.yaml; `sync` always projects sub-agents to this path.
    private const string SubagentTargetId = "claude_agent";
    private const string SubagentTargetPath = ".claude/agents/<id>.md";

    private static bool SubagentsExist(string root)
        => SubagentFiles.LoadAll(new RepoLayout(root)).Count > 0;

    private int RunTargetList(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("target list");

        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else return UnknownOption("target list", arg);
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var workspace = WorkspaceLoader.Load(root);
        var config = workspace.Config;

        if (json)
        {
            var payload = TargetIds.Ordered.Select(id =>
            {
                var setting = config is not null && config.Targets.TryGetValue(id, out var found) ? found : null;
                return new
                {
                    id,
                    configured = setting is not null,
                    enabled = setting?.Enabled ?? false,
                    path = setting?.Path,
                };
            }).ToList();

            // Sub-agents are projected by `sync` to .claude/agents/<id>.md but are managed via
            // `agent subagent`, not the per-target config — surface them so the list is complete.
            payload.Add(new
            {
                id = SubagentTargetId,
                configured = SubagentsExist(root),
                enabled = true,
                path = (string?)SubagentTargetPath,
            });
            _out.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            _out.WriteLine("Agent Sync targets");
            _out.WriteLine();
            foreach (var id in TargetIds.Ordered)
            {
                var setting = config is not null && config.Targets.TryGetValue(id, out var found) ? found : null;
                var state = setting is null ? "not configured"
                    : setting.Enabled ? $"enabled  -> {setting.Path}"
                    : "disabled";
                _out.WriteLine($"  {id,-14} {state}");
            }

            _out.WriteLine($"  {SubagentTargetId,-14} sub-agents -> {SubagentTargetPath} (managed via 'agent subagent')");
        }

        return ExitCodes.Success;
    }

    private int RunTargetShow(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("target show");

        string? id = null;
        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else if (arg.StartsWith('-')) return UnknownOption("target show", arg);
            else if (id is null) id = arg;
            else { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'target show' requires a <target-id>.");
            return ExitCodes.InvalidUsage;
        }

        if (!TargetIds.IsKnown(id))
        {
            _err.WriteLine($"error: unknown target '{id}'.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var workspace = WorkspaceLoader.Load(root);
        var setting = workspace.Config is not null && workspace.Config.Targets.TryGetValue(id, out var found) ? found : null;

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(new
            {
                id,
                configured = setting is not null,
                enabled = setting?.Enabled ?? false,
                path = setting?.Path,
            }, JsonOptions));
        }
        else
        {
            _out.WriteLine($"Target: {id}");
            _out.WriteLine($"  configured: {(setting is not null ? "yes" : "no")}");
            _out.WriteLine($"  enabled:    {setting?.Enabled ?? false}");
            _out.WriteLine($"  path:       {setting?.Path ?? "(none)"}");
        }

        return ExitCodes.Success;
    }

    // --- subagent CRUD --------------------------------------------------------

    private int RunSubagent(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h") return SubUsage("subagent");

        if (args.Length == 0)
        {
            _err.WriteLine("error: 'subagent' requires a subcommand: add | edit | delete | list | show.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "add" => RunSubagentAdd(rest),
            "edit" => RunSubagentEdit(rest),
            "delete" => RunSubagentDelete(rest),
            "list" => RunSubagentList(rest),
            "show" => RunSubagentShow(rest),
            _ => UnknownSubcommand("subagent", sub),
        };
    }

    private int RunSubagentAdd(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("subagent add");

        string? id = null;
        string? name = null;
        string? description = null;
        string? model = null;
        string? color = null;
        var tools = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--name":
                    if (!TryValue(args, ref i, "--name", out name)) return ExitCodes.InvalidUsage;
                    break;
                case "--description":
                    if (!TryValue(args, ref i, "--description", out description)) return ExitCodes.InvalidUsage;
                    break;
                case "--model":
                    if (!TryValue(args, ref i, "--model", out model)) return ExitCodes.InvalidUsage;
                    break;
                case "--color":
                    if (!TryValue(args, ref i, "--color", out color)) return ExitCodes.InvalidUsage;
                    break;
                case "--tool":
                    if (!TryValue(args, ref i, "--tool", out var t)) return ExitCodes.InvalidUsage;
                    tools.Add(t!);
                    break;
                case "--tools":
                    if (!TryValue(args, ref i, "--tools", out var ts)) return ExitCodes.InvalidUsage;
                    tools.AddRange(ts!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("subagent add", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'subagent add' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var result = new SubagentWriter(root).Add(id, name, description, model, color, tools.Count > 0 ? tools : null);
        return RenderAuthoring($"subagent add {id}", result);
    }

    private int RunSubagentEdit(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("subagent edit");

        string? id = null;
        string? name = null;
        string? description = null;
        string? model = null;
        string? color = null;
        string? bodyFile = null;
        List<string>? tools = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--name":
                    if (!TryValue(args, ref i, "--name", out name)) return ExitCodes.InvalidUsage;
                    break;
                case "--description":
                    if (!TryValue(args, ref i, "--description", out description)) return ExitCodes.InvalidUsage;
                    break;
                case "--model":
                    if (!TryValue(args, ref i, "--model", out model)) return ExitCodes.InvalidUsage;
                    break;
                case "--color":
                    if (!TryValue(args, ref i, "--color", out color)) return ExitCodes.InvalidUsage;
                    break;
                case "--body-file":
                    if (!TryValue(args, ref i, "--body-file", out bodyFile)) return ExitCodes.InvalidUsage;
                    break;
                case "--tool":
                    if (!TryValue(args, ref i, "--tool", out var t)) return ExitCodes.InvalidUsage;
                    (tools ??= new List<string>()).Add(t!);
                    break;
                case "--tools":
                    if (!TryValue(args, ref i, "--tools", out var ts)) return ExitCodes.InvalidUsage;
                    (tools ??= new List<string>()).AddRange(ts!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("subagent edit", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'subagent edit' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var result = new SubagentWriter(root).Edit(id, name, description, model, color, bodyFile, tools);
        return RenderAuthoring($"subagent edit {id}", result);
    }

    private int RunSubagentDelete(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("subagent delete");

        string? id = null;
        var force = false;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("subagent delete", arg);
                    if (id is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    id = arg;
                    break;
            }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'subagent delete' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        return RenderAuthoring($"subagent delete {id}", new SubagentWriter(root).Delete(id, force, dryRun));
    }

    private int RunSubagentList(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("subagent list");

        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else return UnknownOption("subagent list", arg);
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var agents = SubagentFiles.LoadAll(new RepoLayout(root));

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(agents.Select(a => new
            {
                id = a.Id,
                name = a.DisplayName,
                description = a.Manifest.Description,
                model = a.Manifest.Model,
                color = a.Manifest.Color,
                tools = a.Manifest.Tools,
            }), JsonOptions));
            return ExitCodes.Success;
        }

        _out.WriteLine("Agent Sync sub-agents");
        _out.WriteLine();
        if (agents.Count == 0)
        {
            _out.WriteLine("No sub-agents defined. Add one with 'agent subagent add <id> --description ...'.");
        }
        else
        {
            foreach (var a in agents)
            {
                var tools = a.Manifest.Tools.Count == 0 ? "all tools" : $"{a.Manifest.Tools.Count} tool(s)";
                _out.WriteLine($"  {a.Id,-24} {a.DisplayName}  ({tools})");
            }
        }

        return ExitCodes.Success;
    }

    private int RunSubagentShow(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("subagent show");

        string? id = null;
        var json = false;
        foreach (var arg in args)
        {
            if (arg == "--json") json = true;
            else if (arg.StartsWith('-')) return UnknownOption("subagent show", arg);
            else if (id is null) id = arg;
            else { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
        }

        if (id is null)
        {
            _err.WriteLine("error: 'subagent show' requires an <id>.");
            return ExitCodes.InvalidUsage;
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;
        var agent = SubagentFiles.LoadAll(new RepoLayout(root)).FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));
        if (agent is null)
        {
            _err.WriteLine($"error: sub-agent '{id}' not found.");
            return ExitCodes.DriftOrValidationFailed;
        }

        if (json)
        {
            _out.WriteLine(JsonSerializer.Serialize(new
            {
                id = agent.Id,
                name = agent.DisplayName,
                description = agent.Manifest.Description,
                model = agent.Manifest.Model,
                color = agent.Manifest.Color,
                tools = agent.Manifest.Tools,
                body = agent.Body,
            }, JsonOptions));
        }
        else
        {
            _out.WriteLine($"Sub-agent: {agent.Id}");
            _out.WriteLine($"  name:        {agent.DisplayName}");
            _out.WriteLine($"  description: {agent.Manifest.Description}");
            _out.WriteLine($"  model:       {agent.Manifest.Model ?? "(inherit)"}");
            _out.WriteLine($"  color:       {agent.Manifest.Color ?? "(none)"}");
            _out.WriteLine($"  tools:       {(agent.Manifest.Tools.Count == 0 ? "(all)" : string.Join(", ", agent.Manifest.Tools))}");
            _out.WriteLine($"  body:        {agent.Body.Length} chars");
        }

        return ExitCodes.Success;
    }

    private int RunImportSubagent(string[] args)
    {
        if (WantsHelp(args)) return SubUsage("import subagent");

        string? path = null;
        string? id = null;
        var force = false;
        var dryRun = false;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--id":
                    if (!TryValue(args, ref i, "--id", out id)) return ExitCodes.InvalidUsage;
                    break;
                case "--force":
                    force = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (arg.StartsWith('-')) return UnknownOption("import subagent", arg);
                    if (path is not null) { _err.WriteLine($"error: unexpected argument '{arg}'."); return ExitCodes.InvalidUsage; }
                    path = arg;
                    break;
            }
        }

        var root = GitRepository.Discover(_workingDirectory) ?? _workingDirectory;

        if (path is null)
        {
            // No <path>: default to discovering the conventional .claude/agents/ directory.
            var defaultDir = Path.Combine(root, RepoLayout.ClaudeAgentsDir);
            if (!Directory.Exists(defaultDir))
            {
                _err.WriteLine($"error: no <path> given and '{RepoLayout.ClaudeAgentsDir}' does not exist. Pass a file or folder to import.");
                return ExitCodes.InvalidUsage;
            }

            path = defaultDir;
        }

        var report = new SubagentImporter(root).Import(path, new SubagentImportOptions(id, force, dryRun));

        // Reconcile projections for sub-agents we just adopted from their existing .claude/agents/
        // files so `agent status` does not immediately flag them as manually edited.
        if (!dryRun && report.AnyWritten)
        {
            var importedIds = report.Items
                .Where(i => i.Action is ImportAction.Create or ImportAction.Overwrite)
                .Select(i => i.Id);
            new SubagentProjector(root).ReconcileImported(importedIds);
        }

        return RenderImport(report, json);
    }

    private bool TryParseBool(string value, out bool result)
    {
        switch (value.ToLowerInvariant())
        {
            case "true":
                result = true;
                return true;
            case "false":
                result = false;
                return true;
            default:
                result = false;
                _err.WriteLine($"error: expected true or false, got '{value}'.");
                return false;
        }
    }

    // --- helpers --------------------------------------------------------------

    private bool TryValue(string[] args, ref int i, string option, out string? value)
    {
        if (i + 1 >= args.Length)
        {
            value = null;
            _err.WriteLine($"error: option '{option}' requires a value.");
            return false;
        }

        value = args[++i];
        return true;
    }

    private int UnknownSubcommand(string command, string sub)
    {
        _err.WriteLine($"error: unknown '{command}' subcommand '{sub}'.");
        _err.WriteLine("Run 'agent --help' for usage.");
        return ExitCodes.InvalidUsage;
    }

    private int UnknownOption(string command, string option)
    {
        _err.WriteLine($"error: unknown option '{option}' for '{command}'.");
        _err.WriteLine("Run 'agent --help' for usage.");
        return ExitCodes.InvalidUsage;
    }

    private static bool WantsHelp(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg is "--help" or "-h") return true;
        }

        return false;
    }

    /// <summary>Prints per-subcommand usage (options list) to stdout and returns success.</summary>
    private int SubUsage(string command)
    {
        if (SubcommandUsage.TryGetValue(command, out var lines))
        {
            foreach (var line in lines) _out.WriteLine(line);
        }
        else
        {
            _out.WriteLine($"agent {command}");
            _out.WriteLine("Run 'agent --help' for usage.");
        }

        return ExitCodes.Success;
    }

    private static readonly IReadOnlyDictionary<string, string[]> SubcommandUsage = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["import"] = new[]
        {
            "Usage: agent import <skill|agent|subagent> <path> [options]",
            "  Adopt existing files into canonical .agent/ sources. Run 'agent import <sub> --help' for options.",
        },
        ["import skill"] = new[]
        {
            "Usage: agent import skill <path> [options]",
            "  Import a SKILL.md or skill folder into .agent/skills (pass a folder to import every *.md inside).",
            "",
            "Options:",
            "  --id <id>          Override the inferred skill id.",
            "  --name <name>      Override the inferred display name.",
            "  --target <id>      Enable only this target (repeatable); default enables all.",
            "  --force            Overwrite an existing canonical skill.",
            "  --dry-run          Preview without writing.",
            "  --json             Emit JSON.",
        },
        ["import agent"] = new[]
        {
            "Usage: agent import agent <path> [options]",
            "  Import an existing instruction file/folder (AGENTS.md, CLAUDE.md, Cursor, ...) into .agent/skills.",
            "",
            "Options:",
            "  --type <type>      Source type override (auto-detected by default).",
            "  --split <mode>     How to split the source into skills: file | heading.",
            "  --id <id>          Override the inferred skill id.",
            "  --include-generated  Import content inside agent-sync markers too.",
            "  --force            Overwrite an existing canonical skill.",
            "  --dry-run          Preview without writing.",
            "  --json             Emit JSON.",
        },
        ["import subagent"] = new[]
        {
            "Usage: agent import subagent [path] [options]",
            "  Import existing sub-agent files (.claude/agents/*.md) into .agent/agents (pass a folder",
            "  for all). With no path, discovers and imports everything under .claude/agents/.",
            "",
            "Options:",
            "  --id <id>          Override the inferred sub-agent id.",
            "  --force            Overwrite an existing canonical sub-agent.",
            "  --dry-run          Preview without writing.",
            "  --json             Emit JSON.",
        },
        ["skill"] = new[]
        {
            "Usage: agent skill <add|edit|delete|list|show> [options]",
            "  Manage canonical skills under .agent/skills. Run 'agent skill <sub> --help' for options.",
        },
        ["skill add"] = new[]
        {
            "Usage: agent skill add <id> [options]",
            "  Scaffold a new canonical skill under .agent/skills/<id>.",
            "",
            "Options:",
            "  --name <name>        Display name (required).",
            "  --description <d>    Trigger description shown to agents (required).",
            "  --version <v>        Skill version (default 0.1.0).",
            "  --target <id>        Enable only this target (repeatable); default enables all targets.",
        },
        ["skill edit"] = new[]
        {
            "Usage: agent skill edit <id> [options]",
            "  Change an existing canonical skill. At least one option is required.",
            "",
            "Options:",
            "  --name <name>        Set the display name.",
            "  --description <d>    Set the trigger description.",
            "  --version <v>        Set the version.",
            "  --body-file <path>   Replace SKILL.md body from a file (absolute or relative paths allowed).",
            "  --enable <id>        Enable a target (repeatable).",
            "  --disable <id>       Disable a target (repeatable).",
        },
        ["skill delete"] = new[]
        {
            "Usage: agent skill delete <id> [options]",
            "",
            "Options:",
            "  --force              Delete even when projections exist; prunes lockfile entries.",
            "  --dry-run            Preview without writing.",
        },
        ["skill list"] = new[]
        {
            "Usage: agent skill list [--json]",
            "  List canonical skills.",
        },
        ["skill show"] = new[]
        {
            "Usage: agent skill show <id> [--json]",
            "  Show a canonical skill's metadata and target flags.",
        },
        ["target"] = new[]
        {
            "Usage: agent target <add|edit|delete|list|show> [options]",
            "  Manage projection targets in agent.yaml. Run 'agent target <sub> --help' for options.",
        },
        ["target add"] = new[]
        {
            "Usage: agent target add <target-id> [options]",
            "",
            "Options:",
            "  --path <path>        Projection destination path.",
            "  --enabled <bool>     Whether the target is enabled (default true).",
        },
        ["target edit"] = new[]
        {
            "Usage: agent target edit <target-id> [options]",
            "  Change a target. At least one option is required.",
            "",
            "Options:",
            "  --path <path>        Set the projection destination path.",
            "  --enabled <bool>     Enable or disable the target.",
        },
        ["target delete"] = new[]
        {
            "Usage: agent target delete <target-id> [options]",
            "",
            "Options:",
            "  --force              Delete even when projections exist.",
            "  --dry-run            Preview without writing.",
        },
        ["target list"] = new[]
        {
            "Usage: agent target list [--json]",
            "  List projection targets, including the sub-agent destination (.claude/agents/<id>.md).",
        },
        ["target show"] = new[]
        {
            "Usage: agent target show <target-id> [--json]",
        },
        ["subagent"] = new[]
        {
            "Usage: agent subagent <add|edit|delete|list|show> [options]",
            "  Manage canonical sub-agents under .agent/agents. Run 'agent subagent <sub> --help' for options.",
        },
        ["subagent add"] = new[]
        {
            "Usage: agent subagent add <id> [options]",
            "  Scaffold a new canonical sub-agent under .agent/agents/<id>.",
            "",
            "Options:",
            "  --name <name>        Display name (defaults to the id).",
            "  --description <d>    Description (required).",
            "  --model <model>      Model the sub-agent should use (optional).",
            "  --color <color>      Display color for the agent list, e.g. green, cyan (optional).",
            "  --tool <tool>        Allow a tool (repeatable).",
            "  --tools <a,b,c>      Allow a comma-separated list of tools.",
        },
        ["subagent edit"] = new[]
        {
            "Usage: agent subagent edit <id> [options]",
            "  Change an existing canonical sub-agent. At least one option is required.",
            "",
            "Options:",
            "  --name <name>        Set the display name.",
            "  --description <d>    Set the description.",
            "  --model <model>      Set the model (empty value clears it).",
            "  --color <color>      Set the display color (empty value clears it).",
            "  --body-file <path>   Replace AGENT.md body from a file (absolute or relative paths allowed).",
            "  --tool <tool>        Set the allowed tools (repeatable; replaces the list).",
            "  --tools <a,b,c>      Set the allowed tools from a comma-separated list.",
        },
        ["subagent delete"] = new[]
        {
            "Usage: agent subagent delete <id> [options]",
            "",
            "Options:",
            "  --force              Delete even when a projection exists; prunes lockfile entries.",
            "  --dry-run            Preview without writing.",
        },
        ["subagent list"] = new[]
        {
            "Usage: agent subagent list [--json]",
            "  List canonical sub-agents.",
        },
        ["subagent show"] = new[]
        {
            "Usage: agent subagent show <id> [--json]",
        },
    };

    private void PrintHelp()
    {
        _out.WriteLine($"agent {GetVersion()} — Git-native consistency manager for AI-agent skills.");
        _out.WriteLine();
        _out.WriteLine("Usage:");
        _out.WriteLine("  agent <command> [options]");
        _out.WriteLine("  git agent <command> [options]   (via the git-agent extension)");
        _out.WriteLine();
        _out.WriteLine("Commands:");
        _out.WriteLine("  init                Scaffold .agent/ and .githooks/ (use --force to overwrite).");
        _out.WriteLine("  status              Report Agent Sync state and drift (--json, --fail-on-drift, --ci).");
        _out.WriteLine("  sync                Write missing/outdated projections (--check, --write, --force, --json).");
        _out.WriteLine("  diff                Show canonical-to-projection differences (--json).");
        _out.WriteLine("  validate            Validate config and skills (--json).");
        _out.WriteLine("  import skill        Import a SKILL.md/skill folder into .agent/skills (--id, --name, --target, --dry-run, --force, --json).");
        _out.WriteLine("  import agent        Import an existing instruction file/folder (AGENTS.md, CLAUDE.md, Cursor, ...) (--type, --split, --id, --dry-run, --force, --json).");
        _out.WriteLine("  import subagent     Import existing sub-agent files (.claude/agents/*.md) into .agent/agents (--id, --dry-run, --force, --json).");
        _out.WriteLine("  skill               Manage canonical skills: add | edit | delete | list | show.");
        _out.WriteLine("  target              Manage projection targets: add | edit | delete | list | show.");
        _out.WriteLine("  subagent            Manage canonical sub-agents: add | edit | delete | list | show.");
        _out.WriteLine("  sessions            Back up / restore agent session history: backup | restore | list | providers.");
        _out.WriteLine("  ui                  Launch the optional local web UI (separate install; CLI stays GUI-free) (--no-open).");
        _out.WriteLine("  install-hooks       Configure core.hooksPath and make hooks executable.");
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
