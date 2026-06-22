# Running NewVistas in Docker

NewVistas ships an **all-in-one demo image** that runs the three runtime
components — the Orleans silo, the REST API, and the Blazor Server UI — inside a
single container. It uses in-memory grain storage and seeds demo data on first
run, so it needs **no external database or other services**.

> For production / Azure deployments, use the per-project Dockerfiles in
> `NewVistas.SiloHost/`, `NewVistas.WebServer/`, and `NewVistas.BlazorWeb/`,
> which build one container per component against SQL Server. See
> [AZURE_DEPLOY.md](AZURE_DEPLOY.md).

## Build

Build from the repository root (the build context spans several projects):

```bash
docker build -t newvistas .
```

## Run

```bash
docker run --rm -p 8080:8080 -p 8081:8081 newvistas
```

Then open:

| URL                              | Service                          |
|----------------------------------|----------------------------------|
| http://localhost:8080            | Blazor Server UI (start here)    |
| http://localhost:8081            | REST API                         |
| http://localhost:8081/openapi/v1.json | OpenAPI document            |

Sign in with any of the seeded demo users — for example **`DOCTOR1`** with
password **`smythVista1`**. (See `NewVistas.WebServer/Program.cs` for the full
list of demo accounts and roles.)

The first start takes ~1–2 minutes while the silo comes up and demo patients,
users, and reference data are seeded. All data is in-memory and is lost when the
container stops.

## What runs inside

| Process     | Role                                   | Internal ports        |
|-------------|----------------------------------------|-----------------------|
| SiloHost    | Orleans silo (grains, state, streams)  | 11111 (silo), 30000 (gateway) |
| WebServer   | REST API + Identity/JWT, data seeding  | 8081 (HTTP)           |
| BlazorWeb   | Blazor Server UI                       | 8080 (HTTP)           |

`docker-entrypoint.sh` starts the silo first, waits for its client gateway to
accept connections, then starts the WebServer and the Blazor UI. If any of the
three exits, the container shuts down so the orchestrator can restart it.

## Configuration

Override these with `docker run -e NAME=value`:

| Variable           | Default                  | Purpose                                            |
|--------------------|--------------------------|----------------------------------------------------|
| `NEWVISTAS_DATASET`| `fifty`                  | Demo patient set to import: `fifty`, `fivehundred`, or `onethousand`. |
| `ApiBaseUrl`       | `http://localhost:8081`  | WebServer base URL the Blazor app calls for login. |

Example — seed the 500-patient dataset:

```bash
docker run --rm -p 8080:8080 -p 8081:8081 -e NEWVISTAS_DATASET=fivehundred newvistas
```
