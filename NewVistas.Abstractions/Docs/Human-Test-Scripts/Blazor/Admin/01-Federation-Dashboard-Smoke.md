# Federation Dashboard Smoke -- Administrator Human Test Script

**Purpose:** Verify the Federation Dashboard at `/admin/federation` renders all
three panels (Outbox health, Provisioning tokens, Revoked certs), is
authorization-gated to Administrators, and reflects backend state in real
time.

This is the lightest of the federation scripts -- it can run on a single silo
and does not require a Spoke. It is included in the Smoke pass.

---

## Prerequisites

- **Login:** `ADMIN1` / Password: `smythVista1` (must hold the `Administrator` role)
- **Pre-conditions:**
  1. SiloHost, WebServer, and BlazorWeb running on the Hub configuration from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md), **or** a single localhost silo with `Federation:HubCa:Enabled = true`.
  2. SQL outbox schema applied (Outbox panel will say "No federation outbox configured" if the SQL connection string is missing -- that is also a valid PASS for a no-outbox deployment).
  3. Browser that accepts the dev HTTPS cert (`dotnet dev-certs https --trust` if needed).

---

## Part A: Authorization

### Scenario 1: Anonymous User Cannot Access

### Steps

1. Open a private/incognito browser window.
2. Navigate to `https://localhost:7137/admin/federation`.

### Expected Result

- Browser redirected to `/login` (or "Access denied" page) -- **not** the federation dashboard.
- No federation panels are rendered.

---

### Scenario 2: Non-Administrator Cannot Access

### Steps

1. Login as `DOCTOR1` / `smythVista1`.
2. Navigate to `https://localhost:7137/admin/federation`.

### Expected Result

- Page returns HTTP 403 / "Access denied" / Blazor `Authorized` failure UI.
- The `[Authorize(Roles = "Administrator")]` attribute on [FederationDashboard.razor](../../../../NewVistas.BlazorWeb/Components/Pages/FederationDashboard.razor) line 2 is enforcing access.

---

### Scenario 3: Administrator Can Access

### Steps

1. Logout, then login as `ADMIN1` / `smythVista1`.
2. Navigate to `https://localhost:7137/admin/federation`.

### Expected Result

- Page title: **"Federation security"**
- Three panels visible:
  - Outbox health
  - Provisioning tokens
  - Revoked certs
- Header: 🔐 Federation security
- Intro text: "Read-only view of cert revocations, provisioning tokens, and outbox health for this cluster. Refreshes every 30 seconds; click a panel's refresh button to update sooner."

---

## Part B: Outbox Health Panel

### Scenario 4: Empty Outbox State

### Steps

