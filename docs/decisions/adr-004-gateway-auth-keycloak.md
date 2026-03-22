# ADR-004: Gateway com Keycloak (OIDC)

Date: 2026-03-20  
Status: Accepted

## Context
A autenticacao deve ser centralizada e independente dos servicos de dominio. O desafio exige independencia entre servicos e integracao segura com OIDC.

## Decision
Usar API Gateway (YARP) com autenticacao via Keycloak (OIDC/OAuth2). O Gateway aplica politica de acesso e encaminha requisicoes autenticadas para a Transaction API.

## Alternatives considered
- Autenticacao embutida em cada servico.
- API Gateway com provedor proprietario.

## Consequences
Positivas:
- Politica de acesso unificada.
- Isolamento do dominio em relacao a autenticacao.
- Facilidade para testes de integracao do gateway.

Negativas:
- Dependencia adicional (Keycloak).
- Necessidade de testes de integracao OIDC para evitar regressao.