using System.Collections.Concurrent;
using Terminal.Gui;
using TGAttr = Terminal.Gui.Attribute;

namespace AutopilotTui;

public sealed class TuiApp : IDisposable
{
    // ---- cross-thread update queue -------------------------------------------
    // Application.MainLoop.Invoke() from background threads is unreliable in TG v1
    // inside VS Code's integrated terminal. Use a ConcurrentQueue drained by an
    // idle handler instead — the idle handler runs on every main-loop iteration.

    private readonly ConcurrentQueue<Action> _uiQueue = new();

    private void Enqueue(Action action) => _uiQueue.Enqueue(action);

    private bool DrainQueue()
    {
        var any = false;
        while (_uiQueue.TryDequeue(out var action))
        {
            action();
            any = true;
        }
        if (any) Application.Refresh();
        return true; // keep idle handler alive
    }

    // ---- state ---------------------------------------------------------------

    private readonly List<SessionRecord> _sessions = [];
    private int _selectedSession = -1;

    // ---- views ---------------------------------------------------------------
    private ListView _sessionList = null!;
    private TextView _transcript = null!;
    private Label _lblElapsed = null!;
    private Label _lblSessions = null!;
    private Label _lblInput = null!;
    private Label _lblOutput = null!;
    private Label _lblCache = null!;
    private Label _lblCost = null!;
    private Label _lblTools = null!;
    private Label _lblRateHits = null!;
    private Label _lblStatus = null!;

    // aggregate stats
    private int _totalInput, _totalOutput, _totalCache, _totalTools, _totalRateHits;
    private decimal _totalCost;
    private TimeSpan _totalElapsed;
    private int _sessionCount;

    // ---- public API ----------------------------------------------------------

    public void Run(CancellationToken ct)
    {
        Application.Init();
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
        Enqueue(() =>
        {
            RefreshAggregateStats();
            RefreshSessionList();
        });
    }

    public void CompleteSession(SessionRecord rec, Verdict verdict)
    {
        rec.Verdict = verdict;
        rec.Stats.IsRunning = false;
        Enqueue(() =>
        {
            RefreshAggregateStats();
            RefreshSessionList();
            SetStatusText(verdict.Done
                ? (verdict.Failed ? "[stopped] hard blocker" : "[done] all work complete")
                : $"[continue] next session in {rec.DelaySeconds}s …");
        });
    }

    public void SetStatus(string message) => Enqueue(() => SetStatusText(message));

    public void Dispose() { }

    // ---- layout --------------------------------------------------------------

    private void BuildLayout()
    {
        var top = Application.Top;
        top.KeyDown += (e) =>
        {
            if (e.KeyEvent.Key == Key.Q) { Application.RequestStop(); e.Handled = true; }
        };

        // Left pane — session list
        var leftFrame = new FrameView("Sessions")
        {
            X = 0, Y = 0,
            Width = 24,
            Height = Dim.Fill(2),
        };

        _sessionList = new ListView
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
        };
        _sessionList.SelectedItemChanged += (e) => SelectSession(e.Item);
        leftFrame.Add(_sessionList);
        top.Add(leftFrame);

        // Right top — transcript
        var transcriptFrame = new FrameView("Transcript")
        {
            X = 24, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(9),
        };
        _transcript = new TextView
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
        };
        transcriptFrame.Add(_transcript);
        top.Add(transcriptFrame);

        // Right bottom — stats
        var statsFrame = new FrameView("Stats")
        {
            X = 24, Y = Pos.Bottom(transcriptFrame),
            Width = Dim.Fill(),
            Height = 7,
        };

        _lblElapsed  = AddStatLabel(statsFrame, "Elapsed:",   0,  0);
        _lblSessions = AddStatLabel(statsFrame, "Sessions:",  22, 0);
        _lblInput    = AddStatLabel(statsFrame, "Input tok:", 0,  1);
        _lblOutput   = AddStatLabel(statsFrame, "Output tok:", 22, 1);
        _lblCache    = AddStatLabel(statsFrame, "Cache read:", 0,  2);
        _lblCost     = AddStatLabel(statsFrame, "Est. cost:",  22, 2);
        _lblTools    = AddStatLabel(statsFrame, "Tool calls:", 0,  3);
        _lblRateHits = AddStatLabel(statsFrame, "Rate hits:",  22, 3);
        top.Add(statsFrame);

        // Status bar
        _lblStatus = new Label("Starting …")
        {
            X = 0, Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            ColorScheme = MakeColors(Color.White, Color.Blue),
        };
        top.Add(_lblStatus);

        RefreshAggregateStats();
    }

    private static Label AddStatLabel(View parent, string label, int x, int y)
    {
        parent.Add(new Label(label) { X = x, Y = y });
        var value = new Label("—") { X = x + label.Length + 1, Y = y, Width = 12 };
        parent.Add(value);
        return value;
    }

    // ---- session list --------------------------------------------------------

    private void RefreshSessionList()
    {
        var items = _sessions.Select(s =>
        {
            var icon = s.Stats.IsRunning ? "●"
                : s.Verdict is null ? "?"
                : s.Verdict.Failed ? "x"
                : s.Verdict.Done ? "v"
                : ">";
            return $" #{s.Number} {s.StartedAt:HH:mm} {icon}";
        }).ToList();

        _sessionList.SetSource(items);
        if (_selectedSession >= 0 && _selectedSession < items.Count)
            _sessionList.SelectedItem = _selectedSession;
    }

    private void SelectSession(int index)
    {
        if (index < 0 || index >= _sessions.Count) return;
        _selectedSession = index;
        _transcript.Text = "";
        var rec = _sessions[index];
        lock (rec.Lines)
        {
            foreach (var (text, _) in rec.Lines)
                _transcript.Text += text;
        }
        _transcript.MoveEnd();
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
        _totalCost = 0;
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

    private void SetStatusText(string message) => _lblStatus.Text = message;

    private static ColorScheme MakeColors(Color fg, Color bg) => new()
    {
        Normal    = new TGAttr(fg, bg),
        Focus     = new TGAttr(fg, bg),
        HotNormal = new TGAttr(fg, bg),
        HotFocus  = new TGAttr(fg, bg),
        Disabled  = new TGAttr(fg, bg),
    };
}

// ---- session record ----------------------------------------------------------

public enum TextKind { Text, Tool, Error }

public sealed class SessionRecord(int number)
{
    public int Number { get; } = number;
    public DateTime StartedAt { get; } = DateTime.Now;
    public SessionStats Stats { get; set; } = new() { SessionNumber = number, IsRunning = true };
    public Verdict? Verdict { get; set; }
    public int DelaySeconds { get; set; }
    public List<(string Text, TextKind Kind)> Lines { get; } = [];

    public void AppendText(string text, TextKind kind)
    {
        lock (Lines) Lines.Add((text, kind));
    }
}
