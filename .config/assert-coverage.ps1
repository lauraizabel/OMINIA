param(
    [Parameter(Mandatory = $true)]
    [string]$SummaryPath,

    [Parameter(Mandatory = $true)]
    [string]$Label,

    [ValidateRange(0, 100)]
    [double]$Minimum = 90
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SummaryPath -PathType Leaf)) {
    throw "$Label coverage summary was not found at '$SummaryPath'."
}

$report = Get-Content -LiteralPath $SummaryPath -Raw | ConvertFrom-Json
$lineCoverage = [double]$report.summary.linecoverage
$branchCoverage = [double]$report.summary.branchcoverage

Write-Host "$Label coverage: $lineCoverage% lines, $branchCoverage% branches (minimum $Minimum%)."

$failures = @()
if ($lineCoverage -lt $Minimum) {
    $failures += "lines are $lineCoverage%"
}
if ($branchCoverage -lt $Minimum) {
    $failures += "branches are $branchCoverage%"
}

if ($failures.Count -gt 0) {
    throw "$Label coverage gate failed: $($failures -join '; '); required minimum is $Minimum%."
}
