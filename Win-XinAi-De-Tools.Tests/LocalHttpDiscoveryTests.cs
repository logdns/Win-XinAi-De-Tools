using System.Net;
using System.Net.Sockets;
using System.Text;
using PortManager.Services;
using Xunit;

namespace WinXinAiDeTools.Tests;

public sealed class LocalHttpDiscoveryTests
{
    [Theory]
    [InlineData("0.0.0.0", "http://127.0.0.1:8980/")]
    [InlineData("::", "http://[::1]:8980/")]
    [InlineData("192.168.1.5", "http://192.168.1.5:8980/")]
    [InlineData("2001:db8::1", "http://[2001:db8::1]:8980/")]
    public void WildcardBindingsUseLoopback(string address, string expected) =>
        Assert.Equal(expected, LocalHttpDiscovery.GetProbeUrl(new(IPAddress.Parse(address), 8980, null)));

    [Theory]
    [InlineData(200)]
    [InlineData(301)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(500)]
    public async Task ConfirmsAnyHttpStatusWithoutFollowingRedirectOrReadingBody(int status)
    {
        using var redirectTarget = new TcpListener(IPAddress.Loopback, 0);
        redirectTarget.Start();
        var targetPort = ((IPEndPoint)redirectTarget.LocalEndpoint).Port;
        await using var fixture = new Fixture($"HTTP/1.1 {status} Test\r\nLocation: http://127.0.0.1:{targetPort}/\r\nContent-Length: 1000000\r\n\r\n");
        var listener = fixture.Listener with { ProcessId = int.MaxValue };
        var discovery = new LocalHttpDiscovery(() => new[] { listener, listener });
        var result = await discovery.ScanAsync();
        var server = Assert.Single(result.Servers);
        Assert.Equal(status, server.StatusCode);
        Assert.Equal(listener.Port, server.Port);
        Assert.Null(server.ProcessName);
        Assert.False(result.IsPartial);
        Assert.StartsWith("HEAD / HTTP/1.1", await fixture.Request.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(redirectTarget.Pending());
    }

    [Theory]
    [InlineData("SSH-2.0-Test\r\n")]
    [InlineData("")]
    public async Task NonHttpAndSilentSocketsAreNotReported(string response)
    {
        await using var fixture = new Fixture(response);
        var result = await new LocalHttpDiscovery(() => new[] { fixture.Listener }).ScanAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(result.Servers);
    }

    [Fact]
    public async Task CancellationInterruptsActiveProbes()
    {
        await using var fixture = new Fixture("");
        using var cancel = new CancellationTokenSource();
        var scan = new LocalHttpDiscovery(() => new[] { fixture.Listener }).ScanAsync(cancel.Token);
        await fixture.Request.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scan);
    }

    [Fact]
    public async Task EnumeratesAndConfirmsRealListener()
    {
        await using var fixture = new Fixture("HTTP/1.1 204 No Content\r\n\r\n");
        var listeners = LocalHttpDiscovery.ReadListeners();
        var actual = Assert.Single(listeners, x => x.Port == fixture.Listener.Port && x.Address.Equals(IPAddress.Loopback));
        if (OperatingSystem.IsWindows()) Assert.Equal(Environment.ProcessId, actual.ProcessId);
        var result = await new LocalHttpDiscovery(() => new[] { actual }).ScanAsync();
        Assert.Equal(204, Assert.Single(result.Servers).StatusCode);
    }

    [Fact]
    public async Task EnumeratesAndConfirmsIpv6Listener()
    {
        if (!Socket.OSSupportsIPv6) return;
        await using var fixture = new Fixture("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n", IPAddress.IPv6Loopback);
        var actual = Assert.Single(LocalHttpDiscovery.ReadListeners(), x => x.Port == fixture.Listener.Port && x.Address.Equals(IPAddress.IPv6Loopback));
        if (OperatingSystem.IsWindows()) Assert.Equal(Environment.ProcessId, actual.ProcessId);
        var result = await new LocalHttpDiscovery(() => new[] { actual }).ScanAsync();
        Assert.Contains("[::1]", Assert.Single(result.Servers).Url);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TcpListener _socket;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serve;
        public LocalTcpListener Listener { get; }
        public TaskCompletionSource<string> Request { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Fixture(string response, IPAddress? address = null)
        {
            _socket = new TcpListener(address ?? IPAddress.Loopback, 0);
            _socket.Start();
            var endpoint = (IPEndPoint)_socket.LocalEndpoint;
            Listener = new(endpoint.Address, endpoint.Port, Environment.ProcessId);
            _serve = ServeAsync(response);
        }
        private async Task ServeAsync(string response)
        {
            try
            {
                using var client = await _socket.AcceptTcpClientAsync(_stop.Token);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);
                var line = await reader.ReadLineAsync(_stop.Token);
                Request.TrySetResult(line ?? "");
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_stop.Token))) { }
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
                await Task.Delay(Timeout.Infinite, _stop.Token);
            }
            catch (OperationCanceledException) { }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _socket.Stop();
            await _serve;
            _stop.Dispose();
        }
    }
}
