<#
.SYNOPSIS
    Installs the OnLocation-Gallagher Bridge as a Windows service.
.DESCRIPTION
    Copies the self-contained publish output to Program Files, sets ACLs,
    and registers the service to run under NT AUTHORITY\NetworkService.
#>
[CmdletBinding()]
param(
    [string]$ServiceName = "OnLocation-Gallagher-Bridge",
    [string]$DisplayName = "OnLocation-Gallagher Bridge",
    [string]$PublishDir = ".\OnLocationGallagherBridge\bin\Release\net9.0\win-x64\publish",
    [string]$InstallDir = "C:\\Program Files\\OnLocation-Gallagher-Bridge",
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory not found: $PublishDir. Run 'dotnet publish' first."
}

$exe = Join-Path $InstallDir "OnLocationGallagherBridge.exe"
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}
Write-Host "Copying files to $InstallDir ..."
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path "$PublishDir\*" -Destination $InstallDir -Recurse -Force

Write-Host "Setting ACLs..."
$acl = Get-Acl $InstallDir
$admin = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-32-544")
$networkService = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-20")
$acl.SetAccessRuleProtection($true, $false)
foreach ($sid in @($admin, $networkService)) {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($sid, "Modify,ReadAndExecute,ListDirectory,Read,Write", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.AddAccessRule($rule)
}
Set-Acl $InstallDir $acl

Write-Host "Registering service..."
New-Service -Name $ServiceName -DisplayName $DisplayName -BinaryPathName "`"$exe`"" -StartupType Automatic -Credential (New-Object System.Management.Automation.PSCredential("NT AUTHORITY\NETWORK SERVICE", (New-Object System.Security.SecureString))) | Out-Null

Write-Host "Creating data directories..."
$dataDir = Join-Path $env:ProgramData "OnLocation-Gallagher-Bridge"
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
$aclData = Get-Acl $dataDir
foreach ($sid in @($admin, $networkService)) {
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($sid, "Modify,ReadAndExecute,ListDirectory,Read,Write", "ContainerInherit,ObjectInherit", "None", "Allow")
    $aclData.AddAccessRule($rule)
}
Set-Acl $dataDir $aclData

Write-Host "Opening firewall port $Port ..."
$ruleName = "OnLocation-Gallagher-Bridge ($Port)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $existing) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow | Out-Null
}

Write-Host "Starting service..."
Start-Service -Name $ServiceName
Write-Host "Done. $DisplayName installed."
