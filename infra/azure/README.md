# Azure Targets

Este diretório agora concentra os alvos Azure da solution, separados por papel:

- `main.bicep`: POC de `Azure Container Apps` para gateway, APIs e workers.
- `servicebus-topology.bicep`: topologia do `Azure Service Bus` com tópico, subscriptions, DLQ e sessão.
- `appservice.bicep`: exemplo de hospedagem web em `Azure App Service` usando as mesmas imagens da solution.
- `functionapps.bicep`: exemplo de `Azure Functions` para os hosts `Balance` e `Report`.
- `apim.bicep`: POC de `Azure API Management` na frente do gateway.
- `aks/README.md`: trilha de segunda onda para `AKS`, reutilizando os manifests já existentes em [`k8s`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/k8s/README.md).

## Estratégia

O objetivo continua sendo multicloud:

- `Providers:*` selecionam capacidades da aplicação.
- Os arquivos deste diretório modelam `targets de hospedagem` no Azure.
- O `default` atual da solution continua preservado.

## Cobertura

Os workflows relacionados ficam em:

- [`backend-emulator-ci.yml`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/.github/workflows/backend-emulator-ci.yml)
- [`azure-provider-smoke.yml`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/.github/workflows/azure-provider-smoke.yml)
- [`azure-functions-deploy.yml`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/.github/workflows/azure-functions-deploy.yml)
- [`azure-deploy.yml`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/.github/workflows/azure-deploy.yml)
