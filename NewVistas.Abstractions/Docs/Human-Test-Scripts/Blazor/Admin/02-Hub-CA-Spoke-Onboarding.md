# Hub-CA Spoke Onboarding -- Administrator Human Test Script

**Purpose:** Walk through the end-to-end onboarding of a new spoke cluster:
the Hub admin issues a one-time provisioning token, the spoke generates a CSR
and exchanges the token for a signed certificate, and the certificate is
verified to chain to the Hub-CA root and carry the correct extended key usage
(client auth + matching cluster ID CN).

This script tests the workflow in commit **90d32551** (Hub-CA) and the
provisioning-token grain wiring in commit **6f9e7a8d**.

---

## Prerequisites

- **Login (for token issuance):** `ADMIN1` / Password: `smythVista1`
- **Pre-conditions:**
  1. Two-silo Hub + Spoke environment from [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md) is running.
  2. Hub-CA root cert exists at `C:\NewVistas-Federation\Hub\Certs\hub-ca.crt`.
  3. `openssl` on PATH.
  4. PowerShell session with cleaned variables: `Remove-Variable jwt, token, csr, cert -ErrorAction SilentlyContinue`

---

## Part A: Issue Provisioning Token

### Scenario 1: Authenticate as Administrator

### Steps

1. Request a JWT for `ADMIN1`:
   ```powershell
   $login = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "ADMIN1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $jwt = $login.token
   $jwt.Substring(0, 24) + "..."
   ```

### Expected Result

- `$login.token` is a non-empty JWT (3 base64url segments separated by dots).
- The truncated print shows the header prefix `eyJhbGciOi...` characteristic of a JWT.

---

### Scenario 2: Issue a Provisioning Token

### Steps

1. Request a token for the target spoke cluster ID:
   ```powershell
   $tokenResponse = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/federation/admin/provisioning-token `
     -Headers @{ Authorization = "Bearer $jwt" } `
     -Body (@{ clusterId = "SPOKE-TEST-1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $tokenResponse | Format-List
   $token = $tokenResponse.token
   ```

### Expected Result

- Response object includes:
  - `token` (non-empty string, opaque or JWT)
  - `clusterId = "SPOKE-TEST-1"`
  - `expiresUtc` ~24 hours from now (matches `Federation:HubCa:ProvisioningTokenValidityHours`)
  - `issuedUtc` ~now
- Token appears on the Federation Dashboard's Provisioning tokens panel within 30s, status `active`.

### Scenario 3: Issue Token Without Admin Role -- Rejected

### Steps

