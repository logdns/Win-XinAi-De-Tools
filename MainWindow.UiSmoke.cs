using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PortManager.Controls;
using PortManager.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PortManager;

public sealed partial class MainWindow
{
    // Opt-in CI harness: visits real pages and manages a dedicated temporary HTTP fixture.
    // Other operating-system configuration/mutation buttons are not invoked.
    private async Task RunUiSmokeAsync()
    {
        try
        {
            var routes = new[] { "Dashboard", "AddPort", "ListRules", "DeleteRule", "PortStatus",
                "NetworkSettings", "SmbSettings", "WslDashboard", "ComingSoon", "ConnectionMonitor",
                "RuleTransfer", "AuditLog", "TemporaryHttp", "About" };
            foreach (var language in new[] { 0, 1 })
            {
                LanguageSelector.SelectedIndex = language;
                foreach (var theme in new[] { 1, 2 })
                {
                    ThemeSelector.SelectedIndex = theme;
                    foreach (var size in new[] { new SizeInt32(480, 640), new SizeInt32(800, 600), new SizeInt32(1400, 900) })
                    {
                        _appWindow!.Resize(size);
                        await Task.Delay(150);
                        App.LogStartup($"UI viewport: requested {size.Width}x{size.Height}, actual {NavView.ActualWidth}x{NavView.ActualHeight} DIP.");
                        foreach (var route in routes)
                        {
                            NavigateTo(route);
                            await Task.Delay(120);
                            PageHost.UpdateLayout();
                            Require(_history.Current == route, $"Wrong route: {route}");
                            Require(Equals(NavView.SelectedItem, FindNavigationItem(route) ?? MoreItem), $"Wrong selection: {route}");
                            Require(NavView.IsPaneOpen || PanePreferences.Visibility == Visibility.Collapsed, "Preferences clipped in compact pane");
                            Require(NavView.DisplayMode != NavigationViewDisplayMode.Minimal || PageHost.Margin.Top >= 48, "Overlay buttons overlap the page");
                            var page = (Page)PageHost.Content;
                            Require(page.ActualWidth > 0 && page.ActualHeight > 0, $"Empty page: {route}");
                            Require(page.ActualTheme == (theme == 1 ? ElementTheme.Light : ElementTheme.Dark), $"Wrong theme: {route}");
                            CheckLayout(page);
                            if (page.FindName("ManagementTabs") is TabView tabs)
                            {
                                for (var tab = 0; tab < tabs.TabItems.Count; tab++)
                                {
                                    tabs.SelectedIndex = tab;
                                    await Task.Delay(50);
                                    page.UpdateLayout();
                                    CheckLayout(page);
                                }
                                tabs.SelectedIndex = 0;
                            }
                            if (language == 1 && route is "Dashboard" or "WslDashboard" or "TemporaryHttp")
                            {
                                var deadline = DateTime.UtcNow.AddSeconds(15);
                                while (page.FindName("LoadingRing") is ProgressRing { IsActive: true } && DateTime.UtcNow < deadline)
                                    await Task.Delay(100);
                                page.UpdateLayout();
                                CheckLayout(page);
                                if (page.Content is ScrollViewer scroll) scroll.ChangeView(null, 0, null, true);
                                await Task.Delay(100);
                                await CaptureAsync($"{route}-{(theme == 1 ? "light" : "dark")}-{size.Width}.png");
                            }
                        }
                    }
                }
            }

            await VerifyTemporaryHttpAsync();
            NavigateTo("Dashboard");
            NavigateTo("ComingSoon");
            NavigateTo("ConnectionMonitor");
            Require(GoBack() && _history.Current == "ComingSoon", "Extension back target");
            Require(GoBack() && _history.Current == "Dashboard", "Dashboard back target");
            NavigateTo("NetworkSettings");
            NavigateTo("SmbSettings");
            LanguageSelector.SelectedIndex = 0;
            Require(GoBack() && _history.Current == "NetworkSettings", "Language change altered history");
            NavigateTo("AddPort");
            var form = (Page)PageHost.Content;
            ((TextBox)form.FindName("RuleNameInput")).Text = "unsaved smoke test draft";
            NavigateTo("About");
            Require(GoBack(), "Draft back navigation");
            Require(((TextBox)((Page)PageHost.Content).FindName("RuleNameInput")).Text == "unsaved smoke test draft", "Draft was lost");
            var current = _history.Current;
            var dialog = new ContentDialog
            {
                XamlRoot = NavView.XamlRoot, RequestedTheme = NavView.RequestedTheme,
                Title = "UI smoke confirmation", CloseButtonText = "Cancel"
            };
            var shown = dialog.ShowAsync();
            await Task.Delay(100);
            try
            {
                Require(!GoBack() && _history.Current == current, "Back navigated behind a modal dialog");
                Require(dialog.ActualTheme == NavView.ActualTheme, "Dialog theme mismatch");
            }
            finally { dialog.Hide(); await shown; }
            NavigateTo("invalid-route");
            Require(_history.Current == current, "Invalid route changed history");
            ThemeSelector.SelectedIndex = 0;
            Require(NavView.RequestedTheme == ElementTheme.Default, "System theme was not restored");
            while (_history.CanGoBack) Require(GoBack(), "Back stack stalled");
            Require(!NavView.IsBackEnabled && !GoBack(), "Root back must be disabled");
            App.LogStartup("UI smoke PASSED: 168 page/theme/language/size combinations, WSL tabs, navigation, draft retention, and temporary HTTP lifecycle.");
        }
        catch (Exception ex)
        {
            App.LogStartup($"UI smoke FAILED: {ex}");
        }
        finally { Close(); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckLayout(DependencyObject root)
    {
        if (root is FrameworkElement element && element.Visibility == Visibility.Collapsed) return;
        if (root is Grid grid && AdaptiveLayout.GetCompactBelow(grid) > 0 && grid.ActualWidth > 0)
        {
            foreach (var child in grid.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible))
                Require(child.ActualWidth <= grid.ActualWidth + 1, $"Horizontal overflow: {child.Name} in {grid.Name}: {child.ActualWidth} > {grid.ActualWidth}");
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            CheckLayout(VisualTreeHelper.GetChild(root, i));
    }

    private async Task CaptureAsync(string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("WINXINAI_UI_SMOKE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(NavView);
        var pixels = await bitmap.GetPixelsAsync();
        var file = await StorageFile.GetFileFromPathAsync(CreateScreenshotFile(Path.Combine(directory, fileName)));
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();
    }

    private static string CreateScreenshotFile(string path)
    {
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }
}
