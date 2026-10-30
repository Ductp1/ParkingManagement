#requires -Version 7.2
param()
$ErrorActionPreference = 'Stop'
$rabbitRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rabbitEnvPath = Join-Path $rabbitRoot '.env.rabbitmq.local'
$rabbitConfigPath = Join-Path $rabbitRoot 'Services/UserService/UserService.API/appsettings.Local.json'
if (Test-Path -LiteralPath $rabbitEnvPath) {
    $rabbitValues = @{}
    foreach ($rabbitLine in Get-Content -LiteralPath $rabbitEnvPath) {
        if ($rabbitLine -match '^(RABBITMQ_USER|RABBITMQ_PASSWORD)=(.+)$') { $rabbitValues[$Matches[1]] = $Matches[2] }
    }
    if (!$rabbitValues['RABBITMQ_USER'] -or !$rabbitValues['RABBITMQ_PASSWORD']) { throw 'File cấu hình RabbitMQ local thiếu credentials; không ghi đè.' }
    $rabbitUser = $rabbitValues['RABBITMQ_USER']
    $rabbitPassword = $rabbitValues['RABBITMQ_PASSWORD']
} else {
    $rabbitUser = 'parking'
    $rabbitPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    [IO.File]::WriteAllText($rabbitEnvPath, "RABBITMQ_USER=$rabbitUser`nRABBITMQ_PASSWORD=$rabbitPassword`n")
}
$rabbitConfig = if (Test-Path -LiteralPath $rabbitConfigPath) { Get-Content -LiteralPath $rabbitConfigPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
if (!$rabbitConfig.ContainsKey('EventBus')) { $rabbitConfig['EventBus'] = @{} }
$rabbitConfig['EventBus']['Enabled'] = $true
$rabbitConfig['EventBus']['RabbitMq'] = @{
    HostName = 'localhost'; Port = 5672; VirtualHost = 'parking'; UserName = $rabbitUser
    Password = $rabbitPassword; Exchange = 'parking.events'; UseTls = $false; OperationTimeoutSeconds = 10
}
# Event publishing uses AMQP now. OTP transport settings remain intact.
$rabbitConfig['EventBus'].Remove('PublishUrl') | Out-Null
$rabbitConfig['EventBus'].Remove('ApiKey') | Out-Null
[IO.File]::WriteAllText($rabbitConfigPath, ($rabbitConfig | ConvertTo-Json -Depth 20))
$rabbitDefinitions = Get-Content -LiteralPath (Join-Path $rabbitRoot 'infrastructure/rabbitmq/definitions.json') -Raw | ConvertFrom-Json -AsHashtable
# Boot import skips RabbitMQ's default user creation. Include a salted hash, never plaintext,
# in an ignored generated file so the first startup has a vhost, user and permissions.
$rabbitSalt = [Security.Cryptography.RandomNumberGenerator]::GetBytes(4)
$rabbitPasswordBytes = [Text.Encoding]::UTF8.GetBytes($rabbitPassword)
[byte[]]$rabbitHashInput = $rabbitSalt + $rabbitPasswordBytes
$rabbitDigest = [Security.Cryptography.SHA256]::HashData($rabbitHashInput)
[byte[]]$rabbitSaltedHash = $rabbitSalt + $rabbitDigest
$rabbitDefinitions['vhosts'] = @(@{ name = 'parking' })
$rabbitDefinitions['users'] = @(@{
    name = $rabbitUser; password_hash = [Convert]::ToBase64String($rabbitSaltedHash)
    hashing_algorithm = 'rabbit_password_hashing_sha256'; tags = @('administrator')
})
$rabbitDefinitions['permissions'] = @(@{ user = $rabbitUser; vhost = 'parking'; configure = '.*'; write = '.*'; read = '.*' })
[IO.File]::WriteAllText((Join-Path $rabbitRoot 'infrastructure/rabbitmq/definitions.local.json'), ($rabbitDefinitions | ConvertTo-Json -Depth 20))
Write-Host 'Đã lưu cấu hình RabbitMQ vào file local (không in mật khẩu).'
Write-Host 'Khởi động broker: docker compose --env-file .env.rabbitmq.local -f compose.rabbitmq.yml up -d --wait'
Write-Host 'Sau đó khởi động lại UserService. Management UI: http://localhost:15672'
