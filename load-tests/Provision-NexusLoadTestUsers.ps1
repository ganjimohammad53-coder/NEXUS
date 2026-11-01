param(
    [string]$BaseUrl = 'http://localhost:8080',
    [int]$Count = 1000,
    [string]$EmailDomain = 'example.invalid',
    [string]$OutputPath = '.\load-tests\test-tokens.json'
)

$ErrorActionPreference = 'Stop'
if ($Count -lt 1 -or $Count -gt 1000) { throw 'Count must be between 1 and 1000.' }
Write-Host "Creating $Count disposable load-test accounts on $BaseUrl" -ForegroundColor Cyan
Write-Host 'Use only against a disposable test deployment/database. The accounts are not automatically deleted.' -ForegroundColor Yellow

$tokens = [System.Collections.Generic.List[string]]::new()
$prefix = 'nexus-load-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
for ($i = 1; $i -le $Count; $i++) {
    $email = "$prefix-$i@$EmailDomain"
    $password = ([guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')).Substring(0, 24) + 'A9!'
    $body = @{ email = $email; password = $password } | ConvertTo-Json -Compress
    try {
        $result = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/register" -ContentType 'application/json' -Body $body -TimeoutSec 20
        if ([string]::IsNullOrWhiteSpace($result.accessToken)) { throw 'API response did not contain accessToken.' }
        $tokens.Add([string]$result.accessToken)
    } catch {
        throw "Failed after creating $($i - 1) of $Count accounts. Check NEXUS_AUTH_RATE_PER_MINUTE and server logs. Details: $($_.Exception.Message)"
    }
    if ($i % 100 -eq 0 -or $i -eq $Count) { Write-Progress -Activity 'Provisioning load-test users' -Status "$i / $Count" -PercentComplete (($i / $Count) * 100) }
    Start-Sleep -Milliseconds 120
}

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
$parent = Split-Path -Parent $fullPath
if ($parent -and -not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
$tokens | ConvertTo-Json -Depth 3 | Set-Content -Path $fullPath -Encoding utf8
Write-Host "Saved $($tokens.Count) test tokens to $fullPath" -ForegroundColor Green
Write-Host 'Treat this file as a secret. Do not commit it. Use it only for the auth/sync load test.' -ForegroundColor Yellow
