<#
.SYNOPSIS
    Uninstalls the OnLocation-Gallagher Bridge Windows service.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "OnLocation-Gallagher-Bridge",
    [string]$InstallDir = "C:\\Program Files\\OnLocation-Gallagher-Bridge"
)

$ErrorActionPreference = "Stop"

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "Removing service..."
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}
else {
    Write-Host "Service not found."
}

if (Test-Path $InstallDir) {
    Write-Host "Removing install directory..."
    Remove-Item -Path $InstallDir -Recurse -Force
}

Get-NetFirewallRule -DisplayName "OnLocation-Gallagher-Bridge*" -ErrorAction SilentlyContinue | Remove-NetFirewallRule

Write-Host "Done."
