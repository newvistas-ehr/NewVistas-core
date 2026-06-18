# Database & Network Configuration Guide

This guide explains **how NewVistas chooses where to store data**, how to **switch
between databases**, and how to **run the system across several machines on a local
network** instead of on one developer box or in Azure.

If you just want to stand the system up in the cloud, see
[AZURE_DEPLOY.md](AZURE_DEPLOY.md). This document is for changing *where the data
lives* and *how the pieces find each other*.

---

## How storage is selected: site profiles

NewVistas is built on Microsoft Orleans. The **SiloHost** (`NewVistas.SiloHost`) is
the stateful process that owns all data; the web apps (WebServer, BlazorWeb,
PatientPortal) are **Orleans clients** that connect to it.

At startup the SiloHost picks a **site profile**
([`SiteProfileResolver`](NewVistas.SiloHost/Infrastructure/Profiles/SiteProfileResolver.cs)).
The profile decides two things: **clustering** (how silos and clients find each
other) and **grain storage** (where data is persisted). Resolution order — first
match wins:

1. `--profile=<name>` command-line argument
2. `NEWVISTAS_PROFILE` environment variable
3. `--use-sqlexpress` command-line argument → the SQL Express profile
4. `ASPNETCORE_ENVIRONMENT=Development` → the in-memory dev profile
5. otherwise → the Azure/SQL Server profile

| Profile name      | Clustering            | Grain storage                  | Connection string |
| ----------------- | --------------------- | ------------------------------ | ----------------- |
| `localhost-dev`   | Localhost             | **In-memory** (lost on restart)| *(none)*          |
| `sql-express-demo`| Localhost             | SQL Express (AdoNet)           | `SqlExpress`      |
| `azure-cloud`     | SQL Server (AdoNet)   | SQL Server (AdoNet)            | `OrleansDatabase` |
| `remote-online`   | Localhost             | SQL Express (AdoNet)           | `SqlExpress`      |
| `remote-offline`  | Localhost             | SQL Express (AdoNet)           | `SqlExpress`      |
| `ihs-tribal`      | Localhost             | SQL Express (AdoNet)           | `SqlExpress`      |