1. Truncate the outbox table to start clean:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -d NewVistasHub -Q "TRUNCATE TABLE FederationOutbox"
   ```
2. On the federation dashboard, click **Refresh** in the Outbox health panel.

### Expected Result

- Pending: `0`
- Sent: `0`
- Oldest pending: `--` or empty
- Max attempts on a pending row: `0`
- Last sent: `--` or empty

### Scenario 5: Pending Row Visible After Clinical Activity

### Steps

1. Login as `DOCTOR1`, select any patient, and place a simple lab order via [Blazor/Doctors/06-Laboratory-Orders.md](../Doctors/06-Laboratory-Orders.md) Scenario 1.
2. Switch back to the `ADMIN1` browser tab on `/admin/federation` and click **Refresh** in the Outbox health panel.

### Expected Result

- Pending: `>= 1` (one envelope per emitted clinical event)
- Oldest pending: a recent UTC timestamp
- Max attempts on a pending row: `0` (drainer has not yet attempted)
- Within ~30 seconds (the drainer interval), a subsequent refresh shows Pending decreasing and Sent incrementing if a transport is configured. If transport is `Logging` or `None`, rows remain Pending -- that is correct.

---

## Part C: Provisioning Tokens Panel

### Scenario 6: No Tokens Yet

### Steps

1. On a Hub freshly cleaned of tokens (run `sqlcmd -d NewVistasHub -Q "DELETE FROM ProvisioningTokenIndex"` if you need to reset; otherwise observe).
2. Click **Refresh** in the Provisioning tokens panel.

### Expected Result

- Either "No tokens issued yet." or a table of previously issued tokens.
- If the deployment has Hub-CA disabled, the panel shows "Hub-CA not configured on this deployment."

### Scenario 7: Issue Token, See It Appear

### Steps

1. From PowerShell, request a JWT for ADMIN1 (login API):
   ```powershell
   $login = Invoke-RestMethod -Method Post -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $jwt = $login.token
   ```
2. Issue a provisioning token:
   ```powershell
   Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/federation/admin/provisioning-token `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{ clusterId = "SPOKE-DEMO-1" } | ConvertTo-Json) `
     -ContentType "application/json"
   ```
3. Click **Refresh** in the Provisioning tokens panel.

### Expected Result

- API response includes `token` (JWT or opaque), `expiresUtc` (~24 hours from now), `clusterId = SPOKE-DEMO-1`.
- Dashboard table now contains a row:
  - Issued: recent timestamp
  - Cluster: `SPOKE-DEMO-1`
  - Token (prefix): first ~12 chars only, in monospace
  - Expires: ~24 hours from now
  - Status: `active`

### Scenario 8: Token Status Reflects Lifecycle

### Steps

1. Repeat Scenario 7 to issue a second token for `SPOKE-DEMO-2`.
2. Use that token to onboard the spoke (see [02-Hub-CA-Spoke-Onboarding.md](02-Hub-CA-Spoke-Onboarding.md) Scenario 3).
3. Refresh the panel.

### Expected Result

- The second row's Status changes from `active` to `consumed` after CSR signing.
- The first (unconsumed) token remains `active` until its expiry, after which it shows `expired`.
- Expired and consumed tokens render with visually-distinct styling (CSS class `status-expired` / `status-consumed`).

---

## Part D: Revoked Certs Panel

### Scenario 9: No Revocations Yet

### Steps

1. Click **Refresh** in the Revoked certs panel.

### Expected Result

- Either "No certs revoked." or a table of previous revocations.
- Hub-CA-disabled deployments show "Revocation tracking not enabled."

### Scenario 10: Revocation Visible After Admin Action

### Steps

1. Complete [04-Certificate-Revocation.md](04-Certificate-Revocation.md) Scenario 2 (revoke a cert via the API).
2. Refresh this panel.

### Expected Result

- New row appears with:
  - Thumbprint (full 40-char SHA1 in monospace, or first 16 chars elided)
  - Cluster ID
  - Revoked at: recent timestamp
  - Reason: as supplied (e.g., `KeyCompromise`)

---

## Part E: Auto-Refresh Behavior

### Scenario 11: Page Auto-Refreshes Every 30s

### Steps

1. Note the current value in any panel.
2. Trigger a backend change (e.g., issue another provisioning token) but **do not** click any panel's Refresh button.
3. Wait up to 30 seconds.

### Expected Result

- Panels update without manual refresh, within ~30 seconds.
- Browser network tab shows periodic SignalR / Blazor Server messages at the refresh interval.

---

## Part F: Verification Checklist

- [ ] Anonymous browser cannot reach `/admin/federation`
- [ ] `DOCTOR1` (non-admin) cannot reach `/admin/federation`
- [ ] `ADMIN1` reaches the dashboard and sees three panels
- [ ] Outbox panel shows correct empty/non-empty state
- [ ] Outbox Pending count rises when clinical events are appended
- [ ] Provisioning Tokens panel shows "Hub-CA not configured" on a spoke
- [ ] Issued provisioning token appears in the table within 30s of issuance
- [ ] Token status transitions `active` → `consumed` after CSR signing
- [ ] Token status transitions `active` → `expired` after expiry
- [ ] Revocations appear after admin revokes a cert
- [ ] Page auto-refreshes within 30 seconds of a backend change
- [ ] Each panel's manual Refresh button works and shows "Refreshing…" while loading

---

## Cross-References

- Page source: [FederationDashboard.razor](../../../../NewVistas.BlazorWeb/Components/Pages/FederationDashboard.razor)
- Backing API: [FederationAdminController.cs](../../../../NewVistas.WebServer/Controllers/FederationAdminController.cs)
- Functional tests:
  - `FederationDashboardTests.TokenIndex_AddThenList_ContainsEntry`
  - `FederationDashboardTests.TokenIndex_MarkConsumed_UpdatesEntry`
  - `FederationDashboardTests.TokenIndex_GetAll_OrdersByIssuedDescending`
  - `FederationDashboardTests.StatsGrain_NoOutboxConfigured_ReturnsNotAvailable`
- Grain interfaces: [IFederationStatsGrain.cs](../../../../NewVistas.Abstractions/GrainInterfaces/IFederationStatsGrain.cs), [IProvisioningTokenIndexGrain.cs](../../../../NewVistas.Abstractions/GrainInterfaces/IProvisioningTokenIndexGrain.cs)
