#requires -Version 5.1
param([string]$HostName='smtp.gmail.com',[int]$Port=587)
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot '../Services/UserService/UserService.API/appsettings.Local.json'
$config=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$address=Read-Host 'Email dung de gui OTP'
if ($address -notmatch '^[^\s@]+@[^\s@]+\.[^\s@]+$') { throw 'Email khong hop le.' }
$password=Read-Host 'Mat khau ung dung SMTP (an khi nhap)' -AsSecureString
$credential=[PSCredential]::new($address,$password)
if (-not $config.Otp) { $config | Add-Member -NotePropertyName Otp -NotePropertyValue ([PSCustomObject]@{}) -Force }
$config.Otp | Add-Member -NotePropertyName Transport -NotePropertyValue 'Smtp' -Force
$config.Otp | Add-Member -NotePropertyName Smtp -NotePropertyValue ([PSCustomObject]@{Host=$HostName;Port=$Port;FromAddress=$address;FromName='ParkingManagement';Username=$address;Password=$credential.GetNetworkCredential().Password.Replace(' ','')}) -Force
$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
Write-Host 'Da luu SMTP trong appsettings.Local.json (Git ignore). Khoi dong lai UserService de ap dung.'
