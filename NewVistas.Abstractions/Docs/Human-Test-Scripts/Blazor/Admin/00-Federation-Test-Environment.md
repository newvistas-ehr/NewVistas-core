# Federation Test Environment -- Administrator Setup Script

**Purpose:** Stand up a two-silo "Hub + Spoke" deployment on one workstation so the
remaining Admin scripts (01-08) can exercise federation, certificate authority,
revocation, sneakernet, and multi-cluster behaviors end-to-end.

This script is a **one-time setup** -- once the environment is configured and
captured in the checklist at the bottom, you can re-run any of scripts 01-08
without repeating it.

---

## Prerequisites

- **Login (Blazor):** `ADMIN1` / Password: `smythVista1`
- **OS:** Windows 10/11 with PowerShell 5.1 or later
- **Tools:**
  - .NET 10 SDK installed
  - SQL Server Express (LocalDB acceptable) running
  - `openssl` on PATH (Git for Windows ships it under `C:\Program Files\Git\usr\bin\openssl.exe`)
  - At least 8 GB free RAM (two silos + two web servers + two Blazor)
- **Repository:** Cloned to `C:\Source\NewVistas-Hub` AND `C:\Source\NewVistas-Spoke` (two working copies of the same branch).

> Why two working copies? Each silo needs its own configuration files, cert
> directory, and outbound/inbound bundle directories. Cloning twice is the
> simplest way to keep the two cleanly separated.

---

## Part A: Directory Layout

### Scenario 1: Create Working Directories

### Steps

1. Open PowerShell **as Administrator** and run:
   ```powershell
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Hub\Certs
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Hub\Outbound
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Hub\Inbound
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Hub\Processed
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Spoke\Certs
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Spoke\Outbound
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Spoke\Inbound
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Spoke\Processed
   New-Item -ItemType Directory -Force -Path C:\NewVistas-Federation\Transfer
   ```
2. Verify the structure:
   ```powershell
   Get-ChildItem C:\NewVistas-Federation -Recurse -Directory | Select-Object FullName
   ```

### Expected Result

- 9 directories created with no errors.
- The `Transfer` directory simulates removable media for the Sneakernet test
  (Admin/05).

---

## Part B: Hub-CA Root Certificate

### Scenario 2: Generate Hub-CA Root

### Steps

1. From PowerShell, generate a self-signed Hub-CA root cert valid 5 years:
   ```powershell
   cd C:\NewVistas-Federation\Hub\Certs
   openssl req -x509 -nodes -newkey rsa:4096 -days 1825 `
     -keyout hub-ca.key -out hub-ca.crt `
     -subj "/CN=NewVistas Hub CA/O=NewVistas Test/C=US"
   ```
2. Confirm both files exist:
   ```powershell
   Get-ChildItem C:\NewVistas-Federation\Hub\Certs
   ```
3. Verify the cert details:
   ```powershell
   openssl x509 -in C:\NewVistas-Federation\Hub\Certs\hub-ca.crt -noout -text | Select-String "Subject:|Not After"
   ```

### Expected Result

- Two files: `hub-ca.crt` (PEM cert), `hub-ca.key` (PEM private key).
- Cert subject = `CN = NewVistas Hub CA, O = NewVistas Test, C = US`.
- "Not After" is ~5 years from today.
- **The `hub-ca.key` file is sensitive -- protect with NTFS ACLs in production.** For testing on a single workstation, default ACLs are acceptable.

---

## Part C: Hub Silo Configuration

### Scenario 3: Configure Hub `appsettings.Development.json`

### Steps

