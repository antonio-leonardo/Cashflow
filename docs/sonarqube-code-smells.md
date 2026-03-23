# SonarQube - Code Smells

Este documento descreve o setup local via Docker e a execucao em nuvem via GitHub Actions.

## 1. Subir SonarQube local

Arquivo de stack:
- `docker-compose.sonarqube.yml`

Comando:

```powershell
docker compose -f docker-compose.sonarqube.yml up -d
```

URLs locais:
- SonarQube: `http://localhost:9000`

Credenciais default (primeiro acesso):
- usuario: `admin`
- senha: `admin`

Observacao:
- No primeiro login o SonarQube pede troca da senha.
- Em seguida, gere um token de usuario em `My Account -> Security`.

## 2. Executar analise da solution localmente

Script:
- `scripts/sonarqube-local.ps1`

Exemplo completo:

```powershell
$env:SONAR_TOKEN = "SEU_TOKEN"
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -ProjectKey "cashflow" `
  -ProjectName "Cashflow"
```

Modos uteis:

```powershell
# sobe apenas a instancia
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 -StartOnly

# executa analise sem subir docker (usa servidor remoto ja existente)
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -SkipDocker `
  -SonarHostUrl "https://seu-sonar.exemplo" `
  -SonarToken "SEU_TOKEN"

# executa apenas build + analise estatica (sem testes)
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -SkipTests `
  -SonarToken "SEU_TOKEN"

# para e remove volumes da stack local do SonarQube
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 -Stop
```

## 3. Integracao GitHub Actions (nuvem)

Workflow dedicado:
- `.github/workflows/sonarqube-analysis.yml`

Secrets obrigatorios no repositorio:
- `SONAR_TOKEN`
- `SONAR_ORGANIZATION` (exemplo: `antonio-leonardo`)

Variaveis opcionais (Repository Variables):
- `SONAR_HOST_URL` (exemplo SonarCloud: `https://sonarcloud.io`)
- `SONAR_PROJECT_KEY` (default: `cashflow`)
- `SONAR_PROJECT_NAME` (default: `Cashflow`)

Comportamento:
- roda em `push`/`pull_request` para `main` e em `workflow_dispatch`;
- provisiona .NET, Java 17 e Node 20 no runner;
- executa `begin -> restore -> build -> test -> end` usando `dotnet-sonarscanner`;
- aguarda o `Quality Gate`.

Checklist no GitHub (uma vez por repositorio):
- `Settings -> Secrets and variables -> Actions -> New repository secret`:
  - `SONAR_TOKEN`
  - `SONAR_ORGANIZATION`
- `Settings -> Secrets and variables -> Actions -> Variables` (opcional):
  - `SONAR_HOST_URL`
  - `SONAR_PROJECT_KEY`
  - `SONAR_PROJECT_NAME`
- `Settings -> Branches -> Branch protection rules (main)`:
  - habilitar status check obrigatorio;
  - selecionar o check `Code Smells And Quality Gate`.

## 4. Boas praticas recomendadas

- Manter servidor SonarQube fora do runner (instancia dedicada).
- Habilitar backup do banco do SonarQube (PostgreSQL).
- Definir Quality Gate minimo para smell/vulnerabilidade/bug.
- Versionar a chave de projeto (`ProjectKey`) por repositorio.
