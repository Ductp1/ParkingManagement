#requires -Version 7.2
# Khởi động 9 service + Gateway cho Playwright E2E (T-903), chạy nền, log ghi vào Tests/e2e/.stack-logs.
# Dùng database riêng pm_e2e_* (tự migrate + seed dữ liệu demo) nên không đụng dữ liệu dev pm_*.
#   ./Tests/e2e/scripts/start-e2e-stack.ps1            # xóa DB pm_e2e_*, build rồi chạy
#   ./Tests/e2e/scripts/start-e2e-stack.ps1 -NoBuild   # bỏ qua build
#   ./Tests/e2e/scripts/start-e2e-stack.ps1 -KeepData  # giữ dữ liệu pm_e2e_* của lần trước
#   ./Tests/e2e/scripts/start-e2e-stack.ps1 -Stop      # dừng toàn bộ
# Chạy trực tiếp trong terminal, không nối "| ...": tiến trình nền giữ pipe nên lệnh sẽ không trả về.
param([switch]$NoBuild, [switch]$KeepData, [switch]$Stop)
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$e2eRoot = Join-Path $root 'Tests/e2e'
$logDir = Join-Path $e2eRoot '.stack-logs'
$pidFile = Join-Path $logDir 'pids.txt'
$ports = @(5101..5109) + 5000

function Stop-Stack {
    if (Test-Path $pidFile) {
        foreach ($id in Get-Content $pidFile) {
            # dotnet run sinh tiến trình con (API host) → dừng cả cây tiến trình.
            & taskkill /PID $id /T /F 2>$null | Out-Null
        }
        Remove-Item $pidFile
    }
    Write-Host 'Đã dừng E2E stack.'
}

if ($Stop) { Stop-Stack; exit 0 }
if (Test-Path $pidFile) { Stop-Stack }

# Cổng đang bận (thường do run-all.ps1 / Visual Studio) → test sẽ chạy nhầm vào service dev và DB dev.
for ($i = 0; $i -lt 10; $i++) {   # chờ tối đa 10 giây cho stack E2E cũ vừa dừng nhả cổng
    $busy = $ports | Where-Object { Get-NetTCPConnection -LocalPort $_ -State Listen -ErrorAction SilentlyContinue }
    if (-not $busy) { break }
    Start-Sleep -Seconds 1
}
if ($busy) { throw "Cổng $($busy -join ', ') đang được dùng – hãy tắt các service dev trước khi chạy E2E." }
New-Item -ItemType Directory -Force $logDir | Out-Null

# Mọi biến môi trường đặt dưới đây chỉ để truyền cho tiến trình con – khôi phục lại khi script kết thúc
# để terminal của bạn không bị "dính" cấu hình E2E (DB pm_e2e_*, CAPTCHA tắt…).
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
    if (-not $KeepData) {
        # Dữ liệu seed sạch mỗi lần chạy → kết quả lặp lại được (cần npm install trong Tests/e2e).
        node (Join-Path $e2eRoot 'scripts/reset-e2e-db.mjs')
        if ($LASTEXITCODE -ne 0) { throw 'Không xóa được database pm_e2e_* – đã chạy npm install trong Tests/e2e chưa?' }
    }

    $dbHost = if ($env:DB_HOST) { $env:DB_HOST } else { 'localhost' }
    $dbPort = if ($env:DB_PORT) { $env:DB_PORT } else { '5432' }
    $dbUser = if ($env:DB_USER) { $env:DB_USER } else { 'postgres' }
    $dbPass = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { 'postgres' }

    # Cặp khóa JWT RS256 riêng cho E2E (sinh 1 lần, nằm trong thư mục bị git ignore) – không dùng khóa thật của nhóm.
    $keyDir = Join-Path $e2eRoot '.keys'
    $privateKey = Join-Path $keyDir 'private.key'
    $publicKey = Join-Path $keyDir 'public.key'
    if (-not (Test-Path $privateKey)) {
        New-Item -ItemType Directory -Force $keyDir | Out-Null
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            [IO.File]::WriteAllText($privateKey, $rsa.ExportPkcs8PrivateKeyPem())
            [IO.File]::WriteAllText($publicKey, $rsa.ExportSubjectPublicKeyInfoPem())
        } finally { $rsa.Dispose() }
    }

    if (-not $NoBuild) {
        Write-Host 'Build solution...' -ForegroundColor Cyan
        dotnet build (Join-Path $root 'ParkingManagement.slnx') -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'Build lỗi.' }
    }

    # Cấu hình dùng chung cho 9 service + Gateway.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Jwt__PublicKeyPath = $publicKey     # mọi service + Gateway kiểm tra chữ ký bằng khóa E2E
    $env:Jwt__PrivateKeyPath = $privateKey   # chỉ UserService dùng để ký
    $env:Security__EncryptionKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    # Secret ký QR dùng chung BookingService ↔ GateService, sinh mới mỗi lần chạy.
    $env:QRTOKEN_SECRET = 'e2e-only-qr-secret-' + [guid]::NewGuid().ToString('N')
    $env:Database__ApplyMigrationsOnStartup = 'true'
    $env:Database__SeedDemoData = 'true'
    $env:Captcha__Enabled = 'false'          # Development cho phép tắt CAPTCHA; UI test giả lập widget Turnstile
    $env:EventBus__Enabled = 'false'         # E2E không phụ thuộc RabbitMQ

    $services = 'UserService', 'VehicleService', 'ParkingService', 'BookingService', 'PaymentService',
        'NotificationService', 'GateService', 'AdminService', 'SupportService'
    $ids = @()
    foreach ($s in $services) {
        $db = 'pm_e2e_' + $s.Replace('Service', '').ToLower()
        $env:ConnectionStrings__ServiceDb = "Host=$dbHost;Port=$dbPort;Database=$db;Username=$dbUser;Password=$dbPass"
        $project = Join-Path $root "Services/$s/$s.API"
        $p = Start-Process dotnet -ArgumentList 'run', '--no-build', '--project', "`"$project`"" -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logDir "$s.log") -RedirectStandardError (Join-Path $logDir "$s.err.log")
        $ids += $p.Id
    }
    $gateway = Join-Path $root 'Gateway/ParkingManagement.Gateway'
    $p = Start-Process dotnet -ArgumentList 'run', '--no-build', '--project', "`"$gateway`"" -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logDir 'Gateway.log') -RedirectStandardError (Join-Path $logDir 'Gateway.err.log')
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

# Chờ Gateway + 9 service sẵn sàng (lần đầu migrate DB mới có thể mất ~1 phút).
$deadline = (Get-Date).AddMinutes(3)
foreach ($port in $ports) {
    $path = if ($port -eq 5000) { '/' } else { '/health' }   # Gateway không có /health riêng
    while ($true) {
        try { Invoke-WebRequest "http://localhost:$port$path" -UseBasicParsing -TimeoutSec 3 | Out-Null; break }
        catch {
            if ((Get-Date) -gt $deadline) { throw "Cổng $port chưa sẵn sàng – xem log trong $logDir" }
            Start-Sleep -Seconds 2
        }
    }
}
Write-Host 'E2E stack sẵn sàng: Gateway http://localhost:5000 (DB pm_e2e_*).' -ForegroundColor Green
