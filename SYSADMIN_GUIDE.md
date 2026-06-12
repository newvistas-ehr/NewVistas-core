# NewVistas — System Administrator Guide

## System Overview

NewVistas is a clinical information system composed of several services that work together. From an administrator's perspective, the key components are:

| Component | Role | Stateful? | Scales How? |
|-----------|------|-----------|-------------|
| **SiloHost** | Data processing engine — holds all clinical data in memory and persists to database | Yes | Add more SiloHost machines to the cluster |
| **WebServer** | REST API — accepts HTTP requests, authenticates users, forwards work to SiloHost | No | Add more WebServer instances behind a load balancer |
| **BlazorWeb** | Web browser UI — serves pages to clinicians using a browser | No | Add more BlazorWeb instances behind a load balancer |
| **PatientPortal** | Patient-facing web portal — self-service access for patients | No | Add more instances behind a load balancer |
| **SQL Server** | Persistent storage — all data is ultimately stored here | Yes | Scale up (bigger machine) or use Azure SQL |

The WPF desktop clients (Wpf_UI, WpfDelphiUI) and the terminal client (CharUI) run on individual workstations and do not need to be "deployed" in the traditional sense — they are installed per-machine like any desktop application.

---

## How the Pieces Fit Together

```
                    ┌─────────────┐
  Browsers ────────▶│  BlazorWeb  │──┐
                    └─────────────┘  │
                    ┌─────────────┐  │     ┌──────────────────┐     ┌────────────┐
  REST/FHIR Apps ──▶│  WebServer  │──┼────▶│    SiloHost(s)   │────▶│ SQL Server │
                    └─────────────┘  │     │  (cluster of 1+) │     └────────────┘
                    ┌─────────────┐  │     └──────────────────┘
  Patients ────────▶│PatientPortal│──┘            ▲
                    └─────────────┘               │
                    ┌─────────────┐               │
  Workstations ────▶│  WPF Client │───────────────┘
                    └─────────────┘        (direct connection
                                            via LAN or VPN)
```

**Data flow**: All clinical data lives in the SiloHost cluster. The WebServer, BlazorWeb, and PatientPortal are stateless — they ask the SiloHost cluster for data and relay it to the user. If any of these stateless services crash, you restart them and nothing is lost. The SiloHost cluster persists all data to SQL Server, so even if the entire cluster restarts, data is recovered from the database.

---

## Deployment Modes

### Development (Single Machine)

Everything runs on one developer workstation. No database needed — data is held in memory and lost on restart.

```
Terminal 1:  dotnet run --project NewVistas.SiloHost
Terminal 2:  dotnet run --project NewVistas.WebServer
Terminal 3:  dotnet run --project NewVistas.BlazorWeb
```

See [START.md](START.md) for full details.

### Development with Persistent Data (Single Machine + SQL Express)

Same as above, but data survives restarts. Requires SQL Server Express installed locally.

```
Terminal 1:  dotnet run --project NewVistas.SiloHost -- --use-sqlexpress
Terminal 2:  dotnet run --project NewVistas.WebServer
Terminal 3:  dotnet run --project NewVistas.BlazorWeb
```

The database and schema are created automatically on first run.

### Production (Multi-Machine)

Each component runs on its own machine (or set of machines). All connect to a shared SQL Server instance.

---

## Production Deployment

### What Goes Where

| Machine(s) | What to Install | Network Requirements |
|------------|-----------------|---------------------|
| 1+ SiloHost servers | `NewVistas.SiloHost` | Must see each other on ports 11111 (silo) and 30000 (gateway). Must reach SQL Server. |
| 1+ WebServer instances | `NewVistas.WebServer` | Must reach SiloHost gateway port 30000. Must reach SQL Server (for Identity DB). |
| 1+ BlazorWeb instances | `NewVistas.BlazorWeb` | Must reach WebServer on its HTTP port. |
| 1 SQL Server | SQL Server 2019+ or Azure SQL | Must be reachable from all SiloHosts and WebServers. |
| Load balancer | Azure App Gateway, NGINX, or similar | Sits in front of WebServer and BlazorWeb instances. |

