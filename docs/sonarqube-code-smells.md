# SonarQube - Code Smells

Este documento descreve o setup local via Docker e a Execução em nuvem via GitHub Actions.

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
- usuário: `admin`
- senha: `admin`

Observação:
- No primeiro login o SonarQube pede troca da senha.
- Em seguida, gere um token de usuário em `My Account -> Security`.

## 2. Executar análise da solution localmente

Script:
- `scripts/sonarqube-local.ps1`

Exemplo completo:

```powershell
$env:SONAR_TOKEN = "SEU_TOKEN"
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -ProjectKey "cashflow" `
  -ProjectName "Cashflow"
```

Modos úteis:

```powershell
# sobe apenas a instância
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 -StartOnly

# executa análise sem subir docker (usa servidor remoto já existente)
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -SkipDocker `
  -SonarHostUrl "https://seu-sonar.exemplo" `
  -SonarToken "SEU_TOKEN"

# executa apenas build + análise estática (sem testes)
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 `
  -SkipTests `
  -SonarToken "SEU_TOKEN"

# para e remove volumes da stack local do SonarQube
powershell -ExecutionPolicy Bypass -File .\scripts\sonarqube-local.ps1 -Stop
```

## 3. Integração GitHub Actions (nuvem)

Workflow dedicado:
- `.github/workflows/sonarqube-analysis.yml`

Secrets obrigatórios no repositório:
- `SONAR_TOKEN`
- `SONAR_ORGANIZATION` (exemplo: `antonio-leonardo`)

Variáveis obrigatórias (Repository Variables):
- `SONAR_HOST_URL` (exemplo SonarCloud: `https://sonarcloud.io`)

Variáveis opcionais (Repository Variables):
- `SONAR_PROJECT_KEY` (recomendado: `antonio-leonardo_Cashflow`; se ausente, o workflow tenta resolver automaticamente)
- `SONAR_PROJECT_NAME` (default: `Cashflow`)

Comportamento:
- roda em `push` para `main`, `feature/**` e `task/**`, além de `workflow_dispatch`;
- provisiona .NET e Java 17 no runner;
- executa `begin -> restore -> build -> test -> end` usando `dotnet-sonarscanner`;
- valida o acesso ao projeto SonarCloud antes do build/test;
- valida a leitura do `Quality Gate` antes da análise;
- aguarda o `Quality Gate` com retry para falhas transientes da API.

Importante sobre token:
- gere `SONAR_TOKEN` em `My Account -> Security` com um usuário que tenha acesso ao projeto (Browse + Execute Analysis).

Checklist no GitHub (uma vez por repositório):
- `Settings -> Secrets and variables -> Actions -> New repository secret`:
  - `SONAR_TOKEN`
  - `SONAR_ORGANIZATION`
- `Settings -> Secrets and variables -> Actions -> Variables`:
  - `SONAR_HOST_URL`
- `Settings -> Secrets and variables -> Actions -> Variables` (opcional):
  - `SONAR_PROJECT_KEY`
  - `SONAR_PROJECT_NAME`
- `Settings -> Branches -> Branch protection rules (main)`:
  - habilitar status check obrigatório;
  - selecionar o check `Code Smells And Quality Gate`.

## 4. Boas práticas recomendadas

- Manter servidor SonarQube fora do runner (instância dedicada).
- Habilitar backup do banco do SonarQube (PostgreSQL).
- Definir Quality Gate mínimo para smell/vulnerabilidade/bug.
- Versionar a chave de projeto (`ProjectKey`) por repositório.
