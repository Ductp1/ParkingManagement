# Chạy toàn bộ hệ thống: 9 service + API Gateway, mỗi cái 1 cửa sổ PowerShell riêng.
# Cách dùng (trong thư mục ParkingManagement):
#   .\run-all.ps1            # build rồi chạy
#   .\run-all.ps1 -NoBuild   # chạy luôn, không build lại
# Đóng cửa sổ nào thì service đó dừng. Kiểm tra trạng thái: http://localhost:5000/health/services
param([switch]$NoBuild)

$root = $PSScriptRoot
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
    if ($LASTEXITCODE -ne 0) { Write-Host "Build lỗi – dừng." -ForegroundColor Red; exit 1 }
}

foreach ($s in $services) {
    $project = "$root\Services\$($s.Name)\$($s.Name).API"
    $cmd = "`$Host.UI.RawUI.WindowTitle = '$($s.Name) :$($s.Port)'; dotnet run --no-build --project '$project'"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $cmd | Out-Null
    Write-Host ("  {0,-20} http://localhost:{1}" -f $s.Name, $s.Port)
}

$gateway = "$root\Gateway\ParkingManagement.Gateway"
Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle = 'Gateway :5000'; dotnet run --no-build --project '$gateway'" | Out-Null
Write-Host ("  {0,-20} http://localhost:5000" -f 'Gateway') -ForegroundColor Green

Write-Host "`nĐợi ~20 giây cho các service tạo database lần đầu, sau đó mở:" -ForegroundColor Cyan
Write-Host "  http://localhost:5000/health/services"
Write-Host "  http://localhost:5000/api/v1/parking-lots/1"
