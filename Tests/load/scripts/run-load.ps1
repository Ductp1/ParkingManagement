#requires -Version 7.2
# T-904: chạy k6. Dùng k6 cài trên máy nếu có, không thì dùng Docker image grafana/k6.
#   ./Tests/load/scripts/run-load.ps1                  # smoke: mỗi luồng 1 lần, ~20 giây
#   ./Tests/load/scripts/run-load.ps1 -Profile light   # tải nhẹ: 45 VU, ~4.5 phút
#   ./Tests/load/scripts/run-load.ps1 -Profile peak    # cao điểm giả lập: 100 VU, ~4.5 phút
param([ValidateSet('smoke', 'light', 'peak')][string]$Profile = 'smoke', [string]$BaseUrl)
$ErrorActionPreference = 'Stop'

$k6Image = 'grafana/k6:1.3.0'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$loadDir = Join-Path $root 'Tests/load'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force (Join-Path $loadDir 'results') | Out-Null

# Mật khẩu tài khoản demo lấy từ nguồn duy nhất, không lặp lại trong script.
$demoIds = Get-Content (Join-Path $root 'BuildingBlocks/ParkingManagement.SharedKernel/Contracts/DemoIds.cs') -Raw
if ($demoIds -notmatch 'Password\s*=\s*"([^"]+)"') { throw 'Không đọc được mật khẩu demo trong DemoIds.cs' }
$password = $Matches[1]

if (Get-Command k6 -ErrorAction SilentlyContinue) {
    $env:DEMO_PASSWORD = $password
    $env:PROFILE = $Profile
    $env:BASE_URL = if ($BaseUrl) { $BaseUrl } else { 'http://localhost:5000' }
    $env:RESULT_FILE = Join-Path $loadDir "results/$Profile-$stamp.json"
    & k6 run --quiet (Join-Path $loadDir 'load-test.js')
} else {
    $url = if ($BaseUrl) { $BaseUrl } else { 'http://host.docker.internal:5000' }
    & docker run --rm -i -v "${loadDir}:/load" -e "DEMO_PASSWORD=$password" -e "PROFILE=$Profile" -e "BASE_URL=$url" `
        -e "RESULT_FILE=/load/results/$Profile-$stamp.json" $k6Image run --quiet /load/load-test.js
}
$code = $LASTEXITCODE
Write-Host "Kết quả chi tiết: Tests/load/results/$Profile-$stamp.json"
exit $code
