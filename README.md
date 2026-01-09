# Infra

## minikube
```
choco install kubernetes-cli minikube

minikube start --cpus 4 --memory 8192
minikube addons enable ingress

```

## monitor
```
minikube addons enable metrics-server
minikube dashboard
kubectl top pods -A
```

## helm
```
choco install kubernetes-helm

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add signoz https://charts.signoz.io
helm repo update

```

### grafana
```
helm install monitoring prometheus-community/kube-prometheus-stack --namespace monitoring --create-namespace

kubectl --namespace monitoring get secrets monitoring-grafana -o jsonpath="{.data.admin-password}" | base64 -d ; echo

$pass = kubectl --namespace monitoring get secrets monitoring-grafana -o jsonpath="{.data.admin-password}"
[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($pass))

kubectl port-forward -n monitoring svc/monitoring-grafana 3000:80
kubectl port-forward -n monitoring svc/monitoring-kube-prometheus-prometheus 9090:9090

```

### signoz
```
helm install signoz signoz/signoz --namespace monitoring
$POD_NAME = kubectl get pods --namespace monitoring -l "app.kubernetes.io/name=signoz,app.kubernetes.io/instance=signoz,app.kubernetes.io/component=signoz" -o jsonpath="{.items[0].metadata.name}"

kubectl --namespace monitoring port-forward $POD_NAME 8080:8080
```

# frontend 
```
& minikube -p minikube docker-env --shell powershell | Invoke-Expression
docker build -t frontend:latest .

kubectl create namespace frontend
kubectl apply -f frontend-deployment.yaml
kubectl apply -f frontend-service.yaml

kubectl rollout restart deployment frontend -n frontend

minikube service frontend-service -n frontend --url


```

# collector
```
kubectl edit configmap signoz-otel-collector -n monitoring
kubectl rollout restart deployment signoz-otel-collector -n monitoring
kubectl port-forward -n monitoring svc/signoz-otel-collector 4318:4318
```

# signoz mcp
```
kubectl apply -f tools/signoz-mcp.yaml

git clone https://github.com/SigNoz/signoz-mcp-server.git
cd signoz-mcp-server
docker build -t signoz-mcp-server:latest .
minikube image load signoz-mcp-server:latest
```