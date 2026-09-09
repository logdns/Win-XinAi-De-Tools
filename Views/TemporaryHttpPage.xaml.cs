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

    private void Page_Loaded(object sender, RoutedEventArgs e) { UpdateState(); _timer.Start(); }
    private void Page_Unloaded(object sender, RoutedEventArgs e) => _timer.Stop();

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
    }

    private void UpdateState()
    {
        var active = Service.Active;
        StateText.Text = App.Text(active is not null ? "Http_Running" : Service.NeedsFirewallCleanup ? "Http_CleanupNeeded" : "Http_Stopped");
        ActiveDirectoryText.Text = active is null ? "" : string.Format(App.Text("Http_ActiveDirectory"), active.Directory);
        CountText.Text = string.Format(App.Text("Http_RequestCount"), Service.RequestCount);
        if (!Equals(_displayedOptions, active) || AddressesText.Text.Length == 0)
        {
            _displayedOptions = active;
            try { AddressesText.Text = active is null ? App.Text("Http_NoAddress") : string.Join(Environment.NewLine, TemporaryHttpService.GetAccessUrls(active.Port)); }
            catch (System.Net.NetworkInformation.NetworkInformationException) { AddressesText.Text = active is null ? "" : $"http://127.0.0.1:{active.Port}/"; }
        }
        StartButton.IsEnabled = !_busy && active is null && !Service.NeedsFirewallCleanup;
        StopButton.IsEnabled = !_busy && (active is not null || Service.NeedsFirewallCleanup);
        RestartButton.IsEnabled = !_busy && active is not null;
        OpenBrowserButton.IsEnabled = CopyButton.IsEnabled = active is not null;
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

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Service.IsRunning) return;
            var data = new DataPackage();
            data.SetText(AddressesText.Text);
            Clipboard.SetContent(data);
            OperationBar.Severity = InfoBarSeverity.Success;
            OperationBar.Title = App.Text("Http_Copied");
            OperationBar.Message = "";
            OperationBar.IsOpen = true;
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception exception)
    {
        OperationBar.Severity = InfoBarSeverity.Error;
        OperationBar.Title = App.Text("Http_Error");
        OperationBar.Message = $"{App.Text("Http_ErrorHelp")}\n{exception.GetBaseException().Message}";
        OperationBar.IsOpen = true;
    }
}
