using System.Text.Json;
using PortManager.Models;
using PortManager.Services;

namespace WinXinAi.Engine;

public sealed class Dispatcher : IAsyncDisposable
{
    private readonly TemporaryHttpService _http = new(new TemporaryHttpFirewall());
    private static readonly HashSet<string> Reads = new(StringComparer.Ordinal)
    {
        "health", "dashboard", "firewall.list", "firewall.query", "network.list", "network.read",
        "smb.status", "smb.addresses", "hyperv.status", "wsl.status", "wsl.online", "wsl.version", "wsl.disk", "wsl.usb",
        "connections.list", "http.status", "http.discover", "audit.list", "firewall.export", "shutdown"
    };

    public async Task<object?> ExecuteAsync(Request request)
    {
        LanguageState.Current = request.Language == "en-US" ? AppLanguage.English : AppLanguage.Chinese;
        if (!Reads.Contains(request.Op) && !request.Confirmed)
            throw new InvalidOperationException("Explicit confirmation is required before a system operation.");
        var a = request.Args;
        string Text(string key, string fallback = "") => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(key, out var v) ? v.GetString() ?? fallback : fallback;
        int Number(string key, int fallback = 0)
        {
            var value = Text(key);
            var number = string.IsNullOrWhiteSpace(value) ? fallback : int.TryParse(value, out var parsed) ? parsed : throw new ArgumentException($"Invalid integer: {key}.");
            if (key is "port" or "listenPort" && number is < 1 or > 65535) throw new ArgumentException("Port must be between 1 and 65535.");
            return number;
        }
        bool Flag(string key) => bool.TryParse(Text(key), out var v) && v;
        T Data<T>(string key) => JsonSerializer.Deserialize<T>(Text(key), Program.Json) ?? throw new ArgumentException($"Invalid {key}.");
        NetworkAdapterModel Adapter() => new() { Name = Text("adapter"), InterfaceGuid = Guid.TryParse(Text("adapterId"), out var g) ? g : Guid.Empty };
        async Task Done(Task operation) { await operation; }
        object HttpStatus() => new { _http.IsRunning, _http.Active, _http.RequestCount, _http.NeedsFirewallCleanup, urls = _http.IsRunning ? TemporaryHttpService.GetAccessUrls(_http.Active!.Port) : Array.Empty<string>() };
        try
        {
            object? result = null;
            switch (request.Op)
            {
                case "health": result = new { version = "1.12.2", engine = "shared-dotnet", platform = Environment.OSVersion.ToString() }; break;
                case "dashboard":
                    var rules = await FirewallService.ListRulesAsync();
                    result = new { rules = rules.Count, inbound = rules.Count(r => r.Direction == "Inbound"), outbound = rules.Count(r => r.Direction == "Outbound") }; break;
                case "firewall.list": result = await FirewallService.ListRulesAsync(); break;
                case "firewall.query": result = await FirewallService.QueryPortAsync(Number("port")); break;
                case "firewall.add": result = await FirewallService.AddRuleAsync(Number("port"), Text("protocol", "TCP"), Text("direction", "in"), Text("name"), Text("application")); break;
                case "firewall.modify": result = await FirewallService.ModifyRuleAsync(Text("oldName"), Number("port"), Text("protocol", "TCP"), Text("direction", "in"), Text("name")); break;
                case "firewall.delete": result = await FirewallService.DeleteRuleAsync(Text("name")); break;
                case "firewall.export": result = RuleTransferService.Serialize(await FirewallService.ListRulesAsync()); break;
                case "firewall.import": result = await RuleTransferService.ImportAsync(RuleTransferService.Parse(Text("json")).Rules); break;
                case "network.list": result = await NetworkConfigurationService.ListAdaptersAsync(); break;
                case "network.read": result = await NetworkConfigurationService.GetConfigurationAsync(Adapter()); break;
                case "network.apply": await Done(NetworkConfigurationService.ApplyAsync(Adapter(), new NetworkConfigurationRequest { UseDhcp = Text("mode", "DHCP") == "DHCP", IPv4Address = Text("ip"), PrefixLength = Number("prefix", 24), Gateway = Text("gateway"), PrimaryDns = Text("dns"), SecondaryDns = Text("dns2"), RouteMetric = Number("metric", 25) })); break;
                case "network.reset":
                    if (!Enum.TryParse<NetworkResetMode>(Text("mode"), out var resetMode) || !Enum.IsDefined(resetMode)) throw new ArgumentException("Invalid reset mode.");
                    result = await NetworkResetService.ResetAsync(new(resetMode, Adapter().InterfaceGuid)); break;
                case "smb.status": result = await SmbConfigurationService.GetStatusAsync(); break;
                case "smb.addresses": result = SmbConfigurationService.GetAccessAddresses(); break;
                case "smb.features": await Done(SmbConfigurationService.ApplyFeaturesAsync(new() { SmbDirectEnabled = Flag("direct"), Smb1Enabled = Flag("smb1"), ForceRestart = false })); break;
                case "smb.share": await Done(SmbConfigurationService.SetShareAsync(Text("path"))); break;
                case "smb.remove": await Done(SmbConfigurationService.RemoveShareAsync()); break;
                case "hyperv.status": result = await HyperVService.GetStatusAsync(); break;
                case "hyperv.enable": await Done(HyperVService.EnableAsync()); break;
                case "hyperv.disable": await Done(HyperVService.DisableFeatureAsync()); break;
                case "hyperv.complete": await Done(HyperVService.CompleteDisableAsync()); break;
                case "hyperv.adapters": await Done(HyperVService.SetAdaptersEnabledAsync(Text("ids").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray(), Flag("enable"))); break;
                case "hyperv.switch.create":
                    if (!Enum.TryParse<HyperVSwitchType>(Text("type"), out var switchType) || !Enum.IsDefined(switchType)) throw new ArgumentException("Invalid switch type.");
                    await Done(HyperVService.CreateSwitchAsync(new(Text("name"), switchType, Guid.TryParse(Text("physicalId"), out var physical) ? physical : Guid.Empty, Flag("share")))); break;
                case "hyperv.switch.remove": await Done(HyperVService.RemoveSwitchAsync(Guid.Parse(Text("id")))); break;
                case "connections.list": result = await ConnectionService.ListConnectionsAsync(); break;
                case "connections.close": await Done(ConnectionService.CloseTcpConnectionAsync(Data<ConnectionModel>("connection"))); break;
                case "connections.terminate": await Done(ConnectionService.TerminateProcessAsync(Number("pid"))); break;
                case "audit.list": result = await AuditLogService.ReadAsync(); break;
                case "audit.clear": await Done(AuditLogService.ClearAsync()); break;
                case "http.status": result = HttpStatus(); break;
                case "http.discover": result = await new LocalHttpDiscovery().ScanAsync(); break;
                case "http.start": await Done(_http.StartAsync(new(Number("port", 8980), Text("path"), Flag("cors")))); result = HttpStatus(); break;
                case "http.restart": await Done(_http.RestartAsync(new(Number("port", 8980), Text("path"), Flag("cors")))); result = HttpStatus(); break;
                case "http.stop": await Done(_http.StopAsync()); result = HttpStatus(); break;
                case "wsl.status": result = await WslService.GetStatusAsync(); break;
                case "wsl.online": result = await WslService.ListOnlineDistributionsAsync(); break;
                case "wsl.version": result = await WslService.GetVersionInfoAsync(); break;
                case "wsl.install": await Done(WslService.InstallAsync()); break;
                case "wsl.install-distro": await Done(WslService.InstallDistributionAsync(Text("distribution", "Ubuntu"))); break;
                case "wsl.update": await Done(WslService.UpdateAsync()); break;
                case "wsl.start": await Done(WslService.StartAsync(Text("distribution"))); break;
                case "wsl.stop": await Done(WslService.StopAsync(Text("distribution"))); break;
                case "wsl.default": await Done(WslService.SetDefaultAsync(Text("distribution"))); break;
                case "wsl.unregister": await Done(WslService.UnregisterAsync(Text("distribution"))); break;
                case "wsl.shutdown": await Done(WslService.ShutdownAllAsync()); break;
                case "wsl.export": await Done(WslService.ExportAsync(Text("distribution"), Text("path"))); break;
                case "wsl.import": await Done(WslService.ImportAsync(Text("distribution"), Text("target"), Text("path"), Number("version", 2))); break;
                case "wsl.move": await Done(WslService.MoveAsync(Text("distribution"), Text("path"))); break;
                case "wsl.disk": result = await WslService.GetDiskUsageAsync(Text("distribution")); break;
                case "wsl.terminal": WslService.OpenTerminal(Text("distribution")); break;
                case "wsl.explorer": WslService.OpenExplorer(Text("distribution")); break;
                case "wsl.code": WslService.OpenVsCode(Text("distribution")); break;
                case "wsl.autostart": await Done(WslService.SetAutostartAsync(Text("distribution"), Flag("enable"))); break;
                case "wsl.schedule": await Done(WslService.ScheduleCommandAsync(Text("name"), Text("distribution"), Text("command"), Text("schedule", "DAILY"), Text("time", "09:00"))); break;
                case "wsl.unschedule": await Done(WslService.RemoveScheduledCommandAsync(Text("name"))); break;
                case "wsl.port.add": await Done(WslService.AddPortProxyAsync(Number("listenPort"), Text("address"), Number("port"))); break;
                case "wsl.port.remove": await Done(WslService.RemovePortProxyAsync(Number("listenPort"))); break;
                case "wsl.proxy": await Done(WslService.SetHttpProxyAsync(Text("proxy"))); break;
                case "wsl.mount": await Done(WslService.MountVhdAsync(Text("path"))); break;
                case "wsl.unmount": await Done(WslService.UnmountVhdAsync(Text("path"))); break;
                case "wsl.usb": result = await WslService.ListUsbDevicesAsync(); break;
                case "wsl.usb.bind": await Done(WslService.BindUsbDeviceAsync(Text("busId"))); break;
                case "wsl.usb.attach": await Done(WslService.AttachUsbDeviceAsync(Text("busId"), Text("distribution"))); break;
                case "wsl.usb.detach": await Done(WslService.DetachUsbDeviceAsync(Text("busId"))); break;
                case "shutdown": await Done(_http.StopAsync()); break;
                default: throw new ArgumentException("Unsupported operation.");
            }
            if (!Reads.Contains(request.Op))
            {
                var success = result switch { OperationResult operation => operation.Success, NetworkResetResult reset => reset.Success, bool value => value, _ => true };
                AuditLogService.Record("MyGo:" + request.Op, "Operation completed; inspect result for per-step or partial failures.", success);
            }
            return result ?? new { completed = true };
        }
        catch (Exception ex)
        {
            AuditLogService.Record("MyGo:" + request.Op, ex.Message, false);
            throw;
        }
    }

    public ValueTask DisposeAsync() => _http.DisposeAsync();
}
