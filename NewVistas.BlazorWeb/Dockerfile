# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching (this project and all its project dependencies)
COPY NewVistas.Abstractions/NewVistas.Abstractions.csproj NewVistas.Abstractions/
COPY NewVistas.ImageStorage/NewVistas.ImageStorage.csproj NewVistas.ImageStorage/
COPY NewVistas.PT/NewVistas.PT.csproj NewVistas.PT/
COPY NewVistas.ServiceDefaults/NewVistas.ServiceDefaults.csproj NewVistas.ServiceDefaults/
COPY NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj NewVistas.BlazorWeb/

# Restore dependencies
RUN dotnet restore NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj

# Copy full source
COPY NewVistas.Abstractions/ NewVistas.Abstractions/
COPY NewVistas.ImageStorage/ NewVistas.ImageStorage/
COPY NewVistas.PT/ NewVistas.PT/
COPY NewVistas.ServiceDefaults/ NewVistas.ServiceDefaults/
COPY NewVistas.BlazorWeb/ NewVistas.BlazorWeb/

# Build and publish in Release mode
RUN dotnet publish NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj \
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

ENTRYPOINT ["dotnet", "NewVistas.BlazorWeb.dll"]
