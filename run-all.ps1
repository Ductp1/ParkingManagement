# Chạy toàn bộ hệ thống: 9 service + API Gateway, mỗi cái 1 cửa sổ PowerShell riêng.
param([switch]$NoBuild)

$root = $PSScriptRoot

# 1. Nạp cấu hình từ file .env (nếu có)
$envPath = "$root\.env"
if (Test-Path $envPath) {
    Write-Host "Dang nap cau hinh tu file .env..." -ForegroundColor Yellow
    Get-Content $envPath | Where-Object { $_ -match '=' -and $_ -notmatch '^#' } | ForEach-Object {
        $name, $value = $_.Split('=', 2)
        [Environment]::SetEnvironmentVariable($name.Trim(), $value.Trim(), "Process")
    }
}

# 2. Lấy giá trị từ biến môi trường (hoặc dùng mặc định nếu không có)
$dbHost = if ($env:DB_HOST) { $env:DB_HOST } else { "localhost" }
$dbPort = if ($env:DB_PORT) { $env:DB_PORT } else { "5432" }
$dbUser = if ($env:DB_USER) { $env:DB_USER } else { "postgres" }
$dbPass = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { "postgres" }
$applyMig = if ($env:APPLY_MIGRATIONS) { $env:APPLY_MIGRATIONS } else { "true" }
$seedData = if ($env:SEED_DATA) { $env:SEED_DATA } else { "true" }

$services = @(
    @{ Name = 'UserService';         Port = 5101 },
    @{ Name = 'VehicleService';      Port = 5102 },
    @{ Name = 'ParkingService';      Port = 5103 },
    @{ Name = 'BookingService';      Port = 5104 },
    @{ Name = 'PaymentService';      Port = 5105 },
    @{ Name = 'NotificationService'; Port = 5106 },
    @{ Name = 'GateService';         Port = 5107 },
    @{ Name = 'AdminService';        Port = 5108 },
    @{ Name = 'SupportService';      Port = 5109 }
)

if (-not $NoBuild) {
    Write-Host "Build solution..." -ForegroundColor Cyan
    dotnet build "$root\ParkingManagement.slnx" -nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host "Build loi - dung." -ForegroundColor Red; exit 1 }
}

foreach ($s in $services) {
    $project = "$root\Services\$($s.Name)\$($s.Name).API"
    
    # Tự động suy luận tên Database từ tên Service (ví dụ BookingService -> pm_booking)
    $dbName = "pm_" + $s.Name.Replace("Service", "").ToLower()
    $connStr = "Host=$dbHost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPass"
    
    # Tiêm biến môi trường thẳng vào shell con của từng service
    $cmd = "`$env:ConnectionStrings__ServiceDb='$connStr'; `$env:Database__ApplyMigrationsOnStartup='$applyMig'; `$env:Database__SeedDemoData='$seedData'; `$Host.UI.RawUI.WindowTitle = '$($s.Name) :$($s.Port)'; dotnet run --no-build --project '$project'"
    
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $cmd | Out-Null
    Write-Host ("  {0,-20} http://localhost:{1}" -f $s.Name, $s.Port)
}

$gateway = "$root\Gateway\ParkingManagement.Gateway"
$cmdGw = "`$Host.UI.RawUI.WindowTitle = 'Gateway :5000'; dotnet run --no-build --project '$gateway'"
Start-Process powershell -ArgumentList '-NoExit', '-Command', $cmdGw | Out-Null
Write-Host ("  {0,-20} http://localhost:5000" -f 'Gateway') -ForegroundColor Green

Write-Host "`nDoi ~20 giay cho cac service khoi tao, sau do mo:" -ForegroundColor Cyan
Write-Host "  http://localhost:5000/health/services"
