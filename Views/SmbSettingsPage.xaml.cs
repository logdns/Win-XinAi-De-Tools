using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PortManager.Models;
using PortManager.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace PortManager.Views;

public sealed partial class SmbSettingsPage : Page
{
    private bool _loaded;
    private bool _busy;
    private IReadOnlyList<SmbAccessAddress> _accessAddresses = Array.Empty<SmbAccessAddress>();

    public SmbSettingsPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            var status = await SmbConfigurationService.GetStatusAsync();
            SmbDirectToggle.IsOn = status.SmbDirectEnabled;
            Smb1Toggle.IsOn = status.Smb1Enabled;
            SharePathBox.Text = status.SharePath;
            StatusText.Text = string.Format(App.Text("Smb_StatusFormat"),
                status.SmbDirectEnabled ? App.Text("Common_On") : App.Text("Common_Off"),
                status.Smb1Enabled ? App.Text("Common_On") : App.Text("Common_Off"));
            ShareStatusText.Text = status.ShareExists
                ? string.Format(App.Text("Smb_ShareActiveFormat"), status.ShareName, status.SharePath)
                : App.Text("Smb_ShareMissing");
            UpdateAccessAddresses(status.ShareExists, status.ShareName);
            ErrorBar.IsOpen = false;
        }
        catch (Exception ex) { ShowError(ex.Message); AuditLogService.Record("ReadSmbConfiguration", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var forceRestart = ForceRestartCheck.IsChecked == true;
        var title = forceRestart ? App.Text("Smb_ConfirmRestartTitle") : App.Text("Smb_ConfirmTitle");
        var content = forceRestart ? App.Text("Smb_ConfirmRestartMessage") : App.Text("Smb_ConfirmMessage");
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme, Title = title, Content = content,
            PrimaryButtonText = App.Text("Common_Confirm"), CloseButtonText = App.Text("Common_Cancel"), DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        SetBusy(true);
        try
        {
            await SmbConfigurationService.ApplyFeaturesAsync(new SmbFeatureRequest { SmbDirectEnabled = SmbDirectToggle.IsOn, Smb1Enabled = Smb1Toggle.IsOn, ForceRestart = forceRestart });
            AuditLogService.Record("ApplySmbFeatures", $"SMBDirect={SmbDirectToggle.IsOn}; SMB1={Smb1Toggle.IsOn}; Restart={forceRestart}");
            StatusText.Text = App.Text("Smb_Applied");
        }
        catch (Exception ex) { ShowError(ex.Message); AuditLogService.Record("ApplySmbFeatures", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private async void SetShareButton_Click(object sender, RoutedEventArgs e)
    {
        var warningDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme,
            Title = App.Text("Smb_GuestConfirmTitle"),
            Content = App.Text("Smb_GuestConfirmMessage"),
            PrimaryButtonText = App.Text("Smb_GuestConfirmButton"),
            CloseButtonText = App.Text("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await warningDialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true);
        try
        {
            await SmbConfigurationService.SetShareAsync(SharePathBox.Text);
            ShareStatusText.Text = string.Format(App.Text("Smb_ShareSetFormat"), SharePathBox.Text.Trim());
            UpdateAccessAddresses(true, "share");
            AuditLogService.Record("SetSmbShare", $"Name=share; Path={SharePathBox.Text.Trim()}");
        }
        catch (Exception ex) { ShowError(ex.Message); AuditLogService.Record("SetSmbShare", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private async void BrowseShareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var window = ((App)Application.Current).MainWindow;
            if (window is null) return;

            var picker = new Microsoft.Windows.Storage.Pickers.FolderPicker(window.AppWindow.Id);
            var result = await picker.PickSingleFolderAsync();
            if (result is not null)
                SharePathBox.Text = result.Path;
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    private async void RemoveShareButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme, Title = App.Text("Smb_RemoveConfirmTitle"), Content = App.Text("Smb_RemoveConfirmMessage"), PrimaryButtonText = App.Text("Common_Confirm"), CloseButtonText = App.Text("Common_Cancel"), DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        SetBusy(true);
        try { await SmbConfigurationService.RemoveShareAsync(); ShareStatusText.Text = App.Text("Smb_ShareRemoved"); UpdateAccessAddresses(false, "share"); AuditLogService.Record("RemoveSmbShare", "Name=share"); }
        catch (Exception ex) { ShowError(ex.Message); AuditLogService.Record("RemoveSmbShare", ex.Message, false); }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) { _busy = busy; LoadingRing.IsActive = busy; ApplyButton.IsEnabled = !busy; RefreshButton.IsEnabled = !busy; BrowseShareButton.IsEnabled = !busy; SetShareButton.IsEnabled = !busy; RemoveShareButton.IsEnabled = !busy; }

    private void UpdateAccessAddresses(bool shareExists, string shareName)
    {
        _accessAddresses = shareExists ? SmbConfigurationService.GetAccessAddresses(shareName) : Array.Empty<SmbAccessAddress>();
        SmbAccessRows.Children.Clear();
        foreach (var address in _accessAddresses)
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock
            {
                Text = $"{App.Text("Smb_WindowsAddress")}: {address.WindowsPath}",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 209, 52, 56)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{App.Text("Smb_MacAddress")}: {address.MacUrl}",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 209, 52, 56)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });
            SmbAccessRows.Children.Add(row);
        }

        SmbAccessPanel.Visibility = shareExists ? Visibility.Visible : Visibility.Collapsed;
        SmbAccessEmptyText.Visibility = shareExists && _accessAddresses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CopySmbAddressesButton.IsEnabled = _accessAddresses.Count > 0;
    }

    private void CopySmbAddressesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_accessAddresses.Count == 0) return;
        try
        {
            var lines = _accessAddresses.Select(address =>
                $"{App.Text("Smb_WindowsAddress")}: {address.WindowsPath}{Environment.NewLine}"
                + $"{App.Text("Smb_MacAddress")}: {address.MacUrl}");
            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, lines));
            Clipboard.SetContent(data);
            ErrorBar.Severity = InfoBarSeverity.Success;
            ErrorBar.Title = App.Text("Smb_Copied");
            ErrorBar.Message = string.Empty;
            ErrorBar.IsOpen = true;
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message) { ErrorBar.Severity = InfoBarSeverity.Error; ErrorBar.Title = App.Text("Smb_Error"); ErrorBar.Message = message; ErrorBar.IsOpen = true; }
}
