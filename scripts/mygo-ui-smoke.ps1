param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
if ($env:CI -ne 'true') { throw 'Disposable Windows CI only.' }
$output = Join-Path $pwd 'artifacts/mygo-ui'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$env:WINXINAI_MYGO_SMOKE_DIR = $output
$process = Start-Process -FilePath (Resolve-Path $Executable) -ArgumentList '--ui-smoke' -PassThru
try {
    if (-not $process.WaitForExit(300000)) { throw 'MyGo UI smoke timed out.' }
    if (Test-Path "$output/FAILED.txt") { throw (Get-Content "$output/FAILED.txt" -Raw) }
    if ($process.ExitCode -ne 0) { throw "MyGo exited with code $($process.ExitCode)." }
    if (-not (Test-Path "$output/verification.json")) { throw 'MyGo did not produce a verification report.' }
    Get-Content "$output/verification.json"
    $log = Join-Path $env:LOCALAPPDATA 'Win-XinAi-De-Tools-MyGo/startup.log'
    Copy-Item $log "$output/startup.log"
    if ((Get-Content $log -Raw) -notmatch 'production=true') { throw 'MyGo production mode was not enabled.' }
    if ((Get-Content $log -Raw) -notmatch 'MyGo shutdown completed') { throw 'MyGo did not shut down its engine cleanly.' }
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item Env:WINXINAI_MYGO_SMOKE_DIR -ErrorAction SilentlyContinue
}
