# Integration Task: Streaming TUI for `agent autopilot claude`

## What you are doing

Integrating the streaming TUI proof-of-concept from `spike/AutopilotTui/` into the main
Agent Sync application. The spike was built and validated end-to-end in a prior session.
Your job is to port it into production quality, following the existing architecture.

## What has already been done — do NOT redo

- **`--output-format json --json-schema` parse step** (`src/AgentSync.Core/Autopilot/
  ClaudeAutopilotProvider.cs`, `AutopilotResultParser.cs`): the verdict step already uses
  structured output and reads `structured_output` from the result envelope. Leave it alone.
- **Spike files** under `spike/AutopilotTui/`: fully working reference implementations.
  Read them; do not modify them; do not delete them.

---

## Spike reference — read these files first

| File | What it proves |
|---|---|
| `spike/AutopilotTui/ClaudeStream.cs` | How to run claude with `--output-format stream-json --verbose --include-partial-messages`, parse NDJSON line-by-line, and raise typed events |
| `spike/AutopilotTui/TuiApp.cs` | Complete Terminal.Gui 1.x TUI: dark theme, session list, transcript panel, summary panel, live stats, cross-thread update pattern |
| `spike/AutopilotTui/SessionStats.cs` | Stats model (input/output/cache tokens, cost, tool calls, rate limit hits) |
| `spike/AutopilotTui/Program.cs` | How the autopilot loop drives the TUI (reference for `CliRunner` changes) |
| `spike/AutopilotTui/StructuredVerdict.cs` | Already superseded — the main app already does this better |

Also read the existing main-app files before touching them:
- `src/AgentSync.Core/Autopilot/IAutopilotProvider.cs`
- `src/AgentSync.Core/Autopilot/ClaudeAutopilotProvider.cs`
- `src/AgentSync.Core/Autopilot/AutopilotService.cs`
- `src/AgentSync.Core/Autopilot/AutopilotResult.cs`
- `src/AgentSync.Cli/CliRunner.cs` (search for `RunAutopilotClaude` around line 2450)
- `tests/AgentSync.Core.Tests/Autopilot/AutopilotResultTests.cs`
- `tests/AgentSync.Core.Tests/Autopilot/AutopilotServiceTests.cs`

---

## Architecture constraints — do not break these

- **Keep `AgentSync.Core` free of any UI dependency.** No Terminal.Gui reference in Core.
  The observer interface (below) is the only new surface area in Core.
- **Keep `AgentSync.GitAgent` unchanged.** It delegates to `CliRunner`; no changes needed.
- **The headless/CI path must still work.** `AutopilotService` must be usable without a TUI
  (e.g. in tests or future CI use). Design the observer as optional.
- **Target framework stays `net10.0`.** Do not retarget anything.
- **No AI/Claude trailers in commit messages.**
- Run `dotnet build --configuration Release` and `dotnet test` before and after your changes.

---

## Step 1 — Add observer interface to `AgentSync.Core`

Create `src/AgentSync.Core/Autopilot/IAutopilotSessionObserver.cs`:

```csharp
namespace AgentSync.Core.Autopilot;

/// <summary>
/// Receives real-time events from a streaming autopilot session.
/// All methods are called from the background thread that drives the session.
/// Implementations must be thread-safe.
/// </summary>
public interface IAutopilotSessionObserver
{
    void OnTextDelta(string text);
    void OnToolStarted(string toolName, string toolId);
    void OnToolCompleted(string toolId, bool isError);
    void OnStats(AutopilotSessionStats stats);
}
```

Create `src/AgentSync.Core/Autopilot/AutopilotSessionStats.cs`:

```csharp
namespace AgentSync.Core.Autopilot;

public sealed record AutopilotSessionStats(
    int InputTokens,
    int CacheReadTokens,
    int CacheCreationTokens,
    int OutputTokens,
    int ToolCallCount,
    int RateLimitHits,
    decimal CostUsd,
    TimeSpan Elapsed);
```

---

## Step 2 — Update `IAutopilotProvider`

Change the `RunSessionAsync` signature. The `string` return value was always empty (the
session output goes to the terminal or observer, not a captured string). Remove it:

```csharp
// OLD:
Task<string> RunSessionAsync(TextWriter consoleOut, CancellationToken ct);

// NEW:
Task RunSessionAsync(IAutopilotSessionObserver? observer, CancellationToken ct);
```

`ParseResultAsync` signature stays exactly the same (it already works via `--json-schema`).

---

## Step 3 — Update `ClaudeAutopilotProvider.RunSessionAsync`

Port the NDJSON streaming logic from `spike/AutopilotTui/ClaudeStream.cs` into
`ClaudeAutopilotProvider.RunSessionAsync`. Key points:

