kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

helm repo add signoz https://signoz.github.io/charts
helm install signoz signoz/signoz -n monitoring --create-namespace

helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm install otel-operator open-telemetry/opentelemetry-operator -n monitoring

kubectl apply -f signoz-instrumentation.yaml
kubectl apply -f signoz-collector.yaml

kubectl port-forward svc/signoz 8080:8080 -n monitoring

