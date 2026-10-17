param(
    [Parameter(Mandatory = $true)]
    [string]$Installer,
    [ValidateSet("WinUI", "MyGo")]
    [string]$Edition = "WinUI"
)

$ErrorActionPreference = 'Stop'
if ($env:CI -ne 'true' -or -not $IsWindows) {
    throw 'Run only on a disposable Windows CI runner.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Installer smoke tests require an elevated disposable runner.'
}

$appName = if ($Edition -eq "MyGo") { "Win-XinAi-De-Tools-MyGo" } else { "Win-XinAi-De-Tools" }
$Installer = (Resolve-Path $Installer).Path
$output = Join-Path $pwd $(if ($Edition -eq 'MyGo') { 'artifacts/installer-smoke-MyGo' } else { 'artifacts/installer-smoke' })
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Runner TEMP can contain an 8.3 alias; Shell resolves .lnk targets to long paths.
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class InstallerSmokePath
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetLongPathName(string path, StringBuilder result, uint capacity);
    private delegate bool EnumProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static void SendKey(IntPtr window, int key)
    {
        if (!PostMessage(window, 0x0100, new IntPtr(key), IntPtr.Zero)
            || !PostMessage(window, 0x0101, new IntPtr(key), IntPtr.Zero))
            throw new InvalidOperationException("Could not send native application key.");
    }

    public static string AdvanceWizard(string application)
    {
        string clicked = null;
        EnumWindows((window, parameter) => {
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            // With the default disabled Welcome page, Inno appends AppVerName to the title.
            if (!IsWindowVisible(window) || !title.ToString().StartsWith("Setup - " + application, StringComparison.Ordinal)) return true;
            EnumChildWindows(window, (child, ignored) => {
                var text = new StringBuilder(256);
                var type = new StringBuilder(256);
                GetWindowText(child, text, text.Capacity);
                GetClassName(child, type, type.Capacity);
                string name = text.ToString().Replace("&", "").Trim();
                if (type.ToString() == "TNewButton" && IsWindowVisible(child) && IsWindowEnabled(child)
                    && (name == "Next" || name == "Next >" || name == "Install" || name == "Finish")) {
                    PostMessage(child, 0x00F5, IntPtr.Zero, IntPtr.Zero);
                    clicked = name;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return clicked == null;
        }, IntPtr.Zero);
        return clicked;
    }

    public static string DescribeWizard(string application)
    {
        var result = new StringBuilder();
        EnumWindows((window, parameter) => {
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            if (!title.ToString().Contains(application)) return true;
            result.AppendLine("WINDOW " + title + " visible=" + IsWindowVisible(window));
            EnumChildWindows(window, (child, ignored) => {
                var text = new StringBuilder(1024);
                var type = new StringBuilder(256);
                GetWindowText(child, text, text.Capacity);
                GetClassName(child, type, type.Capacity);
                result.AppendLine(type + " | " + text + " | visible=" + IsWindowVisible(child) + " enabled=" + IsWindowEnabled(child));
                return true;
            }, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
        return result.ToString();
    }
}
'@
$tempPath = [Text.StringBuilder]::new(32768)
$length = [InstallerSmokePath]::GetLongPathName($env:TEMP, $tempPath, 32768)
if ($length -eq 0 -or $length -ge 32768) { throw 'Cannot resolve the runner temporary directory.' }
$installDirectory = Join-Path $tempPath.ToString() ('WinXinAiInstaller-' + [guid]::NewGuid().ToString('N'))
$executable = Join-Path $installDirectory "$appName.exe"
$programs = [Environment]::GetFolderPath('CommonPrograms')
$desktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$menuDirectory = Join-Path $programs $appName
$menuLink = Join-Path $menuDirectory "$appName.lnk"
$uninstallLink = Join-Path $menuDirectory "Uninstall $appName.lnk"
$desktopLink = Join-Path $desktop "$appName.lnk"
$legacyLink = Join-Path $programs "$appName.lnk"
$uninstallKey = if ($Edition -eq 'MyGo') { '{ED82C612-79BA-4D1D-B2BD-5D6F9FA79E61}_is1' } else { '{AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7}_is1' }

# Refuse to interfere with an existing installation or another user's shortcuts.
foreach ($key in @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKey",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKey",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKey"
)) {
    if (Test-Path $key) { throw "Existing installation: $key" }
}
foreach ($path in @($menuDirectory, $desktopLink, $legacyLink)) {
    if (Test-Path $path) { throw "Existing shortcut path: $path" }
}
if (Get-Process -Name $appName -ErrorAction SilentlyContinue) {
    throw 'The application is already running.'
}

$shell = New-Object -ComObject WScript.Shell

function Invoke-SetupProcess([string]$File, [string[]]$Arguments) {
    # Inno's uninstaller spawns a temporary helper. Wait for the entire process tree
    # before reinstalling, or its delayed cleanup can delete the next installation.
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "Installer process failed: $File (exit $($process.ExitCode))"
    }
}

function Assert-Shortcut([string]$Path, [bool]$Expected, [string]$Target, [bool]$AppIcon) {
    if ((Test-Path $Path) -ne $Expected) {
        throw "Shortcut existence mismatch: $Path (expected $Expected)"
    }
    if (-not $Expected) { return }
    $link = $shell.CreateShortcut($Path)
    if ($link.TargetPath -ne $Target -or -not (Test-Path $link.TargetPath)) {
        throw "Invalid shortcut target: $Path -> $($link.TargetPath)"
    }
    if ($link.WorkingDirectory -ne $installDirectory -or $link.Arguments) {
        throw "Invalid shortcut working directory or arguments: $Path"
    }
    if ($AppIcon -and $link.IconLocation -ne "$executable,0") {
        throw "Invalid application shortcut icon: $Path -> $($link.IconLocation)"
    }
}

function Install-Case([string]$Name, [string]$Tasks, [bool]$Desktop, [bool]$Menu, [string]$Language) {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-',
        "/LANG=$Language", "/DIR=`"$installDirectory`"", "/LOG=`"$output/$Name-install.log`"")
    if ($Tasks -ne 'default') { $arguments += "/TASKS=`"$Tasks`"" }
    Invoke-SetupProcess $Installer $arguments
    if (-not (Test-Path $executable)) { throw 'Installed executable is missing.' }
    if (Get-Process -Name $appName -ErrorAction SilentlyContinue) {
        throw 'Installation launched the app despite skipifsilent.'
    }
    Assert-Shortcut $desktopLink $Desktop $executable $true
    Assert-Shortcut $menuLink $Menu $executable $true
    Assert-Shortcut $uninstallLink $Menu (Join-Path $installDirectory 'unins000.exe') $false
    if (Test-Path $legacyLink) { throw 'Legacy flat Start Menu shortcut was not removed.' }
    Write-Host "PASS: $Name; desktop=$Desktop; Start Menu=$Menu; no postinstall launch"
}

function Uninstall-Case([string]$Name) {
    Invoke-SetupProcess (Join-Path $installDirectory 'unins000.exe') @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=`"$output/$Name-uninstall.log`"")
    foreach ($path in @($executable, $desktopLink, $menuLink, $uninstallLink, $legacyLink)) {
        if (Test-Path $path) { throw "Uninstall left a file or shortcut: $path" }
    }
    Write-Host "PASS: $Name uninstall removed the executable and shortcuts"
}

function Close-InstalledApplication($App) {
    if ($Edition -eq 'WinUI') {
        if (-not $App.CloseMainWindow() -or -not $App.WaitForExit(15000)) { throw 'Installed application did not close normally.' }
        return
    }
    $logPath = Join-Path $env:LOCALAPPDATA "$appName/startup.log"
    $before = Get-Content $logPath -Raw
    $handle = $App.MainWindowHandle
    if (-not $App.CloseMainWindow()) { throw 'Could not request native X close.' }
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 200
        $log = Get-Content $logPath -Raw
    } while ((Get-Date) -lt $deadline -and $log.Substring($before.Length) -notmatch 'MyGo close choice opened')
    if ($App.HasExited -or $log.Substring($before.Length) -notmatch 'MyGo close choice opened') { throw 'Native X did not show close choice.' }
    # Exercise the real default button through HWND input, not a CI bypass.
    [InstallerSmokePath]::SendKey($handle, 0x0D)
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 200
        $log = Get-Content $logPath -Raw
    } while ((Get-Date) -lt $deadline -and $log.Substring($before.Length) -notmatch 'MyGo close choice canceled')
    if ($App.HasExited -or $log.Substring($before.Length) -notmatch 'MyGo close choice canceled') { throw 'Default Enter did not cancel closing.' }
    if (-not $App.CloseMainWindow()) { throw 'Could not reopen close choice.' }
    Start-Sleep -Milliseconds 500
    # Default Cancel -> wrap to first action -> Exit if a tray exists.
    [InstallerSmokePath]::SendKey($handle, 0x09)
    Start-Sleep -Milliseconds 200
    if ($log -match 'MyGo close choice opened; tray=true') {
        [InstallerSmokePath]::SendKey($handle, 0x09)
        Start-Sleep -Milliseconds 200
    }
    [InstallerSmokePath]::SendKey($handle, 0x0D)
    if (-not $App.WaitForExit(15000)) { throw 'Explicit Exit did not shut down normally.' }
    $log = Get-Content $logPath -Raw
    if ($log -notmatch 'MyGo exit approved' -or $log -notmatch 'MyGo shutdown completed') { throw 'MyGo skipped normal shutdown cleanup.' }
    if (Get-Process -Name 'Win-XinAi-De-Tools.Engine' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installDirectory 'engine/Win-XinAi-De-Tools.Engine.exe') }) { throw 'MyGo left its shared engine running.' }
    Write-Host 'PASS: native X opens choices; Enter cancels by default; explicit Exit cleans up engine'
}

