#requires -Version 7.2
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$userApi = Join-Path $projectRoot 'Services/UserService/UserService.API'
$gateway = Join-Path $projectRoot 'Gateway/ParkingManagement.Gateway'
$keyFolder = Join-Path $userApi 'Keys'
New-Item -ItemType Directory -Force -Path $keyFolder | Out-Null
$privatePath = Join-Path $keyFolder 'private.key'
$publicPath = Join-Path $keyFolder 'public.key'
$rsa = [Security.Cryptography.RSA]::Create(2048)
try {
    if (Test-Path -LiteralPath $privatePath) { $rsa.ImportFromPem([IO.File]::ReadAllText($privatePath)) }
    else { [IO.File]::WriteAllText($privatePath, $rsa.ExportPkcs8PrivateKeyPem()) }
    $publicPem = $rsa.ExportSubjectPublicKeyInfoPem()
    if (Test-Path -LiteralPath $publicPath) {
        $existing = [Security.Cryptography.RSA]::Create()
        try { $existing.ImportFromPem([IO.File]::ReadAllText($publicPath)); if ($existing.ExportSubjectPublicKeyInfoPem() -ne $publicPem) { throw 'Public/private key không khớp; script không ghi đè khóa hiện có.' } }
        finally { $existing.Dispose() }
    } else { [IO.File]::WriteAllText($publicPath, $publicPem) }
} finally { $rsa.Dispose() }
$configPath = Join-Path $userApi 'appsettings.Local.json'
$existingConfig = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$encryptionKey = $existingConfig.Security.EncryptionKey
if (-not $encryptionKey) { $encryptionKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)) }
if (-not $OtpDeliveryUrl) { $OtpDeliveryUrl = $existingConfig.Otp.DeliveryUrl }
if (-not $EventBusUrl) { $EventBusUrl = $existingConfig.EventBus.PublishUrl }
$existingConfig.Jwt = @{ PublicKeyPath = 'Keys/public.key'; PrivateKeyPath = 'Keys/private.key' }
if (-not $existingConfig.Security) { $existingConfig.Security = @{} }
$existingConfig.Security.EncryptionKey = $encryptionKey
if (-not $existingConfig.Otp) { $existingConfig.Otp = @{} }
$existingConfig.Otp.DeliveryUrl = $OtpDeliveryUrl
if (-not $existingConfig.EventBus) { $existingConfig.EventBus = @{} }
$existingConfig.EventBus.Enabled = $true; $existingConfig.EventBus.PublishUrl = $EventBusUrl
$existingConfig | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding utf8
$gatewayPath = Join-Path $gateway 'appsettings.Local.json'
$gatewayConfig = if (Test-Path -LiteralPath $gatewayPath) { Get-Content -LiteralPath $gatewayPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$gatewayConfig.Jwt = @{ PublicKeyPath = $publicPath; SessionValidationUrl = 'http://localhost:5101/api/v1/auth/session' }
$gatewayConfig | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $gatewayPath -Encoding utf8
Write-Host 'Đã tạo/cập nhật cấu hình local và giữ nguyên khóa mã hóa hiện có. Không đưa file Local/private key vào Git.'
Write-Host 'OTP cần SMTP hoặc endpoint gửi thực tế. Chạy scripts/configure-otp-email.ps1 để cấu hình SMTP.'
