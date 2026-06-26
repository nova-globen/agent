using System.Collections.Concurrent;
using AgentSync.Core.Autopilot;
using Terminal.Gui;
using TGAttr = Terminal.Gui.Attribute;

namespace AgentSync.Cli.Autopilot;

// ---- colour theme -----------------------------------------------------------

static class Theme
{
    static readonly Color Bg       = Color.Black;
    static readonly Color Fg       = Color.Gray;
    static readonly Color FgBright = Color.White;
    static readonly Color FgDim    = Color.DarkGray;
    static readonly Color SelBg    = Color.DarkGray;

    static ColorScheme CS(Color fg, Color bg, Color? focusFg = null, Color? focusBg = null) => new()
    {
        Normal    = new TGAttr(fg,                  bg),
        Focus     = new TGAttr(focusFg ?? FgBright, focusBg ?? SelBg),
        HotNormal = new TGAttr(FgBright,            SelBg),
        HotFocus  = new TGAttr(FgBright,            SelBg),
        Disabled  = new TGAttr(FgDim,               bg),
    };

    public static readonly ColorScheme Base      = CS(Fg,       Bg);
    public static readonly ColorScheme Bright    = CS(FgBright, Bg);
    public static readonly ColorScheme Dim       = CS(FgDim,    Bg);
    public static readonly ColorScheme List      = CS(Fg,       Bg, FgBright, SelBg);
    public static readonly ColorScheme Content   = CS(FgBright, Bg, FgBright, Bg);
    public static readonly ColorScheme StatusRun = CS(FgBright, Color.Blue);
    public static readonly ColorScheme StatusOk  = CS(FgBright, Color.Green);
    public static readonly ColorScheme StatusErr = CS(FgBright, Color.Red);
    public static readonly ColorScheme StatusWait= CS(FgDim,    SelBg);
}

// ---- session record ---------------------------------------------------------

public enum TextKind { Text, Tool, Error }

public sealed class SessionRecord(int number)
{
    public int       Number    { get; } = number;
    public DateTime  StartedAt { get; } = DateTime.Now;
    public volatile bool IsRunning = true;
    public AutopilotSessionStats? Stats { get; set; }
    public AutopilotResult?       Result { get; set; }
    public string    Summary   { get; set; } = "";
    public int       DelaySeconds { get; set; }
    public List<(string Text, TextKind Kind)> Lines { get; } = [];

    public void AppendText(string text, TextKind kind)
    {
        lock (Lines) Lines.Add((text, kind));
    }
}

// ---- TUI -------------------------------------------------------------------

public sealed class AutopilotTui : IDisposable
{
    // ---- cross-thread queue -------------------------------------------------
    // Application.MainLoop.Invoke() is unreliable from background threads in
    // Terminal.Gui v1 inside VS Code terminal. Poll a ConcurrentQueue via AddIdle.

    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private void Enqueue(Action a) => _uiQueue.Enqueue(a);
    private bool DrainQueue()
    {
        var any = false;
        while (_uiQueue.TryDequeue(out var a)) { a(); any = true; }
        if (any) Application.Refresh();
        return true;
    }

    // ---- state --------------------------------------------------------------
    private readonly List<SessionRecord> _sessions = [];
    private int _selectedSession = -1;

    // ---- views --------------------------------------------------------------
    private ListView _sessionList = null!;
    private TextView _transcript  = null!;
    private TextView _summary     = null!;
    private Label _lblElapsed    = null!;
    private Label _lblSessions   = null!;
    private Label _lblInput      = null!;
    private Label _lblOutput     = null!;
    private Label _lblCache      = null!;
    private Label _lblCost       = null!;
    private Label _lblTools      = null!;
    private Label _lblRateHits   = null!;
    private Label _lblStatus     = null!;

    // ---- aggregate stats ----------------------------------------------------
    private int _totalInput, _totalOutput, _totalCache, _totalTools, _totalRateHits;
    private decimal _totalCost;
    private TimeSpan _totalElapsed;
    private int _sessionCount;

    // ---- public API ---------------------------------------------------------

    public void Run(CancellationToken ct)
    {
        Application.Init();
        // Override the global scheme — kills the default cyan everywhere.
        Application.Top.ColorScheme = Theme.Base;
        BuildLayout();
        Application.MainLoop.AddIdle(DrainQueue);
        Application.Run();
        Application.Shutdown();
    }

    public SessionRecord StartSession(int number)
    {
        var rec = new SessionRecord(number);
        Enqueue(() =>
        {
            _sessions.Add(rec);
            _sessionCount++;
            RefreshSessionList();
            SelectSession(_sessions.Count - 1);
        });
        return rec;
    }

    public void AppendText(SessionRecord rec, string text, TextKind kind)
    {
        rec.AppendText(text, kind);
        if (IsSelected(rec))
            Enqueue(() => AppendToTranscript(text));
    }

