# Autopilot Implementation Plan

Source brief: `.agent/plans/autopilot.txt`

---

## Scope

Two features:

1. **Milestone A — Sample skill pack**: `agent init` gains an interactive step (and `--with-samples` flag)
   that installs a curated set of skills, sub-agents, and git hooks sourced from the DidaBit repository.

2. **Milestone B — `agent autopilot claude`**: A new CLI command that drives Claude Code CLI in a
   headless loop (prompt = `"continue autopilot"`, flag = `--dangerously-skip-permissions`) until all
   planned work is done, retrying automatically on usage-limit responses.

---

## Milestone A: Sample Skill Pack

### A1 — Embed sample files as resources

**New directory:** `src/AgentSync.Core/Resources/Samples/`

Skills to embed (from DidaBit `.agent/skills/`):

| Skill id          | Files |
|-------------------|-------|
| `adr-author`      | `skill.yaml`, `SKILL.md` |
| `agentsync`       | `skill.yaml`, `SKILL.md` |
| `autopilot`       | `skill.yaml`, `SKILL.md` |
| `commit-governor` | `skill.yaml`, `SKILL.md` |
| `dotnet-inspect`  | `skill.yaml`, `SKILL.md` |
| `memory-curator`  | `skill.yaml`, `SKILL.md` |
| `next-step`       | `skill.yaml`, `SKILL.md` |
| `operating-guide` | `skill.yaml`, `SKILL.md` |
| `plan-governor`   | `skill.yaml`, `SKILL.md` |

Sub-agents to embed (from DidaBit `.agent/agents/` and `.claude/agents/`):

| Sub-agent id        | Files |
|---------------------|-------|
| `planner`           | `agent.yaml`, `AGENT.md` |
| `verifier`          | `agent.yaml`, `AGENT.md` |
| `git-ops-executor`  | `agent.yaml`, `AGENT.md` |

Git hooks to embed (from DidaBit `.githooks/`):

| Hook            | Purpose |
|-----------------|---------|
| `pre-commit`    | Runs `pre-commit-validate.sh` |
| `pre-push`      | Runs `agent status --fail-on-drift --ci` |
| `commit-msg`    | Validates commit message format |
| `post-checkout` | Refreshes projections after branch switch |
| `post-merge`    | Refreshes projections after merge/pull |

