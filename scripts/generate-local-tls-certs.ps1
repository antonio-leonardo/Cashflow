param(
    [string]$Password = "changeit"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$certsRoot = Join-Path $root ".appdata/certs"

$services = @(
    @{ Name = "gateway"; File = "gateway.pfx" },
    @{ Name = "transaction-api"; File = "transaction-api.pfx" },
    @{ Name = "balance-query-api"; File = "balance-query-api.pfx" }
)

foreach ($service in $services) {
    $folder = Join-Path $certsRoot $service.Name
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder | Out-Null
    }

    $outputPath = Join-Path $folder $service.File
    Write-Host "Gerando certificado local para $($service.Name): $outputPath"

    dotnet dev-certs https -ep $outputPath -p $Password | Out-Host
}

Write-Host "Certificados gerados em: $certsRoot"
