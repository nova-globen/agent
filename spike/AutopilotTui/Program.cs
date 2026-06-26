using AutopilotTui;

// ---- args ----
var prompt = "continue autopilot";
var repoPath = Directory.GetCurrentDirectory();
var delaySeconds = 5;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--prompt" when i + 1 < args.Length:  prompt       = args[++i]; break;
        case "--repo"   when i + 1 < args.Length:  repoPath     = args[++i]; break;
        case "--delay"  when i + 1 < args.Length &&
             int.TryParse(args[++i], out var d):   delaySeconds = d;         break;
    }
}

if (!Directory.Exists(repoPath))
{
    Console.Error.WriteLine($"error: repo path does not exist: {repoPath}");
    return 1;
}

// ---- Ctrl+C wires into cancellation ----
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var tui = new TuiApp();

// Autopilot loop runs on background thread; TUI blocks this thread.
var loopTask = Task.Run(() => RunLoopAsync(tui, prompt, repoPath, delaySeconds, cts.Token));
tui.Run(cts.Token);

cts.Cancel();
try
{
    await loopTask;
}
catch (OperationCanceledException) { }
catch (Exception ex)
{
    // Surface any loop crash after the TUI exits
    Console.Error.WriteLine($"[autopilot error] {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
}

tui.Dispose();
return 0;

// ---- autopilot loop ----------------------------------------------------------

static async Task RunLoopAsync(
    TuiApp tui, string prompt, string repoPath, int delaySeconds, CancellationToken ct)
{
    var sessionNumber = 0;

    try
    {
    while (!ct.IsCancellationRequested)
    {
        sessionNumber++;
        var rec = tui.StartSession(sessionNumber);
        rec.DelaySeconds = delaySeconds;

        tui.SetStatus($"[session {sessionNumber}] running …");

        var stream = new ClaudeStream();
        stream.TextDelta     += text      => tui.AppendText(rec, text, TextKind.Text);
        stream.ToolStarted   += (name, _) => tui.AppendText(rec, $"\n[tool: {name}]\n", TextKind.Tool);
        stream.ToolCompleted += (_, err)  => tui.AppendText(rec, err ? "[x tool]\n" : "[v tool]\n", TextKind.Tool);
        stream.StatsUpdated  += s         => tui.UpdateStats(rec, s);

        try
        {
            await stream.RunAsync(prompt, repoPath, rec.Stats, ct);
        }
        catch (OperationCanceledException) { return; }

        tui.SetStatus($"[session {sessionNumber}] parsing verdict …");

        Verdict verdict;
        try { verdict = await StructuredVerdict.GetAsync(repoPath, ct); }
        catch (OperationCanceledException) { return; }

        tui.CompleteSession(rec, verdict);

        if (verdict.Done)
        {
            tui.SetStatus(verdict.Failed
                ? "[stopped] hard blocker — check transcript"
                : "[done] all work complete — press Q to quit");
            return;
        }

        var waitSec = verdict.RetryAfterSeconds > 0 ? verdict.RetryAfterSeconds : delaySeconds;
        tui.SetStatus($"[wait] {waitSec}s before next session …");
        try { await Task.Delay(TimeSpan.FromSeconds(waitSec), ct); }
        catch (OperationCanceledException) { return; }
    }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        tui.SetStatus($"[error] {ex.GetType().Name}: {ex.Message}");
        // Give the TUI a moment to display the error before the loop exits
        await Task.Delay(10_000, CancellationToken.None);
    }
}
