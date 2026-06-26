# Research Task: Claude CLI Streaming + TUI Autopilot Display

## Goal

Build a self-contained C# console proof-of-concept that runs `agent autopilot claude`
with real-time streaming output and a polished Terminal.Gui interface. The outcome of
this session is a working spike that can be merged back into `AgentSync.Core/Autopilot/`.

Do NOT touch the main Agent Sync solution. Work in a throwaway spike project under
`spike/AutopilotTui/` (create it with `dotnet new console`). All findings must be
validated by actually running the spike — do not just write the code, run it and confirm
it works end-to-end on this machine.

---

## Background

### Current implementation (what we are replacing/enhancing)

`agent autopilot claude` currently:
1. Runs `claude --dangerously-skip-permissions -p "continue autopilot"` with stdin
   closed (= `/dev/null` semantics), no stdout redirect — output goes raw to the terminal.
2. After the session exits, runs a second `claude -p "<parse prompt>"` with stdout
   captured to get a JSON verdict.

The problems:
- No real-time visibility into what Claude is doing mid-session.
- Output is unstyled plain text.
- No session history, no statistics, no token tracking.

### What we want instead

A Terminal.Gui–based TUI that:
- Streams session output in real time by parsing `--output-format stream-json`.
- Shows a panel of live session statistics (elapsed time, token counts from stream events,
  tool calls executed, cost estimate from the `result` envelope).
- Keeps a scrollable list of past sessions; user can select one to see its transcript.
- Gets the structured autopilot verdict via `--output-format json --json-schema` (no
  capture of the live session stdout needed for the verdict — the parse step handles that).

---

## Step 1 — Read the official headless docs

Read the Claude Code headless documentation at:
  https://code.claude.com/docs/en/headless

Pay particular attention to:
- `--output-format stream-json --verbose --include-partial-messages` for real-time output.
- `--output-format json --json-schema '<schema>'` for structured output from a single call.
- The `--dangerously-skip-permissions` flag and the workspace trust requirement.
- Any notes on stdin behaviour (`-p` prompt vs interactive mode).

---

## Step 2 — Understand the stream-json envelope format

Below is the practical schema derived from a real run. Parse NDJSON line-by-line. Each
line is a JSON object with a `type` field. Key types to handle:

```
type = "system"        subtype = "init"              → session_id, model, tools list
type = "system"        subtype = "status"            → status = "requesting" | "responding"
type = "rate_limit_event"                            → rate_limit_info.status ("allowed"/"rejected")
type = "stream_event"  event.type = "message_start"  → usage snapshot (cache tokens etc.)
type = "stream_event"  event.type = "content_block_delta"
                         delta.type = "text_delta"   → delta.text  ← the assistant's words
                         delta.type = "thinking_delta" → delta.thinking
                         delta.type = "input_json_delta" → tool input accumulation
type = "stream_event"  event.type = "content_block_start"
                         content_block.type = "tool_use" → name, id  ← tool call started
type = "system"        subtype = "task_started"      → tool description
type = "system"        subtype = "task_notification" → status = "completed"/"failed"
type = "user"          message.content[].type = "tool_result" → tool output
type = "assistant"                                   → snapshot after each turn
type = "result"        subtype = "success"/"error_during_execution"
                       → duration_ms, total_cost_usd, usage (full), structured_output (if --json-schema used)
```

Accumulate token counts from `usage` in `result` (authoritative) and update the live
stats panel as `stream_event / message_delta` fires with intermediate usage snapshots.

---

## Step 3 — Understand structured output for the parse step

For the verdict/parse step use:

```
claude -p "<prompt>" \
  --output-format json \
  --json-schema '{"type":"object","additionalProperties":false,"required":["failed","done","message","retry"],"properties":{"failed":{"type":"boolean"},"done":{"type":"boolean"},"message":{"type":"string"},"retry":{"type":"object","additionalProperties":false,"required":["afterSeconds"],"properties":{"afterSeconds":{"type":"integer","minimum":0}}}}}'
```

The last NDJSON line emitted has `type = "result"` and contains `structured_output` with
the validated JSON object. Capture stdout, split on newlines, parse the last non-empty
line, read `.structured_output`. No markdown stripping needed. Retry on parse failure.

Note: `--output-format json` without `--verbose` emits only the final `result` line — use
this for the parse step so there is no stream to consume.

---

## Step 4 — Workspace trust

`--dangerously-skip-permissions` is silently ignored if the project directory has not been
trusted. Symptoms: `result.subtype = "error_during_execution"` and `terminal_reason =
"aborted_streaming"` almost immediately, with a warning line on stderr:

```
Ignoring N permissions.allow entries from .claude/settings.local.json: this workspace
has not been trusted. Run Claude Code interactively here once and accept the trust dialog,
or set projects["<path>"].hasTrustDialogAccepted: true in ~/.claude.json.
```

The spike must detect this and print a clear actionable error rather than looping silently.
Detection: check stderr for the phrase `"has not been trusted"` before starting the main
loop.

---

## Step 5 — Create the spike

```
spike/AutopilotTui/
  AutopilotTui.csproj   (net10.0, Terminal.Gui NuGet, System.Text.Json)
  Program.cs
  ClaudeStream.cs       — process start, NDJSON line reader, event model
  SessionStats.cs       — accumulates token counts, cost, tool calls, elapsed
  TuiApp.cs             — Terminal.Gui layout
  StructuredVerdict.cs  — parse-step runner + result model
```

### ClaudeStream.cs