    public void UpdateStats(SessionRecord rec, AutopilotSessionStats stats)
    {
        rec.Stats = stats;
        Enqueue(() => { RefreshAggregateStats(); RefreshSessionList(); });
    }

    public void CompleteSession(SessionRecord rec, AutopilotResult result, int nextDelaySeconds)
    {
        rec.Result        = result;
        rec.Summary       = result.Message;
        rec.IsRunning     = false;
        rec.DelaySeconds  = nextDelaySeconds;
        Enqueue(() =>
        {
            RefreshAggregateStats();
            RefreshSessionList();
            if (IsSelected(rec)) RefreshSummary(rec);

            var msg = result.Done
                ? (result.Failed
                    ? "[stopped] hard blocker — check transcript"
                    : "[done] all work complete — press Q to quit")
                : $"[continue] next session in {nextDelaySeconds}s …";

            SetStatusText(msg,
                result.Done && result.Failed ? Theme.StatusErr
                : result.Done               ? Theme.StatusOk
                :                             Theme.StatusWait);
        });
    }

    public void SetStatus(string message, ColorScheme? scheme = null)
        => Enqueue(() => SetStatusText(message, scheme ?? Theme.StatusRun));

    public void Dispose() { }

    // ---- layout -------------------------------------------------------------

    private void BuildLayout()
    {
        var top = Application.Top;
        top.ColorScheme = Theme.Base;

        top.KeyDown += e =>
        {
            if (e.KeyEvent.Key == Key.Q) { Application.RequestStop(); e.Handled = true; }
        };

        const int leftW    = 22;
        const int statsH   = 6;
        const int summaryH = 6;
        const int statusH  = 1;

        // ---- left: session list ----
        var leftFrame = Frame("Sessions", 0, 0, leftW, Dim.Fill(statusH));
        _sessionList = new ListView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            AllowsMarking = false,
            ColorScheme   = Theme.List,
        };
        _sessionList.SelectedItemChanged += e => SelectSession(e.Item);
        leftFrame.Add(_sessionList);
        top.Add(leftFrame);