> **Key idea:** "which database" is just **which profile** + **which connection
> string**. The connection strings live under `ConnectionStrings` in each project's
> `appsettings.json`, and can be overridden by environment variables (Azure does
> exactly this — see [Overriding via environment variables](#overriding-via-environment-variables)).

---

## Part A — Changing which database is used

### Option 1: In-memory (default for development)

Nothing to configure. With `ASPNETCORE_ENVIRONMENT=Development` (the default when you
run from Visual Studio or `dotnet run`), the silo uses the `localhost-dev` profile:
all data is held in memory and **discarded when the SiloHost stops**. Great for a
quick look; useless for keeping data.

```bash
dotnet run --project NewVistas.SiloHost
```

### Option 2: SQL Express (persistent, single machine)

The simplest way to **keep your data** on a developer/demo box. The silo will
auto-create the database and Orleans tables on first run.

1. Install SQL Server Express (LocalDB or a full Express instance).
2. Set the `SqlExpress` connection string in
   [`NewVistas.SiloHost/appsettings.json`](NewVistas.SiloHost/appsettings.json):

   ```json
   {
     "ConnectionStrings": {
       "SqlExpress": "Server=.\\SQLEXPRESS;Database=NewVistas;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. Start the silo with the flag:

   ```bash
   dotnet run --project NewVistas.SiloHost -- --use-sqlexpress
   ```

   (`--profile=sql-express-demo` does the same thing.)

The web apps stay on localhost clustering in Development, so they connect with no
extra configuration. Your data now survives restarts.

### Option 3: SQL Server / Azure SQL (persistent, distributed)

This is the profile the Azure deployment uses, and the one to use for any
multi-machine or production-like setup. It uses **AdoNet clustering** — silos and
clients discover each other through a SQL **clustering table** — plus SQL grain
storage, both via the `OrleansDatabase` connection string.

1. Provision a SQL Server database reachable by every process that needs it.
2. Set `OrleansDatabase` (and, for the WebServer, `IdentityDatabase` — it falls back
   to `OrleansDatabase` if absent) on **every** component: SiloHost, WebServer,
   BlazorWeb, and PatientPortal.
3. Run each component with `ASPNETCORE_ENVIRONMENT=Production` (or
   `--profile=azure-cloud` on the silo). On first start the silo applies the Orleans
   schema automatically.

```jsonc
// appsettings.Production.json (SiloHost, WebServer, BlazorWeb, PatientPortal)
{
  "ConnectionStrings": {
    "OrleansDatabase": "Server=MYSQLHOST,1433;Database=NewVistasDB;User ID=nv;Password=...;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

### Overriding via environment variables

Any connection string can be supplied as an environment variable instead of editing
JSON — handy for containers and CI. .NET maps the nested key `ConnectionStrings:Name`
to the variable `ConnectionStrings__Name` (double underscore):

```bash
# Bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__OrleansDatabase="Server=MYSQLHOST,1433;Database=NewVistasDB;User ID=nv;Password=...;Encrypt=True;TrustServerCertificate=True;"
```
```powershell
# PowerShell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ConnectionStrings__OrleansDatabase = 'Server=MYSQLHOST,1433;Database=NewVistasDB;User ID=nv;Password=...;Encrypt=True;TrustServerCertificate=True;'
```

This is exactly what `scripts/azure-deploy.sh` / `azure-deploy.ps1` do — they pass
`ConnectionStrings__OrleansDatabase` into each container.

---

## Part B — Deploying on a local network (LAN)

The goal: run the SiloHost on one machine and let clinician/patient browsers on
**other machines** use it. The mechanism is the same as the cloud profile — a
**shared SQL Server** acts as the meeting point (the Orleans clustering table). Every
process points at that one database; clients discover the silo through it.

```
   Machine A (server)                 Machines B, C ... (any LAN host w/ a browser)
 ┌─────────────────────┐
 │ SQL Server          │◀─────────────── clustering table + grain storage
 │ SiloHost            │                 (OrleansDatabase)
 │ WebServer  :8080    │◀──── HTTP ────  browser → http://machine-a:8080
 │ BlazorWeb  :8081    │◀──── HTTP ────  browser → http://machine-a:8081
 └─────────────────────┘
```

### Steps

1. **Stand up SQL Server** on a machine reachable from the others (e.g. `MACHINE-A`).
   Create an empty database (e.g. `NewVistasDB`) and a SQL login the apps will use.
   Make sure SQL is listening on TCP 1433 and reachable across the LAN.

2. **Configure every component** with `ASPNETCORE_ENVIRONMENT=Production` and the same
   `OrleansDatabase` connection string pointing at that SQL Server. Because they all
   share one database, the silo and all clients automatically land in the same Orleans
   cluster — keep them on the same database and don't override cluster identity on
   only one side.

3. **Start the SiloHost first** on the server machine. Its silo/gateway ports default
   to `11111` (silo) and `30000` (gateway) and can be changed with
   `Orleans:SiloPort` / `Orleans:GatewayPort` (env vars `Orleans__SiloPort` /
   `Orleans__GatewayPort`). The silo registers its address in the clustering table;
   clients read it from there.

4. **Start the web apps.** They can run on the same machine as the silo or on another
   LAN host — wherever they run, give them the same `OrleansDatabase` so they find the
   silo. Bind Kestrel to all interfaces so other machines can reach the UI:

   ```bash
   export ASPNETCORE_URLS=http://0.0.0.0:8080
   ```

   - **WebServer** also needs `IdentityDatabase` (logins) — point it at the same SQL
     Server.
   - **BlazorWeb** needs `ApiBaseUrl` set to the WebServer's LAN address, e.g.
     `ApiBaseUrl=http://machine-a:8080`.

5. **Open the firewall** on the machines that host each role:

   | Port  | Where        | Purpose                                  |
   | ----- | ------------ | ---------------------------------------- |
   | 1433  | SQL machine  | SQL Server (clustering + grain storage)  |
   | 11111 | Silo machine | Orleans silo-to-silo                     |
   | 30000 | Silo machine | Orleans gateway (clients connect here)   |
   | 8080  | Web machine  | WebServer / BlazorWeb HTTP UI            |

6. **Browse from any LAN machine** to `http://<web-machine>:8080`.

### Notes & gotchas

- **Start order:** bring up SQL Server, then the SiloHost, then the web apps. A client
  that starts before the silo has registered will retry; give the silo ~30–60 seconds.
- **One database = one cluster.** All four processes must point at the *same*
  `OrleansDatabase`. Two different databases means two isolated clusters that can't see
  each other.
- **TLS:** the examples use `TrustServerCertificate=True` for convenience on a trusted
  LAN. For anything beyond a closed test network, use a real certificate and put the
  web UIs behind HTTPS.
- **Persistence:** Options 2 and 3 persist data; the default in-memory dev profile does
  not. Don't demo "saved" data on the in-memory profile.

---

## Quick reference

| I want to…                                  | Do this                                                            |
| ------------------------------------------- | ------------------------------------------------------------------ |
| Throwaway data, fastest start               | Default (`ASPNETCORE_ENVIRONMENT=Development`)                      |
| Keep data on one machine                    | `--use-sqlexpress` + set `SqlExpress` connection string            |
| Share across a LAN / production-like        | `ASPNETCORE_ENVIRONMENT=Production` + shared `OrleansDatabase`      |
| Deploy to Azure                             | [AZURE_DEPLOY.md](AZURE_DEPLOY.md)                                  |
| Change silo/gateway ports                   | `Orleans__SiloPort` / `Orleans__GatewayPort`                       |
