using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Views;

public sealed partial class ConnectionMonitorPage : Page
{
    private List<ConnectionModel> _allConnections = new();
    private bool _loaded;

    public ConnectionMonitorPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await LoadConnectionsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadConnectionsAsync();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void CloseConnection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not ConnectionModel connection) return;
        try { await ConnectionService.CloseTcpConnectionAsync(connection); AuditLogService.Record("CloseConnection", connection.LocalEndpoint); await LoadConnectionsAsync(); }
        catch (Exception ex) { ShowActionError(ex); }
    }

    private async void TerminateProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not ConnectionModel connection) return;
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme, Title = App.Text("Connection_ConfirmTerminateTitle"), Content = string.Format(App.Text("Connection_ConfirmTerminateFormat"), connection.ProcessName, connection.ProcessId), PrimaryButtonText = App.Text("Common_Confirm"), CloseButtonText = App.Text("Common_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try { await ConnectionService.TerminateProcessAsync(connection.ProcessId); AuditLogService.Record("TerminateProcess", $"PID {connection.ProcessId}"); await LoadConnectionsAsync(); }
        catch (Exception ex) { ShowActionError(ex); }
    }

    private void ShowActionError(Exception ex)
    {
        ErrorBar.Title = App.Text("Connection_ActionError");
        ErrorBar.Message = ex.Message;
        ErrorBar.IsOpen = true;
        AuditLogService.Record("ConnectionAction", ex.Message, false);
    }

    private async Task LoadConnectionsAsync()
    {
        LoadingRing.IsActive = true;
        ErrorBar.IsOpen = false;
        try
        {
            _allConnections = await ConnectionService.ListConnectionsAsync();
            ApplyFilter();
            AuditLogService.Record("ListConnections", $"Loaded {_allConnections.Count} connection(s).");
        }
        catch (Exception ex) when (ex is ConnectionOperationException or PlatformNotSupportedException)
        {
            _allConnections.Clear();
            ConnectionsList.ItemsSource = null;
            EmptyState.Visibility = Visibility.Collapsed;
            ErrorBar.Title = App.Text("Connection_Error");
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
            CountText.Text = string.Format(App.Text("Connection_CountFormat"), 0);
            AuditLogService.Record("ListConnections", ex.Message, false);
        }
        finally { LoadingRing.IsActive = false; }
    }

    private void ApplyFilter()
    {
        var keyword = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(keyword) ? _allConnections : _allConnections.Where(c =>
            c.Protocol.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            c.LocalEndpoint.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            c.RemoteEndpoint.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            c.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            c.ProcessId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        ConnectionsList.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = string.Format(App.Text("Connection_CountFormat"), filtered.Count);
    }
}
