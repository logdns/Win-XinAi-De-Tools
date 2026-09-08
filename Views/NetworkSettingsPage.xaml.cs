using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Models;
using PortManager.Services;
using System.Collections.ObjectModel;

namespace PortManager.Views;

public sealed partial class NetworkSettingsPage : Page
{
    private readonly ObservableCollection<NetworkAdapterModel> _adapters = new();
    private bool _loaded;
    private bool _updating;

    public NetworkSettingsPage()
    {
        InitializeComponent();
        AdapterSelector.ItemsSource = _adapters;
        AddressModeSelector.SelectedIndex = 0;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await RefreshAdaptersAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAdaptersAsync();

    private async Task RefreshAdaptersAsync()
    {
        SetBusy(true);
        try
        {
            var selectedName = (AdapterSelector.SelectedItem as NetworkAdapterModel)?.Name;
            var adapters = await NetworkConfigurationService.ListAdaptersAsync();
            _adapters.Clear();
            foreach (var adapter in adapters) _adapters.Add(adapter);
            var selected = _adapters.FirstOrDefault(adapter => adapter.Name == selectedName) ?? _adapters.FirstOrDefault();
            AdapterSelector.SelectedItem = selected;
            if (selected is null)
                ShowError(App.Text("Network_NoAdapters"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            AuditLogService.Record("ListNetworkAdapters", ex.Message, false);
        }
        finally { SetBusy(false); }
    }

    private async void AdapterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || AdapterSelector.SelectedItem is not NetworkAdapterModel adapter) return;
        SetBusy(true);
        try
        {
            var configuration = await NetworkConfigurationService.GetConfigurationAsync(adapter);
            Populate(configuration);
            CurrentConfigText.Text = string.Format(App.Text("Network_CurrentFormat"),
                configuration.DhcpEnabled ? App.Text("Network_Dhcp") : App.Text("Network_Static"),
                string.IsNullOrWhiteSpace(configuration.IPv4Address) ? "-" : $"{configuration.IPv4Address}/{configuration.PrefixLength}",
                string.IsNullOrWhiteSpace(configuration.Gateway) ? "-" : configuration.Gateway,
                configuration.DnsDisplay,
                configuration.DefaultRouteMetric == 0 ? "-" : configuration.DefaultRouteMetric.ToString());
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            AuditLogService.Record("ReadNetworkConfiguration", ex.Message, false);
        }
        finally { SetBusy(false); }
    }

    private void AddressModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateInputState();
    }

    private void UpdateInputState()
    {
        var isStatic = AddressModeSelector.SelectedIndex == 1;
        IpAddressBox.IsEnabled = isStatic;
        PrefixLengthBox.IsEnabled = isStatic;
        GatewayBox.IsEnabled = isStatic;
        PrimaryDnsBox.IsEnabled = isStatic;
        SecondaryDnsBox.IsEnabled = isStatic;
        RouteMetricBox.IsEnabled = isStatic;
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationBar.IsOpen = false;
        if (AdapterSelector.SelectedItem is not NetworkAdapterModel adapter)
        {
            ShowError(App.Text("Network_SelectAdapter"));
            return;
        }

        var request = BuildRequest(out var validationError);
        if (request is null)
        {
            ShowError(validationError!);
            return;
        }

        var mode = request.UseDhcp ? App.Text("Network_Dhcp") : App.Text("Network_Static");
        var address = request.UseDhcp ? App.Text("Network_Automatic") : $"{request.IPv4Address}/{request.PrefixLength}";
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme,
            Title = App.Text("Network_ConfirmTitle"),
            Content = string.Format(App.Text("Network_ConfirmFormat"), adapter.Name, mode, address, request.Gateway, request.PrimaryDns, request.SecondaryDns),
            PrimaryButtonText = App.Text("Common_Confirm"),
            CloseButtonText = App.Text("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            await NetworkConfigurationService.ApplyAsync(adapter, request);
            StatusText.Text = App.Text("Network_Applied");
            AuditLogService.Record("ApplyNetworkConfiguration", $"Adapter={adapter.Name}; Mode={mode}; Address={address}");
            await Task.Delay(250);
            await AdapterSelector_RefreshAsync(adapter);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            AuditLogService.Record("ApplyNetworkConfiguration", ex.Message, false);
        }
        finally { SetBusy(false); }
    }