1. Edit `C:\Source\NewVistas-Hub\NewVistas.SiloHost\appsettings.Development.json` and add:
   ```json
   {
     "ClusterIdentity": {
       "ClusterId": "HUB-PRIMARY"
     },
     "Federation": {
       "HubCa": {
         "Enabled": true,
         "RootCertPath": "C:\\NewVistas-Federation\\Hub\\Certs\\hub-ca.crt",
         "RootKeyPath": "C:\\NewVistas-Federation\\Hub\\Certs\\hub-ca.key",
         "IssuedCertValidityDays": 365,
         "ProvisioningTokenValidityHours": 24
       },
       "Inbound": {
         "TrustedCaPath": "C:\\NewVistas-Federation\\Hub\\Certs\\hub-ca.crt",
         "AllowedClusterIds": [ "SPOKE-TEST-1", "SPOKE-TEST-2" ],
         "RequireMatchingClusterId": true
       },
       "Revocation": {
         "Enabled": true,
         "CacheTtlSeconds": 60
       },
       "Outbox": {
         "Enabled": true,
         "CheckIntervalSeconds": 15,
         "MaxRetries": 5,
         "RetryDelaySeconds": 30
       },
       "FileBundle": {
         "OutboundDirectory": "C:\\NewVistas-Federation\\Hub\\Outbound",
         "InboundDirectory":  "C:\\NewVistas-Federation\\Hub\\Inbound",
         "ProcessedDirectory":"C:\\NewVistas-Federation\\Hub\\Processed",
         "ScanIntervalSeconds": 15
       }
     }
   }
   ```
2. Edit the same file's `Kestrel` section (or matching `WebServer` config) so the **Hub WebServer** uses HTTPS port **7127** and HTTP port **5298** (default).
3. Save and close.

### Expected Result

- File parses as valid JSON (`Get-Content ... | ConvertFrom-Json` returns no error).
- `ClusterId` = `HUB-PRIMARY`.

---

## Part D: Spoke Silo Configuration

### Scenario 4: Configure Spoke `appsettings.Development.json`

### Steps

1. Edit `C:\Source\NewVistas-Spoke\NewVistas.SiloHost\appsettings.Development.json` and add:
   ```json
   {
     "ClusterIdentity": {
       "ClusterId": "SPOKE-TEST-1"
     },
     "Federation": {
       "Renewal": {
         "Enabled": true,
         "CheckIntervalHours": 1,
         "RenewBeforeDaysExpire": 350,
         "HubUrl": "https://localhost:7127",
         "CertificatePath": "C:\\NewVistas-Federation\\Spoke\\Certs\\spoke.pfx",
         "BackupDirectory": "C:\\NewVistas-Federation\\Spoke\\Certs\\Backup"
       },
       "FileBundle": {
         "OutboundDirectory": "C:\\NewVistas-Federation\\Spoke\\Outbound",
         "InboundDirectory":  "C:\\NewVistas-Federation\\Spoke\\Inbound",
         "ProcessedDirectory":"C:\\NewVistas-Federation\\Spoke\\Processed",
         "ScanIntervalSeconds": 15
       },
       "Outbox": {
         "Enabled": true,
         "CheckIntervalSeconds": 15
       },
       "Transport": {
         "Type": "Http",
         "HubUrl": "https://localhost:7127/api/federation/inbound"
       }
     }
   }
   ```
2. Edit the Spoke's `Kestrel`/`WebServer` config so its **WebServer** uses HTTPS port **8127** and HTTP port **6298** (offset from the hub by +1000).
3. Edit the Spoke's `BlazorWeb` Kestrel config so it uses HTTPS port **8137** (offset from hub +1000).
4. Edit the Spoke's `Orleans` clustering port to **11112** and gateway to **30001** (so it does not collide with the hub).
5. Save and close.

### Expected Result

- File parses as valid JSON.
- Spoke ports do not collide with Hub ports.
- `RenewBeforeDaysExpire = 350` is intentional for testing -- it ensures the
  renewal service will trigger almost immediately on a 365-day cert (used
  in Admin/03).

---

## Part E: Database Provisioning (SQL Express)

### Scenario 5: Create Outbox Schema for Both Silos

### Steps

1. Connect to SQL Express and create two databases:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -Q "CREATE DATABASE NewVistasHub"
   sqlcmd -S .\SQLEXPRESS -Q "CREATE DATABASE NewVistasSpoke"
   ```
2. Apply the federation outbox schema to each:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasHub   -i C:\Source\NewVistas-Hub\NewVistas.SiloHost\Sql\FederationOutboxSchema.sql
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -i C:\Source\NewVistas-Spoke\NewVistas.SiloHost\Sql\FederationOutboxSchema.sql
   ```
