using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Views;

public sealed partial class AuditLogPage : Page
{
    private bool _loaded;
    public AuditLogPage() => InitializeComponent();
    private async void Page_Loaded(object sender, RoutedEventArgs e) { if (_loaded) return; _loaded = true; await LoadAsync(); }
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();
    private async Task LoadAsync()
    {
        LoadingRing.IsActive = true;
        ErrorBar.IsOpen = false;
        try { var entries = await AuditLogService.ReadAsync(); LogsList.ItemsSource = entries; EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed; CountText.Text = string.Format(App.Text("Audit_CountFormat"), entries.Count); }
        catch (Exception ex) { ErrorBar.Title = App.Text("Audit_Error"); ErrorBar.Message = ex.Message; ErrorBar.IsOpen = true; }
        finally { LoadingRing.IsActive = false; }
    }
    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme, Title = App.Text("Audit_ClearConfirmTitle"), Content = App.Text("Audit_ClearConfirmFormat"), PrimaryButtonText = App.Text("Common_Confirm"), CloseButtonText = App.Text("Common_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try { await AuditLogService.ClearAsync(); await LoadAsync(); }
        catch (Exception ex) { ErrorBar.Title = App.Text("Audit_Error"); ErrorBar.Message = ex.Message; ErrorBar.IsOpen = true; }
        CountText.Text = string.Format(App.Text("Audit_CountFormat"), 0);
    }
}