- **CLI args**: `--dangerously-skip-permissions --output-format stream-json --verbose
  --include-partial-messages -p "continue autopilot"`
- **Redirect stdin, stdout, stderr.** Close stdin immediately (the `/dev/null` trick).
- **Read stdout line-by-line** with `ReadLineAsync`. Skip empty/null lines.
- **Per-line try/catch** — a malformed JSON line must not crash the whole session.
- **Fire observer events** (when observer is non-null) for:
  - `text_delta` → `OnTextDelta`
  - `content_block_start` with `tool_use` → `OnToolStarted`
  - `task_notification` with `status` → `OnToolCompleted`
  - Updated stats (from `message_delta/usage` or `result/usage`) → `OnStats`
- **When observer is null**: do NOT redirect stdout — let it go directly to the terminal
  (the current v0.3.2 behaviour for headless/CI use).
- **Drain stderr concurrently** to avoid pipe-full deadlock.
- See `spike/AutopilotTui/ClaudeStream.cs` for the exact NDJSON field paths.

The NDJSON event types to handle (copy the dispatch logic from the spike):

```
type = "rate_limit_event"        → increment rate_limit_hits if status != "allowed"
type = "stream_event"
  event.type = "content_block_start"
    content_block.type = "tool_use"  → OnToolStarted(name, id); tool_count++
  event.type = "content_block_delta"
    delta.type = "text_delta"        → OnTextDelta(text)
  event.type = "message_delta"
    usage                            → intermediate stats update
type = "system"
  subtype = "task_notification"      → OnToolCompleted(tool_use_id, status != "completed")
type = "result"
  total_cost_usd + usage             → final authoritative stats; OnStats
```

---

## Step 4 — Update `AutopilotService`

`AutopilotService.RunAsync` currently takes `TextWriter consoleOut` and writes
`[autopilot] === session N starting ===` etc. Keep that API — but also accept an
optional observer to pass through to the provider.

Add a new overload (keep the existing one for backward compat with tests):

```csharp
public Task<int> RunAsync(
    IAutopilotProvider provider,
    AutopilotOptions options,
    TextWriter consoleOut,
    TextWriter consoleErr,
    IAutopilotSessionObserver? observer,  // new
    CancellationToken ct)
```

Have the original overload call the new one with `observer: null`.

Pass `observer` through to `provider.RunSessionAsync(observer, ct)`.

When `observer` is non-null, suppress the `[autopilot] === session N starting ===` and
`[autopilot] session complete. parsing result ...` console noise — the TUI shows that.
When `observer` is null (headless), keep the existing console output.

---

## Step 5 — Add Terminal.Gui TUI to `AgentSync.Cli`

### 5a — Add package reference

In `src/AgentSync.Cli/AgentSync.Cli.csproj`, add:

```xml
<PackageReference Include="Terminal.Gui" Version="1.*" />
```

**Do not** add it to Core, GitAgent, or any other project.

**Important known issue:** Terminal.Gui v2.x has a type-resolution failure on net10.0
(types like `Label`, `ListView`, `View` resolve in method bodies but not as field/parameter
types — a build-system interaction with the package that only affects v2). Use `1.*`
(resolves to 1.17.x), which targets netstandard2.0 and works correctly.

### 5b — Create the TUI files

Create `src/AgentSync.Cli/Autopilot/AutopilotTui.cs`:

Port `spike/AutopilotTui/TuiApp.cs` into this file with the following adaptations:
- Namespace: `AgentSync.Cli.Autopilot`
- Class name: `AutopilotTui` (was `TuiApp`)
- `SessionRecord` moves here too (or into its own file)
- Implement `IAutopilotSessionObserver` on a bridge class `AutopilotTuiObserver` that
  calls the appropriate `AutopilotTui` methods (`AppendText`, `UpdateStats`, etc.)
  This keeps the TUI decoupled from the observer interface.

Keep these proven patterns from the spike **exactly**:

**Cross-thread updates — ConcurrentQueue + AddIdle:**
```csharp
// DO NOT use Application.MainLoop.Invoke() — unreliable in VS Code terminal (v1 TG)
private readonly ConcurrentQueue<Action> _uiQueue = new();
private void Enqueue(Action a) => _uiQueue.Enqueue(a);
private bool DrainQueue() {
    var any = false;
    while (_uiQueue.TryDequeue(out var a)) { a(); any = true; }
    if (any) Application.Refresh();
    return true; // keep idle handler active
}
// In Run(): Application.MainLoop.AddIdle(DrainQueue);
```

**Kill default cyan — set scheme on Application.Top:**
```csharp
Application.Init();
Application.Top.ColorScheme = Theme.Base;
```

