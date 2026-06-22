# NewVistas — all-in-one demo image.
#
# Runs the Orleans silo, the REST API (WebServer), and the Blazor Server UI as
# three processes in a single container. Storage is in-memory and demo data is
# seeded on first run, so the image is fully self-contained — no database or
# other services required. Build from the repository root:
#
#     docker build -t newvistas .
#     docker run --rm -p 8080:8080 -p 8081:8081 newvistas
#
# Then browse http://localhost:8080 (UI). REST API/OpenAPI is on :8081.
# Sign in with any demo user, e.g. DOCTOR1 / smythVista1.
#
# For multi-container (Azure) deployments use the per-project Dockerfiles in
# NewVistas.SiloHost/, NewVistas.WebServer/, and NewVistas.BlazorWeb/ instead.

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for restore-layer caching — the union of project
# dependencies across the silo, the WebServer, and the Blazor app.
COPY NewVistas.Abstractions/NewVistas.Abstractions.csproj NewVistas.Abstractions/
COPY NewVistas.ImageStorage/NewVistas.ImageStorage.csproj NewVistas.ImageStorage/
COPY NewVistas.PT/NewVistas.PT.csproj NewVistas.PT/
COPY NewVistas.ServiceDefaults/NewVistas.ServiceDefaults.csproj NewVistas.ServiceDefaults/
COPY NewVistas.SiloHost/NewVistas.SiloHost.csproj NewVistas.SiloHost/
COPY NewVistas.WebServer/NewVistas.WebServer.csproj NewVistas.WebServer/
COPY NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj NewVistas.BlazorWeb/

RUN dotnet restore NewVistas.SiloHost/NewVistas.SiloHost.csproj \
 && dotnet restore NewVistas.WebServer/NewVistas.WebServer.csproj \
 && dotnet restore NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj

# Copy full source for the projects involved.
COPY NewVistas.Abstractions/ NewVistas.Abstractions/
COPY NewVistas.ImageStorage/ NewVistas.ImageStorage/
COPY NewVistas.PT/ NewVistas.PT/
COPY NewVistas.ServiceDefaults/ NewVistas.ServiceDefaults/
COPY NewVistas.SiloHost/ NewVistas.SiloHost/
COPY NewVistas.WebServer/ NewVistas.WebServer/
COPY NewVistas.BlazorWeb/ NewVistas.BlazorWeb/

# Synthetic demo patient data (ZWR exports) auto-imported by the WebServer.
COPY exports/ exports/

# Publish each app into its own output folder.
RUN dotnet publish NewVistas.SiloHost/NewVistas.SiloHost.csproj   -c Release -o /app/silo      --no-restore \
 && dotnet publish NewVistas.WebServer/NewVistas.WebServer.csproj -c Release -o /app/webserver --no-restore \
 && dotnet publish NewVistas.BlazorWeb/NewVistas.BlazorWeb.csproj -c Release -o /app/blazor    --no-restore

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Development environment selects the in-memory site profile (silo) + in-memory
# Identity (WebServer), making the image self-contained. ApiBaseUrl points the
# Blazor app at the co-hosted WebServer; NEWVISTAS_DATASET picks the seed size
# (fifty | fivehundred | onethousand).
ENV DOTNET_ENVIRONMENT=Development \
    ASPNETCORE_ENVIRONMENT=Development \
    ApiBaseUrl=http://localhost:8081 \
    NEWVISTAS_DATASET=fifty

COPY --from=build /app/silo ./silo
COPY --from=build /app/webserver ./webserver
COPY --from=build /app/blazor ./blazor
# exports/ sits one level above the WebServer content root (/app/webserver) so
# the seeder's FindExportsDirectory walk (content root → parents) locates it.
COPY --from=build /src/exports ./exports

COPY docker-entrypoint.sh /app/docker-entrypoint.sh
# Strip any CR so the script runs on a Linux container even if checked out CRLF.
RUN sed -i 's/\r$//' /app/docker-entrypoint.sh && chmod +x /app/docker-entrypoint.sh

# 8080 = Blazor Server UI (browse here); 8081 = REST API / OpenAPI.
# Orleans silo (11111) and gateway (30000) stay container-internal.
EXPOSE 8080 8081

# Liveness probe against the UI port using bash's /dev/tcp (no curl in the
# runtime image). start-period covers silo startup + demo-data seeding.
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --retries=5 \
  CMD bash -c '</dev/tcp/localhost/8080' || exit 1

ENTRYPOINT ["/app/docker-entrypoint.sh"]
