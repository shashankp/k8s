# setup-k8s.ps1

# 1. Infrastructure
Write-Host "Setting up Kubernetes infrastructure..." -ForegroundColor Green
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/cloud/deploy.yaml
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Wait for ingress controller
Write-Host "Waiting for ingress controller..." -ForegroundColor Yellow
kubectl wait --namespace ingress-nginx --for=condition=ready pod --selector=app.kubernetes.io/component=controller --timeout=120s

# 2. Helm setup
Write-Host "Setting up Helm repos..." -ForegroundColor Green
helm repo add headlamp https://kubernetes-sigs.github.io/headlamp/
helm repo add signoz https://charts.signoz.io
helm repo update

# 3. Install monitoring
Write-Host "Installing monitoring tools..." -ForegroundColor Green
helm upgrade --install headlamp headlamp/headlamp --namespace monitoring --create-namespace -f headlamp-values.yaml
kubectl create token headlamp --namespace monitoring

helm upgrade --install signoz signoz/signoz --namespace monitoring --create-namespace 
helm upgrade --install signoz signoz/signoz --namespace monitoring --create-namespace -f signoz-values.yaml
kubectl rollout restart deployment signoz-otel-collector -n monitoring


# 4. Build and deploy apps
Write-Host "Building frontend..." -ForegroundColor Green
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

cd ../config/signoz
kubectl apply -f signoz-mcp.deployment.yaml
kubectl apply -f signoz-mcp.service.yaml

# 5. Apply ingresses
Write-Host "Configuring ingresses..." -ForegroundColor Green

Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "Don't forget to add these to your hosts file:" -ForegroundColor Yellow
Write-Host "127.0.0.1 frontend.local backend.local headlamp.local signoz.local collector.local"
