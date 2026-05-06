# Login & Patient Selection -- Physician CharUI Human Test Script

## Prerequisites

- **Login:** DOCTOR1 / Password: `smythVista1`
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
2. At the `Access Code` prompt, type: `DOCTOR1`
3. At the `Verify Code` prompt, type: `smythVista1` (characters display as asterisks)

### Expected Result

- The terminal displays a greeting:
  ```
  Good [morning/afternoon/evening], DOCTOR1
  You last signed on [date/time]
  6 security key(s) on file.
  ```
- The keys on file are: PROVIDER, ORES, TIU SIGN, GMRA ALLERGY, GMRV VITALS, GMPL PROBLEM
- The Main Menu appears with the full list of clinical modules.

---

## Scenario 2: Failed Login -- Wrong Access Code

### Steps

1. Restart the CharUI application.
2. At the `Access Code` prompt, type: `BADUSER`
3. At the `Verify Code` prompt, type: `smythVista1`

### Expected Result

- The terminal displays an authentication failure message.
- The terminal shows: `2 attempt(s) remaining.`
- The Access Code prompt reappears.

---

## Scenario 3: Failed Login -- Wrong Verify Code

### Steps

1. Restart the CharUI application.
2. At the `Access Code` prompt, type: `DOCTOR1`
3. At the `Verify Code` prompt, type: `wrongpassword`

### Expected Result

- The terminal displays an authentication failure message.
- The terminal shows: `2 attempt(s) remaining.`
- The Access Code prompt reappears.

---

## Scenario 4: Account Lockout After 3 Failures

### Steps

1. Restart the CharUI application.
2. Enter incorrect credentials 3 times in a row:
   - Attempt 1: Access Code: `DOCTOR1`, Verify Code: `bad1`
   - Attempt 2: Access Code: `DOCTOR1`, Verify Code: `bad2`
   - Attempt 3: Access Code: `DOCTOR1`, Verify Code: `bad3`

### Expected Result

- After attempt 1: `2 attempt(s) remaining.`
- After attempt 2: `1 attempt(s) remaining.`
- After attempt 3: The terminal displays a lockout message and the application exits.
- The user cannot re-authenticate until the lockout period expires (15 minutes).

---

## Scenario 5: MFA Challenge (If MFA Enabled)

### Steps

1. Pre-condition: MFA must be enabled for DOCTOR1 via `POST /api/auth/mfa/setup` and `POST /api/auth/mfa/enable`.
2. Restart the CharUI application.
3. At the `Access Code` prompt, type: `DOCTOR1`
4. At the `Verify Code` prompt, type: `smythVista1`
5. The terminal prompts: `Enter TOTP Code`
6. Enter the current 6-digit TOTP code from your authenticator app.

### Expected Result

- If the TOTP code is valid: Login succeeds with greeting message and Main Menu appears.
- If the TOTP code is invalid: Authentication failure, remaining attempts shown.

---

## Scenario 6: Select a Patient (Happy Path)

### Steps

1. After successful login, at the Main Menu, type: `SP` and press Enter.
2. The terminal displays: `Select PATIENT NAME (or ^ to cancel)`
3. Type a patient name or partial name, e.g.: `SMITH`
4. A numbered list of matching patients appears:
   ```
   #  Patient Name          DOB          Sex  SSN Last 4
   1  SMITH,JOHN            01/15/1955   M    1234
   2  SMITH,JANE            03/22/1968   F    5678
   ```
5. At the prompt `Choose Patient (1-N)`, type: `1`

### Expected Result

- The terminal displays:
  ```
  *** PATIENT SELECTED: SMITH,JOHN ***
  Loading cover sheet...
  Done.
  ```
- The Main Menu reappears with the patient banner visible at the top showing patient name, DOB, sex, age.
- Subsequent clinical modules now have a patient context.

---

## Scenario 7: Patient Search with No Results

### Steps

1. At the Main Menu, type: `SP`
2. At the patient name prompt, type: `ZZZZNOPATIENT`

### Expected Result

- The terminal displays no matches or an empty results table.
- The patient selection prompt reappears or returns to the Main Menu.
- No patient is selected.

---

## Scenario 8: Cancel Patient Selection

### Steps

1. At the Main Menu, type: `SP`
2. At the patient name prompt, type: `^`

### Expected Result

- Patient selection is cancelled.
- The Main Menu reappears with no change to the current patient context.

---

## Scenario 9: Access a Sensitive/Restricted Patient Record (Break-the-Glass)

### Steps

1. Pre-condition: A patient record must be flagged as SENSITIVE (set via DG SENSITIVITY in the system).
2. At the Main Menu, type: `SP`
3. Search for and select the sensitive patient.
4. The terminal displays:
   ```
   *** RESTRICTED RECORD ***
   This patient's record is flagged as SENSITIVE.
   Access to this record is monitored and logged.
   Category: [SensitivityCategories]
   ```

### Branch A: Authorized Provider

5a. If DOCTOR1 is on the authorized provider list, the terminal displays:
   ```
   You are on the authorized provider list.
   Access is permitted. This access has been logged.
   ```

**Expected Result (Branch A):**
- Patient is selected successfully.
- Cover sheet loads normally.

### Branch B: Not Authorized -- Accept Break-the-Glass

5b. If DOCTOR1 is NOT on the authorized provider list, the terminal displays:
   ```
   You are NOT on the authorized provider list.
   ```
6b. At the prompt `Do you wish to access this record? (This will be logged)`, type: `Y`
7b. At the prompt `Reason for access`, type: `Urgent consultation requested by attending physician`

**Expected Result (Branch B):**
- Patient is selected successfully.
- Access is logged as "BREAK_THE_GLASS" with the reason provided.
- Cover sheet loads normally.

### Branch C: Not Authorized -- Decline Access

5c. At the prompt `Do you wish to access this record?`, type: `N`

**Expected Result (Branch C):**
- The terminal displays: `Access denied by user.`
- Returns to patient search. No patient is selected.

### Branch D: Not Authorized -- No Reason Given

5d. At the prompt `Do you wish to access this record?`, type: `Y`
6d. At the prompt `Reason for access`, press Enter (blank)

**Expected Result (Branch D):**
- Access is denied because a reason is required.
- Returns to patient search. No patient is selected.

---

## Scenario 10: Access Clinical Module Without Patient Selected

### Steps

1. After login (before selecting a patient), type: `CV` (Cover Sheet) at the Main Menu.

### Expected Result

- The terminal displays: `Please select a patient first (SP).`
- The Main Menu reappears. No clinical data is shown.
- Repeat with other clinical options (PL, ME, OR, NO, etc.) -- all should show the same message.

---

## Scenario 11: Quit the Application

### Steps

1. At the Main Menu, type: `Q` (or `Quit`) and press Enter.

### Expected Result

- The terminal displays a sign-off message.
- The Orleans session ends (EndSessionAsync called).
- The application exits cleanly.