3. Verify the table exists in each database:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasHub   -Q "SELECT TOP 1 name FROM sys.tables WHERE name = 'FederationOutbox'"
   sqlcmd -S .\SQLEXPRESS -d NewVistasSpoke -Q "SELECT TOP 1 name FROM sys.tables WHERE name = 'FederationOutbox'"
   ```

### Expected Result

- Both `CREATE DATABASE` commands succeed (or report "database already exists" and continue).
- Schema script completes with no errors.
- Both `SELECT TOP 1` queries return the table name `FederationOutbox`.

---

## Part F: Start Both Silo Stacks

### Scenario 6: Boot Hub Stack

### Steps

1. In a fresh PowerShell window:
   ```powershell
   cd C:\Source\NewVistas-Hub
   dotnet run --project NewVistas.SiloHost -- --use-sqlexpress --profile RemoteOnline
   ```
2. Wait until the silo log emits `Application started. Press Ctrl+C to shut down.`
3. In a second PowerShell window:
   ```powershell
   cd C:\Source\NewVistas-Hub
   dotnet run --project NewVistas.WebServer
   ```
4. In a third PowerShell window:
   ```powershell
   cd C:\Source\NewVistas-Hub
   dotnet run --project NewVistas.BlazorWeb
   ```

### Expected Result

- Silo log includes `ClusterId: HUB-PRIMARY` (or equivalent log line from the
  `IClusterIdentity` registration -- see [StaticClusterIdentity.cs](../../../../NewVistas.Abstractions/Federation/StaticClusterIdentity.cs)).
- WebServer log shows `Now listening on: https://localhost:7127`.
- BlazorWeb log shows `Now listening on: https://localhost:7137`.
- Browse to `https://localhost:7137/admin/federation` and login as `ADMIN1`. Page renders without errors (panels may show "Loading..." until first refresh -- that is expected).

### Scenario 7: Boot Spoke Stack

### Steps

1. Three more PowerShell windows, repeating the same pattern but from `C:\Source\NewVistas-Spoke` and on the +1000 ports.
2. Verify the Spoke silo log shows `ClusterId: SPOKE-TEST-1`.

### Expected Result

- All three Spoke processes start without port conflicts.
- Browse `https://localhost:8137/admin/federation` -- page loads. The "Provisioning tokens" panel says "Hub-CA not configured on this deployment." (correct -- spoke has no Hub-CA).

---

## Part G: Verification Checklist

- [ ] Directory layout under `C:\NewVistas-Federation\` matches Scenario 1
- [ ] Hub-CA root cert + key present in `C:\NewVistas-Federation\Hub\Certs\`
- [ ] Hub `appsettings.Development.json` has `ClusterId = HUB-PRIMARY`
- [ ] Hub `appsettings.Development.json` has `Federation:HubCa:Enabled = true`
- [ ] Spoke `appsettings.Development.json` has `ClusterId = SPOKE-TEST-1`
- [ ] Spoke `Federation:HubCa` block is **absent** (spokes are not CAs)
- [ ] Spoke `Federation:Renewal:HubUrl` points to `https://localhost:7127`
- [ ] SQL `FederationOutbox` table exists in both `NewVistasHub` and `NewVistasSpoke`
- [ ] Hub silo log shows `ClusterId: HUB-PRIMARY`
- [ ] Spoke silo log shows `ClusterId: SPOKE-TEST-1`
- [ ] Hub Federation Dashboard accessible at `https://localhost:7137/admin/federation`
- [ ] Spoke Federation Dashboard accessible at `https://localhost:8137/admin/federation`
- [ ] Orleans Dashboard for Hub accessible at `http://localhost:8080`

---

## Cross-References

- Site profile resolver: [SiteProfileResolverTests.cs](../../../../../NewVistas.UnitTests/SiteProfileResolverTests.cs) -- `Resolve_DefaultsToLocalhostDev_InDevelopment`, `Resolve_EnvironmentVariable_PicksRemoteOnline`
- Cluster identity: [StaticClusterIdentity.cs](../../../../NewVistas.Abstractions/Federation/StaticClusterIdentity.cs)
- Outbox schema: [FederationOutboxSchema.sql](../../../../../NewVistas.SiloHost/Sql/FederationOutboxSchema.sql)
- Hub-CA wiring: [HubCaExtensions.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/HubCaExtensions.cs)
- Inbound auth options: [InboundAuthOptions.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/InboundAuthOptions.cs)
