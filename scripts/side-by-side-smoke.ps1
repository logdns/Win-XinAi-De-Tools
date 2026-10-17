param(
    [Parameter(Mandatory=$true)][string]$WinUIInstaller,
    [Parameter(Mandatory=$true)][string]$MyGoInstaller
)
$ErrorActionPreference = 'Stop'
if ($env:CI -ne 'true' -or -not $IsWindows) { throw 'Disposable elevated Windows CI only.' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator required.' }
$output = Join-Path $pwd 'artifacts/side-by-side'
New-Item -ItemType Directory $output -Force | Out-Null
$root = Join-Path $env:TEMP ('WinXinAiParallel-' + [guid]::NewGuid().ToString('N'))
$programs = [Environment]::GetFolderPath('CommonPrograms')
$desktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$products = @(
    @{ Name='Win-XinAi-De-Tools'; ID='{AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7}'; Installer=(Resolve-Path $WinUIInstaller).Path },
    @{ Name='Win-XinAi-De-Tools-MyGo'; ID='{ED82C612-79BA-4D1D-B2BD-5D6F9FA79E61}'; Installer=(Resolve-Path $MyGoInstaller).Path }
)
foreach ($p in $products) {
    foreach ($hive in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall')) {
        if (Test-Path "$hive\$($p.ID)_is1") { throw 'Existing installation; refusing to modify.' }
    }
    foreach ($path in @("$programs\$($p.Name)", "$programs\$($p.Name).lnk", "$desktop\$($p.Name).lnk")) {
        if (Test-Path $path) { throw "Existing shortcut: $path" }
    }
    if (Get-Process -Name $p.Name -ErrorAction SilentlyContinue) { throw 'Application already running.' }
}
function Run-Setup($file, $arguments) {
    $process = Start-Process $file -ArgumentList $arguments -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Setup failed: $file ($($process.ExitCode))" }
}
function Assert-Installed($p) {
    $directory = Join-Path $root $p.Name
    $exe = Join-Path $directory "$($p.Name).exe"
    if (-not (Test-Path $exe)) { throw "Missing executable: $exe" }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($link in @("$desktop\$($p.Name).lnk", "$programs\$($p.Name)\$($p.Name).lnk")) {
        if (-not (Test-Path $link)) { throw "Missing independent shortcut: $link" }
        $target = $shell.CreateShortcut($link).TargetPath
        if (-not (Test-Path $target) -or (Get-FileHash $target).Hash -ne (Get-FileHash $exe).Hash) { throw "Wrong shortcut target: $link" }
    }
}
try {
    foreach ($p in $products) {
        Run-Setup $p.Installer @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/DIR=`"$root\$($p.Name)`"", "/LOG=`"$output\$($p.Name)-install.log`"")
    }
    foreach ($p in $products) { Assert-Installed $p }
    # Reinstalling WinUI without shortcuts must not remove MyGo's entry points.
    $p = $products[0]
    Run-Setup $p.Installer @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART', '/TASKS=""', "/DIR=`"$root\$($p.Name)`"")
    Assert-Installed $products[1]
    Run-Setup "$root\$($p.Name)\unins000.exe" @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
    Assert-Installed $products[1]
    'PASS: simultaneous installation, independent shortcuts, WinUI reinstall/deselection/uninstall preserves MyGo' | Tee-Object "$output/verification.txt"
}
finally {
    foreach ($p in $products) {
        $uninstaller = "$root\$($p.Name)\unins000.exe"
        if (Test-Path $uninstaller) { Run-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') }
    }
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
}
foreach ($p in $products) {
    foreach ($link in @("$desktop\$($p.Name).lnk", "$programs\$($p.Name)\$($p.Name).lnk")) {
        if (Test-Path $link) { throw "Uninstall left shortcut: $link" }
    }
}
