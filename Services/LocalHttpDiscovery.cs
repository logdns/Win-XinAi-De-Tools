using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace PortManager.Services;

public sealed record LocalTcpListener(IPAddress Address, int Port, int? ProcessId);
public sealed record LocalHttpServer(string Url, int Port, int StatusCode, int? ProcessId, string? ProcessName);
public sealed record LocalHttpScan(IReadOnlyList<LocalHttpServer> Servers, bool IsPartial);

/// <summary>Confirms HTTP on local TCP listeners only. Never follows redirects or uses a proxy.</summary>
public sealed class LocalHttpDiscovery
{
    private readonly Func<IReadOnlyList<LocalTcpListener>> _listeners;
    public LocalHttpDiscovery() : this(ReadListeners) { }
    internal LocalHttpDiscovery(Func<IReadOnlyList<LocalTcpListener>> listeners) => _listeners = listeners;

    public async Task<LocalHttpScan> ScanAsync(CancellationToken cancellationToken = default)
    {
        var listeners = await Task.Run(_listeners, cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var handler = new SocketsHttpHandler
        {
            UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
            Credentials = null, ConnectTimeout = TimeSpan.FromSeconds(1), MaxResponseHeadersLength = 16
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };
        var found = new ConcurrentBag<LocalHttpServer>();
        var candidates = listeners.Select(listener => (Listener: listener, Url: GetProbeUrl(listener)))
            .DistinctBy(candidate => candidate.Url).ToArray();
        try
        {
            await Parallel.ForEachAsync(candidates, new ParallelOptions
            {
                MaxDegreeOfParallelism = 16, CancellationToken = deadline.Token
            }, async (candidate, token) =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, candidate.Url);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    var pid = candidate.Listener.ProcessId;
                    found.Add(new(candidate.Url, candidate.Listener.Port, (int)response.StatusCode, pid, ProcessName(pid)));
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        cancellationToken.ThrowIfCancellationRequested();
        return new(found.OrderBy(server => server.Port).ThenBy(server => server.Url, StringComparer.Ordinal).ToArray(), deadline.IsCancellationRequested);
    }

    internal static string GetProbeUrl(LocalTcpListener listener)
    {
        var address = listener.Address.Equals(IPAddress.Any) ? IPAddress.Loopback
            : listener.Address.Equals(IPAddress.IPv6Any) ? IPAddress.IPv6Loopback : listener.Address;
        return new UriBuilder(Uri.UriSchemeHttp, address.ToString(), listener.Port).Uri.AbsoluteUri;
    }

    private static string? ProcessName(int? pid)
    {
        if (pid is null) return null;
        try { using var process = Process.GetProcessById(pid.Value); return process.ProcessName; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException) { return null; }
    }

    internal static IReadOnlyList<LocalTcpListener> ReadListeners()
    {
        if (!OperatingSystem.IsWindows())
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Select(endpoint => new LocalTcpListener(endpoint.Address, endpoint.Port, null)).ToArray();
        var result = new List<LocalTcpListener>();
        ReadWindowsTable(2, result);
        if (Socket.OSSupportsIPv6) ReadWindowsTable(23, result);
        return result;
    }

    private static void ReadWindowsTable(int family, List<LocalTcpListener> listeners)
    {
        // TCP_TABLE_OWNER_PID_LISTENER. IPv4 rows: 24 bytes; IPv6 rows: 56 bytes.
        var size = 0;
        var error = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 3, 0);
        if (error != 0 && error != 122) throw new Win32Exception(error);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var allocated = Math.Max(size, 4);
            var buffer = Marshal.AllocHGlobal(allocated);
            try
            {
                error = GetExtendedTcpTable(buffer, ref size, false, family, 3, 0);
                if (error == 122) continue; // The listener table grew between reads.
                if (error != 0) throw new Win32Exception(error);
                var count = Marshal.ReadInt32(buffer);
                var rowSize = family == 2 ? 24 : 56;
                if (count < 0 || count > (allocated - 4) / rowSize) throw new IOException("Invalid Windows TCP listener table.");
                for (var i = 0; i < count; i++)
                {
                    var row = IntPtr.Add(buffer, 4 + i * rowSize);
                    var bytes = new byte[family == 2 ? 4 : 16];
                    Marshal.Copy(IntPtr.Add(row, family == 2 ? 4 : 0), bytes, 0, bytes.Length);
                    var address = family == 2 ? new IPAddress(bytes)
                        : new IPAddress(bytes, unchecked((uint)Marshal.ReadInt32(row, 16)));
                    var port = (ushort)IPAddress.NetworkToHostOrder(unchecked((short)Marshal.ReadInt32(row, family == 2 ? 8 : 20)));
                    var pid = Marshal.ReadInt32(row, family == 2 ? 20 : 52);
                    listeners.Add(new(address, port, pid >= 0 ? pid : null));
                }
                return;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new Win32Exception(error);
    }

    [DllImport("iphlpapi.dll")]
    private static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int tableClass, uint reserved);
}