**Embed as EmbeddedResource in AgentSync.Core.csproj:**

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources/Samples/**" />
</ItemGroup>
```

Directory layout in source:

```
src/AgentSync.Core/Resources/Samples/
  skills/
    adr-author/
      skill.yaml
      SKILL.md
    agentsync/
      skill.yaml
      SKILL.md
    autopilot/
      skill.yaml
      SKILL.md
    commit-governor/
      skill.yaml
      SKILL.md
    dotnet-inspect/
      skill.yaml
      SKILL.md
    memory-curator/
      skill.yaml
      SKILL.md
    next-step/
      skill.yaml
      SKILL.md
    operating-guide/
      skill.yaml
      SKILL.md
    plan-governor/
      skill.yaml
      SKILL.md
  agents/
    planner/
      agent.yaml
      AGENT.md
    verifier/
      agent.yaml
      AGENT.md
    git-ops-executor/
      agent.yaml
      AGENT.md
  hooks/
    pre-commit
    pre-push
    commit-msg
    post-checkout
    post-merge
```

**New file: `src/AgentSync.Core/SamplePack.cs`**

```csharp
// Loads embedded sample resources.
// Skills: GetSkills() -> IEnumerable<(string id, string skillYaml, string skillMd)>
// Agents: GetAgents() -> IEnumerable<(string id, string agentYaml, string agentMd)>
// Hooks: GetHooks() -> IEnumerable<(string name, string content)>
```

Uses `Assembly.GetManifestResourceStream()` to load each embedded resource by its
manifest name (e.g. `AgentSync.Core.Resources.Samples.skills.autopilot.SKILL_md`).

---

### A2 — Modify `InitService`

**File:** `src/AgentSync.Core/InitService.cs`

Add parameter `bool installSamples = false` to `Run()`.

When `installSamples == true`, after the existing scaffolding:

1. Load all skills from `SamplePack.GetSkills()`.
2. For each skill, write `{root}/.agent/skills/{id}/skill.yaml` and `SKILL.md`
   (skip if file exists, unless `force == true`).
3. Load all agents from `SamplePack.GetAgents()`.
4. For each agent, write `{root}/.agent/agents/{id}/agent.yaml` and `AGENT.md`.
5. Load all hooks from `SamplePack.GetHooks()`.
6. Write hooks to `{root}/.githooks/{name}`, make executable on Unix.
7. Track each file in `InitResult.Files` with appropriate action.

No changes to return type — `InitResult` already carries `IEnumerable<InitFileResult>`.

---

### A3 — Modify `CliRunner.RunInit()`

**File:** `src/AgentSync.Cli/CliRunner.cs`

Add new optional flags:

| Flag | Meaning |
|------|---------|
| `--with-samples` | Install sample skills and sub-agents without prompting |
| `--no-samples` | Skip sample installation without prompting |

Logic:

```
if --with-samples  → installSamples = true  (no prompt)
if --no-samples    → installSamples = false (no prompt)
else if stdin is TTY:
    print "Install sample skills (autopilot, commit-governor, etc.)? [y/N] "
    read line → installSamples = (line.Trim().ToLower() == "y")
else:
    installSamples = false  // non-interactive CI default
```

After the existing result reporting, add a section listing installed sample skills.

Add help text entry for `--with-samples` / `--no-samples`.

---

### A4 — Tests

- `tests/AgentSync.Core.Tests/SamplePackTests.cs`: verify all expected skill ids, agent ids, and hook names load without error.
- `tests/AgentSync.Cli.Tests/InitCommandTests.cs`: extend existing init tests with `--with-samples` producing additional files.

---

## Milestone B: `agent autopilot claude`

### B1 — Domain types

**New file: `src/AgentSync.Core/Autopilot/AutopilotResult.cs`**

```csharp
/// JSON verdict returned by the parse step.
public record AutopilotResult(
    bool Failed,
    bool Done,
    string Message,
    AutopilotRetry? Retry
);

public record AutopilotRetry(int AfterSeconds);
```

JSON schema sent to Claude for parsing:

```json
{
  "failed": boolean,
  "done": boolean,
  "message": "<one-paragraph markdown summary>",
  "retry": { "after": <seconds> }   // omit if no retry needed
}
```

- `failed: true, done: false, retry: { after: N }` → usage limit hit; wait N seconds then retry.
- `failed: true, done: true` → hard failure (unrecoverable); stop the loop and report.
- `failed: false, done: false` → session completed one handoff; continue the loop after delay.
- `failed: false, done: true` → all work complete; exit cleanly.

---

### B2 — Provider abstraction

**New file: `src/AgentSync.Core/Autopilot/IAutopilotProvider.cs`**

```csharp
public interface IAutopilotProvider
{
    string Name { get; }
    bool IsAvailable();
    // Runs one headless session; streams output to writer; returns full captured output.
    Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct);
    // Parses captured session output to structured result via a second headless call.
    Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct);
}
```

---

### B3 — Claude provider

**New file: `src/AgentSync.Core/Autopilot/ClaudeAutopilotProvider.cs`**

```csharp
public class ClaudeAutopilotProvider : IAutopilotProvider
{
    public string Name => "claude";

    public bool IsAvailable()
        => TryLocateCli(out _);  // checks PATH for `claude`

    public async Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct)
    {
        // Runs:
        //   claude --dangerously-skip-permissions -p "continue autopilot"
        //
        // Streams each stdout line to consoleOut AND accumulates in a StringBuilder.
        // Returns the full accumulated output.
    }

    public async Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
    {
        // Builds a parsing prompt:
        //   <session output>
        //
        //   ---
        //   Analyze the output above and respond ONLY with a JSON object matching this schema:
        //   { "failed": bool, "done": bool, "message": "...", "retry": { "after": N } }
        //   Rules:
        //   - failed=true if the session ended with an error, network failure, or unresolvable blocker.
        //   - done=true if the session explicitly states all planned work is complete (no more increments).
        //   - retry.after is the number of seconds to wait (e.g. 3600 for a 1h usage limit reset).
        //     Omit retry if no retry is appropriate.
        //   - message is a 1-paragraph markdown summary of what happened.
        //   Respond with ONLY the JSON object, no prose, no markdown fences.
        //
        // Runs:
        //   claude --dangerously-skip-permissions -p "<above prompt>"
        //
        // Strips markdown fences if present, then deserializes to AutopilotResult.
        // On JSON parse failure: returns AutopilotResult(Failed:true, Done:true, Message:"Parse error: ...")
    }

    private bool TryLocateCli(out string path)
    {
        // Check PATH for `claude` / `claude.exe`
    }
}
```

Process execution details:
- Use `Process.Start` with `RedirectStandardOutput = true`, `RedirectStandardError = true`.
- Merge stderr into stdout stream (or discard stderr from parse step).
- Respect `CancellationToken` by killing the process on cancel.
- Timeout: no global timeout (sessions can be long); respect only CT.

---

### B4 — AutopilotService (loop)

**New file: `src/AgentSync.Core/Autopilot/AutopilotService.cs`**

```csharp
public class AutopilotService
{
    public async Task<int> RunAsync(
        IAutopilotProvider provider,
        AutopilotOptions options,
        TextWriter consoleOut,
        TextWriter consoleErr,
        CancellationToken ct)
    {
        if (!provider.IsAvailable())
        {
            consoleErr.WriteLine($"error: '{provider.Name}' CLI not found on PATH.");
            return ExitCodes.EnvironmentProblem;
        }

        int iteration = 0;

        while (!ct.IsCancellationRequested)
        {
            iteration++;
            consoleOut.WriteLine($"[autopilot] session {iteration} starting ...");

            string output = await provider.RunSessionAsync(consoleOut, ct);

            consoleOut.WriteLine();
            consoleOut.WriteLine("[autopilot] parsing result ...");

            AutopilotResult result = await provider.ParseResultAsync(output, ct);

            consoleOut.WriteLine($"[autopilot] {result.Message}");

            if (result.Retry is not null)
            {
                int wait = result.Retry.AfterSeconds;
                consoleOut.WriteLine($"[autopilot] retry in {wait}s ...");
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                continue;
            }

            if (result.Done)
            {
                consoleOut.WriteLine(result.Failed
                    ? "[autopilot] stopped: hard blocker."
                    : "[autopilot] all work complete.");
                return result.Failed ? ExitCodes.DriftOrValidationFailed : ExitCodes.Success;
            }

            // Session completed one chunk; wait and continue.
            await Task.Delay(TimeSpan.FromSeconds(options.DelaySeconds), ct);
        }

        return ExitCodes.Success;
    }
}
```

**New file: `src/AgentSync.Core/Autopilot/AutopilotOptions.cs`**

```csharp
public record AutopilotOptions(int DelaySeconds = 5);
```

---

### B5 — CLI handler

**File: `src/AgentSync.Cli/CliRunner.cs`**

Register new command in the dispatch switch:

```csharp
"autopilot" => RunAutopilot(rest),
```

Implement:

```csharp
private int RunAutopilot(string[] args)
{
    if (args.Length == 0) { /* print help */ return ExitCodes.InvalidUsage; }
    var sub = args[0];
    var rest = args.Skip(1).ToArray();
    return sub switch
    {
        "claude" => RunAutopilotClaude(rest),
        _ => UnknownSubcommand("autopilot", sub),
    };
}

