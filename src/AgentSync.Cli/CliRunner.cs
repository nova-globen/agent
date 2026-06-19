using System.Reflection;
using System.Text.Json;
using AgentSync.Core;
using AgentSync.Core.Authoring;
using AgentSync.Core.Configuration;
using AgentSync.Core.Drift;
using AgentSync.Core.Import;
using AgentSync.Core.Projections;

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
            "sync" => RunSync(rest),
            "diff" => RunDiff(rest),
            "validate" => RunValidate(rest),
            "import" => RunImport(rest),
            "skill" => RunSkill(rest),
            "skills" => RunSkillList(rest),
            "target" => RunTarget(rest),
            "targets" => RunTargetList(rest),
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

        if (json)
        {
            WriteSyncJson(report);
        }
        else
        {
            WriteSyncHuman(report, check);
        }

        if (!report.ConfigValid)
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        // In check mode, pending changes or manual edits are a non-zero (drift) result.
        if (check && (report.AnyChanges || report.AnyManualEdits))
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        // In write mode, manual edits we refused to overwrite are a problem.
        if (!check && report.AnyManualEdits)
        {
            return ExitCodes.DriftOrValidationFailed;
        }

        return ExitCodes.Success;
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

        if (report.AnyManualEdits)
        {
            _out.WriteLine();
            _out.WriteLine("Some projections were manually edited and left untouched. Use --force to overwrite.");
        }
    }

    private void WriteSyncJson(SyncReport report)
    {
        var payload = new
        {
            configValid = report.ConfigValid,
            dryRun = report.DryRun,
            anyChanges = report.AnyChanges,
            anyManualEdits = report.AnyManualEdits,
            outcomes = report.Outcomes.Select(o => new
            {
                skill = o.Projection.SkillId,
                target = o.Projection.TargetId,
                path = o.Projection.RelativePath,
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
        if (args.Length == 0)
        {
            _err.WriteLine("error: 'import' requires a subcommand: skill | agent.");
            _err.WriteLine("Run 'agent --help' for usage.");
            return ExitCodes.InvalidUsage;
        }

        var sub = args[0];
        var rest = args.Skip(1).ToArray();
        return sub switch
        {
            "skill" => RunImportSkill(rest),
            "agent" => RunImportAgent(rest),
            _ => UnknownSubcommand("import", sub),
        };
    }

    private int RunImportSkill(string[] args)
    {
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

    // --- target CRUD ----------------------------------------------------------

    private int RunTarget(string[] args)
    {
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

    private int RunTargetList(string[] args)
    {
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
        }

        return ExitCodes.Success;
    }

    private int RunTargetShow(string[] args)
    {
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
        _out.WriteLine("  skill               Manage canonical skills: add | edit | delete | list | show.");
        _out.WriteLine("  target              Manage projection targets: add | edit | delete | list | show.");
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
