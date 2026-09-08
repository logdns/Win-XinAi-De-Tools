using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;
using WinRT.Interop;

namespace PortManager.Views;

public sealed partial class RuleTransferPage : Page
{
    private bool _loaded;
    public RuleTransferPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await RefreshCountAsync();
    }

    private async Task RefreshCountAsync()
    {
        try
        {
            var rules = await FirewallService.ListRulesAsync();
            RuleCountText.Text = string.Format(App.Text("Transfer_RuleCountFormat"), rules.Count);
        }
        catch (Exception ex) { RuleCountText.Text = ex.Message; }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, App.Text("Transfer_Exporting"));
        try
        {
            var path = NativeFileDialogService.SaveJson(WindowHandle());
            if (path is null) return;
            var rules = await FirewallService.ListRulesAsync();
            await File.WriteAllTextAsync(path, RuleTransferService.Serialize(rules), System.Text.Encoding.UTF8);
            FileText.Text = string.Format(App.Text("Transfer_ExportSuccess"), Path.GetFileName(path), rules.Count);
            ShowStatus(InfoBarSeverity.Success, FileText.Text);
            AuditLogService.Record("ExportRules", $"Exported {rules.Count} rule(s).");
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, App.Text("Transfer_Error"), ex.Message); AuditLogService.Record("ExportRules", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = NativeFileDialogService.OpenJson(WindowHandle());
            if (path is null) return;
            SetBusy(true, App.Text("Transfer_Importing"));
            var document = RuleTransferService.Parse(await File.ReadAllTextAsync(path));
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme,
                Title = App.Text("Transfer_ConfirmImportTitle"),
                Content = string.Format(App.Text("Transfer_ConfirmImportFormat"), Path.GetFileName(path), document.Rules.Count),
                PrimaryButtonText = App.Text("Common_Confirm"),
                CloseButtonText = App.Text("Common_Cancel"),
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var result = await RuleTransferService.ImportAsync(document.Rules);
            var message = string.Format(App.Text("Transfer_ImportSuccess"), result.SuccessCount, result.FailedCount);
            ShowStatus(result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, message, result.ErrorMessage);
            AuditLogService.Record("ImportRules", message, result.Success);
            await RefreshCountAsync();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, App.Text("Transfer_Error"), ex.Message); AuditLogService.Record("ImportRules", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private IntPtr WindowHandle() => WindowNative.GetWindowHandle((App.Current as App)!.MainWindow);
    private void SetBusy(bool busy, string? text = null) { ExportButton.IsEnabled = !busy; ImportButton.IsEnabled = !busy; LoadingRing.IsActive = busy; if (text is not null) FileText.Text = text; }
    private void ShowStatus(InfoBarSeverity severity, string title, string? message = null) { StatusBar.Severity = severity; StatusBar.Title = title; StatusBar.Message = message ?? string.Empty; StatusBar.IsOpen = true; }
}