### Connection Strings

All SiloHost and WebServer instances share the same connection string pointing to the production SQL Server:

```json
{
  "ConnectionStrings": {
    "OrleansDatabase": "Server=your-sql-server;Database=NewVistasDB;User Id=newvistas_svc;Password=...;TrustServerCertificate=True;"
  }
}
```

Set this via `appsettings.json`, environment variables, or Azure Key Vault in production.

### Environment Configuration

| Setting | How to Set | Purpose |
|---------|-----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment variable | Set to `Production` on all production servers |
| `ConnectionStrings__OrleansDatabase` | Env var or Key Vault | SQL Server connection for clustering + data storage |
| `Orleans__SiloPort` | appsettings.json | Default 11111 — change if running multiple silos per machine |
| `Orleans__GatewayPort` | appsettings.json | Default 30000 — change if running multiple silos per machine |

### Database Setup

On first deployment, the SiloHost automatically creates:
1. The database (if it doesn't exist)
2. The Orleans schema tables (OrleansStorage, OrleansQuery, etc.)

This is idempotent — safe to run on every startup. To reset all data, drop the database; it will be recreated on next start.

---

## Scaling

### Adding Capacity to the SiloHost Cluster

The SiloHost cluster is the core of the system. When you need to handle more users, more data, or more concurrent operations, you add SiloHost machines.

**To add a new SiloHost to an existing cluster:**

1. Deploy the `NewVistas.SiloHost` application to the new machine
2. Configure it with the same `OrleansDatabase` connection string as the other silos
3. Set `ASPNETCORE_ENVIRONMENT=Production`
4. Start it: `dotnet run --project NewVistas.SiloHost`

That's it. The new silo registers itself in the SQL Server clustering table. The existing silos discover it automatically. Data is rebalanced across the cluster without any manual intervention. There is no "master" silo — all silos are equal peers.

**What happens when a silo joins:**
- It announces itself via the shared SQL Server clustering table
- Other silos notice within seconds
- New work is distributed to include the new silo
- Existing data migrates on-demand (when accessed, not all at once)

**What happens when a silo leaves (planned or crash):**
- Other silos detect the departure (via heartbeat timeout)
- Data that was on the departed silo is recovered from SQL Server when next accessed
- No data is lost — SQL Server is the source of truth
- The cluster continues operating with the remaining silos

**Sizing guidance:**

| Concurrent Users | Recommended SiloHosts | RAM per SiloHost |
|-----------------|----------------------|-------------------|
| Up to 100 | 1 | 8 GB |
| 100–500 | 2 | 16 GB |
| 500–2,000 | 3–4 | 16–32 GB |
| 2,000–10,000 | 5–8 | 32 GB |

These are rough guidelines. The SiloHost holds frequently-accessed data in memory for fast retrieval, so more RAM means more data can be served without hitting the database.

### Scaling the WebServer

The WebServer is stateless — it authenticates HTTP requests and forwards them to the SiloHost cluster. Scale it by running more instances behind a load balancer.

**To add a WebServer instance:**

1. Deploy `NewVistas.WebServer` to a new machine
2. Configure the same `OrleansDatabase` connection string
3. Add it to your load balancer's backend pool
4. Any load balancing strategy works (round-robin is fine) — there is no session affinity requirement for the API

### Scaling BlazorWeb

BlazorWeb uses SignalR (WebSocket) connections to maintain live UI updates. It requires **sticky sessions** (session affinity) at the load balancer level — a user's WebSocket must stay connected to the same BlazorWeb instance for the duration of their session.

**To add a BlazorWeb instance:**

1. Deploy `NewVistas.BlazorWeb` to a new machine
2. Configure it to point to the WebServer URL
3. Add it to your load balancer with **sticky sessions enabled** (cookie-based affinity)

### Scaling SQL Server

SQL Server is the persistent storage layer. It does not scale horizontally in the same way — instead:

- **Scale up**: Use a larger SQL Server machine (more CPU, RAM, faster storage)
- **Azure SQL**: Use Azure SQL Database with auto-scaling
- **Read replicas**: For reporting workloads, add read replicas (the SiloHost cluster handles all writes)

The database is primarily write-heavy during clinical data entry and read-heavy during patient lookups. Ensure fast storage (SSD/NVMe) for the SQL Server data files.

---

## Network Architecture

### Ports

| Service | Port | Protocol | Who Connects |
|---------|------|----------|--------------|
| SiloHost (silo-to-silo) | 11111 | TCP | Other SiloHosts only |
| SiloHost (gateway) | 30000 | TCP | WebServer, BlazorWeb, WPF clients |
| WebServer | 5298 / 7127 | HTTP / HTTPS | BlazorWeb, PatientPortal, external apps, load balancer |
| BlazorWeb | 5196 / 7137 | HTTP / HTTPS | End-user browsers via load balancer |
| Orleans Dashboard | 8080 | HTTP | Administrators (dev/staging only) |
| SQL Server | 1433 | TCP | SiloHosts, WebServers |

### Firewall Rules

**SiloHost machines** need:
- Inbound 11111/TCP from other SiloHosts
- Inbound 30000/TCP from WebServers, BlazorWeb, and WPF client networks
- Outbound 1433/TCP to SQL Server

**WebServer machines** need:
- Inbound 443/TCP (HTTPS) from load balancer
- Outbound 30000/TCP to SiloHost gateway
- Outbound 1433/TCP to SQL Server (for Identity/auth database)

**BlazorWeb machines** need:
- Inbound 443/TCP (HTTPS) from load balancer
- Outbound to WebServer HTTP port

**SQL Server** needs:
- Inbound 1433/TCP from SiloHosts and WebServers only
- No public internet access (ever)

### Hospital Facility Connectivity

Hospital workstations connect to the SiloHost cluster via Site-to-Site VPN or Azure ExpressRoute. The hospital's router maintains the VPN tunnel — individual workstations do not need VPN client software.

```
Hospital Campus A  ──S2S VPN──┐
Hospital Campus B  ──S2S VPN──┼──▶ Azure VNet ──▶ SiloHost Cluster
Satellite Clinic   ──S2S VPN──┘
Remote Provider    ──P2S VPN──▶ (individual VPN client on their device)
```

For deployment commands and container configuration, see [AZURE_DEPLOY.md](AZURE_DEPLOY.md).

---

## Monitoring

### Orleans Dashboard (Development/Staging Only)

Available at `http://<silohost>:8080` when running in Development mode. Shows:
- Active silos in the cluster
- Data distribution across silos
- Processing throughput and latency
- Memory usage per silo

**Do not expose this in production** — it has no authentication.

### Logging

All components log to standard .NET logging infrastructure. Configure log levels in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Orleans": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

In production, route logs to a centralized logging system (Azure Monitor, ELK, Splunk, etc.).

Key log categories to watch:
- `Orleans.Runtime` — cluster membership changes (silo join/leave)
- `Orleans.Storage` — database read/write errors
- `Microsoft.AspNetCore.Authentication` — login failures
- `DatabaseInitializer` — schema creation on startup

### Health Checks

**SiloHost**: A healthy silo registers heartbeats in the SQL Server clustering table. If heartbeats stop, the silo is declared dead by peers after a timeout period.

**WebServer / BlazorWeb**: Standard ASP.NET Core health check endpoints. Configure your load balancer to poll these and remove unhealthy instances.

---

## Backup and Recovery

### What to Back Up

| Item | Frequency | Method |
|------|-----------|--------|
| SQL Server database (`NewVistasDB`) | Continuous / every 15 min | SQL Server backup, Azure SQL auto-backup |
| `appsettings.json` / environment config | On change | Source control or configuration management |
| TLS certificates | On renewal | Azure Key Vault (automatic) |

### What Does NOT Need Backup

- SiloHost machines — they are stateless once the database is backed up. All in-memory data is recoverable from SQL Server.
- WebServer / BlazorWeb machines — completely stateless. Rebuild from deployment artifacts.

### Disaster Recovery

1. **SQL Server is the single source of truth.** If you can restore the database, you can rebuild the entire system from scratch.
2. Deploy SiloHost(s), point them at the restored database, start them. All data reloads on demand.
3. Deploy WebServer and BlazorWeb. They reconnect to the SiloHost cluster automatically.

**Recovery Time Objective (RTO)**: Depends primarily on SQL Server restore time + application deployment time. The SiloHost cluster itself starts in seconds.

### Encryption at Rest (Required for Production)

Grain state is stored as Orleans **binary serialization** in SQL Server. This is
**not encryption** — the serialization format is public, and string fields
(names, SSNs, diagnoses) are stored as length-prefixed UTF-8 readable in a hex
dump of the table without any application assemblies. Do not represent the
binary format as a security control.

For HIPAA encryption at rest:

1. **Enable SQL Server Transparent Data Encryption (TDE)** on `NewVistasDB`
   (SQL Server Standard 2019+ or Azure SQL, where it is on by default). TDE
   encrypts data files, log files, and backups and is completely transparent
   to the Orleans ADO.NET provider — no application changes.
2. **Encrypt backups** (`BACKUP ... WITH ENCRYPTION` if not using TDE-covered
   backups) and protect the certificate/key material separately from the
   backups themselves.
3. **SQL Express demo mode does not support TDE.** Demo machines using
   `--use-sqlexpress` must rely on full-disk encryption (BitLocker). This is
   acceptable for demos only — never for production PHI.

### Rolling Restart (Zero Downtime)

To update the SiloHost cluster without downtime:

1. Restart silos **one at a time**, waiting for each to rejoin the cluster before restarting the next
2. The cluster redistributes work away from the departing silo and back to it when it returns
3. Never restart all silos simultaneously — this causes a full outage

For WebServer and BlazorWeb, simply deploy new instances and drain old ones through the load balancer.

### Database Maintenance

- **Index maintenance**: Run standard SQL Server index rebuild/reorganize on a weekly schedule during off-hours
- **Statistics update**: SQL Server auto-update is usually sufficient; force update after bulk data imports
- **Table growth**: The `OrleansStorage` table grows with the number of active data records. Monitor disk space.

### Resetting Demo Data

In development/staging with SQL Express:
1. Stop all SiloHosts
2. Drop the `NewVistasDB` database
3. Restart the SiloHost — it recreates the database and schema automatically
4. Re-import test data as needed (see [START.md](START.md))

---

## Troubleshooting

### SiloHost Won't Start

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| "Connection string not found" | Missing `OrleansDatabase` or `SqlExpress` config | Check `appsettings.json` or environment variables |
| SQL connection timeout | SQL Server unreachable | Check network, firewall port 1433, SQL Server service running |
| "Silo port already in use" | Another silo on same machine using same port | Change `Orleans:SiloPort` in config |
| Schema errors on startup | Corrupt or missing Orleans tables | Drop and recreate the database (SiloHost rebuilds schema on start) |

### Cluster Issues

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Silos don't see each other | Different connection strings or network isolation | Verify all silos point to the same SQL Server database |
| Silo marked dead but is running | Clock skew or network partition | Synchronize clocks (NTP); check network between silos |
| Slow data access after silo restart | Data reloading from SQL Server into memory | Normal — performance improves as data warms up in memory |

### WebServer / BlazorWeb Issues

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| "No silos available" | SiloHost cluster not running or unreachable | Start SiloHost first; check gateway port 30000 connectivity |
| Login failures | Identity database not created | WebServer creates it on first start; check SQL connection |
| BlazorWeb disconnects | SignalR WebSocket dropped | Check load balancer sticky sessions; increase timeout |
