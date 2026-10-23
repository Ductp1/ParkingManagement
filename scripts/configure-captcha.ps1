#requires -Version 7.2
param(
    [string[]]$AllowedHostnames = @('localhost')
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$siteKey = (Read-Host 'Cloudflare Turnstile SiteKey thật').Trim()
$secureSecret = Read-Host 'Cloudflare Turnstile SecretKey thật (nhập ẩn)' -AsSecureString
$secretKey = [Net.NetworkCredential]::new('', $secureSecret).Password.Trim()
try {
    $testSiteKeys = @('1x00000000000000000000AA', '2x00000000000000000000AB',
        '1x00000000000000000000BB', '2x00000000000000000000BB', '3x00000000000000000000FF')
    $testSecretKeys = @('1x0000000000000000000000000000000AA',
        '2x0000000000000000000000000000000AA', '3x0000000000000000000000000000000AA')
    if ([string]::IsNullOrWhiteSpace($siteKey) -or [string]::IsNullOrWhiteSpace($secretKey) -or
        $siteKey -match '\s|^your-' -or $secretKey -match '\s|^your-' -or
        $siteKey -in $testSiteKeys -or $secretKey -in $testSecretKeys) {
        throw 'Cần SiteKey và SecretKey thật của cùng widget Cloudflare; không chấp nhận khóa test hoặc placeholder.'
    }
    if ($AllowedHostnames.Count -eq 0 -or ($AllowedHostnames | Where-Object {
        [string]::IsNullOrWhiteSpace($_) -or $_ -match '[\s/:]'
    })) { throw 'Hostname không được chứa scheme, port, path hoặc khoảng trắng.' }

    $configPath = Join-Path $projectRoot 'Services/UserService/UserService.API/appsettings.Local.json'
    $config = if (Test-Path -LiteralPath $configPath) {
        Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable
    } else { @{} }
    $config.Captcha = @{
        Enabled = $true
        SecretKey = $secretKey
        AllowedHostnames = $AllowedHostnames
        ExpectedAction = 'login'
        TimeoutSeconds = 5
    }
    $frontendPath = Join-Path $projectRoot 'Frontend/ParkingManagement.Web/.env.development.local'
    $lines = if (Test-Path -LiteralPath $frontendPath) {
        @(Get-Content -LiteralPath $frontendPath | Where-Object {
            $_ -notmatch '^\s*VITE_TURNSTILE_SITE_KEY\s*=' -and
            $_ -ne '# Cloudflare public test sitekey for local Development only.'
        })
    } else { @() }

    $config | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $configPath -Encoding utf8
    @($lines; "VITE_TURNSTILE_SITE_KEY=$siteKey") | Set-Content -LiteralPath $frontendPath -Encoding utf8
    Write-Host 'Đã lưu cấu hình CAPTCHA thật cho local. Khởi động lại UserService và Vite.'
    Write-Host 'Các hostname trên phải được thêm vào chính widget trong Cloudflare dashboard.'
    Write-Host 'Script chỉ lưu cấu hình; cần kiểm tra đăng nhập thực tế để xác nhận cặp khóa hợp lệ.'
} finally {
    $secretKey = $null
    $secureSecret.Dispose()
}
