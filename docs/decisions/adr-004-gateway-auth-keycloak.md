# ADR-004: Gateway com Keycloak (OIDC)

Date: 2026-03-20
Status: Accepted

## Context
A Autenticação deve ser centralizada e independente dos serviços de domínio. O desafio exige independência entre serviços e Integração segura com OIDC.

## Decision
Usar API Gateway (YARP) com autenticação via Keycloak (OIDC/OAuth2). O Gateway aplica política de acesso e encaminha requisições autenticadas para a Transaction API.

## Alternatives considered
- Autenticação embutida em cada serviço.
- API Gateway com provedor proprietário.

## Consequences
Positivas:
- política de acesso unificada.
- Isolamento do domínio em relação a Autenticação.
- Facilidade para testes de Integração do gateway.

Negativas:
- Dependência adicional (Keycloak).
- Necessidade de testes de Integração OIDC para evitar regressão.
