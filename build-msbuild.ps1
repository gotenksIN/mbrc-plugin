# Build script using MSBuild directly (similar to build.bat)
param(
    [string]$Configuration = "Release"
)

# Find MSBuild path
$msbuildPaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\MSBuild\Current\bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\bin\MSBuild.exe"
)

$msbuild = $null
foreach ($path in $msbuildPaths) {
    if (Test-Path $path) {
        $msbuild = $path
        break
    }
}

if (-not $msbuild) {
    Write-Error "MSBuild not found. Please install Visual Studio 2022."
    exit 1
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Green

# Clean previous output
if (Test-Path ".\build") {
    Write-Host "Cleaning previous build output..." -ForegroundColor Yellow
    Remove-Item -Path ".\build" -Recurse -Force
}

# Restore packages
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Yellow
& $msbuild MBRC.sln /t:Restore /p:Configuration="$Configuration" /v:M

if ($LASTEXITCODE -ne 0) {
    Write-Error "Package restore failed"
    exit $LASTEXITCODE
}

# Build solution
Write-Host "`nBuilding solution..." -ForegroundColor Yellow
& $msbuild MBRC.sln /p:Configuration="$Configuration" /p:Platform="Any CPU" /m /v:M /fl /flp:LogFile=msbuild.log`;Verbosity=Normal /nr:false

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Check msbuild.log for details."
    exit $LASTEXITCODE
}

Write-Host "`nBuild completed successfully!" -ForegroundColor Green