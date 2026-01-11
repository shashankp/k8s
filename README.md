# Infra

## kubectl
```
kubectl config use-context docker-desktop
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/cloud/deploy.yaml
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
kubectl apply -f https://raw.githubusercontent.com/kubernetes/dashboard/v2.7.0/aio/deploy/recommended.yaml

kubectl proxy

helm repo add headlamp https://kubernetes-sigs.github.io/headlamp/
helm repo add signoz https://charts.signoz.io
helm repo update

helm install headlamp headlamp/headlamp --namespace monitoring --create-namespace
helm install signoz signoz/signoz --namespace monitoring

helm upgrade --install headlamp headlamp/headlamp --namespace monitoring --create-namespace -f headlamp-values.yaml
kubectl create token headlamp --namespace monitoring
```

## apps
```
cd frontend
docker build -t frontend:latest .
kubectl create namespace frontend
kubectl apply -f frontend-deployment.yaml
kubectl apply -f frontend-service.yaml

cd backend
docker build -t backend:latest .
kubectl create namespace backend
kubectl apply -f backend-deployment.yaml
kubectl apply -f backend-service.yaml

cd config/ingress
kubectl apply -f .\frontend-ingress.yaml
kubectl apply -f .\backend-ingress.yaml
kubectl apply -f .\monitoring-ingress.yaml

```


## monitor

- Add to Hosts
```
127.0.0.1 frontend.local
127.0.0.1 backend.local
127.0.0.1 grafana.local
127.0.0.1 prometheus.local
127.0.0.1 signoz.local
127.0.0.1 collector.local
127.0.0.1 signozmcp.local
```

```
kubectl patch svc ingress-nginx-controller -n ingress-nginx -p '{\"spec\":{\"type\":\"LoadBalancer\"}}'
kubectl get svc ingress-nginx-controller -n ingress-nginx
```


### signoz
```

$POD_NAME = kubectl get pods --namespace monitoring -l "app.kubernetes.io/name=signoz,app.kubernetes.io/instance=signoz,app.kubernetes.io/component=signoz" -o jsonpath="{.items[0].metadata.name}"

kubectl --namespace monitoring port-forward $POD_NAME 8080:8080
```

# frontend 
```
kubectl rollout restart deployment frontend -n frontend
minikube service frontend-service -n frontend --url
```

# collector
```
kubectl edit configmap signoz-otel-collector -n monitoring
kubectl rollout restart deployment signoz-otel-collector -n monitoring


kubectl port-forward -n monitoring svc/signoz-otel-collector 4318:4318

kubectl port-forward -n monitoring svc/signoz-mcp-server 8000:8000
```

# signoz mcp
```
kubectl apply -f tools/signoz-mcp.yaml

git clone https://github.com/SigNoz/signoz-mcp-server.git
cd signoz-mcp-server
docker build -t signoz-mcp-server:latest .
minikube image load signoz-mcp-server:latest

kubectl port-forward -n monitoring svc/signoz-mcp-server 8000:8000

Invoke-RestMethod -Uri "http://signozmcp.local/mcp" -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' -Headers @{ "Accept" = "application/json" }

{
  "mcpServers": {
    "signoz": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://signozmcp.local/mcp",
		"--allow-http"
      ]
    }
  }
}

```


# backend
```
& minikube -p minikube docker-env --shell powershell | Invoke-Expression

kubectl rollout restart -n backend deployment backend

kubectl port-forward -n backend svc/backend 8081:80

kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml
kubectl apply -f https://github.com/open-telemetry/opentelemetry-operator/releases/latest/download/opentelemetry-operator.yaml


```