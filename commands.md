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
helm upgrade --install signoz signoz/signoz --namespace monitoring --create-namespace

# 4. Build and deploy apps
Write-Host "Building frontend..." -ForegroundColor Green
cd frontend
docker build -t frontend:latest .
kubectl create namespace frontend --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -f frontend-deployment.yaml
kubectl apply -f frontend-service.yaml

Write-Host "Building backend..." -ForegroundColor Green
cd ../backend
docker build -t backend:latest .
kubectl create namespace backend --dry-run=client -o yaml | kubectl apply -f -
kubectl apply -f backend-deployment.yaml
kubectl apply -f backend-service.yaml

# 5. Apply ingresses
Write-Host "Configuring ingresses..." -ForegroundColor Green
cd ../config/ingress
kubectl apply -f frontend-ingress.yaml
kubectl apply -f backend-ingress.yaml
kubectl apply -f monitoring-ingress.yaml

Write-Host "Setup complete!" -ForegroundColor Green
Write-Host "Don't forget to add these to your hosts file:" -ForegroundColor Yellow
Write-Host "127.0.0.1 frontend.local backend.local headlamp.local signoz.local"