function Test-InteractivePostinstallLaunch {
    # Do not use /SILENT: skipifsilent bypasses precisely the [Run] path under test.
    $logPath = Join-Path $env:LOCALAPPDATA "$appName/startup.log"
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
    $setup = Start-Process -FilePath $Installer -ArgumentList @('/SP-', '/NORESTART', '/LANG=english',
        "/DIR=`"$installDirectory`"", "/LOG=`"$output/postinstall-launch.log`"") -PassThru
    $null = $setup.Handle
    $deadline = (Get-Date).AddSeconds(150)
    $clicked = [Collections.Generic.List[string]]::new()
    $ticks = 0
    while (-not $setup.HasExited -and (Get-Date) -lt $deadline) {
        if ($ticks++ % 10 -eq 0) {
            [InstallerSmokePath]::DescribeWizard($appName) | Add-Content (Join-Path $output 'wizard-state.txt')
        }
        $button = [InstallerSmokePath]::AdvanceWizard($appName)
        if ($button) { $clicked.Add($button); Write-Host "Wizard: $button" }
        Start-Sleep -Seconds 1
    }
    if (-not $setup.HasExited) { throw 'Interactive installer did not finish (possible postinstall launch error).' }
    $setup.WaitForExit()
    if ($setup.ExitCode -ne 0 -or -not $clicked.Contains('Finish')) { throw 'Installer did not reach the real Finish/launch path.' }
    $deadline = (Get-Date).AddSeconds(60)
    $loaded = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
        if (Test-Path $logPath) {
            $log = Get-Content $logPath -Raw
            if (($Edition -eq 'WinUI' -and $log -match 'Navigation completed: DashboardPage\.') -or
                ($Edition -eq 'MyGo' -and $log -match 'MyGo ready: dashboard')) { $loaded = $true; break }
        }
    }
    if (-not $loaded) { throw 'Finish did not launch the installed administrator application.' }
    $app = @(Get-Process -Name $appName -ErrorAction Stop)
    if ($app.Count -ne 1 -or $app[0].Path -ne $executable) { throw 'Finish launched an unexpected application.' }
    Close-InstalledApplication $app[0]
    Copy-Item $logPath (Join-Path $output 'postinstall-startup.log')
    $clicked | Set-Content (Join-Path $output 'wizard-buttons.txt')
    Write-Host 'PASS: real non-silent installer Finish launched requireAdministrator application; no error 740'
}

