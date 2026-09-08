using Microsoft.UI.Xaml.Controls;

namespace PortManager.Views;

public sealed partial class ComingSoonPage : Page
{
    public ComingSoonPage() => InitializeComponent();

    private void ConnectionMonitor_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => App.NavigateTo("ConnectionMonitor");
    private void RuleTransfer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => App.NavigateTo("RuleTransfer");
    private void AuditLog_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => App.NavigateTo("AuditLog");
}
