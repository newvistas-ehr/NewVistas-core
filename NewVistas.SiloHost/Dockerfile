# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching (only this project and its dependencies)
COPY NewVistas.Abstractions/NewVistas.Abstractions.csproj NewVistas.Abstractions/
COPY NewVistas.SiloHost/NewVistas.SiloHost.csproj NewVistas.SiloHost/

# Restore dependencies
RUN dotnet restore NewVistas.SiloHost/NewVistas.SiloHost.csproj

# Copy full source
COPY NewVistas.Abstractions/ NewVistas.Abstractions/
COPY NewVistas.SiloHost/ NewVistas.SiloHost/

# Build and publish in Release mode
RUN dotnet publish NewVistas.SiloHost/NewVistas.SiloHost.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SiloHost is a generic host (not a web app) — no HTTP port to expose.
# It listens on Orleans silo port 11111 and gateway port 30000 (TCP).
EXPOSE 11111
EXPOSE 30000

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NewVistas.SiloHost.dll"]
