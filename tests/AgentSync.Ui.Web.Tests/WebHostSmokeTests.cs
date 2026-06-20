using System.Diagnostics;
using System.Net;
using AgentSync.Core;

namespace AgentSync.Ui.Web.Tests;

/// <summary>
/// Boots the real <c>agent-sync-ui</c> host as a process against a throwaway repository and
/// drives it over HTTP — no browser. Proves readiness, the token gate (401 without a token),
/// the token→cookie exchange + redirect that strips the token from the URL, and that the
/// (interactive-server) pages render their server-side HTML.
/// </summary>
public sealed class WebHostSmokeTests : IDisposable
{
    private readonly string _repo;
    private readonly string _token = "smoke-test-token";
    private readonly int _port;
    private Process? _host;

    public WebHostSmokeTests()
    {
        _repo = Path.Combine(Path.GetTempPath(), "agentsync-web-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repo);
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
        new InitService(_repo).Run();
        _port = UiSession.FindFreePort();
    }

    public void Dispose()
    {
        try { if (_host is { HasExited: false }) _host.Kill(entireProcessTree: true); } catch { /* best effort */ }
        try { _host?.Dispose(); } catch { /* best effort */ }
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort */ }
    }

    private static string HostDll
        => Path.Combine(AppContext.BaseDirectory, "agent-sync-ui.dll");

    private async Task<HttpClient> StartHostAndConnectAsync()
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(HostDll);
        psi.ArgumentList.Add("--repo");
        psi.ArgumentList.Add(_repo);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(_port.ToString());
        psi.ArgumentList.Add("--token");
        psi.ArgumentList.Add(_token);
        psi.ArgumentList.Add("--no-open");

        _host = Process.Start(psi);
        Assert.NotNull(_host);

        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}"),
            Timeout = TimeSpan.FromSeconds(5),
        };

        // Wait for readiness using the same endpoint the launcher polls.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (_host!.HasExited)
            {
                Assert.Fail($"host exited early (code {_host.ExitCode}): {await _host.StandardError.ReadToEndAsync()}");
            }

            try
            {
                using var r = await client.GetAsync("/healthz");
                if (r.IsSuccessStatusCode)
                {
                    return client;
                }
            }
            catch
            {
                // not up yet
            }

            await Task.Delay(200);
        }

        Assert.Fail("host did not become ready within 30s");
        return client; // unreachable
    }

    [Fact]
    public async Task Host_EnforcesAuth_RedirectsTokenToCookie_AndRendersPages()
    {
        using var client = await StartHostAndConnectAsync();

        // /healthz is public.
        using (var health = await client.GetAsync("/healthz"))
        {
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal("ok", await health.Content.ReadAsStringAsync());
        }

        // No token, no cookie => 401.
        using (var denied = await client.GetAsync("/"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        // Wrong token => 401.
        using (var wrong = await client.GetAsync("/?token=nope"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // Valid token query => 302 to the same path with the token stripped + Set-Cookie.
        string cookie;
        using (var auth = await client.GetAsync($"/?token={_token}"))
        {
            Assert.Equal(HttpStatusCode.Redirect, auth.StatusCode);
            Assert.Equal("/", auth.Headers.Location!.ToString());
            Assert.DoesNotContain("token", auth.Headers.Location!.ToString());
            var setCookie = Assert.Single(auth.Headers.GetValues("Set-Cookie"));
            Assert.Contains(SessionGate.CookieName, setCookie);
            Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
            cookie = setCookie.Split(';')[0];
        }

        // With the cookie, the dashboard and other pages render their SSR HTML.
        await AssertPageRendersAsync(client, "/", "Dashboard", cookie);
        await AssertPageRendersAsync(client, "/skills", "Skills", cookie);
        await AssertPageRendersAsync(client, "/imports", "Imports", cookie);
        await AssertPageRendersAsync(client, "/targets", "Targets", cookie);
        await AssertPageRendersAsync(client, "/status", "Status", cookie);
        await AssertPageRendersAsync(client, "/diff", "Diff", cookie);
    }

    private static async Task AssertPageRendersAsync(HttpClient client, string path, string expected, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, body);
    }
}
