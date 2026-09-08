using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PortManager.Services;
using PortManager.Views;
using Windows.Graphics;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace PortManager;

public sealed partial class MainWindow : Window
{
    private bool _isReady;
    private bool _allowClose;
    private bool _shutdownStarted;
    private AppWindow? _appWindow;
    private IntPtr _windowHandle;
    private readonly NavigationHistory _history = new();
    private bool _syncingNavigation;
    private bool _closeDialogOpen;
    private readonly UISettings _uiSettings = new();
    private readonly UiPreferences _preferences;
    private static string PreferencesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win-XinAi-De-Tools", "ui-preferences.json");

    public MainWindow(bool startHidden = false)
    {
        _preferences = UiPreferences.Load(PreferencesPath);
        App.SetLanguage(_preferences.Language);
        InitializeComponent();
        ThemeSelector.SelectedIndex = (int)_preferences.Theme;
        LanguageSelector.SelectedIndex = _preferences.Language == AppLanguage.English ? 1 : 0;
        ApplyTheme();
        ConfigureWindow();
        ContentFrame.SizeChanged += (_, _) => UpdatePagePadding();
        ApplyLanguage();
        Closed += MainWindow_Closed;
        _isReady = true;
        if (startHidden)
            DispatcherQueue.TryEnqueue(MinimizeToTray);
    }

    public void NavigateTo(string tag)
    {
        NavigateFrame(tag);
    }

    private void SavePreferences()
    {
        try { _preferences.Save(PreferencesPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { App.LogStartup($"Unable to save UI preferences: {ex.Message}"); }
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;
        _preferences.Theme = (AppTheme)ThemeSelector.SelectedIndex;
        ApplyTheme();
        SavePreferences();
    }

    private void ApplyTheme()
    {
        NavView.RequestedTheme = _preferences.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ConfigureWindow()
    {
        Title = App.Text("App_Title");
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Closing += AppWindow_Closing;
        _appWindow.Resize(new SizeInt32(1100, 720));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Win-XinAi-De-Tools.ico");
        if (File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
            try
            {
                TrayIconService.Initialize(_windowHandle, iconPath, RestoreFromTray, ExitFromTray);
            }
            catch (Exception ex)
            {
                App.LogStartup($"Tray icon initialization failed: {ex.Message}");
            }
        }

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 480;
            presenter.PreferredMinimumHeight = 480;
            presenter.IsMinimizable = true;
            presenter.IsMaximizable = true;
            presenter.IsResizable = true;
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Automated smoke tests send WM_CLOSE and must be able to terminate deterministically.
        if (_allowClose || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)) return;
        args.Cancel = true;
        if (_closeDialogOpen) return;
        _closeDialogOpen = true;
        var dialog = new ContentDialog
        {
            XamlRoot = NavView.XamlRoot,
            RequestedTheme = NavView.RequestedTheme,
            Title = App.Text("Window_CloseTitle"),
            Content = App.Text("Window_CloseContent"),
            PrimaryButtonText = App.Text("Window_Exit"),
            SecondaryButtonText = App.Text("Window_Minimize"),
            CloseButtonText = App.Text("Common_Cancel"),
            DefaultButton = ContentDialogButton.Close
        };
        ContentDialogResult result;
        try { result = await dialog.ShowAsync(); }
        finally { _closeDialogOpen = false; }
        if (result == ContentDialogResult.Secondary)
        {
            MinimizeToTray();
        }
        else if (result == ContentDialogResult.Primary)
        {
            ExitApplication();
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        App.LogStartup("Main window closed.");
        ExitApplication();
    }

    private void MinimizeToTray()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        ShowWindow(_windowHandle, SwHide);
        App.LogStartup("Main window hidden to the notification area.");
    }

    private void RestoreFromTray()
    {
        if (_windowHandle != IntPtr.Zero)
            ShowWindow(_windowHandle, SwShow);
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
            presenter.Restore();
        _appWindow?.Show();
        Activate();
    }

    private void ExitFromTray()
    {
        ExitApplication();
    }

    private void ExitApplication()
    {
        if (_shutdownStarted)
            return;

        _shutdownStarted = true;
        _allowClose = true;
        App.LogStartup("Application shutdown requested.");
        if (WslService.ShutdownOnExit)
        {
            try { WslService.ShutdownAll(); App.LogStartup("WSL distributions terminated on exit."); }
            catch (Exception ex) { App.LogStartup($"WSL shutdown on exit failed: {ex.Message}"); }
        }
        TrayIconService.Dispose();
        Application.Current.Exit();
        ExitProcess(0);
    }

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (NavView.SelectedItem is null)
            NavView.SelectedItem = DashboardItem;
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            && Environment.GetCommandLineArgs().Contains("--ui-smoke"))
            await RunUiSmokeAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (!_syncingNavigation && args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            NavigateFrame(tag);
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string tag) NavigateTo(tag);
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        GoBack();
    }

