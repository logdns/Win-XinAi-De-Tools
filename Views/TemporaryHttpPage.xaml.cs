using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;
using Windows.ApplicationModel.DataTransfer;

namespace PortManager.Views;

public sealed partial class TemporaryHttpPage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _busy;
    private bool _loaded;
    private CancellationTokenSource? _scan;
    private readonly LocalHttpDiscovery _discovery = new();
    internal IReadOnlyList<LocalHttpServer> DetectedServers { get; private set; } = Array.Empty<LocalHttpServer>();
    private TemporaryHttpOptions? _displayedOptions;
    private TemporaryHttpService Service => ((App)Application.Current).TemporaryHttp;

    public TemporaryHttpPage()
    {
        InitializeComponent();
        if (Service.Active is { } active)
        {
            PortInput.Value = active.Port;
            DirectoryInput.Text = active.Directory;
            CorsCheckBox.IsChecked = active.AllowCors;
        }
        _timer.Tick += (_, _) => UpdateState();
        UpdateState();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        UpdateState();
        _timer.Start();
        await RefreshDiscoveryAsync();
    }
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        _timer.Stop();
        _scan?.Cancel();
    }

    private TemporaryHttpOptions ReadOptions()
    {
        var port = PortInput.Value;
        if (!double.IsFinite(port) || port != Math.Truncate(port) || port is < 1 or > 65535)
            throw new ArgumentException(App.Text("Http_InvalidPort"));
        if (string.IsNullOrWhiteSpace(DirectoryInput.Text) || !Directory.Exists(DirectoryInput.Text.Trim()))
            throw new ArgumentException(App.Text("Http_InvalidDirectory"));
        return new TemporaryHttpOptions((int)port, DirectoryInput.Text.Trim(), CorsCheckBox.IsChecked == true);
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e) => await RunOperationAsync("StartHttp", () => Service.StartAsync(ReadOptions()));
    private async void StopButton_Click(object sender, RoutedEventArgs e) => await RunOperationAsync("StopHttp", Service.StopAsync);
    private async void RestartButton_Click(object sender, RoutedEventArgs e) => await RunOperationAsync("RestartHttp", () => Service.RestartAsync(ReadOptions()));

    private async Task RunOperationAsync(string action, Func<Task> operation)
    {
        if (_busy) return;
        _busy = true;
        OperationBar.IsOpen = false;
        UpdateState();
        try
        {
            await operation();
            AuditLogService.Record(action, Service.Active is { } active ? $"TCP {active.Port}; directory {active.Directory}" : "HTTP stopped; temporary firewall rule removed.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
            AuditLogService.Record(action, ex.Message, false);
        }
        finally { _busy = false; UpdateState(); }
        if (_loaded) await RefreshDiscoveryAsync();
    }

    private void UpdateState()
    {
        var active = Service.Active;
        StateText.Text = App.Text(active is not null ? "Http_Running" : Service.NeedsFirewallCleanup ? "Http_CleanupNeeded" : "Http_Stopped");
        ActiveDirectoryText.Text = active is null ? "" : string.Format(App.Text("Http_ActiveDirectory"), active.Directory);
        CountText.Text = string.Format(App.Text("Http_RequestCount"), Service.RequestCount);
        if (!Equals(_displayedOptions, active) || (active is not null && AddressRows.Children.Count == 0))
        {
            _displayedOptions = active;
            AddressRows.Children.Clear();
            if (active is not null)
            {
                IReadOnlyList<string> urls;
                try { urls = TemporaryHttpService.GetAccessUrls(active.Port); }
                catch (System.Net.NetworkInformation.NetworkInformationException) { urls = new[] { $"http://127.0.0.1:{active.Port}/" }; }
                foreach (var url in urls) AddressRows.Children.Add(CreateAddressRow(url));
            }
        }
        NoAddressText.Visibility = active is null ? Visibility.Visible : Visibility.Collapsed;
        StartButton.IsEnabled = !_busy && active is null && !Service.NeedsFirewallCleanup;
        StopButton.IsEnabled = !_busy && (active is not null || Service.NeedsFirewallCleanup);
        RestartButton.IsEnabled = !_busy && active is not null;
        OpenBrowserButton.IsEnabled = active is not null;
        PortInput.IsEnabled = DirectoryInput.IsEnabled = CorsCheckBox.IsEnabled = BrowseButton.IsEnabled = !_busy;
        BusyRing.IsActive = _busy;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        UpdateState();
        try
        {
            var window = ((App)Application.Current).MainWindow!;
            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(window.AppWindow.Id);
            var result = await picker.PickSingleFolderAsync();
            if (result is not null) DirectoryInput.Text = result.Path;
        }
        catch (Exception ex) { ShowError(ex); }
        finally { _busy = false; UpdateState(); }
    }

    private void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = DirectoryInput.Text.Trim();
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(App.Text("Http_InvalidDirectory"));
            Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Service.Active is { } active)
                Process.Start(new ProcessStartInfo($"http://127.0.0.1:{active.Port}/") { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void CopyAddress(string url)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(url);
            Clipboard.SetContent(data);
            OperationBar.Severity = InfoBarSeverity.Success;
            OperationBar.Title = App.Text("Http_Copied");
            OperationBar.Message = "";
            OperationBar.IsOpen = true;
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private StackPanel CreateAddressRow(string url)
    {
        var row = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
        row.Children.Add(new TextBlock { Text = url, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        var actions = new PortManager.Controls.WrapPanel { Spacing = 8 };
        var copy = new Button { Content = App.Text("Http_CopyOne"), Tag = url };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(copy, $"{App.Text("Http_CopyOne")} {url}");
        copy.Click += (_, _) => CopyAddress(url);
        var open = new Button { Content = App.Text("Http_OpenBrowser"), Tag = url };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(open, $"{App.Text("Http_OpenBrowser")} {url}");
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError(ex); }
        };
        actions.Children.Add(copy);
        actions.Children.Add(open);
        row.Children.Add(actions);
        return row;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshDiscoveryAsync();

    internal async Task RefreshDiscoveryAsync()
    {
        _scan?.Cancel();
        using var scan = new CancellationTokenSource();
        _scan = scan;
        ScanRing.IsActive = true;
        RefreshButton.IsEnabled = false;
        ScanStatusText.Text = App.Text("Http_Scanning");
        DiscoveryRows.Children.Clear();
        DetectedServers = Array.Empty<LocalHttpServer>();
        try
        {
            var result = await _discovery.ScanAsync(scan.Token);
            if (!_loaded || _scan != scan) return;
            DetectedServers = result.Servers;
            foreach (var server in result.Servers)
            {
                var row = CreateAddressRow(server.Url);
                var owned = server.ProcessId == Environment.ProcessId && Service.Active?.Port == server.Port;
                row.Children.Insert(0, new TextBlock
                {
                    Text = string.Format(App.Text("Http_ServerDetails"), server.Port, server.StatusCode,
                        server.ProcessId?.ToString() ?? App.Text("Http_Unknown"), server.ProcessName ?? App.Text("Http_Unknown"))
                        + (owned ? $" · {App.Text("Http_Managed")}" : ""),
                    TextWrapping = TextWrapping.Wrap
                });
                DiscoveryRows.Children.Add(row);
            }
            ScanStatusText.Text = (result.Servers.Count == 0 ? App.Text("Http_ScanEmpty")
                : string.Format(App.Text("Http_ScanCount"), result.Servers.Count))
                + (result.IsPartial ? $" {App.Text("Http_ScanPartial")}" : "");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_loaded && _scan == scan)
                ScanStatusText.Text = $"{App.Text("Http_ScanError")} {ex.GetBaseException().Message}";
        }
        finally
        {
            if (_scan == scan)
            {
                _scan = null;
                ScanRing.IsActive = false;
                RefreshButton.IsEnabled = true;
            }
        }
    }

    private void ShowError(Exception exception)
    {
        OperationBar.Severity = InfoBarSeverity.Error;
        OperationBar.Title = App.Text("Http_Error");
        OperationBar.Message = $"{App.Text("Http_ErrorHelp")}\n{exception.GetBaseException().Message}";
        OperationBar.IsOpen = true;
    }
}
