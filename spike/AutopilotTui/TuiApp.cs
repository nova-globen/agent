using System.Collections.Concurrent;
using Terminal.Gui;
using TGAttr = Terminal.Gui.Attribute;

namespace AutopilotTui;

// ---- colour theme -----------------------------------------------------------

static class Theme
{
    // palette
    static readonly Color Bg       = Color.Black;
    static readonly Color Fg       = Color.Gray;
    static readonly Color FgBright = Color.White;
    static readonly Color FgDim    = Color.DarkGray;
    static readonly Color SelBg    = Color.DarkGray;

    static ColorScheme CS(Color fg, Color bg, Color? focusFg = null, Color? focusBg = null) => new()
    {
        Normal    = new TGAttr(fg,                 bg),
        Focus     = new TGAttr(focusFg ?? FgBright, focusBg ?? SelBg),
        HotNormal = new TGAttr(FgBright,            SelBg),
        HotFocus  = new TGAttr(FgBright,            SelBg),
        Disabled  = new TGAttr(FgDim,               bg),
    };

    public static readonly ColorScheme Base      = CS(Fg,        Bg);
    public static readonly ColorScheme Bright    = CS(FgBright,  Bg);
    public static readonly ColorScheme Dim       = CS(FgDim,     Bg);
    public static readonly ColorScheme List      = CS(Fg,        Bg, FgBright, SelBg);
    public static readonly ColorScheme Content   = CS(FgBright,  Bg, FgBright, Bg);   // no focus highlight
    public static readonly ColorScheme StatusRun = CS(FgBright,  Color.Blue);
    public static readonly ColorScheme StatusOk  = CS(FgBright,  Color.Green);
    public static readonly ColorScheme StatusErr = CS(FgBright,  Color.Red);
    public static readonly ColorScheme StatusWait= CS(FgDim,     SelBg);
}

// ---- TuiApp -----------------------------------------------------------------

public sealed class TuiApp : IDisposable
{
    // ---- cross-thread queue --------------------------------------------------
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

    // ---- state ---------------------------------------------------------------
    private readonly List<SessionRecord> _sessions = [];
    private int _selectedSession = -1;

    // ---- views ---------------------------------------------------------------
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

    // ---- aggregate stats -----------------------------------------------------
    private int _totalInput, _totalOutput, _totalCache, _totalTools, _totalRateHits;
    private decimal _totalCost;
    private TimeSpan _totalElapsed;
    private int _sessionCount;

    // ---- public API ----------------------------------------------------------

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

    public void UpdateStats(SessionRecord rec, SessionStats stats)
    {
        rec.Stats = stats;
        Enqueue(() => { RefreshAggregateStats(); RefreshSessionList(); });
    }

    public void CompleteSession(SessionRecord rec, Verdict verdict)
    {
        rec.Verdict  = verdict;
        rec.Summary  = verdict.Message;
        rec.Stats.IsRunning = false;
        Enqueue(() =>
        {
            RefreshAggregateStats();
            RefreshSessionList();
            if (IsSelected(rec)) RefreshSummary(rec);
            var msg = verdict.Done
                ? (verdict.Failed ? "[stopped] hard blocker" : "[done] all work complete — press Q to quit")
                : $"[continue] next session in {rec.DelaySeconds}s …";
            SetStatusText(msg, verdict.Done && verdict.Failed ? Theme.StatusErr
                             : verdict.Done                   ? Theme.StatusOk
                             :                                  Theme.StatusWait);
        });
    }

    public void SetStatus(string message, ColorScheme? scheme = null)
        => Enqueue(() => SetStatusText(message, scheme ?? Theme.StatusRun));

    public void Dispose() { }

    // ---- layout --------------------------------------------------------------

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
            ReadOnly     = true,
            WordWrap     = true,
            ColorScheme  = Theme.Content,
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

        int col2 = 26;
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

    // ---- session list --------------------------------------------------------

    private void RefreshSessionList()
    {
        var items = _sessions.Select(s =>
        {
            string icon = s.Stats.IsRunning ? "●"
                : s.Verdict?.Failed == true  ? "✗"
                : s.Verdict?.Done   == true  ? "✓"
                : s.Verdict is not null       ? "→"
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

        // rebuild transcript
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
            ? rec.Stats.IsRunning ? "(session in progress …)" : ""
            : rec.Summary;
    }

    private bool IsSelected(SessionRecord rec) =>
        _selectedSession >= 0 &&
        _selectedSession < _sessions.Count &&
        _sessions[_selectedSession] == rec;

    // ---- transcript ----------------------------------------------------------

    private void AppendToTranscript(string text)
    {
        _transcript.Text += text;
        _transcript.MoveEnd();
    }

    // ---- aggregate stats -----------------------------------------------------

    private void RefreshAggregateStats()
    {
        _totalInput = _totalOutput = _totalCache = _totalTools = _totalRateHits = 0;
        _totalCost  = 0;
        _totalElapsed = TimeSpan.Zero;

        foreach (var s in _sessions)
        {
            _totalInput    += s.Stats.InputTokens;
            _totalOutput   += s.Stats.OutputTokens;
            _totalCache    += s.Stats.CacheReadTokens;
            _totalTools    += s.Stats.ToolCallCount;
            _totalRateHits += s.Stats.RateLimitHits;
            _totalCost     += s.Stats.CostUsd;
            _totalElapsed  += s.Stats.Elapsed;
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

    // ---- helpers -------------------------------------------------------------

    private void SetStatusText(string text, ColorScheme scheme)
    {
        _lblStatus.Text        = $" {text}";
        _lblStatus.ColorScheme = scheme;
    }
}

// ---- session record ---------------------------------------------------------

public enum TextKind { Text, Tool, Error }

public sealed class SessionRecord(int number)
{
    public int       Number    { get; }      = number;
    public DateTime  StartedAt { get; }      = DateTime.Now;
    public SessionStats Stats  { get; set; } = new() { SessionNumber = number, IsRunning = true };
    public Verdict?  Verdict   { get; set; }
    public string    Summary   { get; set; } = "";
    public int       DelaySeconds { get; set; }
    public List<(string Text, TextKind Kind)> Lines { get; } = [];

    public void AppendText(string text, TextKind kind)
    {
        lock (Lines) Lines.Add((text, kind));
    }
}
