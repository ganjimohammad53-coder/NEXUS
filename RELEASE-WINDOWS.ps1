$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK is required. Install a supported .NET 8 SDK and reopen PowerShell.'
}
function Invoke-DotNetStep([string]$Label, [string[]]$Arguments) {
    Write-Host "`n== $Label ==" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Step '$Label' failed with exit code $LASTEXITCODE. Release artifacts were not accepted." }
}
Invoke-DotNetStep 'Restore' @('restore', '.\NEXUS.sln')
Invoke-DotNetStep 'Release build' @('build', '.\NEXUS.sln', '-c', 'Release', '--no-restore')
Invoke-DotNetStep 'Tests' @('test', '.\NEXUS.sln', '-c', 'Release', '--no-build')
Invoke-DotNetStep 'Publish Windows x64' @('publish', '.\src\Nexus.App\Nexus.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-o', '.\artifacts\win-x64')

$publishDir = Join-Path $root 'artifacts\win-x64'
if (-not (Test-Path (Join-Path $publishDir 'NEXUS.exe'))) { throw "Publish output does not contain NEXUS.exe: $publishDir" }
$archive = Join-Path $root 'artifacts\NEXUS-1.0-win-x64-portable.zip'
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $archive -Force
Write-Host "`nPortable package created: $archive" -ForegroundColor Green
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($iscc) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root 'artifacts\installer') | Out-Null
    & $iscc.Source (Join-Path $root 'installer\NEXUS.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
} else { Write-Warning 'Inno Setup compiler (ISCC.exe) was not found. Portable ZIP was created; installer was skipped.' }
Write-Host 'Packaging complete. Complete the manual Windows smoke-test checklist before public distribution.' -ForegroundColor Yellow
