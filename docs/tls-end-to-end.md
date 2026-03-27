# TLS End-to-End (Runbook)

## Objetivo

Demonstrar criptografia em trânsito entre cliente -> gateway -> APIs.

## 1) Gerar certificados locais

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-local-tls-certs.ps1 -Password "changeit"
```

Arquivos gerados:

- `.appdata/certs/gateway/gateway.pfx`
- `.appdata/certs/transaction-api/transaction-api.pfx`
- `.appdata/certs/balance-query-api/balance-query-api.pfx`

## 2) Subir stack com override TLS

```bash
TLS_CERT_PASSWORD=changeit docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d
```

## 3) Validar tráfego HTTPS

- Gateway HTTPS: `https://localhost:5443/health/ready`
- Transaction API HTTPS: `https://localhost:5441/health/ready`
- Balance API HTTPS: `https://localhost:5442/health/ready`

## 4) Observações

- O override `docker-compose.tls.yml` configura Kestrel com certificados PFX.
- O gateway passa a encaminhar para downstreams em `https://...:8443`.
- A opção `DangerousAcceptAnyServerCertificate=true` foi incluída apenas para laboratório local com certificado self-signed.
- Em produção, substituir por certificados emitidos por AC confiável e remover essa opção.