try {
    # A legacy v1.10.0 shortcut must migrate without leaving duplicates.
    $legacy = $shell.CreateShortcut($legacyLink)
    $legacy.TargetPath = $executable
    $legacy.Save()
    Install-Case 'default' 'default' $true $true 'chinesesimplified'

    # Start the installed application via the actual Start Menu shortcut, not the ZIP.
    $logPath = Join-Path $env:LOCALAPPDATA "$appName/startup.log"
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath $menuLink
    $deadline = (Get-Date).AddSeconds(60)
    $loaded = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
        if (Test-Path $logPath) {
            $log = Get-Content $logPath -Raw
            if ($log -match 'Application initialization failed|Unable to open the main window|Unexpected application error') {
                throw "Installed application startup failed:`n$log"
            }
            if (($Edition -eq 'WinUI' -and $log -match 'Navigation completed: DashboardPage\.') -or ($Edition -eq 'MyGo' -and $log -match 'MyGo ready: dashboard')) { $loaded = $true; break }
        }
    }
    if (-not $loaded) { throw 'Installed application did not load from its Start Menu shortcut.' }
    $app = Get-Process -Name $appName
    if (@($app).Count -ne 1 -or $app.Path -ne $executable) {
        throw 'Shortcut launched an unexpected application path or process count.'
    }
    Close-InstalledApplication $app
    if ($Edition -eq 'MyGo') {
        if (Get-Process -Name 'Win-XinAi-De-Tools.Engine' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installDirectory 'engine/Win-XinAi-De-Tools.Engine.exe') }) {
            throw 'Installed MyGo left its shared engine running.'
        }
    }
    Copy-Item $logPath (Join-Path $output 'installed-startup.log')
    Write-Host 'PASS: actual Start Menu shortcut launched the installed app and closed normally'

    Test-InteractivePostinstallLaunch

    # Reinstallation must honor deselection and not inherit previously checked tasks.
    Install-Case 'reinstall-none' '' $false $false 'english'
    Install-Case 'reinstall-default' 'default' $true $true 'chinesesimplified'
    Uninstall-Case 'default'

    foreach ($case in @(
        @{ Name = 'none'; Tasks = ''; Desktop = $false; Menu = $false; Language = 'english' },
        @{ Name = 'desktop-only'; Tasks = 'desktopicon'; Desktop = $true; Menu = $false; Language = 'chinesesimplified' },
        @{ Name = 'startmenu-only'; Tasks = 'startmenuicon'; Desktop = $false; Menu = $true; Language = 'english' },
        @{ Name = 'both'; Tasks = 'desktopicon,startmenuicon'; Desktop = $true; Menu = $true; Language = 'chinesesimplified' }
    )) {
        Install-Case $case.Name $case.Tasks $case.Desktop $case.Menu $case.Language
        Uninstall-Case $case.Name
    }
    Write-Host 'PASS: all installer shortcut selection, reinstallation, and uninstall checks'
}
finally {
    Get-Process -Name $appName -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $executable } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path (Join-Path $installDirectory 'unins000.exe')) {
        Invoke-SetupProcess (Join-Path $installDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    }
    if (Test-Path $legacyLink) { Remove-Item $legacyLink -Force }
    if (Test-Path $installDirectory) { Remove-Item $installDirectory -Recurse -Force }
}
