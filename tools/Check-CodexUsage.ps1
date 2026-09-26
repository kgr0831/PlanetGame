param(
    [ValidateRange(1, 100)]
    [double]$Threshold = 50,
    [ValidateRange(1, 1440)]
    [int]$MaxAgeMinutes = 30
)

$ErrorActionPreference = 'Stop'

try {
    $response = & orca account list --json 2>$null | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $response.ok) {
        throw 'Orca response error'
    }

    $usage = $response.result.rateLimits.codex
    if ($null -eq $usage -or $usage.status -ne 'ok') {
        throw 'Codex usage unavailable'
    }

    $ageMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() - [long]$usage.updatedAt
    if ($ageMs -lt 0 -or $ageMs -gt ($MaxAgeMinutes * 60 * 1000)) {
        throw 'Codex usage data is stale'
    }

    $windows = @()
    foreach ($name in @('session', 'weekly')) {
        $window = $usage.$name
        if ($null -ne $window -and $null -ne $window.usedPercent) {
            $percent = [double]$window.usedPercent
            if ($percent -lt 0 -or $percent -gt 100) {
                throw 'Codex usage percent out of range'
            }
            $windows += [pscustomobject]@{ name = $name; usedPercent = $percent }
        }
    }

    if ($windows.Count -eq 0) {
        throw 'Codex usage windows unavailable'
    }

    $maxUsedPercent = [double](($windows | Sort-Object usedPercent -Descending | Select-Object -First 1).usedPercent)
    $allowed = $maxUsedPercent -lt $Threshold
    [pscustomobject]@{
        status = $(if ($allowed) { 'allowed' } else { 'skipped' })
        maxUsedPercent = $maxUsedPercent
        threshold = $Threshold
        windows = $windows
    } | ConvertTo-Json -Compress -Depth 4

    if ($allowed) { exit 0 }
    exit 10
}
catch {
    [pscustomobject]@{
        status = 'unavailable'
        threshold = $Threshold
        reason = 'Codex usage could not be verified safely'
    } | ConvertTo-Json -Compress
    exit 20
}