1. Login as `DOCTOR1` and obtain their JWT:
   ```powershell
   $docLogin = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/auth/login `
     -Body (@{ username = "DOCTOR1"; password = "smythVista1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $docJwt = $docLogin.token
   ```
2. Attempt to issue a token:
   ```powershell
   try {
     Invoke-RestMethod -Method Post `
       -Uri https://localhost:7127/api/federation/admin/provisioning-token `
       -Headers @{ Authorization = "Bearer $docJwt" } `
       -Body (@{ clusterId = "SPOKE-EVIL" } | ConvertTo-Json) `
       -ContentType "application/json"
   } catch {
     Write-Host "Status: $($_.Exception.Response.StatusCode.value__)"
   }
   ```

### Expected Result

- HTTP status **403 Forbidden** (or 401 if the auth pipeline rejects earlier).
- No token issued; nothing new on the Federation Dashboard panel.

---

## Part B: Spoke Generates CSR

### Scenario 4: Generate Private Key + CSR with Matching CN

### Steps

1. From PowerShell:
   ```powershell
   cd C:\NewVistas-Federation\Spoke\Certs
   openssl req -new -nodes -newkey rsa:3072 `
     -keyout spoke.key -out spoke.csr `
     -subj "/CN=SPOKE-TEST-1/O=NewVistas Test/C=US"
   ```
2. Inspect the CSR:
   ```powershell
   openssl req -in spoke.csr -noout -text | Select-String "Subject:|Public Key:"
   ```

### Expected Result

- Two files: `spoke.key` (PEM private key, RSA-3072), `spoke.csr` (PEM CSR).
- CSR Subject: `CN = SPOKE-TEST-1, O = NewVistas Test, C = US`.
- Public Key: 3072-bit RSA.

### Scenario 5: Reject CSR Below Minimum Key Strength

### Steps

1. Generate an intentionally-weak CSR:
   ```powershell
   openssl req -new -nodes -newkey rsa:1024 `
     -keyout spoke-weak.key -out spoke-weak.csr `
     -subj "/CN=SPOKE-TEST-1/O=NewVistas Test/C=US"
   ```
2. Read the CSR PEM into a string and submit it (see Scenario 6 for the `Invoke-RestMethod` shape -- substitute `spoke-weak.csr`).

### Expected Result

- Hub returns HTTP **400 Bad Request** with body indicating the key strength is insufficient.
- Cross-ref: `HubCaIssuanceTests.IssueCertificate_RejectsTooWeakKey`.

---

## Part C: Exchange Token for Signed Certificate

### Scenario 6: Submit CSR with Bearer Token

### Steps

1. Read CSR into a variable:
   ```powershell
   $csr = Get-Content -Raw C:\NewVistas-Federation\Spoke\Certs\spoke.csr
   ```
2. POST the CSR with the provisioning token in the Authorization header:
   ```powershell
   $signResponse = Invoke-RestMethod -Method Post `
     -Uri https://localhost:7127/api/federation/csr `
     -Headers @{ Authorization = "Bearer $token" } `
     -Body (@{ csr = $csr; clusterId = "SPOKE-TEST-1" } | ConvertTo-Json) `
     -ContentType "application/json"
   $signResponse | Format-List
   ```
3. Save the issued cert and the chain root:
   ```powershell
   $signResponse.certificatePem | Set-Content C:\NewVistas-Federation\Spoke\Certs\spoke.crt
   $signResponse.rootCertificatePem | Set-Content C:\NewVistas-Federation\Spoke\Certs\hub-ca.crt
   ```

### Expected Result

- Response includes:
  - `certificatePem` -- signed cert (PEM, `-----BEGIN CERTIFICATE-----`)
  - `rootCertificatePem` -- the Hub-CA root (matches `hub-ca.crt`)
  - `serialNumber`, `notBeforeUtc`, `notAfterUtc`
- `notAfterUtc - notBeforeUtc` = 365 days (matches `Federation:HubCa:IssuedCertValidityDays`).
- Cross-ref: `HubCaIssuanceTests.IssueCertificate_ReturnsCertChainedToRoot`.

### Scenario 7: Verify Cert Chains to Hub-CA

### Steps

1. Verify chain:
   ```powershell
   openssl verify -CAfile C:\NewVistas-Federation\Spoke\Certs\hub-ca.crt `
     C:\NewVistas-Federation\Spoke\Certs\spoke.crt
   ```
2. Inspect the issued cert:
   ```powershell
   openssl x509 -in C:\NewVistas-Federation\Spoke\Certs\spoke.crt -noout -text |
     Select-String "Subject:|Issuer:|Not Before|Not After|TLS Web Client Authentication|X509v3 Extended Key Usage"
   ```

### Expected Result

- `openssl verify` prints `spoke.crt: OK`.
- Subject: `CN = SPOKE-TEST-1, O = NewVistas Test, C = US`.
- Issuer: `CN = NewVistas Hub CA, O = NewVistas Test, C = US`.
- X509v3 Extended Key Usage includes `TLS Web Client Authentication`.
- Cross-ref: `HubCaIssuanceTests.IssueCertificate_AppliesClientAuthEku`.

### Scenario 8: Token Cannot Be Reused

### Steps

1. Attempt the same `Invoke-RestMethod` from Scenario 6 a **second** time using the same `$token`.

### Expected Result

- Hub returns HTTP **409 Conflict** (or 400) with body indicating the token has been consumed.
- Federation Dashboard now shows the token's status as `consumed`.
- Cross-ref: `HubCaIssuanceTests.TokenGrain_DoubleConsume_Throws`.

### Scenario 9: Expired Token Rejected

### Steps

1. Issue a token with a 24h validity (Scenario 2). For testing, edit the Hub config to set `ProvisioningTokenValidityHours: 0` (treat as immediately expired) **OR** wait long enough for natural expiry. (Recommended: temporarily set the validity to a very short value such as `0.001` if the deployment supports fractional hours; otherwise use the unit test as the authoritative coverage.)
2. Try to use the expired token to sign a CSR.

### Expected Result

- Hub returns HTTP **401** or **400** with body indicating the token has expired.
- Cross-ref: `HubCaIssuanceTests.TokenGrain_ConsumeAfterExpiry_Throws`.

### Scenario 10: Cluster ID Mismatch Rejected

### Steps

1. Issue a fresh token for `clusterId = "SPOKE-TEST-1"` (Scenario 2).
2. Generate a CSR with `CN = SPOKE-TEST-2` (mismatched).
3. Submit the CSR with the SPOKE-TEST-1 token.

### Expected Result

- Hub returns HTTP **400 Bad Request** with body explaining the CSR's CN must match the token's clusterId.

---

## Part D: Install Cert on Spoke

### Scenario 11: Bundle Cert + Key into PFX for Spoke Use

### Steps

1. Combine cert + key into a PFX:
   ```powershell
   cd C:\NewVistas-Federation\Spoke\Certs
   openssl pkcs12 -export -out spoke.pfx `
     -inkey spoke.key -in spoke.crt -certfile hub-ca.crt `
     -passout pass:
   ```
2. Restart the Spoke silo (`Ctrl+C` then `dotnet run --project NewVistas.SiloHost ...` again per [00-Federation-Test-Environment.md](00-Federation-Test-Environment.md)).

### Expected Result

- `spoke.pfx` exists (~6 KB).
- Spoke silo log shows:
  - `Loaded federation certificate: subject=CN=SPOKE-TEST-1, expires=<date>, days remaining=365`
  - No `Failed to load federation certificate` errors.

### Scenario 12: Spoke Successfully Calls Hub-Authenticated Endpoint

### Steps

1. From the Spoke side (or a manual `curl`/`Invoke-RestMethod` using the PFX as a client cert):
   ```powershell
   $clientCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
     "C:\NewVistas-Federation\Spoke\Certs\spoke.pfx", "")
   $req = [System.Net.HttpWebRequest]::Create("https://localhost:7127/api/federation/inbound/ping")
   $req.Method = "GET"
   $req.ClientCertificates.Add($clientCert) | Out-Null
   $resp = $req.GetResponse()
   $resp.StatusCode
   ```

### Expected Result

- HTTP **200 OK** (or **404** if `/inbound/ping` is not implemented; in that case substitute any inbound endpoint -- the key check is that mTLS handshake succeeds without `RemoteCertificateNotAvailable` errors).
- Hub log shows successful client certificate validation with subject `CN=SPOKE-TEST-1`.

---

## Part E: Verification Checklist

- [ ] `ADMIN1` JWT obtained successfully
- [ ] Provisioning token issued with valid expiry & clusterId
- [ ] Non-admin (`DOCTOR1`) cannot issue a provisioning token (403)
- [ ] CSR generated for `SPOKE-TEST-1` with RSA-3072 key
- [ ] Weak (1024-bit) CSR is rejected by Hub
- [ ] CSR signed; cert chains to Hub-CA root via `openssl verify`
- [ ] Issued cert carries `TLS Web Client Authentication` EKU
- [ ] Issued cert validity = 365 days
- [ ] Token cannot be re-used (409 Conflict on second attempt)
- [ ] Expired token cannot be used
- [ ] CSR with mismatched CN is rejected
- [ ] PFX bundle created and Spoke silo loads it without error
- [ ] Spoke can authenticate to Hub via mTLS using the new cert
- [ ] Token status on Federation Dashboard transitions to `consumed`

---

## Cross-References

- Hub-CA implementation: [HubCertificateAuthority.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/HubCertificateAuthority.cs)
- Controller: [HubCaController.cs](../../../../../NewVistas.WebServer/Controllers/HubCaController.cs)
- Auth wiring: [FederationAuthExtensions.cs](../../../../../NewVistas.WebServer/Infrastructure/Federation/FederationAuthExtensions.cs)
- Token grain: [ProvisioningTokenGrain.cs](../../../../NewVistas.Abstractions/Grains/ProvisioningTokenGrain.cs)
- Functional tests:
  - `HubCaIssuanceTests.IssueCertificate_ReturnsCertChainedToRoot`
  - `HubCaIssuanceTests.IssueCertificate_AppliesClientAuthEku`
  - `HubCaIssuanceTests.IssueCertificate_RejectsTooWeakKey`
  - `HubCaIssuanceTests.TokenGrain_IssueThenConsume_Succeeds`
  - `HubCaIssuanceTests.TokenGrain_DoubleConsume_Throws`
  - `HubCaIssuanceTests.TokenGrain_ConsumeAfterExpiry_Throws`