    private async Task AdapterSelector_RefreshAsync(NetworkAdapterModel adapter)
    {
        var configuration = await NetworkConfigurationService.GetConfigurationAsync(adapter);
        Populate(configuration);
    }

    private NetworkConfigurationRequest? BuildRequest(out string? error)
    {
        error = null;
        var useDhcp = AddressModeSelector.SelectedIndex == 0;
        if (useDhcp)
            return new NetworkConfigurationRequest { UseDhcp = true };
        if (double.IsNaN(PrefixLengthBox.Value) || double.IsInfinity(PrefixLengthBox.Value)) { error = App.Text("Network_InvalidPrefix"); return null; }
        if (double.IsNaN(RouteMetricBox.Value) || double.IsInfinity(RouteMetricBox.Value)) { error = App.Text("Network_InvalidMetric"); return null; }
        if (PrefixLengthBox.Value < 1 || PrefixLengthBox.Value > 32 || PrefixLengthBox.Value % 1 != 0)
        {
            error = App.Text("Network_InvalidPrefix");
            return null;
        }
        if (RouteMetricBox.Value < 1 || RouteMetricBox.Value > 9999 || RouteMetricBox.Value % 1 != 0)
        {
            error = App.Text("Network_InvalidMetric");
            return null;
        }
        var prefix = (int)PrefixLengthBox.Value;
        var metric = (int)RouteMetricBox.Value;
        var request = new NetworkConfigurationRequest
        {
            UseDhcp = false,
            IPv4Address = IpAddressBox.Text.Trim(),
            PrefixLength = prefix,
            Gateway = GatewayBox.Text.Trim(),
            PrimaryDns = PrimaryDnsBox.Text.Trim(),
            SecondaryDns = SecondaryDnsBox.Text.Trim(),
            RouteMetric = metric
        };
        if (!NetworkConfigurationModel.IsValidIpv4(request.IPv4Address) || prefix is < 1 or > 32) { error = App.Text("Network_InvalidAddress"); return null; }
        if ((!string.IsNullOrWhiteSpace(request.Gateway) && !NetworkConfigurationModel.IsValidIpv4(request.Gateway)) ||
            (!string.IsNullOrWhiteSpace(request.PrimaryDns) && !NetworkConfigurationModel.IsValidIpv4(request.PrimaryDns)) ||
            (!string.IsNullOrWhiteSpace(request.SecondaryDns) && !NetworkConfigurationModel.IsValidIpv4(request.SecondaryDns))) { error = App.Text("Network_InvalidAddress"); return null; }
        if (metric is < 1 or > 9999) { error = App.Text("Network_InvalidMetric"); return null; }
        return request;
    }

    private void Populate(NetworkConfigurationModel configuration)
    {
        _updating = true;
        try
        {
            AddressModeSelector.SelectedIndex = configuration.DhcpEnabled ? 0 : 1;
            IpAddressBox.Text = configuration.IPv4Address;
            PrefixLengthBox.Value = configuration.PrefixLength == 0 ? 24 : configuration.PrefixLength;
            GatewayBox.Text = configuration.Gateway;
            PrimaryDnsBox.Text = configuration.DnsServers.ElementAtOrDefault(0) ?? string.Empty;
            SecondaryDnsBox.Text = configuration.DnsServers.ElementAtOrDefault(1) ?? string.Empty;
            RouteMetricBox.Value = configuration.DefaultRouteMetric == 0 ? 25 : configuration.DefaultRouteMetric;
            StatusText.Text = string.Empty;
        }
        finally { _updating = false; UpdateInputState(); }
    }

    private void SetBusy(bool busy)
    {
        LoadingRing.IsActive = busy;
        ApplyButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        AdapterSelector.IsEnabled = !busy;
    }

    private void ShowError(string message)
    {
        ValidationBar.Title = App.Text("Network_Error");
        ValidationBar.Message = message;
        ValidationBar.IsOpen = true;
    }
}
