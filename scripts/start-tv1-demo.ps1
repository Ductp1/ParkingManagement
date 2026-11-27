#requires -Version 7.2
param()
$ErrorActionPreference = 'Stop'
$demoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$demoLogRoot = Join-Path $demoRoot 'test-results'
New-Item -ItemType Directory -Force -Path $demoLogRoot | Out-Null
$demoHosts = @(
    @{ Name = 'UserService.API'; Folder = 'Services/UserService/UserService.API'; Port = 5101; Health = '/health' },
    @{ Name = 'ParkingManagement.Gateway'; Folder = 'Gateway/ParkingManagement.Gateway'; Port = 5000; Health = '/' }
)

foreach ($demoHost in $demoHosts) {
    $demoProject = Join-Path $demoRoot $demoHost.Folder
    $demoExecutable = Join-Path $demoProject "bin/Debug/net10.0/$($demoHost.Name).exe"
    if (!(Test-Path -LiteralPath $demoExecutable)) {
        throw "Chưa có bản build: $demoExecutable. Build project trước khi chạy demo."
    }
}

foreach ($demoHost in $demoHosts) {
    $demoProject = Join-Path $demoRoot $demoHost.Folder
    $demoExecutable = Join-Path $demoProject "bin/Debug/net10.0/$($demoHost.Name).exe"
    $demoRunning = @(Get-Process -Name $demoHost.Name -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.Equals($demoExecutable, [StringComparison]::OrdinalIgnoreCase)
    })
    if (!$demoRunning.Count) {
        $demoProcess = Start-Process -FilePath $demoExecutable -ArgumentList @('--urls', "http://localhost:$($demoHost.Port)") `
            -WorkingDirectory $demoProject -WindowStyle Hidden -PassThru `
            -Environment @{ ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development' } `
            -RedirectStandardOutput (Join-Path $demoLogRoot "$($demoHost.Name).demo.out.log") `
            -RedirectStandardError (Join-Path $demoLogRoot "$($demoHost.Name).demo.err.log")
        Write-Host "$($demoHost.Name): started PID $($demoProcess.Id)"
    } else { Write-Host "$($demoHost.Name): đang chạy, giữ tiến trình hiện tại." }

    $demoHealthy = $false
    for ($demoAttempt = 0; $demoAttempt -lt 15; $demoAttempt++) {
        try {
            $demoResponse = Invoke-WebRequest "http://localhost:$($demoHost.Port)$($demoHost.Health)" -TimeoutSec 2 -SkipHttpErrorCheck
            if ($demoResponse.StatusCode -eq 200) { $demoHealthy = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    if (!$demoHealthy) { throw "$($demoHost.Name) chưa sẵn sàng. Xem log trong $demoLogRoot." }
    Write-Host "$($demoHost.Name): ready http://localhost:$($demoHost.Port)"
}
Write-Host 'TV1 sẵn sàng. Giữ PostgreSQL, RabbitMQ và frontend đang chạy. Không cần chạy run-all.ps1 để demo Auth/Profile/Event Bus.'
