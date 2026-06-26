param(
    [string]$ProjectPath = ".\WinPicker\WinPicker.csproj"
)

$ErrorActionPreference = "Stop"

Write-Host "=== WinPicker v0.33 publish helper ===" -ForegroundColor Cyan

Write-Host "Stopping running WinPicker.exe..." -ForegroundColor Yellow
Get-Process WinPicker -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Checking source version..." -ForegroundColor Yellow
$uiText = Get-ChildItem -Path . -Recurse -Filter UiText.cs | Select-Object -First 1
if (-not $uiText) {
    throw "UiText.cs was not found. Please run this script from the extracted WinPicker folder."
}
Select-String -Path $uiText.FullName -Pattern 'Version =>|AppTitleWithVersion' | ForEach-Object { $_.Line }

Write-Host "Checking Alt tap source..." -ForegroundColor Yellow
$tapFile = Get-ChildItem -Path . -Recurse -Filter ModifierChordMouseMover.cs | Select-Object -First 1
Select-String -Path $tapFile.FullName -Pattern 'AltTapDecisionWaitMilliseconds|Alt tap decision|Tap count' | ForEach-Object { $_.Line }

Write-Host "Cleaning old bin/obj..." -ForegroundColor Yellow
Get-ChildItem -Path . -Directory -Recurse -Include bin,obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Publishing win-x64 single EXE..." -ForegroundColor Yellow
dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

$publishExe = Get-ChildItem -Path . -Recurse -Filter WinPicker.exe | Where-Object { $_.FullName -match 'publish' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $publishExe) {
    throw "Published WinPicker.exe was not found."
}

Write-Host ""
Write-Host "Published EXE:" -ForegroundColor Green
Write-Host $publishExe.FullName
Write-Host ""
Write-Host "Run this EXE after confirming no old WinPicker remains in the tray." -ForegroundColor Cyan
