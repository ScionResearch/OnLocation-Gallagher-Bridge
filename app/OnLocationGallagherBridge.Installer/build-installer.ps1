param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path (Join-Path $root "..") "OnLocationGallagherBridge\OnLocationGallagherBridge.csproj"
$trayProject = Join-Path (Join-Path $root "..") "OnLocationGallagherBridge.Tray\OnLocationGallagherBridge.Tray.csproj"

if ([string]::IsNullOrEmpty($Version)) {
    $projectVersion = Select-String -Path $appProject -Pattern '<Version>([^<]+)</Version>' | ForEach-Object { $_.Matches.Groups[1].Value }
    if ([string]::IsNullOrEmpty($projectVersion)) {
        $Version = "1.0.0.0"
    }
    else {
        $Version = switch ($projectVersion.Split('.').Count) {
            2 { "$projectVersion.0.0" }
            3 { "$projectVersion.0" }
            4 { $projectVersion }
            default { "1.0.0.0" }
        }
    }
}

$publishDir = Join-Path $root "publishForMsi"
$trayPublishDir = Join-Path $root "publishForMsiTray"
$msiOutput = Join-Path $root "OnLocationGallagherBridge.Installer.msi"

Write-Host "Publishing application..." -ForegroundColor Cyan
& dotnet publish $appProject -c $Configuration -r $Runtime -o $publishDir /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Publishing tray app..." -ForegroundColor Cyan
& dotnet publish $trayProject -c $Configuration -r $Runtime -o $trayPublishDir /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish tray failed" }

Write-Host "Building MSI..." -ForegroundColor Cyan
& wix build -acceptEula wix7 (Join-Path $root "Package.wxs") `
    -define "PublishDir=$publishDir" `
    -define "TrayPublishDir=$trayPublishDir" `
    -define "ProductVersion=$Version" `
    -ext WixToolset.Util.wixext `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Firewall.wixext `
    -out $msiOutput
if ($LASTEXITCODE -ne 0) { throw "wix build failed" }

Write-Host "MSI created: $msiOutput" -ForegroundColor Green
