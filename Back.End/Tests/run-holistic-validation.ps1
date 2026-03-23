param(
    [switch]$Quick
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

# Ensure dotnet CLI writes only inside workspace (portable, CI-friendly, sandbox-safe).
$dotnetCliHome = $root
$dotnetRoot = Join-Path $root ".dotnet"
$dotnetTools = Join-Path $dotnetRoot "tools"
$nugetRoot = Join-Path $root ".nuget"
$nugetPackages = Join-Path $nugetRoot "packages"
$nugetHttpCache = Join-Path $nugetRoot "http-cache"
$nugetConfigFile = Join-Path $nugetRoot "NuGet.Config"
$appDataRoot = Join-Path $root ".appdata"
$roamingAppData = Join-Path $appDataRoot "Roaming"
$localAppData = Join-Path $appDataRoot "Local"
$roamingNuGet = Join-Path $roamingAppData "NuGet"

foreach ($path in @($dotnetRoot, $dotnetTools, $nugetRoot, $nugetPackages, $nugetHttpCache, $appDataRoot, $roamingAppData, $localAppData, $roamingNuGet)) {
    if (-not (Test-Path $path)) {
        New-Item -Path $path -ItemType Directory | Out-Null
    }
}

if (-not (Test-Path $nugetConfigFile)) {
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@ | Set-Content -Path $nugetConfigFile -Encoding UTF8
}

$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:NUGET_PACKAGES = $nugetPackages
$env:NUGET_HTTP_CACHE_PATH = $nugetHttpCache
$env:NUGET_CONFIG_FILE = $nugetConfigFile
$env:APPDATA = $roamingAppData
$env:LOCALAPPDATA = $localAppData
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Copy-Item -Path $nugetConfigFile -Destination (Join-Path $roamingNuGet "NuGet.Config") -Force

if ($env:PATH -notlike "*$dotnetTools*") {
    $env:PATH = "$dotnetTools;$env:PATH"
}

$summaryPath = Join-Path $root "TestResults\holistic-validation-summary.json"
$summaryDirectory = Split-Path $summaryPath -Parent

if (-not (Test-Path $summaryDirectory)) {
    New-Item -Path $summaryDirectory -ItemType Directory | Out-Null
}

$results = @()
$hasFailure = $false

function Invoke-TestSuite {
    param(
        [string]$Name,
        [string]$ProjectPath,
        [string]$Filter = ""
    )

    Write-Host "=== Running: $Name ===" -ForegroundColor Cyan
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    $arguments = @("test", $ProjectPath, "-v", "minimal")
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @("--filter", $Filter)
    }

    & dotnet @arguments
    $exitCode = $LASTEXITCODE

    $stopwatch.Stop()

    $result = [PSCustomObject]@{
        Name = $Name
        Project = $ProjectPath
        Filter = $Filter
        ExitCode = $exitCode
        Passed = ($exitCode -eq 0)
        DurationSeconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
        ExecutedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }

    $script:results += $result

    if ($exitCode -ne 0) {
        $script:hasFailure = $true
        Write-Host "FAILED: $Name" -ForegroundColor Red
    }
    else {
        Write-Host "PASSED: $Name" -ForegroundColor Green
    }
}

$suites = @(
    @{
        Name = "Contract Gateway"
        Project = "Back.End/Tests/ContractTests/Gateway/Gateway.Transaction.ContractTests.csproj"
        Filter = ""
    },
    @{
        Name = "Messaging Robustness (FanOut + DLQ)"
        Project = "Back.End/Tests/IntegrationTests/Messaging/Messaging.Integration.Tests.csproj"
        Filter = ""
    },
    @{
        Name = "Performance NFR Deep"
        Project = "Back.End/Tests/Performance/k6/K6.Performance.Tests.csproj"
        Filter = ""
    },
    @{
        Name = "Balance Service Independence"
        Project = "Back.End/Tests/E2E/Balance/E2E.Balance.Tests.csproj"
        Filter = "FullyQualifiedName~ServiceIndependenceE2ETests"
    },
    @{
        Name = "Audit Service Independence"
        Project = "Back.End/Tests/E2E/Audit/E2E.Audit.Test.csproj"
        Filter = "FullyQualifiedName~ServiceIndependenceE2ETests"
    },
    @{
        Name = "Report Service Independence"
        Project = "Back.End/Tests/E2E/Report/E2E.Report.Test.csproj"
        Filter = "FullyQualifiedName~ServiceIndependenceE2ETests"
    },
    @{
        Name = "Holistic Authenticated Flow"
        Project = "Back.End/Tests/IntegrationTests/Holistic/Holistic.Integration.Tests.csproj"
        Filter = "FullyQualifiedName~Should_Allow_Authenticated_Requests"
    }
)

if ($Quick) {
    $suites = @(
        $suites[1], # Messaging Robustness
        $suites[2], # Performance NFR Deep
        $suites[6]  # Holistic Authenticated Flow
    )
}

foreach ($suite in $suites) {
    Invoke-TestSuite -Name $suite.Name -ProjectPath $suite.Project -Filter $suite.Filter
}

$summary = [PSCustomObject]@{
    QuickMode = [bool]$Quick
    Success = (-not $hasFailure)
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    Results = $results
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host ""
Write-Host "Summary written to: $summaryPath" -ForegroundColor Yellow
$results | Format-Table Name, Passed, ExitCode, DurationSeconds -AutoSize

if ($hasFailure) {
    exit 1
}

exit 0
