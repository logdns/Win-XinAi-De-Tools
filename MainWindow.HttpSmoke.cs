using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;
using PortManager.Views;
using Windows.ApplicationModel.DataTransfer;

namespace PortManager;

public sealed partial class MainWindow
{
    private async Task VerifyTemporaryHttpAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinXinAiHttpUi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var service = ((App)Microsoft.UI.Xaml.Application.Current).TemporaryHttp;
        await using var fixture = new TemporaryHttpService(new HttpSmokeFirewall());
        try
        {
            await fixture.StartAsync(new TemporaryHttpOptions(FreeHttpPort(), root));
            await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "temporary HTTP UI test");
            NavigateTo("TemporaryHttp");
            var page = (Page)PageHost.Content;
            Require(((NumberBox)page.FindName("PortInput")).Value == 8980, "HTTP default port");
            await WaitForHttpAsync(() => !((ProgressRing)page.FindName("ScanRing")).IsActive
                && ((TemporaryHttpPage)page).DetectedServers.Any(server => server.Port == fixture.Active!.Port));
            Require(!service.IsRunning, "Detection started managed HTTP server");
            ((TextBox)page.FindName("DirectoryInput")).Text = root;
            ((NumberBox)page.FindName("PortInput")).Value = FreeHttpPort();
            InvokeHttpButton(page, "StartButton");
            await WaitForHttpAsync(() => service.IsRunning && !((ProgressRing)page.FindName("BusyRing")).IsActive);
            var firstPort = service.Active!.Port;
            await VerifyHttpAddressesAsync(page, firstPort);
            await WaitForHttpAsync(() => !((ProgressRing)page.FindName("ScanRing")).IsActive);
            Require(((TemporaryHttpPage)page).DetectedServers.Any(server => server.Port == firstPort && server.ProcessId == Environment.ProcessId), "Managed HTTP discovery");
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };
            Require(await client.GetStringAsync($"http://127.0.0.1:{firstPort}/") == "temporary HTTP UI test", "Packaged HTTP response");
            NavigateTo("About");
            Require(GoBack() && service.IsRunning, "HTTP stopped during navigation");
            LanguageSelector.SelectedIndex = LanguageSelector.SelectedIndex == 0 ? 1 : 0;
            page = (Page)PageHost.Content;
            Require(service.IsRunning && ((TextBox)page.FindName("DirectoryInput")).Text == root, "HTTP lost state when language changed");
            var secondPort = FreeHttpPort();
            ((NumberBox)page.FindName("PortInput")).Value = secondPort;
            InvokeHttpButton(page, "RestartButton");
            await WaitForHttpAsync(() => service.Active?.Port == secondPort && !((ProgressRing)page.FindName("BusyRing")).IsActive);
            Require(await client.GetStringAsync($"http://127.0.0.1:{secondPort}/") == "temporary HTTP UI test", "Restarted HTTP response");
            await VerifyHttpAddressesAsync(page, secondPort);
            await WaitForHttpAsync(() => !((ProgressRing)page.FindName("ScanRing")).IsActive);
            Require(!((TemporaryHttpPage)page).DetectedServers.Any(server => server.Port == firstPort), "Stale HTTP port after restart");
            await CaptureAsync("TemporaryHttp-discovery.png");
            ((Microsoft.UI.Xaml.FrameworkElement)page.FindName("AddressRows")).StartBringIntoView();
            await Task.Delay(200);
            await CaptureAsync("TemporaryHttp-running.png");
            InvokeHttpButton(page, "StopButton");
            await WaitForHttpAsync(() => !service.IsRunning && !service.NeedsFirewallCleanup && !((ProgressRing)page.FindName("BusyRing")).IsActive);
            await WaitForHttpAsync(() => !((ProgressRing)page.FindName("ScanRing")).IsActive);
            Require(!((TemporaryHttpPage)page).DetectedServers.Any(server => server.Port == secondPort), "Stopped HTTP server still listed");
            Require(((StackPanel)page.FindName("AddressRows")).Children.Count == 0, "Stopped HTTP addresses still visible");
            Require(fixture.IsRunning, "Detection stopped independent HTTP server");
            InvokeHttpButton(page, "RefreshButton");
            await WaitForHttpAsync(() => !((ProgressRing)page.FindName("ScanRing")).IsActive);
            Require(((TemporaryHttpPage)page).DetectedServers.Any(server => server.Port == fixture.Active!.Port), "Manual HTTP refresh");
            App.LogStartup("Temporary HTTP UI lifecycle PASSED: start, serve, navigate, translate, restart, stop, and firewall cleanup; individual clipboard URLs, automatic/manual discovery, independent server, and stale-result removal.");
        }
        finally { await fixture.StopAsync(); await service.StopAsync(); Directory.Delete(root, true); }
    }

    private sealed class HttpSmokeFirewall : ITemporaryHttpFirewall
    {
        public Task OpenAsync(string ruleName, int port) => Task.CompletedTask;
        public Task CloseAsync(string ruleName) => Task.CompletedTask;
    }

    private static async Task VerifyHttpAddressesAsync(Page page, int port)
    {
        var rows = (StackPanel)page.FindName("AddressRows");
        Require(rows.Children.Count >= 2, "Expected individually actionable interface addresses");
        foreach (var row in rows.Children.Cast<StackPanel>())
        {
            var url = ((TextBlock)row.Children[0]).Text;
            Require(new Uri(url).Port == port, "Stale HTTP address");
            var actions = (PortManager.Controls.WrapPanel)row.Children[1];
            var button = (Button)actions.Children[0];
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await Task.Delay(100);
            Require(await Clipboard.GetContent().GetTextAsync() == url, "Clipboard did not contain only the selected HTTP URL");
        }
    }

    private static int FreeHttpPort()
    {
        using var socket = new TcpListener(IPAddress.Any, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private static void InvokeHttpButton(Page page, string name)
    {
        var button = (Button)page.FindName(name);
        Require(button.IsEnabled, $"HTTP button disabled: {name}");
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private static async Task WaitForHttpAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(50);
        Require(predicate(), "HTTP operation timed out");
    }
}
