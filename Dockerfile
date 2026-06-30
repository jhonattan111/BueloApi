# syntax=docker/dockerfile:1
# ── build ──────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Buelo.slnx
RUN dotnet publish Buelo.Api/Buelo.Api.csproj -c Release -o /app --no-restore

# ── runtime ────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is used by the container health check (GET /health).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
# Example definitions are read on first boot and seeded into the database (idempotent).
COPY --from=build /src/Buelo.Api/definitions ./definitions

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Buelo.Api.dll"]
