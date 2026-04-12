# AKS Second Wave

`AKS` continua sendo a segunda onda da trilha Azure, mas agora há um trilho explícito no repositório para esse target.

## Base

Os manifests canônicos continuam em [`k8s`](/c:/Users/AntonioLeonardodeAbr/source/repos/antonio-leonardo/Cashflow/k8s/README.md).

Eles podem ser aplicados em um cluster AKS com:

```bash
az aks get-credentials --resource-group <rg> --name <aks-name>
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/services
kubectl apply -f k8s/deployments
kubectl apply -f k8s/ingress-tls.yaml
```

## Ajustes Azure recomendados

- `Workload Identity` para Service Bus, Blob e Redis sem secrets long-lived.
- `Secret Store CSI Driver` ou `External Secrets` para Azure Key Vault.
- `Ingress NGINX` ou `Application Gateway Ingress Controller`.
- `Azure Monitor` e `Container Insights` para logs e métricas.
- `KEDA` para autoscaling de workloads orientados a fila.

## Mapeamento

- Gateway, APIs e workers continuam reaproveitando os mesmos artefatos da solution.
- A decisão entre `AKS`, `Container Apps`, `Functions` e `App Service` continua sendo de hospedagem, não de domínio.
