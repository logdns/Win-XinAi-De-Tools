using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;

namespace PortManager;

public sealed partial class MainWindow
{
    private async Task VerifyTemporaryHttpAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinXinAiHttpUi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var service = ((App)Microsoft.UI.Xaml.Application.Current).TemporaryHttp;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "index.html"), "temporary HTTP UI test");
            NavigateTo("TemporaryHttp");
            var page = (Page)PageHost.Content;
            Require(((NumberBox)page.FindName("PortInput")).Value == 8980, "HTTP default port");
            ((TextBox)page.FindName("DirectoryInput")).Text = root;
            ((NumberBox)page.FindName("PortInput")).Value = FreeHttpPort();
            InvokeHttpButton(page, "StartButton");
            await WaitForHttpAsync(() => service.IsRunning && !((ProgressRing)page.FindName("BusyRing")).IsActive);
            var firstPort = service.Active!.Port;
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
            await CaptureAsync("TemporaryHttp-running.png");
            InvokeHttpButton(page, "StopButton");
            await WaitForHttpAsync(() => !service.IsRunning && !service.NeedsFirewallCleanup && !((ProgressRing)page.FindName("BusyRing")).IsActive);
            App.LogStartup("Temporary HTTP UI lifecycle PASSED: start, serve, navigate, translate, restart, stop, and firewall cleanup.");
        }
        finally { await service.StopAsync(); Directory.Delete(root, true); }
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
