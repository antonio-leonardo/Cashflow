param(
    [int]$Checks = 10,
    [switch]$KeepStack
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$composeArgs = @(
    "-f", "docker-compose.yml",
    "-f", "docker-compose.ha.yml"
)

try {
    docker compose @composeArgs up -d --scale transaction-api=2 --scale balance-query-api=2 `
        gateway transaction-api balance-query-api postgres rabbitmq redis keycloak | Out-Host

    Start-Sleep -Seconds 8

    $transactionContainers = docker ps --format "{{.Names}}" |
        Where-Object { $_ -like "cashflow-transaction-api-*" }

    if (-not $transactionContainers -or $transactionContainers.Count -lt 2) {
        throw "Nao foi possivel identificar duas replicas de transaction-api."
    }

    $containerToStop = $transactionContainers[0]
    Write-Host "Parando replica para teste de failover: $containerToStop"
    docker stop $containerToStop | Out-Host

    $okCount = 0

    for ($i = 1; $i -le $Checks; $i++) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5000/health/ready" -TimeoutSec 5 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                $okCount++
                Write-Host "[$i/$Checks] Gateway ready=200"
            }
            else {
                Write-Host "[$i/$Checks] Gateway status=$($response.StatusCode)"
            }
        }
        catch {
            Write-Host "[$i/$Checks] Falha ao consultar /health/ready: $($_.Exception.Message)"
        }

        Start-Sleep -Seconds 1
    }

    Write-Host "Resultado: $okCount/$Checks verificacoes com status 200."

    Write-Host "Reiniciando replica parada..."
    docker start $containerToStop | Out-Host
}
finally {
    if (-not $KeepStack) {
        docker compose @composeArgs down --remove-orphans | Out-Host
    }
}
