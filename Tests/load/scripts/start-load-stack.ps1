#requires -Version 7.2
# T-904: chạy 9 service + Gateway bản Release cho kiểm thử tải, trên database riêng pm_load_* (không đụng dữ liệu dev pm_*).
#   ./Tests/load/scripts/start-load-stack.ps1            # build Release rồi chạy nền
#   ./Tests/load/scripts/start-load-stack.ps1 -NoBuild   # bỏ qua build
#   ./Tests/load/scripts/start-load-stack.ps1 -Stop      # dừng toàn bộ
# Chạy trực tiếp trong terminal, không nối "| ...": tiến trình nền giữ pipe nên lệnh sẽ không trả về.
param([switch]$NoBuild, [switch]$Stop)
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$workDir = Join-Path $root 'Tests/load/.stack'
$pidFile = Join-Path $workDir 'pids.txt'
$ports = @(5101..5109) + 5000

function Stop-Stack {
    if (Test-Path $pidFile) {
        foreach ($id in Get-Content $pidFile) { & taskkill /PID $id /T /F 2>$null | Out-Null }
        Remove-Item $pidFile
    }
    Write-Host 'Đã dừng hệ thống kiểm thử tải.'
}

if ($Stop) { Stop-Stack; exit 0 }
if (Test-Path $pidFile) { Stop-Stack }

# Cổng đang bận (thường do run-all.ps1 / Visual Studio) → tải sẽ chạy nhầm vào service dev và DB dev.
for ($i = 0; $i -lt 10; $i++) {
    $busy = $ports | Where-Object { Get-NetTCPConnection -LocalPort $_ -State Listen -ErrorAction SilentlyContinue }
    if (-not $busy) { break }
    Start-Sleep -Seconds 1
}
if ($busy) { throw "Cổng $($busy -join ', ') đang được dùng – hãy tắt các service dev trước." }
New-Item -ItemType Directory -Force $workDir | Out-Null

# Biến môi trường chỉ truyền cho tiến trình con, khôi phục khi script kết thúc.
$savedEnv = @{}
Get-ChildItem env: | ForEach-Object { $savedEnv[$_.Name] = $_.Value }
try {
    # Cấu hình DB lấy từ .env giống run-all.ps1 (không in mật khẩu).
    $envPath = Join-Path $root '.env'
    if (Test-Path $envPath) {
        Get-Content $envPath | Where-Object { $_ -match '=' -and $_ -notmatch '^#' } | ForEach-Object {
            $name, $value = $_.Split('=', 2)
            [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), 'Process')
        }
    }
    $dbHost = if ($env:DB_HOST) { $env:DB_HOST } else { 'localhost' }
    $dbPort = if ($env:DB_PORT) { $env:DB_PORT } else { '5432' }
    $dbUser = if ($env:DB_USER) { $env:DB_USER } else { 'postgres' }
    $dbPass = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { 'postgres' }

    # Khóa riêng cho kiểm thử tải, sinh 1 lần trong thư mục bị git ignore – không dùng khóa thật của nhóm.
    $privateKey = Join-Path $workDir 'private.key'
    $publicKey = Join-Path $workDir 'public.key'
    $secretsFile = Join-Path $workDir 'secrets.txt'
    if (-not (Test-Path $privateKey)) {
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            [IO.File]::WriteAllText($privateKey, $rsa.ExportPkcs8PrivateKeyPem())
            [IO.File]::WriteAllText($publicKey, $rsa.ExportSubjectPublicKeyInfoPem())
        } finally { $rsa.Dispose() }
    }
    if (-not (Test-Path $secretsFile)) {
        # Giữ nguyên giữa các lần chạy vì dữ liệu pm_load_* đã mã hóa bằng khóa này.
        @([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)), ('load-only-qr-' + [guid]::NewGuid().ToString('N'))) |
            Set-Content $secretsFile
    }
    $secrets = Get-Content $secretsFile

    if (-not $NoBuild) {
        Write-Host 'Build Release...' -ForegroundColor Cyan
        dotnet build (Join-Path $root 'ParkingManagement.slnx') -c Release -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'Build lỗi.' }
    }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'   # CAPTCHA chỉ được tắt ở Development
    $env:Logging__LogLevel__Default = 'Warning'   # ghi log ít để không tính chi phí log vào số đo
    $env:Jwt__PublicKeyPath = $publicKey
    $env:Jwt__PrivateKeyPath = $privateKey
    $env:Security__EncryptionKey = $secrets[0]
    $env:QRTOKEN_SECRET = $secrets[1]
    $env:Database__ApplyMigrationsOnStartup = 'true'
    $env:Database__SeedDemoData = 'true'
    $env:Captcha__Enabled = 'false'
    $env:EventBus__Enabled = 'false'              # không phụ thuộc RabbitMQ; sự kiện nằm lại trong Outbox

    $ids = @()
    foreach ($s in 'UserService', 'VehicleService', 'ParkingService', 'BookingService', 'PaymentService',
        'NotificationService', 'GateService', 'AdminService', 'SupportService') {
        $db = 'pm_load_' + $s.Replace('Service', '').ToLower()
        $env:ConnectionStrings__ServiceDb = "Host=$dbHost;Port=$dbPort;Database=$db;Username=$dbUser;Password=$dbPass"
        $project = Join-Path $root "Services/$s/$s.API"
        $p = Start-Process dotnet -ArgumentList 'run', '-c', 'Release', '--no-build', '--project', "`"$project`"" -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $workDir "$s.log") -RedirectStandardError (Join-Path $workDir "$s.err.log")
        $ids += $p.Id
    }
    $gateway = Join-Path $root 'Gateway/ParkingManagement.Gateway'
    $p = Start-Process dotnet -ArgumentList 'run', '-c', 'Release', '--no-build', '--project', "`"$gateway`"" -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $workDir 'Gateway.log') -RedirectStandardError (Join-Path $workDir 'Gateway.err.log')
    $ids += $p.Id
    $ids | Set-Content $pidFile
}
finally {
    foreach ($name in @(Get-ChildItem env: | ForEach-Object Name)) {
        if (-not $savedEnv.ContainsKey($name)) { Remove-Item "env:$name" }
    }
    foreach ($entry in $savedEnv.GetEnumerator()) {
        if ((Get-Item "env:$($entry.Key)" -ErrorAction SilentlyContinue).Value -ne $entry.Value) {
            Set-Item "env:$($entry.Key)" $entry.Value
        }
    }
}

Write-Host 'Đang chờ service sẵn sàng (lần đầu cần tạo + seed DB)...' -ForegroundColor Cyan
$servicePorts = 5101..5109
for ($i = 0; $i -lt 90; $i++) {
    $down = $servicePorts | Where-Object {
        try { (Invoke-WebRequest "http://localhost:$_/health" -TimeoutSec 2 -UseBasicParsing).StatusCode -ne 200 } catch { $true }
    }
    if (-not $down) { Write-Host 'Hệ thống đã sẵn sàng tại http://localhost:5000' -ForegroundColor Green; exit 0 }
    Start-Sleep -Seconds 2
}
throw "Service cổng $($down -join ', ') chưa sẵn sàng sau 3 phút – xem log trong Tests/load/.stack."
