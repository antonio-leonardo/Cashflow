param(
    [string]$ProjectKey = "cashflow",
    [string]$ProjectName = "Cashflow",
    [string]$Configuration = "Release",
    [string]$SonarHostUrl = $(if ([string]::IsNullOrWhiteSpace($env:SONAR_HOST_URL)) { "http://localhost:9000" } else { $env:SONAR_HOST_URL }),
    [string]$SonarToken = $env:SONAR_TOKEN,
    [int]$ReadyTimeoutSeconds = 300,
    [switch]$SkipDocker,
    [switch]$SkipTests,
    [switch]$StartOnly,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$composeFile = Join-Path $repoRoot "docker-compose.sonarqube.yml"
$toolPath = Join-Path $repoRoot ".dotnet/tools"
$scannerPath = Join-Path $toolPath "dotnet-sonarscanner.exe"

if (-not (Test-Path $composeFile))
{
    throw "Arquivo docker-compose.sonarqube.yml nao encontrado em '$composeFile'."
}

if (-not $env:DOTNET_CLI_HOME)
{
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet"
}

function Invoke-NativeCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$CommandLabel = $FilePath
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "Comando falhou com codigo de saida ${LASTEXITCODE}: $CommandLabel"
    }
}

function Invoke-Compose
{
    param([string[]]$Arguments)
    $composeArguments = @("compose", "-f", $composeFile) + $Arguments
    Invoke-NativeCommand -FilePath "docker" -Arguments $composeArguments -CommandLabel "docker compose -f $composeFile $($Arguments -join ' ')"
}

function Ensure-JavaRuntime
{
    $javaCommand = Get-Command "java" -ErrorAction SilentlyContinue
    if ($javaCommand)
    {
        return
    }

    $resolvedJavaHome = $env:JAVA_HOME
    if ([string]::IsNullOrWhiteSpace($resolvedJavaHome))
    {
        $resolvedJavaHome = [Environment]::GetEnvironmentVariable("JAVA_HOME", "User")
    }

    if ([string]::IsNullOrWhiteSpace($resolvedJavaHome))
    {
        $resolvedJavaHome = [Environment]::GetEnvironmentVariable("JAVA_HOME", "Machine")
    }

    if (-not [string]::IsNullOrWhiteSpace($resolvedJavaHome))
    {
        $javaExe = Join-Path $resolvedJavaHome "bin\java.exe"
        if (Test-Path $javaExe)
        {
            $env:JAVA_HOME = $resolvedJavaHome
            if (-not (($env:PATH -split ";") -contains (Join-Path $resolvedJavaHome "bin")))
            {
                $env:PATH = "$(Join-Path $resolvedJavaHome 'bin');$env:PATH"
            }

            return
        }

        throw "JAVA_HOME esta definido em '$resolvedJavaHome', mas o arquivo 'bin\\java.exe' nao foi encontrado."
    }

    throw "Java nao encontrado. Instale um JDK compativel com SonarScanner e configure JAVA_HOME ou inclua java.exe no PATH."
}

function Wait-SonarQubeReady
{
    param(
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline)
    {
        try
        {
            $statusResponse = Invoke-RestMethod -Method Get -Uri "$Url/api/system/status" -TimeoutSec 10
            if ($statusResponse.status -eq "UP")
            {
                return
            }

            Write-Host "Aguardando SonarQube ficar UP. Status atual: $($statusResponse.status)"
        }
        catch
        {
            Write-Host "Aguardando SonarQube responder em $Url ..."
        }

        Start-Sleep -Seconds 5
    }

    throw "SonarQube nao ficou pronto em $TimeoutSeconds segundos."
}

function Ensure-SonarScanner
{
    if (-not (Test-Path $toolPath))
    {
        New-Item -ItemType Directory -Path $toolPath | Out-Null
    }

    if (Test-Path $scannerPath)
    {
        Write-Host "Atualizando dotnet-sonarscanner..."
        Invoke-NativeCommand -FilePath "dotnet" -Arguments @("tool", "update", "dotnet-sonarscanner", "--tool-path", $toolPath) -CommandLabel "dotnet tool update dotnet-sonarscanner --tool-path $toolPath"
    }
    else
    {
        Write-Host "Instalando dotnet-sonarscanner..."
        Invoke-NativeCommand -FilePath "dotnet" -Arguments @("tool", "install", "dotnet-sonarscanner", "--tool-path", $toolPath) -CommandLabel "dotnet tool install dotnet-sonarscanner --tool-path $toolPath"
    }
}

if ($Stop)
{
    Write-Host "Parando stack SonarQube..."
    Invoke-Compose -Arguments @("down", "-v")
    exit 0
}

if (-not $SkipDocker)
{
    Write-Host "Subindo SonarQube via Docker..."
    Invoke-Compose -Arguments @("up", "-d")
}

Wait-SonarQubeReady -Url $SonarHostUrl -TimeoutSeconds $ReadyTimeoutSeconds
Write-Host "SonarQube pronto em $SonarHostUrl"

if ($StartOnly)
{
    Write-Host "Modo StartOnly: instancia pronta e sem analise."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($SonarToken))
{
    throw "SONAR_TOKEN nao informado. Defina variavel de ambiente SONAR_TOKEN ou passe -SonarToken."
}

Push-Location $repoRoot

$analysisStarted = $false
try
{
    Ensure-SonarScanner
    Ensure-JavaRuntime

    Write-Host "Iniciando analise SonarQube..."
    Invoke-NativeCommand -FilePath $scannerPath -Arguments @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectName",
        "/d:sonar.host.url=$SonarHostUrl",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.qualitygate.wait=true",
        "/d:sonar.exclusions=**/bin/**,**/obj/**,**/*.g.cs,**/*.designer.cs",
        "/d:sonar.cs.vstest.reportsPaths=**/TestResults/*.trx"
    ) -CommandLabel "dotnet-sonarscanner begin"

    $analysisStarted = $true

    Invoke-NativeCommand -FilePath "dotnet" -Arguments @("restore", "Cashflow.slnx") -CommandLabel "dotnet restore Cashflow.slnx"
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @("build", "Cashflow.slnx", "-c", $Configuration, "--no-restore") -CommandLabel "dotnet build Cashflow.slnx -c $Configuration --no-restore"

    if (-not $SkipTests)
    {
        Invoke-NativeCommand -FilePath "dotnet" -Arguments @("test", "Cashflow.slnx", "-c", $Configuration, "--no-build", "-m:1", "--logger", "trx;LogFileName=sonar-tests.trx") -CommandLabel "dotnet test Cashflow.slnx -c $Configuration --no-build -m:1 --logger trx;LogFileName=sonar-tests.trx"
    }
}
finally
{
    if ($analysisStarted)
    {
        Invoke-NativeCommand -FilePath $scannerPath -Arguments @("end", "/d:sonar.token=$SonarToken") -CommandLabel "dotnet-sonarscanner end"
    }

    Pop-Location
}

Write-Host "Analise SonarQube finalizada com sucesso."
