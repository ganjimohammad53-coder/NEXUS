$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install .NET SDK 8.0.426 (or a compatible .NET 8 SDK), reopen PowerShell, and run this script again.'
}
function Invoke-DotNetStep([string]$Label, [string[]]$Arguments) {
    Write-Host "`n== $Label ==" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE. Fix this step before continuing."
    }
}
Invoke-DotNetStep 'Restore packages' @('restore', '.\NEXUS.sln')
Invoke-DotNetStep 'Build solution' @('build', '.\NEXUS.sln', '-c', 'Debug', '--no-restore')
Invoke-DotNetStep 'Run tests' @('test', '.\NEXUS.sln', '-c', 'Debug', '--no-build')
Invoke-DotNetStep 'Launch NEXUS 1.0' @('run', '--project', '.\src\Nexus.App\Nexus.App.csproj')
