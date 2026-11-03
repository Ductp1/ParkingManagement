#requires -Version 7.2
param()
$ErrorActionPreference = 'Stop'
$rabbitTestRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rabbitTestConfig = Get-Content -LiteralPath (Join-Path $rabbitTestRoot 'Services/UserService/UserService.API/appsettings.Local.json') -Raw | ConvertFrom-Json
$rabbitTestSettings = $rabbitTestConfig.EventBus.RabbitMq
if (!$rabbitTestSettings -or !$rabbitTestSettings.Password) { throw 'Chạy setup-rabbitmq-local.ps1 trước.' }
$rabbitPreviousUri = $env:TV1_TEST_RABBITMQ
try {
    $rabbitScheme = if ($rabbitTestSettings.UseTls) { 'amqps' } else { 'amqp' }
    $rabbitEscapedUser = [Uri]::EscapeDataString($rabbitTestSettings.UserName)
    $rabbitEscapedPassword = [Uri]::EscapeDataString($rabbitTestSettings.Password)
    $rabbitEscapedVhost = [Uri]::EscapeDataString($rabbitTestSettings.VirtualHost)
    $env:TV1_TEST_RABBITMQ = "${rabbitScheme}://${rabbitEscapedUser}:${rabbitEscapedPassword}@$($rabbitTestSettings.HostName):$($rabbitTestSettings.Port)/${rabbitEscapedVhost}"
    Push-Location $rabbitTestRoot
    try {
        & dotnet test Services/UserService/UserService.Test/UserService.Test.csproj --filter 'FullyQualifiedName~RabbitMq' -m:1 -p:UseSharedCompilation=false -nodeReuse:false --verbosity minimal
        $rabbitTestExitCode = $LASTEXITCODE
    } finally { Pop-Location }
} finally { $env:TV1_TEST_RABBITMQ = $rabbitPreviousUri }
exit $rabbitTestExitCode