    private void BackAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Do not navigate the page behind an active modal dialog.
        if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(NavView.XamlRoot)
            .Any(p => p.Child is ContentDialog)) return;
        args.Handled = GoBack();
    }

    private bool GoBack()
    {
        if (_history.Previous is not string previous) return false;
        return NavigateFrame(previous, isBack: true);
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady)
            return;

        var language = LanguageSelector.SelectedIndex == 1
            ? AppLanguage.English
            : AppLanguage.Chinese;
        if (LanguageState.Current == language)
            return;

        App.SetLanguage(language);
        _preferences.Language = language;
        SavePreferences();
        ApplyLanguage();
        // Drop cached translations while keeping route history intact.
        if (ContentFrame.Content is Page current) current.NavigationCacheMode = NavigationCacheMode.Disabled;
        ContentFrame.CacheSize = 0;
        NavigateFrame(_history.Current, force: true);
        ContentFrame.CacheSize = 16;
    }

    private void ApplyLanguage()
    {
        Title = App.Text("App_Title");
        PaneTitle.Text = App.Text("App_Title");
        DashboardItem.Content = App.Text("Nav_Dashboard");
        AddPortItem.Content = App.Text("Nav_AddPort");
        RulesItem.Content = App.Text("Nav_Rules");
        DeleteItem.Content = App.Text("Nav_Delete");
        QueryItem.Content = App.Text("Nav_Query");
        NetworkItem.Content = App.Text("Nav_Network");
        SmbItem.Content = App.Text("Nav_Smb");
        WslItem.Content = App.Text("Nav_Wsl");
        MoreItem.Content = App.Text("Nav_More");
        AboutItem.Content = App.Text("Nav_About");
        LanguageHeader.Text = App.Text("Language_Header");
        ChineseOption.Content = App.Text("Language_Chinese");
        EnglishOption.Content = App.Text("Language_English");
        ThemeHeader.Text = App.Text("Theme_Header");
        SystemThemeOption.Content = App.Text("Theme_System");
        LightThemeOption.Content = App.Text("Theme_Light");
        DarkThemeOption.Content = App.Text("Theme_Dark");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ThemeSelector, App.Text("Theme_Header"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(LanguageSelector, App.Text("Language_Header"));
    }

    private bool NavigateFrame(string tag, bool force = false, bool isBack = false)
    {
        var pageType = tag switch
        {
            "Dashboard" => typeof(DashboardPage),
            "AddPort" => typeof(AddPortPage),
            "ListRules" => typeof(ListRulesPage),
            "DeleteRule" => typeof(DeleteRulePage),
            "PortStatus" => typeof(PortStatusPage),
            "NetworkSettings" => typeof(NetworkSettingsPage),
            "SmbSettings" => typeof(SmbSettingsPage),
            "WslDashboard" => typeof(WslDashboardPage),
            "ComingSoon" => typeof(ComingSoonPage),
            "ConnectionMonitor" => typeof(ConnectionMonitorPage),
            "RuleTransfer" => typeof(RuleTransferPage),
            "AuditLog" => typeof(AuditLogPage),
            "About" => typeof(AboutPage),
            _ => null
        };

        if (pageType is null) return false;
        if (!force && ContentFrame.CurrentSourcePageType == pageType) return false;
        NavigationTransitionInfo transition = _uiSettings.AnimationsEnabled
            ? new EntranceNavigationTransitionInfo()
            : new SuppressNavigationTransitionInfo();
        if (!ContentFrame.Navigate(pageType, null, transition)) return false;
        if (ContentFrame.Content is Page page)
            page.NavigationCacheMode = tag is "AddPort" or "PortStatus" or "NetworkSettings" or "SmbSettings" or "WslDashboard"
                ? NavigationCacheMode.Enabled : NavigationCacheMode.Disabled;
        if (isBack) _history.GoBack();
        else if (!force) _history.Visit(tag);
        _syncingNavigation = true;
        try { NavView.SelectedItem = FindNavigationItem(tag) ?? MoreItem; }
        finally { _syncingNavigation = false; }
        NavView.IsBackEnabled = _history.CanGoBack;
        if (NavView.DisplayMode != NavigationViewDisplayMode.Expanded) NavView.IsPaneOpen = false;
        UpdatePagePadding();
        App.LogStartup($"Navigation completed: {pageType.Name}.");
        return true;
    }

    private void UpdatePagePadding()
    {
        if (ContentFrame.Content is not Page page) return;
        var padding = ContentFrame.ActualWidth < 600 ? new Thickness(16) : new Thickness(28, 24, 28, 28);
        if (page.Content is Grid grid) grid.Padding = padding;
        else if (page.Content is ScrollViewer scroll) scroll.Padding = padding;
    }

    private NavigationViewItem? FindNavigationItem(string tag)
    {
        foreach (var item in NavView.MenuItems.Concat(NavView.FooterMenuItems))
        {
            if (item is NavigationViewItem navigationItem && navigationItem.Tag as string == tag)
                return navigationItem;
        }

        return null;
    }

    private const int SwHide = 0;
    private const int SwShow = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern void ExitProcess(uint exitCode);
}
