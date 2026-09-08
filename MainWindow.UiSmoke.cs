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
    // Opt-in CI harness: visits real pages but never invokes configuration/mutation buttons.
    private async Task RunUiSmokeAsync()
    {
        try
        {
            var routes = new[] { "Dashboard", "AddPort", "ListRules", "DeleteRule", "PortStatus",
                "NetworkSettings", "SmbSettings", "WslDashboard", "ComingSoon", "ConnectionMonitor",
                "RuleTransfer", "AuditLog", "About" };
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
                        foreach (var route in routes)
                        {
                            NavigateTo(route);
                            await Task.Delay(120);
                            ContentFrame.UpdateLayout();
                            Require(_history.Current == route, $"Wrong route: {route}");
                            Require(NavView.SelectedItem == (FindNavigationItem(route) ?? MoreItem), $"Wrong selection: {route}");
                            var page = (Page)ContentFrame.Content;
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
                            if (language == 1 && route is "Dashboard" or "WslDashboard")
                                await CaptureAsync($"{route}-{(theme == 1 ? "light" : "dark")}-{size.Width}.png");
                        }
                    }
                }
            }

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
            var form = (Page)ContentFrame.Content;
            ((TextBox)form.FindName("RuleNameInput")).Text = "unsaved smoke test draft";
            NavigateTo("About");
            Require(GoBack(), "Draft back navigation");
            Require(((TextBox)((Page)ContentFrame.Content).FindName("RuleNameInput")).Text == "unsaved smoke test draft", "Draft was lost");
            var current = _history.Current;
            NavigateTo("invalid-route");
            Require(_history.Current == current, "Invalid route changed history");
            ThemeSelector.SelectedIndex = 0;
            Require(NavView.RequestedTheme == ElementTheme.Default, "System theme was not restored");
            while (_history.CanGoBack) Require(GoBack(), "Back stack stalled");
            Require(!NavView.IsBackEnabled && !GoBack(), "Root back must be disabled");
            App.LogStartup("UI smoke PASSED: 156 page/theme/language/size combinations, WSL tabs, navigation, and draft retention.");
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
