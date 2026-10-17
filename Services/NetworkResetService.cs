using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PortManager.Models;

namespace PortManager.Services;

public static class NetworkResetService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private const string Preamble = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); ";

    internal const string SnapshotScript = Preamble + "[pscustomobject]@{CapturedAt=[DateTimeOffset]::Now.ToString('o');Adapters=@(Get-NetAdapter -IncludeHidden | Select-Object Name,InterfaceGuid,ifIndex,Status);Addresses=@(Get-NetIPAddress | Select-Object InterfaceIndex,AddressFamily,IPAddress,PrefixLength,PrefixOrigin);Interfaces=@(Get-NetIPInterface | Select-Object InterfaceIndex,AddressFamily,Dhcp,InterfaceMetric);Dns=@(Get-DnsClientServerAddress | Select-Object InterfaceIndex,AddressFamily,ServerAddresses);Routes=@(Get-NetRoute | Select-Object InterfaceIndex,AddressFamily,DestinationPrefix,NextHop,RouteMetric,Protocol)} | ConvertTo-Json -Depth 6 -Compress";

    internal static string BuildResetScript(NetworkResetRequest request)
    {
        if (!Enum.IsDefined(request.Mode)) throw new ArgumentException("Unknown network reset mode.");
        return request.Mode switch
        {
            NetworkResetMode.FlushDns => Preamble + "Clear-DnsClientCache -ErrorAction Stop; 'DNS cache cleared.'",
            NetworkResetMode.AdapterDhcp when request.AdapterId != Guid.Empty => Preamble +
                $"$a=@(Get-NetAdapter -IncludeHidden | Where-Object {{[guid]$_.InterfaceGuid -eq [guid]'{request.AdapterId:D}'}}); if($a.Count -ne 1 -or $a[0].Status -eq 'Disabled'){{throw 'Selected adapter is missing or disabled. Refresh before retrying.'}}; $index=$a[0].ifIndex; " +
                "Get-NetRoute -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction Stop | Where-Object {$_.Protocol -eq 'NetMgmt' -and $_.DestinationPrefix -eq '0.0.0.0/0'} | Remove-NetRoute -Confirm:$false -ErrorAction Stop; " +
                "Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction Stop | Where-Object {$_.PrefixOrigin -eq 'Manual'} | Remove-NetIPAddress -Confirm:$false -ErrorAction Stop; " +
                "Set-NetIPInterface -InterfaceIndex $index -AddressFamily IPv4 -Dhcp Enabled -ErrorAction Stop; Set-DnsClientServerAddress -InterfaceIndex $index -ResetServerAddresses -ErrorAction Stop; 'Selected adapter restored to IPv4 DHCP and automatic DNS.'",
            NetworkResetMode.AdapterDhcp => throw new ArgumentException("Select a valid adapter GUID."),
            NetworkResetMode.SystemStack => throw new ArgumentException("System stack reset uses separately checked native commands."),
            _ => throw new ArgumentException("Unknown network reset mode.")
        };
    }

    public static async Task<NetworkResetResult> ResetAsync(NetworkResetRequest request)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Network reset is available only on Windows.");
        if (!Enum.IsDefined(request.Mode)) throw new ArgumentException("Unknown network reset mode.");
        if (request.Mode == NetworkResetMode.AdapterDhcp && request.AdapterId == Guid.Empty)
            throw new ArgumentException("Select a valid adapter GUID.");
        await Gate.WaitAsync();
        try
        {
            var snapshot = await PowerShellAsync(SnapshotScript);
            using var parsed = JsonDocument.Parse(snapshot);
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Win-XinAi-De-Tools", "network-backups", $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var backup = Path.Combine(directory, "network.json");
            await File.WriteAllTextAsync(backup, snapshot, new UTF8Encoding(false));
            var steps = new List<NetworkResetStep>();
            if (request.Mode == NetworkResetMode.SystemStack)
            {
                foreach (var command in new[] { new[] { "winsock", "reset" }, new[] { "int", "ip", "reset", Path.Combine(directory, "tcpip-reset.log") } })
                {
                    try
                    {
                        var output = await RunAsync(SystemTool("netsh.exe"), command, TimeSpan.FromMinutes(2));
                        steps.Add(new NetworkResetStep(string.Join(' ', command.Take(3)), true, output));
                    }
                    catch (Exception ex) { steps.Add(new NetworkResetStep(string.Join(' ', command.Take(3)), false, ex.Message)); }
                }
            }
            else
            {
                try { steps.Add(new NetworkResetStep(request.Mode.ToString(), true, await PowerShellAsync(BuildResetScript(request)))); }
                catch (Exception ex) { steps.Add(new NetworkResetStep(request.Mode.ToString(), false, ex.Message)); }
            }
            var result = new NetworkResetResult(backup, request.Mode == NetworkResetMode.SystemStack, steps);
            AuditLogService.Record("ResetLocalNetwork", JsonSerializer.Serialize(new { request, result }), result.Success);
            return result;
        }
        catch (Exception ex)
        {
            AuditLogService.Record("ResetLocalNetwork", ex.Message, false);
            throw;
        }
        finally { Gate.Release(); }
    }

    private static Task<string> PowerShellAsync(string script) => RunAsync(SystemTool(@"WindowsPowerShell\v1.0\powershell.exe"),
        new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }, TimeSpan.FromMinutes(2));

    private static string SystemTool(string relative) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32", relative);

    private static async Task<string> RunAsync(string file, IEnumerable<string> arguments, TimeSpan timeout)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileName(file).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding = Encoding.UTF8;
        }
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start the Windows network command.");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try { await process.WaitForExitAsync(cancellation.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new TimeoutException("Network command timed out. Changes may already have been applied; inspect the backup and current state locally.");
        }
        var text = await output;
        var errors = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Windows network command failed ({process.ExitCode}): {errors} {text}".Trim());
        return text.Trim();
    }
}
