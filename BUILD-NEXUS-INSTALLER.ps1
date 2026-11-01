$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Step([string]$Label, [string[]]$Arguments) {
    Write-Host "`n== $Label ==" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Label failed (exit code $LASTEXITCODE). No installer should be distributed." }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK was not found. Install .NET 8 SDK, reopen PowerShell, and run this script again.'
}

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles (x86)\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
    )
    $isccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($isccPath) { $iscc = Get-Item $isccPath }
}
if (-not $iscc) {
    throw 'Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 or 7 from https://jrsoftware.org/isinfo.php, then run this script again.'
}

Step 'Restore' @('restore', '.\NEXUS.sln')
Step 'Release build' @('build', '.\NEXUS.sln', '-c', 'Release', '--no-restore')
Step 'Automated tests' @('test', '.\NEXUS.sln', '-c', 'Release', '--no-build')
Step 'Publish self-contained Windows x64 application' @('publish', '.\src\Nexus.App\Nexus.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-o', '.\artifacts\win-x64')

$publishDir = Join-Path $root 'artifacts\win-x64'
if (-not (Test-Path (Join-Path $publishDir 'NEXUS.exe'))) { throw "NEXUS.exe was not produced at $publishDir" }
New-Item -ItemType Directory -Force -Path (Join-Path $root 'artifacts\installer') | Out-Null
Write-Host "`n== Build Windows installer ==" -ForegroundColor Cyan
& $iscc (Join-Path $root 'installer\NEXUS.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

$setup = Join-Path $root 'artifacts\installer\NEXUS-1.0-Setup-x64.exe'
if (-not (Test-Path $setup)) { throw "Installer compiler returned success but output was not found: $setup" }
Write-Host "`nSUCCESS: NEXUS installer created at:`n$setup" -ForegroundColor Green
Write-Host 'Before public release, manually test install, launch, desktop shortcut, upgrade, and uninstall on a clean Windows account.' -ForegroundColor Yellow

