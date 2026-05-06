# Login & Patient Selection -- Pharmacist CharUI Human Test Script

## Prerequisites

- **Login:** PHARM1 / Password: `smythVista1`
- **Security Keys on file:** PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Pre-conditions:**
  1. The SiloHost and WebServer must be running.
  2. Demo users must be seeded (automatic on SiloHost startup).
  3. Security keys must be loaded: `POST /api/accesscontrol/demo/load` (requires any authenticated user).
  4. Demo patients must exist (patients 1-50 from Fifty dataset).
  5. Launch the CharUI console application: `dotnet run --project NewVistas.CharUI`

---

## Scenario 1: Successful Login (Happy Path)

### Steps

1. The terminal displays:
   ```
   NEWVISTAS CLINICAL INFORMATION SYSTEM
   VistA-Style Character User Interface
   ```
2. At the `Access Code` prompt, type: `PHARM1`
3. At the `Verify Code` prompt, type: `smythVista1` (characters display as asterisks)

### Expected Result

- The terminal displays:
  ```
  Good [morning/afternoon/evening], PHARM1
  You last signed on [date/time]
  4 security key(s) on file.
  ```
- The 4 keys are: PSO PHARMACY, PSJ RPHARM, PSA ORDERS, PSB MANAGER
- **Note:** Pharmacists do NOT hold PROVIDER, ORES, ORELSE, TIU SIGN, or TIU COSIGN keys. This limits order entry/signing, note writing, and encounter creation in the CharUI.
- The Main Menu appears.

---

## Scenario 2: Failed Login (3-Attempt Lockout)

### Steps

1. Restart CharUI.
2. Enter incorrect credentials 3 times:
   - Attempt 1: Access Code: `PHARM1`, Verify Code: `wrong1`
   - Attempt 2: Access Code: `PHARM1`, Verify Code: `wrong2`
   - Attempt 3: Access Code: `PHARM1`, Verify Code: `wrong3`

### Expected Result

- After each failure: remaining attempts shown.
- After 3rd failure: lockout message, application exits.

---

## Scenario 3: Login with Alternative Pharmacist Accounts

### Steps

Test login for each pharmacist account:

| Access Code | Display Name | Specialty |
|-------------|-------------|-----------|
| PHARM1 | CHEN,DAVID L | Clinical Pharmacy |
| PHARM2 | WILLIAMS,SARAH K | Oncology Pharmacy |
| PHARM3 | MARTINEZ,CARLOS R | Ambulatory Pharmacy |
| PHARM4 | KUMAR,PRIYA S | Inpatient Pharmacy |
| PHARM5 | O'BRIEN,MICHAEL T | Psychiatric Pharmacy |

All use password: `smythVista1`

### Expected Result

- Each pharmacist logs in successfully with 4 security keys on file.

---

## Scenario 4: Select a Patient

### Steps

1. After login, at the Main Menu, type: `SP` and press Enter.
2. At the prompt `Select PATIENT NAME (or ^ to cancel)`, type a patient name (e.g., `SMITH`).
3. Select a patient from the numbered list.

### Expected Result

- Patient selected successfully.
- Cover sheet loads.
- Main Menu reappears with patient banner.

---

## Scenario 5: Verify Pharmacist Security Key Limitations

### Steps

1. After login and patient selection, attempt these restricted operations:

| Action | Menu | Option | Expected Result |
|--------|------|--------|-----------------|
| Place Order | OR | 3 | `You do not hold the ORES or ORELSE key. Order entry is not permitted.` |
| Sign Order | OR | 4 | `You do not hold the ORES key. Signing is not permitted.` |
| Discontinue Order | OR | 5 | `You do not hold the ORES key. DC is not permitted.` |
| Write Note | NO | 3 | `You do not hold the PROVIDER key. Note entry is not permitted.` |
| Sign Note | NO | 4 | `You do not hold the TIU SIGN key.` |
| Cosign Note | NO | 5 | `You do not hold the TIU COSIGN key.` |
| Add Addendum | NO | 6 | `You do not hold the PROVIDER key.` |
| Amend Note | NO | 7 | `You do not hold the PROVIDER key.` |
| Create D/C Summary | DC | 3 | `You do not hold the PROVIDER key.` |
| Sign D/C Summary | DC | 4 | `You do not hold the TIU SIGN key.` |
| Create Encounter | EN | 3 | `You do not hold the PROVIDER key.` |

### Expected Result

- Each restricted action shows the appropriate access denied message.
- No data is modified.

---

## Scenario 6: Verify Pharmacist Permitted Operations

### Steps

1. Confirm these operations work for PHARM1:

| Action | Menu | Option | Expected |
|--------|------|--------|----------|
| View Cover Sheet | CV | - | Allowed |
| List Medications | ME | 1 | Allowed |
| List Orders | OR | 1, 2 | Allowed (read-only) |
| View Notes | NO | 1, 2 | Allowed (read-only) |
| List Lab Results | LA | 1, 2, 3 | Allowed |
| Record Allergy | AL | 2 | Allowed (no key required) |
| Record Vitals | VT | 2 | Allowed (no key required) |
| Add Problem | PL | 3 | Allowed (no key required) |
| Request Consult | CO | 4 | Allowed (no key required) |
| Order Lab Test | LA | 4 | Allowed (no key required) |
| All Reports | RP | 1-14 | Allowed |
| Hold/Release Order | OR | 6, 7 | Allowed (no key required) |

### Expected Result

- All listed operations succeed without access denied messages.
- Pharmacists have full read access to all clinical data.

---

## Scenario 7: Quit the Application

### Steps

1. At the Main Menu, type: `Q`.

### Expected Result

- Session ends, application exits cleanly.
