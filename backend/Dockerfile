# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "backend.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8081/health || exit 1

ENTRYPOINT ["dotnet", "backend.dll"]
