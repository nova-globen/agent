using AgentSync.Core.Autopilot;

namespace AgentSync.Core.Tests.Autopilot;

public sealed class AutopilotServiceTests
{
    private static AutopilotOptions ZeroDelay => new(DelaySeconds: 0);

    [Fact]
    public async Task RunAsync_ReturnsMissingCli_WhenProviderNotAvailable()
    {
        var provider = new FakeAutopilotProvider(available: false, results: []);
        var (service, stdout, stderr) = Build();

        var code = await service.RunAsync(provider, ZeroDelay, stdout, stderr, CancellationToken.None);

        Assert.Equal(ExitCodes.EnvironmentProblem, code);
        Assert.Contains("not found on PATH", stderr.ToString());
    }

    [Fact]
    public async Task RunAsync_ExitsSuccess_WhenDoneWithoutFailure()
    {
        var provider = new FakeAutopilotProvider(available: true, results:
        [
            new AutopilotResult(Failed: false, Done: true, Message: "All done.", Retry: null),
        ]);
        var (service, stdout, stderr) = Build();

        var code = await service.RunAsync(provider, ZeroDelay, stdout, stderr, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("all work complete", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsDriftCode_OnHardFailure()
    {
        var provider = new FakeAutopilotProvider(available: true, results:
        [
            new AutopilotResult(Failed: true, Done: true, Message: "Unresolvable error.", Retry: null),
        ]);
        var (service, _, _) = Build();

        var code = await service.RunAsync(provider, ZeroDelay, StringWriter.Null, StringWriter.Null, CancellationToken.None);

        Assert.Equal(ExitCodes.DriftOrValidationFailed, code);
    }

    [Fact]
    public async Task RunAsync_RetriesThenSucceeds()
    {
        // Session 1: transient failure — retry in 0s
        // Session 2: done successfully
        var provider = new FakeAutopilotProvider(available: true, results:
        [
            new AutopilotResult(Failed: true, Done: false, Message: "Usage limit.", Retry: new AutopilotRetry(0)),
            new AutopilotResult(Failed: false, Done: true, Message: "All done.", Retry: null),
        ]);
        var (service, _, _) = Build();

        var code = await service.RunAsync(provider, ZeroDelay, StringWriter.Null, StringWriter.Null, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(2, provider.SessionCount);
    }

    [Fact]
    public async Task RunAsync_ContinuesAcrossMultipleSessions()
    {
        // Three sessions: partial work, partial work, done
        var provider = new FakeAutopilotProvider(available: true, results:
        [
            new AutopilotResult(Failed: false, Done: false, Message: "Handoff 1.", Retry: null),
            new AutopilotResult(Failed: false, Done: false, Message: "Handoff 2.", Retry: null),
            new AutopilotResult(Failed: false, Done: true, Message: "All done.", Retry: null),
        ]);
        var (service, _, _) = Build();

        var code = await service.RunAsync(provider, ZeroDelay, StringWriter.Null, StringWriter.Null, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(3, provider.SessionCount);
    }

    [Fact]
    public async Task RunAsync_StopsOnCancellation()
    {
        // Provider would loop forever without cancellation
        var provider = new FakeAutopilotProvider(available: true, results:
        [
            new AutopilotResult(Failed: false, Done: false, Message: "Partial.", Retry: null),
        ],
        repeat: true);

        using var cts = new CancellationTokenSource();
        var (service, _, _) = Build();

        // Cancel after first session
        provider.OnSessionComplete = () => cts.Cancel();

        var code = await service.RunAsync(provider, ZeroDelay, StringWriter.Null, StringWriter.Null, cts.Token);

        Assert.Equal(ExitCodes.Success, code);
    }

    // -------------------------------------------------------------------------

    private static (AutopilotService, StringWriter, StringWriter) Build()
        => (new AutopilotService(), new StringWriter(), new StringWriter());

    private sealed class FakeAutopilotProvider : IAutopilotProvider
    {
        private readonly IReadOnlyList<AutopilotResult> _results;
        private readonly bool _repeat;
        private int _index;

        public FakeAutopilotProvider(bool available, AutopilotResult[] results, bool repeat = false)
        {
            Available = available;
            _results = results;
            _repeat = repeat;
        }

        public string Name => "fake";
        private bool Available { get; }
        public int SessionCount { get; private set; }
        public Action? OnSessionComplete { get; set; }

        public bool IsAvailable() => Available;

        public Task RunSessionAsync(IAutopilotSessionObserver? observer, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            SessionCount++;
            OnSessionComplete?.Invoke();
            return Task.CompletedTask;
        }

        public Task<AutopilotResult> ParseResultAsync(string sessionOutput, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_index >= _results.Count)
            {
                if (_repeat) _index = 0;
                else return Task.FromResult(new AutopilotResult(false, true, "No more results.", null));
            }

            return Task.FromResult(_results[_index++]);
        }
    }
}