**Theme:** Copy the `Theme` static class from `spike/AutopilotTui/TuiApp.cs` verbatim.
It uses Terminal.Gui v1 `Color` enum values and `TGAttr = Terminal.Gui.Attribute` alias.

**Layout (3-panel right column):**
- Left: session list (22 wide)
- Right top: Transcript (scrollable transcript of text deltas + tool events)
- Right middle: Summary (verdict message for selected completed session; 6 rows)
- Right bottom: Stats (6 rows: elapsed, sessions, input/output tokens, cache, cost, tools, rate hits)
- Bottom: status bar (1 row, colour-coded: blue=running, green=done, red=error, dark-gray=waiting)

### 5c — Update `CliRunner.RunAutopilotClaude`

Replace the current implementation:

```csharp
private int RunAutopilotClaude(string[] args)
{
    // ... parse --delay flag (keep as-is) ...

    var options  = new AutopilotOptions(DelaySeconds: delaySeconds);
    var provider = new ClaudeAutopilotProvider();
    var service  = new AutopilotService();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    // Launch TUI; run autopilot loop on background thread
    using var tui = new AutopilotTui();
    var observer  = new AutopilotTuiObserver(tui);

    var loopTask = Task.Run(async () =>
    {
        try
        {
            await service.RunAsync(provider, options, TextWriter.Null, _err, observer, cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            tui.SetStatus($"[error] {ex.GetType().Name}: {ex.Message}", StatusKind.Error);
            await Task.Delay(10_000, CancellationToken.None);
        }
    });

    tui.Run(cts.Token);   // blocks until Q or done
    cts.Cancel();

    try { loopTask.GetAwaiter().GetResult(); }
    catch (OperationCanceledException) { }

    return ExitCodes.Success;
}
```

---

## Step 6 — Update tests

### `AutopilotServiceTests.cs`

The `FakeAutopilotProvider` uses the old `RunSessionAsync(TextWriter, CancellationToken)`
signature. Update it to the new `RunSessionAsync(IAutopilotSessionObserver?, CancellationToken)`.
It should do nothing with the observer (just increment `SessionCount` and return).

All existing service tests should still pass unchanged in behaviour.

### New test: NDJSON event dispatch

Add `tests/AgentSync.Core.Tests/Autopilot/ClaudeStreamParserTests.cs`:

Extract the NDJSON dispatch logic from `ClaudeAutopilotProvider` into a small static helper
`ClaudeStreamParser.DispatchLine(string line, AutopilotSessionStats stats, IAutopilotSessionObserver? observer)`
so it can be unit-tested without spawning a process.

Test cases (use realistic NDJSON lines from the stream format):
- `text_delta` line → `OnTextDelta` called with correct text
- `content_block_start` (tool_use) → `OnToolStarted` called with name and id
- `task_notification` completed → `OnToolCompleted(id, false)`
- `task_notification` failed → `OnToolCompleted(id, true)`
- `rate_limit_event` rejected → stats.RateLimitHits incremented
- `result` line → stats.CostUsd and token counts updated; `OnStats` called
- Malformed JSON line → no exception thrown, line skipped

---

## Step 7 — Verify end-to-end

Build and test:
```bash
dotnet build --configuration Release
dotnet test
```

Then verify the TUI works in the autopilot-test example:
```bash
cd examples/autopilot-test
git init && git add . && git commit -m "chore: initial"
# Trust the workspace first (run claude here interactively once, or edit ~/.claude.json)
cd ../..
agent autopilot claude --delay 3
```

Expected:
- TUI launches (dark theme, no cyan)
- Sessions appear in left panel as they start
- Transcript streams text in real time
- Stats update (tokens, cost, tool calls)
- Summary panel fills in when a session completes
- Loop stops with green `[done] all work complete` status

---

## What NOT to do

- Do not change the `ParseResultAsync` implementation — the `--json-schema` parse step works.
- Do not add Terminal.Gui to `AgentSync.Core`, `AgentSync.GitAgent`, `AgentSync.Ui.Web`,
  or any other project. CLI only.
- Do not use `Terminal.Gui` version `2.*` — it has a type-resolution bug on net10.0.
- Do not use `Application.MainLoop.Invoke()` for cross-thread TUI updates — it silently
  fails in VS Code's integrated terminal. Use the `ConcurrentQueue` + `AddIdle` pattern.
- Do not remove the headless path (observer=null in AutopilotService) — it is needed for
  tests and CI.
- Do not change `agent autopilot claude` exit codes or the `--delay` flag semantics.
- Do not add tests for the TUI rendering itself (Terminal.Gui is a runtime dependency;
  test the observer/parser logic, not the screen output).
