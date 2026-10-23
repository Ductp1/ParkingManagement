#requires -Version 7.2
param([ValidateSet('start','stop','status')][string]$Action = 'start')
$ErrorActionPreference = 'Stop'
$pgRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.tools/postgres'))
$pgBin = Join-Path $pgRoot 'pgsql/bin'
if ($Action -eq 'status') { & (Join-Path $pgBin 'pg_isready.exe') -h 127.0.0.1 -p 5432; exit $LASTEXITCODE }
$arguments = @('-D', ('"{0}"' -f (Join-Path $pgRoot 'data')), '-w')
if ($Action -eq 'start') {
    & (Join-Path $pgBin 'pg_isready.exe') -h 127.0.0.1 -p 5432 | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host 'PostgreSQL đang chạy.'; exit 0 }
    $arguments += @('-l', ('"{0}"' -f (Join-Path $pgRoot 'postgres.log')), 'start')
} else { $arguments += @('-m','fast','stop') }
$process = Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
exit $process.ExitCode