private int RunAutopilotClaude(string[] args)
{
    // Parse flags:
    //   --delay <seconds>   (default 5)
    //   --dry-run           (print what would happen, run one parse step, exit)
    // Validate no unexpected flags.
    
    var options = new AutopilotOptions(delaySeconds);
    var provider = new ClaudeAutopilotProvider();
    var service = new AutopilotService();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    return service.RunAsync(provider, options, _out, _err, cts.Token).GetAwaiter().GetResult();
}
```

Help text entry:

```
autopilot claude   Run Claude Code CLI in a headless loop until all work is done
  --delay <sec>   Seconds to wait between sessions (default: 5)
```

Main help addition:

```
agent autopilot <provider>   Headless autopilot loop (providers: claude)
```

---

### B6 — Tests

**`tests/AgentSync.Core.Tests/Autopilot/AutopilotServiceTests.cs`**

- `RunAsync_RetriesOnRetryResult`: mock provider returns retry(10) twice then done=true; assert delay called twice.
- `RunAsync_StopsOnHardFailure`: mock returns failed=true, done=true; assert exit non-zero.
- `RunAsync_ExitsOnDone`: mock returns done=true; assert exit 0.

**`tests/AgentSync.Core.Tests/Autopilot/ClaudeProviderParseTests.cs`**

- Test `ParseResultAsync` with various JSON strings (with/without retry, with markdown fences, malformed JSON).

---

## Implementation Order

```
A1  Embed resource files
A2  SamplePack.cs loader
A3  InitService changes
A4  CliRunner init changes + interactive prompt
A-tests  SamplePack + init tests

B1  AutopilotResult + AutopilotOptions domain types
B2  IAutopilotProvider interface
B3  ClaudeAutopilotProvider (process launch + parse)
B4  AutopilotService (loop)
B5  CliRunner dispatch + RunAutopilotClaude
B-tests  AutopilotService + parser tests
```

Run `dotnet build && dotnet test` between each milestone.

---

## File Change Summary

| Action   | Path |
|----------|------|
| Create   | `src/AgentSync.Core/Resources/Samples/**` (all embedded resource files) |
| Create   | `src/AgentSync.Core/SamplePack.cs` |
| Modify   | `src/AgentSync.Core/InitService.cs` |
| Create   | `src/AgentSync.Core/Autopilot/AutopilotResult.cs` |
| Create   | `src/AgentSync.Core/Autopilot/AutopilotOptions.cs` |
| Create   | `src/AgentSync.Core/Autopilot/IAutopilotProvider.cs` |
| Create   | `src/AgentSync.Core/Autopilot/ClaudeAutopilotProvider.cs` |
| Create   | `src/AgentSync.Core/Autopilot/AutopilotService.cs` |
| Modify   | `src/AgentSync.Cli/CliRunner.cs` |
| Modify   | `src/AgentSync.Core/AgentSync.Core.csproj` (EmbeddedResource glob) |
| Create   | `tests/AgentSync.Core.Tests/SamplePackTests.cs` |
| Create   | `tests/AgentSync.Core.Tests/Autopilot/AutopilotServiceTests.cs` |
| Create   | `tests/AgentSync.Core.Tests/Autopilot/ClaudeProviderParseTests.cs` |
| Extend   | `tests/AgentSync.Cli.Tests/InitCommandTests.cs` |
