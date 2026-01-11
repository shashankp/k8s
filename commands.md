# setup-k8s.ps1

# 1. Infrastructure
kubectl config use-context docker-desktop
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/cloud/deploy.yaml
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Wait for ingress controller
kubectl wait --namespace ingress-nginx --for=condition=ready pod --selector=app.kubernetes.io/component=controller --timeout=120s

# 2. Helm setup
helm repo add headlamp https://kubernetes-sigs.github.io/headlamp/
helm repo add signoz https://charts.signoz.io
helm repo update

# 3. Install monitoring
helm upgrade --install headlamp headlamp/headlamp --namespace monitoring --create-namespace -f headlamp-values.yaml
kubectl create token headlamp --namespace monitoring

helm upgrade --install signoz signoz/signoz --namespace monitoring --create-namespace 
helm upgrade --install signoz signoz/signoz --namespace monitoring --create-namespace -f signoz-values.yaml
kubectl rollout restart deployment signoz-otel-collector -n monitoring


# 4. Build and deploy apps
cd frontend
docker build -t frontend:latest .
kubectl create namespace frontend
kubectl apply -f frontend-deployment.yaml
kubectl apply -f frontend-service.yaml

Write-Host "Building backend..." -ForegroundColor Green
cd ../backend
docker build -t backend:latest .
kubectl create namespace backend
kubectl apply -f backend-deployment.yaml
kubectl apply -f backend-service.yaml

cd ../config/ingress
kubectl apply -f frontend-ingress.yaml
kubectl apply -f backend-ingress.yaml
kubectl apply -f monitoring-ingress.yaml

cd ../config/signoz
kubectl apply -f signoz-mcp.deployment.yaml
kubectl apply -f signoz-mcp.service.yaml

# 5. Apply ingresses
## "127.0.0.1 frontend.local backend.local headlamp.local signoz.local collector.local"
## echo "127.0.0.1 frontend.local backend.local headlamp.local signoz.local signozmcp.local collector.local" | sudo tee -a /etc/hosts

# 6. Verify MCP
curl -X POST http://signozmcp.local/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
