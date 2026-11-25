# Builds every Revit configuration (R2022, R2024, R2025) and then packages the combined InstallerWIX MSI.

$sln = "$PSScriptRoot\RevitNumbering.sln"
$installerProj = "$PSScriptRoot\InstallerWIX\InstallerWIX.wixproj"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = $null
if (Test-Path $vswhere) {
    $vsPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    if ($vsPath) { $msbuild = $vsPath }
}
if (-not $msbuild) {
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { $msbuild = $cmd.Source }
}
if (-not $msbuild) {
    Write-Host "MSBuild.exe not found (checked vswhere and PATH). Run this script from a Developer PowerShell for VS." -ForegroundColor Red
    exit 1
}

# Each configuration is restored and built by its own MSBuild run: the Revit API package version
# depends on the configuration, and one run keeps the first restored version for all of them.
#   R2022, R2024 -> net48
#   R2025        -> net8.0-windows
$configs = @("R2022", "R2024", "R2025")

$allOk = $true
foreach ($cfg in $configs) {
    Write-Host "=== Building $cfg (solution) ===" -ForegroundColor Cyan
    & $msbuild $sln /restore /t:Build /p:Configuration=$cfg /p:Platform=x64 /v:minimal /nologo
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: $cfg" -ForegroundColor Green
    } else {
        Write-Host "FAIL: $cfg" -ForegroundColor Red
        $allOk = $false
    }
}

if (-not $allOk) {
    Write-Host "=== Skipping InstallerWIX: one or more code builds failed ===" -ForegroundColor Red
    exit 1
}

Write-Host "=== Building InstallerWIX ===" -ForegroundColor Cyan
& $msbuild $installerProj /t:Build /p:Configuration=Release /p:Platform=x86 /v:minimal /nologo
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK: InstallerWIX" -ForegroundColor Green
    $msi = Get-ChildItem "$PSScriptRoot\InstallerWIX\bin\Release\*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($msi) {
        Write-Host "=== Done. MSI: $($msi.FullName) ===" -ForegroundColor Green
    } else {
        Write-Host "=== Done, but no MSI file was found in InstallerWIX\bin\Release ===" -ForegroundColor Yellow
    }
} else {
    Write-Host "FAIL: InstallerWIX" -ForegroundColor Red
    exit 1
}
