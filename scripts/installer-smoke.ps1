param(
    [Parameter(Mandatory = $true)]
    [string]$Installer
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

$Installer = (Resolve-Path $Installer).Path
$output = Join-Path $pwd 'artifacts/installer-smoke'
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Runner TEMP can contain an 8.3 alias; Shell resolves .lnk targets to long paths.
Add-Type @'
using System.Runtime.InteropServices;
using System.Text;
public static class InstallerSmokePath
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetLongPathName(string path, StringBuilder result, uint capacity);
}
'@
$tempPath = [Text.StringBuilder]::new(32768)
$length = [InstallerSmokePath]::GetLongPathName($env:TEMP, $tempPath, 32768)
if ($length -eq 0 -or $length -ge 32768) { throw 'Cannot resolve the runner temporary directory.' }
$installDirectory = Join-Path $tempPath.ToString() ('WinXinAiInstaller-' + [guid]::NewGuid().ToString('N'))
$executable = Join-Path $installDirectory 'Win-XinAi-De-Tools.exe'
$programs = [Environment]::GetFolderPath('CommonPrograms')
$desktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$menuDirectory = Join-Path $programs 'Win-XinAi-De-Tools'
$menuLink = Join-Path $menuDirectory 'Win-XinAi-De-Tools.lnk'
$uninstallLink = Join-Path $menuDirectory 'Uninstall Win-XinAi-De-Tools.lnk'
$desktopLink = Join-Path $desktop 'Win-XinAi-De-Tools.lnk'
$legacyLink = Join-Path $programs 'Win-XinAi-De-Tools.lnk'
$uninstallKey = '{AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7}_is1'

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
if (Get-Process -Name 'Win-XinAi-De-Tools' -ErrorAction SilentlyContinue) {
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
    if (Get-Process -Name 'Win-XinAi-De-Tools' -ErrorAction SilentlyContinue) {
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

try {
    # A legacy v1.10.0 shortcut must migrate without leaving duplicates.
    $legacy = $shell.CreateShortcut($legacyLink)
    $legacy.TargetPath = $executable
    $legacy.Save()
    Install-Case 'default' 'default' $true $true 'chinesesimplified'

    # Start the installed application via the actual Start Menu shortcut, not the ZIP.
    $logPath = Join-Path $env:LOCALAPPDATA 'Win-XinAi-De-Tools/startup.log'
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
            if ($log -match 'Navigation completed: DashboardPage\.') { $loaded = $true; break }
        }
    }
    if (-not $loaded) { throw 'Installed application did not load from its Start Menu shortcut.' }
    $app = Get-Process -Name 'Win-XinAi-De-Tools'
    if (@($app).Count -ne 1 -or $app.Path -ne $executable) {
        throw 'Shortcut launched an unexpected application path or process count.'
    }
    if (-not $app.CloseMainWindow() -or -not $app.WaitForExit(15000)) {
        throw 'Installed application did not shut down normally.'
    }
    Copy-Item $logPath (Join-Path $output 'installed-startup.log')
    Write-Host 'PASS: actual Start Menu shortcut launched the installed app and closed normally'

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
    Get-Process -Name 'Win-XinAi-De-Tools' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $executable } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path (Join-Path $installDirectory 'unins000.exe')) {
        Invoke-SetupProcess (Join-Path $installDirectory 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    }
    if (Test-Path $legacyLink) { Remove-Item $legacyLink -Force }
    if (Test-Path $installDirectory) { Remove-Item $installDirectory -Recurse -Force }
}
