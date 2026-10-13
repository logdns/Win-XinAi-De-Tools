using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Views;

public sealed partial class HyperVSettingsPage : Page
{
    private bool _busy;
    private HyperVStatus? _status;

    public HyperVSettingsPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_status is null && !_busy) await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try { await LoadStatusAsync(); ResultBar.IsOpen = false; }
        catch (Exception ex) { _status = null; ShowResult(ex.Message, false); AuditLogService.Record("ReadHyperV", ex.Message, false); App.LogStartup($"Hyper-V status read failed: {ex.Message}"); }
        finally { SetBusy(false); }
    }

    private async Task LoadStatusAsync()
    {
        var adapterId = (VirtualAdapterSelector.SelectedItem as HyperVAdapter)?.Id;
        var physicalId = (PhysicalAdapterSelector.SelectedItem as HyperVAdapter)?.Id;
        var switchId = (SwitchSelector.SelectedItem as HyperVSwitch)?.Id;
        _status = await HyperVService.GetStatusAsync();
        App.LogStartup($"Hyper-V status loaded: {_status.Features.Count} features, {_status.VirtualAdapters.Count} virtual adapters, {_status.Switches.Count} switches.");
        StatusText.Text = string.Join(Environment.NewLine, _status.Features.Select(f => f.DisplayName))
            + Environment.NewLine + string.Format(App.Text("HyperV_Status"),
                _status.HypervisorPresent ? App.Text("Common_On") : App.Text("Common_Off"), _status.BootLaunchType,
                _status.ManagementAvailable ? App.Text("Common_On") : App.Text("Common_Off"))
            + (string.IsNullOrEmpty(_status.NetworkError) ? string.Empty : Environment.NewLine + _status.NetworkError)
            + (string.IsNullOrEmpty(_status.SwitchError) ? string.Empty : Environment.NewLine + _status.SwitchError);
        VirtualAdapterSelector.ItemsSource = _status.VirtualAdapters;
        VirtualAdapterSelector.SelectedItem = _status.VirtualAdapters.FirstOrDefault(a => a.Id == adapterId) ?? _status.VirtualAdapters.FirstOrDefault();
        PhysicalAdapterSelector.ItemsSource = _status.PhysicalAdapters;
        PhysicalAdapterSelector.SelectedItem = _status.PhysicalAdapters.FirstOrDefault(a => a.Id == physicalId) ?? _status.PhysicalAdapters.FirstOrDefault();
        SwitchSelector.ItemsSource = _status.Switches;
        SwitchSelector.SelectedItem = _status.Switches.FirstOrDefault(s => s.Id == switchId) ?? _status.Switches.FirstOrDefault();
    }

    private async Task ExecuteAsync(string action, string details, string confirmation, Func<Task> operation, bool restart = false)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme,
                Title = App.Text("HyperV_ConfirmTitle"), Content = confirmation + Environment.NewLine + details,
                PrimaryButtonText = App.Text("Common_Confirm"), CloseButtonText = App.Text("Common_Cancel"),
                DefaultButton = ContentDialogButton.Close
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            try
            {
                await operation();
                AuditLogService.Record(action, details);
            }
            catch (Exception ex)
            {
                AuditLogService.Record(action, $"{details}; {ex.Message}", false);
                try { await LoadStatusAsync(); } catch { _status = null; }
                ShowResult(App.Text("HyperV_PartialFailure") + Environment.NewLine + ex.Message, false);
                return;
            }
            try { await LoadStatusAsync(); }
            catch (Exception ex)
            {
                _status = null;
                ShowResult(App.Text("HyperV_RefreshFailed") + Environment.NewLine + ex.Message, false);
                return;
            }
            ShowResult(App.Text(restart ? "HyperV_RestartRequired" : "HyperV_Applied"), true);
        }
        catch (Exception ex) { ShowResult(ex.Message, false); }
        finally { SetBusy(false); }
    }

    private async void EnableFeatureButton_Click(object sender, RoutedEventArgs e) =>
        await ExecuteAsync("EnableHyperV", "Microsoft-Hyper-V-All; hypervisorlaunchtype=auto", App.Text("HyperV_FeatureConfirm"), HyperVService.EnableAsync, restart: true);

    private async void DisableFeatureButton_Click(object sender, RoutedEventArgs e) =>
        await ExecuteAsync("DisableHyperVFeature", "Microsoft-Hyper-V-All", App.Text("HyperV_FeatureConfirm"), HyperVService.DisableFeatureAsync, restart: true);

    private async void CompleteDisableButton_Click(object sender, RoutedEventArgs e) =>
        await ExecuteAsync("CompleteDisableHyperV", "Hyper-V; VirtualMachinePlatform; HypervisorPlatform; hypervisorlaunchtype=off", App.Text("HyperV_CompleteConfirm"), HyperVService.CompleteDisableAsync, restart: true);

    private async Task SetAdaptersAsync(bool enable, bool all)
    {
        var adapters = all ? _status?.VirtualAdapters.ToArray() ?? Array.Empty<HyperVAdapter>()
            : VirtualAdapterSelector.SelectedItem is HyperVAdapter adapter ? new[] { adapter } : Array.Empty<HyperVAdapter>();
        if (adapters.Length == 0) return;
        var ids = adapters.Select(a => a.Id).ToArray();
        var details = string.Join("; ", adapters.Select(a => $"{a.Name} [{a.Id:D}]"));
        await ExecuteAsync(enable ? "EnableHyperVAdapters" : "DisableHyperVAdapters", details,
            App.Text("HyperV_NetworkConfirm"), () => HyperVService.SetAdaptersEnabledAsync(ids, enable));
    }

    private async void EnableAdapterButton_Click(object sender, RoutedEventArgs e) => await SetAdaptersAsync(true, false);
    private async void DisableAdapterButton_Click(object sender, RoutedEventArgs e) => await SetAdaptersAsync(false, false);
    private async void EnableAllButton_Click(object sender, RoutedEventArgs e) => await SetAdaptersAsync(true, true);
    private async void DisableAllButton_Click(object sender, RoutedEventArgs e) => await SetAdaptersAsync(false, true);
    private void IpSettingsButton_Click(object sender, RoutedEventArgs e) => ((App)Application.Current).MainWindow?.NavigateTo("NetworkSettings");

    private async void CreateSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        var request = new HyperVSwitchRequest(SwitchNameBox.Text, (HyperVSwitchType)SwitchTypeSelector.SelectedIndex,
            (PhysicalAdapterSelector.SelectedItem as HyperVAdapter)?.Id ?? Guid.Empty, AllowHostCheck.IsChecked == true);
        try
        {
            _ = HyperVService.BuildCreateSwitchScript(request);
            await ExecuteAsync("CreateHyperVSwitch", $"WinXinAi-{request.Name.Trim()}; {request.Type}; NIC={request.PhysicalAdapterId}; Host={request.AllowManagementOS}",
                App.Text(request.Type == HyperVSwitchType.External ? "HyperV_ExternalConfirm" : "HyperV_SwitchConfirm"), () => HyperVService.CreateSwitchAsync(request));
        }
        catch (ArgumentException) { ShowResult(App.Text("HyperV_InvalidSwitch"), false); }
    }

    private async void RemoveSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        if (SwitchSelector.SelectedItem is not HyperVSwitch { CanRemove: true } item) return;
        await ExecuteAsync("RemoveHyperVSwitch", $"{item.Name} [{item.Id:D}]", App.Text("HyperV_RemoveConfirm"), () => HyperVService.RemoveSwitchAsync(item.Id));
    }

    private void Selector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CreateSwitchButton is not null) UpdateActions();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        LoadingRing.IsActive = busy;
        RefreshButton.IsEnabled = !busy;
        UpdateActions();
    }

    private void UpdateActions()
    {
        var ready = !_busy && _status is not null;
        var feature = _status?.Features.FirstOrDefault(f => f.Name == "Microsoft-Hyper-V-All");
        var stable = feature?.State is "Enabled" or "Disabled" or "DisabledWithPayloadRemoved";
        EnableFeatureButton.IsEnabled = ready && stable;
        DisableFeatureButton.IsEnabled = ready && stable;
        CompleteDisableButton.IsEnabled = ready;
        IpSettingsButton.IsEnabled = !_busy;
        VirtualAdapterSelector.IsEnabled = ready;
        EnableAdapterButton.IsEnabled = DisableAdapterButton.IsEnabled = ready && VirtualAdapterSelector.SelectedItem is HyperVAdapter;
        EnableAllButton.IsEnabled = DisableAllButton.IsEnabled = ready && _status!.VirtualAdapters.Count > 0;
        var management = ready && _status!.ManagementAvailable && string.IsNullOrEmpty(_status.SwitchError);
        SwitchSelector.IsEnabled = management;
        SwitchNameBox.IsEnabled = SwitchTypeSelector.IsEnabled = management;
        CreateSwitchButton.IsEnabled = management;
        RemoveSwitchButton.IsEnabled = management && SwitchSelector.SelectedItem is HyperVSwitch { CanRemove: true };
        var external = SwitchTypeSelector.SelectedIndex == (int)HyperVSwitchType.External;
        PhysicalAdapterSelector.IsEnabled = AllowHostCheck.IsEnabled = management && external;
    }

    private void ShowResult(string message, bool success)
    {
        ResultBar.Severity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = App.Text(success ? "HyperV_Applied" : "HyperV_Error");
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