        // ---- right: transcript (top) ----
        var transcriptFrame = Frame("Transcript", leftW, 0, Dim.Fill(), Dim.Fill(statsH + summaryH + statusH));
        _transcript = new TextView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            ReadOnly    = true,
            WordWrap    = true,
            ColorScheme = Theme.Content,
        };
        transcriptFrame.Add(_transcript);
        top.Add(transcriptFrame);

        // ---- right: summary (middle) ----
        var summaryFrame = Frame("Summary", leftW, Pos.Bottom(transcriptFrame), Dim.Fill(), summaryH);
        _summary = new TextView
        {
            X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(),
            ReadOnly    = true,
            WordWrap    = true,
            ColorScheme = Theme.Dim,
        };
        summaryFrame.Add(_summary);
        top.Add(summaryFrame);

        // ---- right: stats (bottom) ----
        var statsFrame = Frame("Stats", leftW, Pos.Bottom(summaryFrame), Dim.Fill(), statsH);

        const int col2 = 26;
        _lblElapsed  = Stat(statsFrame, "Elapsed:",    0,    0);
        _lblSessions = Stat(statsFrame, "Sessions:",   col2, 0);
        _lblInput    = Stat(statsFrame, "Input:",      0,    1);
        _lblOutput   = Stat(statsFrame, "Output:",     col2, 1);
        _lblCache    = Stat(statsFrame, "Cache read:", 0,    2);
        _lblCost     = Stat(statsFrame, "Cost:",       col2, 2);
        _lblTools    = Stat(statsFrame, "Tools:",      0,    3);
        _lblRateHits = Stat(statsFrame, "Rate hits:",  col2, 3);
        top.Add(statsFrame);

        // ---- status bar ----
        _lblStatus = new Label("[starting]")
        {
            X = 0, Y = Pos.AnchorEnd(statusH), Width = Dim.Fill(),
            ColorScheme = Theme.StatusRun,
        };
        top.Add(_lblStatus);

        RefreshAggregateStats();
    }

    private static FrameView Frame(string title, Pos x, Pos y, Dim w, Dim h)
    {
        var f = new FrameView(title) { X = x, Y = y, Width = w, Height = h };
        f.ColorScheme = Theme.Base;
        return f;
    }

    private static Label Stat(View parent, string label, int x, int y)
    {
        var lbl = new Label(label) { X = x, Y = y, ColorScheme = Theme.Dim };
        var val = new Label("—")   { X = x + label.Length + 1, Y = y, Width = 14, ColorScheme = Theme.Bright };
        parent.Add(lbl, val);
        return val;
    }

    // ---- session list -------------------------------------------------------

    private void RefreshSessionList()
    {
        var items = _sessions.Select(s =>
        {
            string icon = s.IsRunning              ? "●"
                : s.Result?.Failed == true         ? "✗"
                : s.Result?.Done   == true         ? "✓"
                : s.Result is not null             ? "→"
                : "?";
            return $"  #{s.Number}  {s.StartedAt:HH:mm}  {icon}";
        }).ToList();

        _sessionList.SetSource(items);
        if (_selectedSession >= 0 && _selectedSession < items.Count)
            _sessionList.SelectedItem = _selectedSession;
    }

    private void SelectSession(int index)
    {
        if (index < 0 || index >= _sessions.Count) return;
        _selectedSession = index;

        var rec = _sessions[index];

        _transcript.Text = "";
        lock (rec.Lines)
        {
            foreach (var (text, _) in rec.Lines)
                _transcript.Text += text;
        }
        _transcript.MoveEnd();

        RefreshSummary(rec);
    }

    private void RefreshSummary(SessionRecord rec)
    {
        _summary.Text = string.IsNullOrEmpty(rec.Summary)
            ? rec.IsRunning ? "(session in progress …)" : ""
            : rec.Summary;
    }

    private bool IsSelected(SessionRecord rec) =>
        _selectedSession >= 0 &&
        _selectedSession < _sessions.Count &&
        _sessions[_selectedSession] == rec;

    // ---- transcript ---------------------------------------------------------

    private void AppendToTranscript(string text)
    {
        _transcript.Text += text;
        _transcript.MoveEnd();
    }

    // ---- aggregate stats ----------------------------------------------------

    private void RefreshAggregateStats()
    {
        _totalInput = _totalOutput = _totalCache = _totalTools = _totalRateHits = 0;
        _totalCost    = 0;
        _totalElapsed = TimeSpan.Zero;

        foreach (var s in _sessions)
        {
            var stats = s.Stats;
            if (stats is not null)
            {
                _totalInput    += stats.InputTokens;
                _totalOutput   += stats.OutputTokens;
                _totalCache    += stats.CacheReadTokens;
                _totalTools    += stats.ToolCallCount;
                _totalRateHits += stats.RateLimitHits;
                _totalCost     += stats.CostUsd;
                _totalElapsed  += stats.Elapsed;
            }
        }

        var e = _totalElapsed;
        _lblElapsed.Text  = $"{(int)e.TotalHours:D2}:{e.Minutes:D2}:{e.Seconds:D2}";
        _lblSessions.Text = _sessionCount.ToString();
        _lblInput.Text    = _totalInput.ToString("N0");
        _lblOutput.Text   = _totalOutput.ToString("N0");
        _lblCache.Text    = _totalCache.ToString("N0");
        _lblCost.Text     = $"${_totalCost:F4}";
        _lblTools.Text    = _totalTools.ToString();
        _lblRateHits.Text = _totalRateHits.ToString();
    }

    // ---- helpers ------------------------------------------------------------

    private void SetStatusText(string text, ColorScheme scheme)
    {
        _lblStatus.Text        = $" {text}";
        _lblStatus.ColorScheme = scheme;
    }
}

// ---- observer bridge --------------------------------------------------------

/// <summary>
/// Bridges <see cref="IAutopilotSessionObserver"/> events from the background autopilot
/// thread into the <see cref="AutopilotTui"/> UI queue.
/// </summary>
public sealed class AutopilotTuiObserver(AutopilotTui tui) : IAutopilotSessionObserver
{
    private SessionRecord? _current;

    public void OnSessionStarted(int sessionNumber)
    {
        _current = tui.StartSession(sessionNumber);
        tui.SetStatus($"[session {sessionNumber}] running …");
    }

    public void OnTextDelta(string text)
    {
        if (_current is not null)
            tui.AppendText(_current, text, TextKind.Text);
    }

    public void OnToolStarted(string toolName, string toolId)
    {
        if (_current is not null)
            tui.AppendText(_current, $"\n[tool: {toolName}]\n", TextKind.Tool);
        tui.SetStatus($"[tool] {toolName} …");
    }

    public void OnToolCompleted(string toolId, bool isError)
    {
        if (_current is not null)
            tui.AppendText(_current, isError ? "[✗]\n" : "[✓]\n", TextKind.Tool);
    }

    public void OnStats(AutopilotSessionStats stats)
    {
        if (_current is not null)
            tui.UpdateStats(_current, stats);
    }

    public void OnSessionCompleted(AutopilotResult result, int nextDelaySeconds)
    {
        if (_current is not null)
            tui.CompleteSession(_current, result, nextDelaySeconds);
        // TUI stays open after work is done so the user can review the transcript;
        // press Q to quit (the status bar shows the prompt).
    }
}
