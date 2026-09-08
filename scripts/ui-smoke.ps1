param([Parameter(Mandatory=$true)][string]$Executable, [string]$OutputDirectory = 'artifacts/ui-smoke')
$ErrorActionPreference = 'Stop'
$env:CI = 'true'
$env:WINXINAI_UI_SMOKE_DIR = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $env:WINXINAI_UI_SMOKE_DIR | Out-Null
$logPath = Join-Path $env:LOCALAPPDATA 'Win-XinAi-De-Tools/startup.log'
Remove-Item $logPath -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath ([IO.Path]::GetFullPath($Executable)) -ArgumentList '--ui-smoke' -PassThru
try {
    if (-not $process.WaitForExit(240000)) { throw 'UI smoke timed out after four minutes.' }
    $log = Get-Content $logPath -Raw
    if ($log -match 'UI smoke FAILED|Unexpected application error|initialization failed|Unable to open') { throw "UI smoke failed:`n$log" }
    if ($log -notmatch 'UI smoke PASSED:') { throw "UI smoke did not finish:`n$log" }
    if ($log -notmatch 'Skia chart rendered:') { throw 'The packaged Skia native renderer was not exercised.' }
    if ($process.ExitCode -ne 0) { throw "Process exited with $($process.ExitCode)." }
    Write-Output ($log -split "`n" | Where-Object { $_ -match 'UI smoke PASSED' })
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    if (Test-Path $logPath) { Copy-Item $logPath (Join-Path $env:WINXINAI_UI_SMOKE_DIR 'startup.log') }
}
