using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

namespace PortManager.Services;

public sealed record TemporaryHttpOptions(int Port, string Directory, bool AllowCors = false)
{
    public const int DefaultPort = 8980;

    public TemporaryHttpOptions Validate()
    {
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (string.IsNullOrWhiteSpace(Directory)) throw new ArgumentException("Select a directory first.");
        var root = Path.GetFullPath(Directory);
        if (!System.IO.Directory.Exists(root)) throw new DirectoryNotFoundException("The selected directory does not exist.");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("Select a real directory, not a symbolic link or junction.");
        return this with { Directory = root };
    }
}

public interface ITemporaryHttpFirewall
{
    Task OpenAsync(string ruleName, int port);
    Task CloseAsync(string ruleName);
}

/// <summary>One application-owned HTTP server. UI navigation does not own its lifetime.</summary>
public sealed class TemporaryHttpService(ITemporaryHttpFirewall firewall) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _ruleName = $"Win-XinAi-De-Tools Temporary HTTP {Guid.NewGuid():N}";
    private readonly FileExtensionContentTypeProvider _contentTypes = new();
    private WebApplication? _server;
    private bool _firewallPending;
    private bool _disposed;
    private long _requests;
    private TemporaryHttpOptions? _active;
    public TemporaryHttpOptions? Active => Volatile.Read(ref _active);
    public bool IsRunning => Active is not null;
    public long RequestCount => Interlocked.Read(ref _requests);
    public bool NeedsFirewallCleanup => Volatile.Read(ref _firewallPending);

    public Task StartAsync(TemporaryHttpOptions options) => ChangeAsync(options, false);
    public Task RestartAsync(TemporaryHttpOptions options) => ChangeAsync(options, true);

    private async Task ChangeAsync(TemporaryHttpOptions options, bool restart)
    {
        // Invalid replacement settings must not stop a working server.
        options = options.Validate();
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_server is not null && !restart) throw new InvalidOperationException("The HTTP server is already running.");
            await StopCoreAsync().ConfigureAwait(false);
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
            {
                Args = [], ContentRootPath = AppContext.BaseDirectory, ApplicationName = typeof(TemporaryHttpService).Assembly.GetName().Name
            });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server =>
            {
                server.AddServerHeader = false;
                server.Limits.MaxConcurrentConnections = 64;
                server.Limits.MaxRequestBodySize = 65536;
                server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
                server.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(15);
                server.Listen(IPAddress.Any, options.Port, endpoint => endpoint.Protocols = HttpProtocols.Http1);
                if (Socket.OSSupportsIPv6)
                    server.Listen(IPAddress.IPv6Any, options.Port, endpoint => endpoint.Protocols = HttpProtocols.Http1);
            });
            builder.WebHost.UseSockets(transport => transport.CreateBoundListenSocket = endpoint =>
            {
                var socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    if (endpoint.AddressFamily == AddressFamily.InterNetworkV6) socket.DualMode = false;
                    socket.ExclusiveAddressUse = true;
                    socket.Bind(endpoint);
                    return socket;
                }
                catch { socket.Dispose(); throw; }
            });
            var app = builder.Build();
            app.Run(context => ServeAsync(context, options));
            try
            {
                await app.StartAsync().ConfigureAwait(false);
                _firewallPending = true; // Also clean up a partially successful firewall write.
                await firewall.OpenAsync(_ruleName, options.Port).ConfigureAwait(false);
                _server = app;
                Interlocked.Exchange(ref _requests, 0);
                Volatile.Write(ref _active, options);
            }
            catch (Exception startError)
            {
                await app.DisposeAsync().ConfigureAwait(false);
                try { await ClearFirewallAsync().ConfigureAwait(false); }
                catch (Exception cleanupError) { throw new AggregateException(startError, cleanupError); }
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task StopCoreAsync()
    {
        var app = _server;
        _server = null;
        Volatile.Write(ref _active, null);
        try
        {
            if (app is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await app.StopAsync(timeout.Token).ConfigureAwait(false); }
                finally { await app.DisposeAsync().ConfigureAwait(false); }
            }
        }
        finally { await ClearFirewallAsync().ConfigureAwait(false); }
    }

    private async Task ClearFirewallAsync()
    {
        if (!_firewallPending) return;
        await firewall.CloseAsync(_ruleName).ConfigureAwait(false);
        _firewallPending = false;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task ServeAsync(HttpContext context, TemporaryHttpOptions options)
    {
        Interlocked.Increment(ref _requests);
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.CacheControl = "no-store";
        if (options.AllowCors)
        {
            context.Response.Headers.AccessControlAllowOrigin = "*";
            context.Response.Headers.AccessControlAllowMethods = "GET, HEAD, OPTIONS";
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = options.AllowCors ? "GET, HEAD, OPTIONS" : "GET, HEAD";
            return;
        }
        try
        {
            var path = ResolvePath(options.Directory, context.Request.Path.Value ?? "/");
            if (path is null) { context.Response.StatusCode = 403; return; }
            if (Directory.Exists(path))
            {
                if (!context.Request.Path.Value!.EndsWith('/'))
                {
                    // A local relative redirect; never reflect an untrusted Host header.
                    context.Response.StatusCode = 301;
                    context.Response.Headers.Location = EscapeUrlPath(context.Request.Path.Value) + "/";
                    return;
                }
                foreach (var index in new[] { "index.html", "index.htm" })
                {
                    var candidate = Path.Combine(path, index);
                    if (File.Exists(candidate) && !IsLink(candidate)) { await SendFileAsync(context, candidate).ConfigureAwait(false); return; }
                }
                await ListDirectoryAsync(context, path, options.Directory).ConfigureAwait(false);
                return;
            }
            if (!File.Exists(path)) { context.Response.StatusCode = 404; return; }
            await SendFileAsync(context, path).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = ex is UnauthorizedAccessException ? 403 : 404;
            else context.Abort();
        }
    }

    internal static string? ResolvePath(string root, string requestPath)
    {
        if (IsLink(root)) return null;
        var decoded = Uri.UnescapeDataString(requestPath);
        if (decoded.Any(c => c == '\\' || c == ':' || char.IsControl(c))) return null;
        var segments = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var segment in segments)
        {
            // Reject Windows normalization aliases and NTFS alternate data streams as well as traversal.
            if (segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ')) return null;
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) && IsLink(current)) return null;
        }
        return current;
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    private static string EscapeUrlPath(string path) => "/" + string.Join('/', path.TrimStart('/').Split('/').Select(Uri.EscapeDataString));

    private async Task SendFileAsync(HttpContext context, string path)
    {
        // Stream from one open handle; cancellation on stop/client disconnect releases the file.
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        context.Response.ContentType = _contentTypes.TryGetContentType(path, out var type) ? type : "application/octet-stream";
        context.Response.ContentLength = file.Length;
        if (!HttpMethods.IsHead(context.Request.Method))
            await file.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task ListDirectoryAsync(HttpContext context, string path, string root)
    {
        var body = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Temporary HTTP server</title></head><body><h1>Files</h1><ul>");
        if (!string.Equals(path, root, StringComparison.Ordinal)) body.Append("<li><a href=\"../\">../</a></li>");
        var entries = Directory.EnumerateFileSystemEntries(path).Take(1001).ToArray();
        foreach (var entry in entries.Take(1000).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (IsLink(entry)) continue;
            var name = Path.GetFileName(entry);
            var suffix = Directory.Exists(entry) ? "/" : "";
            body.Append("<li><a href=\"").Append(Uri.EscapeDataString(name)).Append(suffix).Append("\">")
                .Append(WebUtility.HtmlEncode(name)).Append(suffix).Append("</a></li>");
        }
        body.Append("</ul>");
        if (entries.Length > 1000) body.Append("<p>Showing the first 1000 entries. Use a direct file URL for other files.</p>");
        body.Append("</body></html>");
        var bytes = Encoding.UTF8.GetBytes(body.ToString());
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";
        if (!HttpMethods.IsHead(context.Request.Method)) await context.Response.Body.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }

    public static IReadOnlyList<string> GetAccessUrls(int port)
    {
        var result = new List<string> { $"http://127.0.0.1:{port}/" };
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up))
        foreach (var address in adapter.GetIPProperties().UnicastAddresses.Select(a => a.Address))
        {
            if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6Multicast) continue;
            if (address.AddressFamily == AddressFamily.InterNetwork) result.Add($"http://{address}:{port}/");
            else if (address.AddressFamily == AddressFamily.InterNetworkV6) result.Add($"http://[{address}]:{port}/");
        }
        return result.Distinct().ToArray();
    }
}
