# Cashflow Kubernetes Baseline

Este diretório contém manifests mínimos para evidência de prontidão cloud:

- Namespace e configuração central (`namespace.yaml`, `configmap.yaml`)
- Deployments com probes e réplicas para Gateway/Transaction/Balance
- Services internos para comunicação entre pods
- Ingress com TLS termination (`ingress-tls.yaml`)

## Aplicação (ordem sugerida)

```bash
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret-example.yaml
kubectl apply -f k8s/services/
kubectl apply -f k8s/deployments/
kubectl apply -f k8s/ingress-tls.yaml
```

## Observações

- Troque as imagens `ghcr.io/...:latest` pelas imagens publicadas do pipeline.
- O arquivo `secret-example.yaml` é apenas modelo; use um Secret real no cluster.
- O Ingress assume controlador NGINX e certificado no Secret `cashflow-tls`.
- Este baseline cobre deploy/service/config e pode ser estendido com HPA, PDB e NetworkPolicy.
