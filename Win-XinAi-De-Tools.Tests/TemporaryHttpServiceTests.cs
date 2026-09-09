using System.Net;
using System.Net.Sockets;
using PortManager.Services;
using Xunit;

namespace WinXinAiDeTools.Tests;

public sealed class TemporaryHttpServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinXinAiHttpTests", Guid.NewGuid().ToString("N"));
    private readonly FakeFirewall _firewall = new();
    private readonly HttpClient _client = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private TemporaryHttpService _service = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        _service = new TemporaryHttpService(_firewall);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _firewall.FailClose = false;
        await _service.DisposeAsync();
        _client.Dispose();
        Directory.Delete(_root, true);
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private string Url(string path = "") => $"http://127.0.0.1:{_service.Active!.Port}/{path}";

    [Fact]
    public async Task StartServesIndexAndJsonAndHeadWithoutWritingFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "index.html"), "<h1>hello</h1>");
        await File.WriteAllTextAsync(Path.Combine(_root, "data.json"), "{\"ok\":true}");
        await _service.StartAsync(new(FreePort(), _root));
        Assert.Equal("<h1>hello</h1>", await _client.GetStringAsync(Url()));
        using var json = await _client.GetAsync(Url("data.json"));
        Assert.Equal("application/json", json.Content.Headers.ContentType!.MediaType);
        using var head = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, Url("data.json")));
        Assert.Equal(11, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        using var post = await _client.PostAsync(Url("new.txt"), new StringContent("do not upload"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.False(File.Exists(Path.Combine(_root, "new.txt")));
        Assert.Equal(4, _service.RequestCount);
        Assert.Single(_firewall.Rules);
    }

    [Fact]
    public async Task ListingEscapesNamesAndNestedDirectoriesHaveRelativeRedirects()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "a & b.txt"), "sample");
        Directory.CreateDirectory(Path.Combine(_root, "nested"));
        await _service.StartAsync(new(FreePort(), _root));
        var html = await _client.GetStringAsync(Url());
        Assert.Contains("a &amp; b.txt", html);
        Assert.Contains("a%20%26%20b.txt", html);
        Assert.DoesNotContain(_root, html);
        using var redirect = await _client.GetAsync(Url("nested"));
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal("/nested/", redirect.Headers.Location!.OriginalString);
        Assert.Equal("sample", await _client.GetStringAsync(Url("a%20%26%20b.txt")));
        using var missing = await _client.GetAsync(Url("missing"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("/../secret")]
    [InlineData("/%2e%2e/secret")]
    [InlineData("/folder%2f..%2fsecret")]
    [InlineData("/..%5csecret")]
    [InlineData("/file.txt:stream")]
    [InlineData("/folder./file")]
    [InlineData("/folder%20/file")]
    [InlineData("/file%00.txt")]
    public void UnsafePathsCannotResolveOutsideRoot(string path) => Assert.Null(TemporaryHttpService.ResolvePath(_root, path));

    [Fact]
    public async Task SymbolicLinksCannotExposeOutsideFiles()
    {
        var outside = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outside, "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(_root, "linked.txt"), outside);
            Assert.Null(TemporaryHttpService.ResolvePath(_root, "/linked.txt"));
            await _service.StartAsync(new(FreePort(), _root));
            using var response = await _client.GetAsync(Url("linked.txt"));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain("linked.txt", await _client.GetStringAsync(Url()));
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public async Task StopReleasesPortAndRestartAppliesNewDirectoryAndPort()
    {
        var first = FreePort();
        await File.WriteAllTextAsync(Path.Combine(_root, "index.html"), "one");
        await _service.StartAsync(new(first, _root));
        var secondDirectory = Directory.CreateDirectory(Path.Combine(_root, "two")).FullName;
        await File.WriteAllTextAsync(Path.Combine(secondDirectory, "index.html"), "two");
        var second = FreePort();
        await _service.RestartAsync(new(second, secondDirectory));
        Assert.Equal("two", await _client.GetStringAsync(Url()));
        Assert.Single(_firewall.Rules);
        Assert.Equal(second, _firewall.Rules.Values.Single());
        using var released = new TcpListener(IPAddress.Any, first);
        released.Start();
        await _service.StopAsync();
        Assert.False(_service.IsRunning);
        Assert.Empty(_firewall.Rules);
        await _service.StopAsync();
        await _service.StartAsync(new(second, _root));
        Assert.Equal("one", await _client.GetStringAsync(Url()));
    }

    [Fact]
    public async Task InvalidRestartKeepsWorkingServerAndOccupiedPortDoesNotOpenFirewall()
    {
        await _service.StartAsync(new(FreePort(), _root));
        var original = _service.Active;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.RestartAsync(new(65536, _root)));
        Assert.Equal(original, _service.Active);
        await _service.StopAsync();
        using var occupied = new TcpListener(IPAddress.Any, 0);
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        await Assert.ThrowsAnyAsync<IOException>(() => _service.StartAsync(new(port, _root)));
        Assert.False(_service.IsRunning);
        Assert.Empty(_firewall.Rules);
    }

    [Fact]
    public async Task FirewallFailureRollsBackListenerAndCleanupCanBeRetried()
    {
        _firewall.FailOpen = true;
        var port = FreePort();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartAsync(new(port, _root)));
        Assert.False(_service.IsRunning);
        Assert.Empty(_firewall.Rules);
        _firewall.FailOpen = false;
        await _service.StartAsync(new(port, _root));
        _firewall.FailClose = true;
        await Assert.ThrowsAsync<InvalidOperationException>(_service.StopAsync);
        Assert.False(_service.IsRunning);
        Assert.True(_service.NeedsFirewallCleanup);
        using var released = new TcpListener(IPAddress.Any, port);
        released.Start();
        _firewall.FailClose = false;
        await _service.StopAsync();
        Assert.False(_service.NeedsFirewallCleanup);
        Assert.Empty(_firewall.Rules);
    }

    [Fact]
    public async Task CorsIsOptInAndSupportsReadPreflight()
    {
        await _service.StartAsync(new(FreePort(), _root));
        using var normal = await _client.GetAsync(Url());
        Assert.False(normal.Headers.Contains("Access-Control-Allow-Origin"));
        await _service.RestartAsync(new(_service.Active!.Port, _root, true));
        using var preflight = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, Url()));
        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal("*", preflight.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("GET", preflight.Headers.GetValues("Access-Control-Allow-Methods").Single());
    }

    [Fact]
    public async Task DisposeClosesFirewallAndRejectsNewStarts()
    {
        await _service.StartAsync(new(FreePort(), _root));
        await _service.DisposeAsync();
        Assert.Empty(_firewall.Rules);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _service.StartAsync(new(FreePort(), _root)));
    }

    [Fact]
    public void OpenedHandleCannotBeOutsideRootOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var outside = Path.GetTempFileName();
        try
        {
            using var file = File.OpenRead(outside);
            Assert.Throws<UnauthorizedAccessException>(() => HttpFileBoundary.VerifyOpenedFile(file, _root));
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public async Task IPv6PortConflictRollsBackTheIpv4Listener()
    {
        if (!Socket.OSSupportsIPv6) return;
        using var occupied = new TcpListener(IPAddress.IPv6Any, 0);
        occupied.Server.DualMode = false;
        occupied.Start();
        var port = ((IPEndPoint)occupied.LocalEndpoint).Port;
        await Assert.ThrowsAnyAsync<IOException>(() => _service.StartAsync(new(port, _root)));
        Assert.False(_service.IsRunning);
        Assert.Empty(_firewall.Rules);
        using var ipv4 = new TcpListener(IPAddress.Any, port);
        ipv4.Start();
    }

    private sealed class FakeFirewall : ITemporaryHttpFirewall
    {
        public Dictionary<string, int> Rules { get; } = new();
        public bool FailOpen { get; set; }
        public bool FailClose { get; set; }
        public Task OpenAsync(string ruleName, int port)
        {
            Rules[ruleName] = port;
            if (FailOpen) throw new InvalidOperationException("simulated partial firewall write");
            return Task.CompletedTask;
        }
        public Task CloseAsync(string ruleName)
        {
            if (FailClose) throw new InvalidOperationException("simulated firewall cleanup failure");
            Rules.Remove(ruleName);
            return Task.CompletedTask;
        }
    }
}
