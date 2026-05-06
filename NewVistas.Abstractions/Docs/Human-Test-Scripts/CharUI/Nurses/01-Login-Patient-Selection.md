# Login & Patient Selection -- Nurse CharUI Human Test Script

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Security Keys on file:** ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
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
2. At the `Access Code` prompt, type: `NURSE1`
3. At the `Verify Code` prompt, type: `smythVista1` (characters display as asterisks)

### Expected Result

- The terminal displays:
  ```
  Good [morning/afternoon/evening], NURSE1
  You last signed on [date/time]
  5 security key(s) on file.
  ```
- The 5 keys are: ORELSE, GMRV VITALS, GMRA ALLERGY, GMPL PROBLEM, SD SCHEDULING
- **Note:** Nurses do NOT hold PROVIDER, ORES, or TIU SIGN keys. This limits note writing, order signing, and encounter creation.
- The Main Menu appears.

---

## Scenario 2: Failed Login (3-Attempt Lockout)

### Steps

1. Restart CharUI.
2. Enter incorrect credentials 3 times:
   - Attempt 1: Access Code: `NURSE1`, Verify Code: `wrong1`
   - Attempt 2: Access Code: `NURSE1`, Verify Code: `wrong2`
   - Attempt 3: Access Code: `NURSE1`, Verify Code: `wrong3`

### Expected Result

- After each failure: remaining attempts count shown.
- After 3rd failure: lockout message, application exits.

---

## Scenario 3: Select a Patient

### Steps

1. After login, at the Main Menu, type: `SP` and press Enter.
2. At the prompt `Select PATIENT NAME (or ^ to cancel)`, type a patient name (e.g., `SMITH`).
3. Select a patient from the numbered list.

### Expected Result

- Patient selected successfully.
- Cover sheet loads.
- Main Menu reappears with patient banner.

---

## Scenario 4: Verify Nurse Security Key Limitations

### Steps

1. After login and patient selection, try these restricted operations:

| Action | Menu | Option | Expected Result |
|--------|------|--------|-----------------|
| Write a Note | NO | 3 | `You do not hold the PROVIDER key. Note entry is not permitted.` |
| Sign a Note | NO | 4 | `You do not hold the TIU SIGN key.` |
| Cosign a Note | NO | 5 | `You do not hold the TIU COSIGN key.` |
| Add Addendum | NO | 6 | `You do not hold the PROVIDER key.` |
| Amend Note | NO | 7 | `You do not hold the PROVIDER key.` |
| Sign an Order | OR | 4 | `You do not hold the ORES key. Signing is not permitted.` |
| D/C Summary Create | DC | 3 | `You do not hold the PROVIDER key.` |
| D/C Summary Sign | DC | 4 | `You do not hold the TIU SIGN key.` |
| Create Encounter | EN | 3 | `You do not hold the PROVIDER key.` |

### Expected Result

- Each restricted action shows the appropriate access denied message.
- No data is modified.

---

## Scenario 5: Verify Nurse Permitted Operations

### Steps

1. Confirm these operations are permitted for NURSE1:

| Action | Menu | Option | Expected |
|--------|------|--------|----------|
| Place Order (ORELSE) | OR | 3 | Allowed -- nurse can place orders |
| Record Vitals | VT | 2 | Allowed |
| Record Allergy | AL | 2 | Allowed |
| Add Problem | PL | 3 | Allowed |
| Schedule Appointment | SC | 3 | Allowed |
| View all clinical data | CV, PL, ME, etc. | View options | Allowed (read-only) |

### Expected Result

- All listed operations succeed without access denied messages.

---

## Scenario 6: Quit the Application

### Steps

1. At the Main Menu, type: `Q`.

### Expected Result

- Session ends, application exits cleanly.
