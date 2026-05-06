# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching (only this project and its dependencies)
COPY NewVistas.Abstractions/NewVistas.Abstractions.csproj NewVistas.Abstractions/
COPY NewVistas.WebServer/NewVistas.WebServer.csproj NewVistas.WebServer/

# Restore dependencies
RUN dotnet restore NewVistas.WebServer/NewVistas.WebServer.csproj

# Copy full source
COPY NewVistas.Abstractions/ NewVistas.Abstractions/
COPY NewVistas.WebServer/ NewVistas.WebServer/

# Copy exports directory for demo patient data seeding
COPY exports/ exports/

# Build and publish in Release mode
RUN dotnet publish NewVistas.WebServer/NewVistas.WebServer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
COPY --from=build /src/exports /app/exports

ENTRYPOINT ["dotnet", "NewVistas.WebServer.dll"]
