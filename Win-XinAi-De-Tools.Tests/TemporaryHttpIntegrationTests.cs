using System.Net;
using System.Net.Sockets;
using PortManager.Services;
using Xunit;

namespace WinXinAiDeTools.Tests;

public sealed class TemporaryHttpIntegrationTests
{
    [Fact]
    [Trait("Category", "WindowsIntegration")]
    public async Task RealFirewallRuleAllowsAllProfilesAndIsRemovedAfterStop()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("PORTMANAGER_RUN_INTEGRATION") != "1") return;
        var root = Path.Combine(Path.GetTempPath(), "WinXinAiHttpIntegration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var probe = new TcpListener(IPAddress.Any, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        await using var service = new TemporaryHttpService(new TemporaryHttpFirewall());
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "HTTP integration OK");
            await service.StartAsync(new(port, root));
            var rules = await FirewallService.ListRulesAsync();
            var owned = Assert.Single(rules.Where(r => r.Name.StartsWith("Win-XinAi-De-Tools Temporary HTTP ") && r.LocalPort == port.ToString()));
            Assert.Equal("Inbound", owned.Direction);
            Assert.Equal("TCP", owned.Protocol);
            Assert.Equal("Any", owned.Profile);
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
            var address = TemporaryHttpService.GetAccessUrls(port).FirstOrDefault(a => !a.Contains("127.0.0.1") && !a.Contains('[')) ?? $"http://127.0.0.1:{port}/";
            Assert.Equal("HTTP integration OK", await client.GetStringAsync(address));
            await service.StopAsync();
            Assert.DoesNotContain(await FirewallService.ListRulesAsync(), r => r.Name == owned.Name);
        }
        finally { await service.StopAsync(); Directory.Delete(root, true); }
    }
}