- `ProcessStartInfo`: `FileName = "claude"`, args = `--dangerously-skip-permissions`,
  `--output-format stream-json`, `--verbose`, `--include-partial-messages`, `-p`, `<prompt>`.
- `RedirectStandardInput = true`, `RedirectStandardOutput = true`,
  `RedirectStandardError = true`. `UseShellExecute = false`.
- Close stdin immediately after `Process.Start()` — this is the `/dev/null` trick that
  prevents the 3-second wait and prevents claude from launching its interactive TUI.
- Read stdout line-by-line asynchronously with `StreamReader.ReadLineAsync()`.
- Deserialise each line with `JsonDocument.Parse()`. Dispatch on `type`.
- Raise typed C# events: `OnTextDelta(string text)`, `OnToolStarted(string name, string id)`,
  `OnToolCompleted(string id, bool isError)`, `OnStats(SessionStats stats)`,
  `OnSessionComplete(ClaudeResult result)`.

### TuiApp.cs — Terminal.Gui layout

Use **Terminal.Gui** (NuGet: `Terminal.Gui`). Verified to work on Windows (winconpty)
and WSL/Linux (curses). Target layout:

```
┌─ Agent Autopilot ────────────────────────────────────────────────────────────┐
│ Sessions                     │ Current Session                               │
│ ─────────────────────        │ ─────────────────────────────────────────────│
│ #1  2026-06-26 10:05  ✓     │  (scrollable transcript: text deltas +        │
│ #2  2026-06-26 10:22  ✓     │   tool call events in a distinct colour)       │
│ #3  2026-06-26 10:41  ●     │                                               │
│                              │                                               │
│                              ├─ Stats ──────────────────────────────────────│
│                              │ Elapsed: 00:02:14   Sessions: 3              │
│                              │ Input tokens: 52,878  Output: 432            │
│                              │ Cache read: 52,878    Cost: $0.025            │
│                              │ Tool calls: 7         Rate limit hits: 0     │
└──────────────────────────────┴───────────────────────────────────────────────┘
```

- Left pane: `ListView` of past sessions. Click/navigate to load transcript into right pane.
- Right top pane: `TextView` (read-only, scroll locked to bottom during live session,
  free-scroll when viewing history). Text deltas append in white. Tool call lines in cyan.
  Error lines in red.
- Right bottom pane: stats labels, updated every time `OnStats` fires.
- Status bar at the bottom: `[Q] Quit  [↑↓] Navigate sessions  [Enter] View session`.

Do not use any third-party UI library other than Terminal.Gui. It handles Windows ConPTY
and WSL out of the box.

### SessionStats.cs

Fields to accumulate from `stream_event / message_delta` usage and the final `result`:

```csharp
public record SessionStats
{
    public int InputTokens;
    public int CacheReadInputTokens;
    public int CacheCreationInputTokens;
    public int OutputTokens;
    public int ToolCallCount;
    public int RateLimitHits;       // rate_limit_event where status != "allowed"
    public decimal CostUsd;         // from result.total_cost_usd
    public TimeSpan Elapsed;
    public bool IsComplete;
    public bool IsError;
}
```

---

## Step 6 — Windows vs WSL/Linux notes

- On Windows: `FileName = "claude"` resolves to `claude.exe` via CreateProcess. Works
  without `cmd.exe`. Do not use `cmd /c claude` — it caused spurious CTRL+C signals in
  prior work.
- On WSL: same binary name, same args. Node.js block-buffering of stdout is avoided
  because we redirect stdout (it's a pipe, and stream-json flushes per JSON line).
- ANSI escape codes in tool_result content (e.g. PowerShell colour codes) should be
  stripped before displaying in the TUI pane. Use a simple regex: `\x1B\[[0-9;]*[mK]`.

---

## Step 7 — End-to-end validation

Run the spike against this repository:

```
cd spike/AutopilotTui
dotnet run -- --prompt "continue autopilot" --repo "C:\Users\desma\Data\Repositories\NovaGloben\agent-sync-ai-agent-kit"
```

Confirm:
1. TUI launches, left pane shows "Session #1 (running)".
2. Text deltas appear in the right pane as they arrive (not all at once at the end).
3. Stats panel updates as tokens accumulate.
4. When the session ends, `result` line is parsed, cost and final token counts update.
5. Parse step runs, verdict is printed to status bar.
6. Session moves to "✓" or "✗" in the left pane.
7. Press Q to quit cleanly.

If text deltas do not appear in real time (all arrive at end): investigate whether
stdout is being block-buffered. With `--output-format stream-json` and a redirected pipe,
each JSON line should flush immediately. If not, try adding `--no-buffer` env or
investigate the Node.js `--no-interactive` codepath.

---

## Step 8 — Document findings

After the spike runs correctly, write a short findings summary to
`spike/AutopilotTui/FINDINGS.md`:

- Does stream-json flush per-line when stdout is a pipe? (yes/no)
- Any Windows-specific quirks with Terminal.Gui?
- Any WSL-specific quirks?
- Recommended changes to `ClaudeAutopilotProvider.cs` in the main solution:
  - Replace current `RunSessionAsync` with the streaming approach.
  - Update `ParseResultAsync` to use `--json-schema` + `structured_output`.
  - Note any new NuGet dependencies needed in `AgentSync.Core`.

---

## Constraints

- Use .NET 10 (`net10.0`). Do not add anything to the main solution — spike only.
- Only NuGet package allowed beyond the BCL: `Terminal.Gui` (latest stable).
  `System.Text.Json` is already in the BCL.
- Do not commit the spike to master. The findings doc is what matters.
- Do not add AI/Claude trailers to any commits.
- The spike must actually run, not just compile.
