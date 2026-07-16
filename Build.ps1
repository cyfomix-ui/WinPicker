param(
    [string]$ProjectPath = ".\WinPicker\WinPicker.csproj",
    [switch]$NoRun
)

$ErrorActionPreference = "Stop"

Write-Host "=== WinPicker Build / Publish helper ===" -ForegroundColor Cyan

Write-Host "Stopping running WinPicker.exe..." -ForegroundColor Yellow
Get-Process WinPicker -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Checking XML version..." -ForegroundColor Yellow
$versionXml = Get-ChildItem -Path . -Recurse -Filter VersionInfo.xml | Select-Object -First 1
if (-not $versionXml) {
    throw "VersionInfo.xml was not found."
}
Get-Content $versionXml.FullName

Write-Host "Checking source version binding..." -ForegroundColor Yellow
$uiText = Get-ChildItem -Path . -Recurse -Filter UiText.cs | Select-Object -First 1
if (-not $uiText) {
    throw "UiText.cs was not found."
}
Select-String -Path $uiText.FullName -Pattern 'VersionInfoService|TrayTooltip|AppTitleWithVersion' | ForEach-Object { $_.Line }

$tray = Get-ChildItem -Path . -Recurse -Filter TrayApplicationContext.cs | Select-Object -First 1
if (-not $tray) {
    throw "TrayApplicationContext.cs was not found."
}
Select-String -Path $tray.FullName -Pattern 'Text = UiText.TrayTooltip|ShowBalloonTip' | ForEach-Object { $_.Line }

Write-Host "Checking branding assets..." -ForegroundColor Yellow
foreach ($asset in @("App.ico", "AppIcon.png", "CyfomixAbout.png", "CyfomixHeader.png", "WinPickerSplash.png")) {
    $found = Get-ChildItem -Path . -Recurse -Filter $asset | Select-Object -First 1
    if (-not $found) { throw "$asset was not found." }
    Write-Host "  $($found.FullName)"
}

Write-Host "Cleaning old bin/obj..." -ForegroundColor Yellow
Get-ChildItem -Path . -Directory -Recurse -Include bin,obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Publishing win-x64 single EXE..." -ForegroundColor Yellow
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

$publishExe = Get-ChildItem -Path . -Recurse -Filter WinPicker.exe |
    Where-Object { $_.FullName -match 'publish' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $publishExe) {
    throw "Published WinPicker.exe was not found."
}

Write-Host ""
Write-Host "Published EXE:" -ForegroundColor Green
Write-Host $publishExe.FullName
Write-Host ""

if (-not $NoRun) {
    Write-Host "Starting WinPicker..." -ForegroundColor Cyan
    Start-Process $publishExe.FullName
} else {
    Write-Host "NoRun was specified. WinPicker was not started." -ForegroundColor Cyan
}
