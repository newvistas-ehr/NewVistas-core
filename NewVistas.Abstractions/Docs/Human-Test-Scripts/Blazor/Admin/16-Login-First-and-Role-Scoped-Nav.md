# Login-First and Role-Scoped Navigation -- Access Control Human Test Script

Verifies two related behaviors of the main Blazor app:

1. **Login-first** — an unauthenticated visitor never sees the application
   "UI map". Any protected route redirects to the Sign-In page, which shows
   no sidebar and no feature list.
2. **Role-scoped navigation** — once logged in, the sidebar shows only the
   sections the user's VistA security keys grant. The mapping (security key →
   menu area) lives in `NewVistas.Abstractions/Security/MenuAccessMap.cs` and is
   shared by all UI layers.

## Prerequisites

- **Logins (all use Password: `smythVista1`):**
  - DOCTOR1 — Provider
  - NURSE4 — Nurse
  - PHARM1 — Pharmacist
  - LABTECH1 — Lab Technician
  - CLERK1 — Registration Clerk
- **Pre-conditions:**
  1. The SiloHost, WebServer, and BlazorWeb projects must all be running.
  2. Demo users seeded at startup (automatic).
  3. Use a fresh browser session (or private window) for Scenario 1 so you start
     unauthenticated.

---

## Scenario 1: Login-First — No UI Map Before Sign-In (Happy Path)

### Steps

1. In a fresh / private browser window (not logged in), navigate directly to a
   protected route, e.g. `http://localhost:5196/problems`.

### Expected Result

- The browser is redirected to **`/login`** (the address bar shows
  `/login?ReturnUrl=%2Fproblems` or similar).
- The page shows **only** the centered **NewVistas Sign-In** card (Access Code,
  Verify Code, Sign In button).
- There is **no left sidebar**, **no navigation menu**, and **no list of
  clinical/financial/admin features** anywhere on the page.

### Repeat for other entry points

2. Repeat with `http://localhost:5196/` (root) and
   `http://localhost:5196/cover-sheet`.

### Expected Result

- Each redirects to `/login` with the same clean, nav-free Sign-In screen.
- At no point is the application's feature map visible to an anonymous visitor.

---

## Scenario 2: Role-Scoped Sidebar After Sign-In

Log in as each user (use a fresh browser/private window per user, since a page
reload signs you out by design) and inspect the **section titles** in the left
sidebar.

| Login    | Role               | Sidebar sections (in addition to Home + Patient Lookup) |
|----------|--------------------|----------------------------------------------------------|
| DOCTOR1  | Provider           | Clinical, Nursing, Laboratory, Radiology, Reference, Dashboards |
| NURSE4   | Nurse              | Clinical, Nursing, Laboratory, Radiology, Administrative, Reference, Dashboards |
| LABTECH1 | Lab Technician     | Clinical, Laboratory, Reference, Dashboards |
| PHARM1   | Pharmacist         | **Pharmacy**, Reference, Dashboards |
| CLERK1   | Registration Clerk | **Administrative**, Reference, Dashboards |

### Steps (per user)

1. Navigate to `/login`, sign in as the user.
2. After landing on Home, read the bold section titles down the left sidebar.

### Expected Result

- The sidebar matches the row for that user above.
- **Negative checks worth confirming:**
  - **PHARM1** sees **no** Clinical, Nursing, Administrative, or Financial
    sections — only Pharmacy + Reference + Dashboards. (A pharmacist sees
    "their part.")
  - **CLERK1** sees **no** Clinical or Pharmacy sections — only Administrative +
    Reference + Dashboards.
  - The **Nursing** section (Nursing Assessment, Shift Handoff, Care Plan,
    Triage, Task Worklist, Pain Assessment) appears for DOCTOR1 and NURSE4 but
    **not** for PHARM1 or CLERK1.

---

## Scenario 3: Sign Out Returns to the Nav-Free Login Screen

### Steps

1. While signed in as any user, click **Sign Out** (top-right of the header).

### Expected Result

- The app returns to **`/login`**.
- The sidebar / nav is gone again (the login-first state from Scenario 1).
- Signing back in as a different role shows that role's sections (re-verifies
  Scenario 2 without a stale menu from the previous user).

---

## Scenario 4: Nav Visibility Is Not the Same as Route Authorization

This documents an intentional design point so testers interpret results
correctly: the sidebar controls **visibility**, while data actions are guarded
at the **grain** layer by security keys.

### Steps

1. Sign in as **PHARM1** (whose sidebar shows no Clinical section).
2. Manually type a clinical URL into the address bar **in-app** (use a link
   click or the URL bar without a full reload), e.g. `/cover-sheet`.

### Expected Result

- The page **loads** (Blazor routes require only authentication, not a specific
  key), but it is simply not advertised in PHARM1's sidebar.
- Attempting a **mutating clinical action** that requires a key PHARM1 does not
  hold is rejected at the grain layer (authorization), not by the menu.
- Interpretation: role-scoped nav is a usability/"see your part" feature; the
  hard security boundary is the per-grain `[RequiresSecurityKey]` enforcement.

---

## Notes

- Security-key → menu-area mapping: `MenuAccessMap.cs`. Adding a key to a role
  (or a role to a key group) changes the sidebar for everyone with that key.
- Demo key assignments by role are seeded in `NewVistas.WebServer/Program.cs`
  (`SeedDemoSecurityKeysAsync`).
- Login-first is implemented by wrapping the sidebar in `<AuthorizeView>` in
  `Components/Layout/MainLayout.razor`; the redirect itself is `RedirectToLogin`
  via `Components/Routes.razor`.
