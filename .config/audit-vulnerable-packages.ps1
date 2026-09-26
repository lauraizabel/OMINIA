$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projects = Get-ChildItem `
    (Join-Path $repositoryRoot 'src/backend'), `
    (Join-Path $repositoryRoot 'tests/backend') `
    -Filter '*.csproj' `
    -Recurse
$findings = @()

foreach ($projectFile in $projects) {
    $json = & dotnet list $projectFile.FullName package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        throw "Dependency audit failed for $($projectFile.FullName)."
    }

    $report = $json | ConvertFrom-Json
    foreach ($project in @($report.projects)) {
        foreach ($framework in @($project.frameworks)) {
            $packages = @($framework.topLevelPackages) + @($framework.transitivePackages)
            foreach ($package in $packages) {
                if ($null -ne $package -and $null -ne $package.vulnerabilities) {
                    foreach ($vulnerability in @($package.vulnerabilities)) {
                        $findings += "$($project.path): $($package.id) $($package.resolvedVersion) [$($vulnerability.severity)] $($vulnerability.advisoryurl)"
                    }
                }
            }
        }
    }
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Output $_ }
    throw 'Vulnerable NuGet packages were found.'
}

Write-Host "No vulnerable NuGet packages found across $($projects.Count) projects."
