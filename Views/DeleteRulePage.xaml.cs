using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;

namespace PortManager.Views;

public sealed partial class DeleteRulePage : Page
{
    private bool _loaded;

    public DeleteRulePage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
            return;

        _loaded = true;
        await LoadRulesAsync();
    }

    private async Task LoadRulesAsync()
    {
        LoadingRing.IsActive = true;
        StatusBar.IsOpen = false;

        try
        {
            var rules = await FirewallService.ListRulesAsync();
            RulesList.ItemsSource = rules;
            EmptyState.Visibility = rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (FirewallOperationException ex)
        {
            RulesList.ItemsSource = null;
            EmptyState.Visibility = Visibility.Collapsed;
            ShowStatus(InfoBarSeverity.Error, App.Text("Common_FirewallError"),
                $"{App.Text("Common_FirewallErrorDetail")}\n{ex.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string ruleName)
            await ConfirmAndDeleteAsync(ruleName, button);
    }

    private async void DeleteManual_Click(object sender, RoutedEventArgs e)
    {
        var ruleName = ManualNameInput.Text.Trim();
        if (string.IsNullOrEmpty(ruleName))
        {
            ShowStatus(InfoBarSeverity.Warning, App.Text("Delete_NameRequired"), string.Empty);
            return;
        }

        await ConfirmAndDeleteAsync(ruleName, DeleteManualButton);
    }

    private async Task ConfirmAndDeleteAsync(string ruleName, Button sourceButton)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = ((FrameworkElement)XamlRoot.Content).RequestedTheme,
            Title = App.Text("Delete_ConfirmTitle"),
            Content = string.Format(App.Text("Delete_ConfirmFormat"), ruleName),
            PrimaryButtonText = App.Text("Common_Delete"),
            CloseButtonText = App.Text("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        sourceButton.IsEnabled = false;
        try
        {
            var deleted = await FirewallService.DeleteRuleAsync(ruleName);
            if (deleted)
            {
                ManualNameInput.Text = string.Empty;
                await LoadRulesAsync();
                ShowStatus(InfoBarSeverity.Success,
                    string.Format(App.Text("Delete_SuccessFormat"), ruleName), string.Empty);
                AuditLogService.Record("DeleteRule", $"Deleted rule '{ruleName}'.");
            }
            else
            {
                ShowStatus(InfoBarSeverity.Error,
                    string.Format(App.Text("Delete_FailedFormat"), ruleName), string.Empty);
                AuditLogService.Record("DeleteRule", $"Rule '{ruleName}' was not found.", false);
            }
        }
        catch (FirewallOperationException ex)
        {
            AuditLogService.Record("DeleteRule", ex.Message, false);
            ShowStatus(InfoBarSeverity.Error, App.Text("Common_FirewallError"), ex.Message);
        }
        finally
        {
            sourceButton.IsEnabled = true;
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
