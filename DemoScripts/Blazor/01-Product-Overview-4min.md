# NewVistas — 4-Minute Product Overview Demo

> **UI:** Blazor Web (`NewVistas.BlazorWeb`)
> **Target length:** ≤ 4:00
> **Audience:** Prospective customer / stakeholder — first look at the product
> **Goal:** Show that NewVistas is real, runs, and delivers a CPRS-style clinical workflow.
> Not goal: dig into every module.

Parallel scripts for other UIs (WPF, CharUI, Patient Portal) belong in sibling folders
under `DemoScripts/` (e.g., `DemoScripts/Wpf/`, `DemoScripts/CharUI/`).

---

## Before You Record

### Services to start (three terminals, in this order)
1. `dotnet run --project NewVistas.SiloHost`
2. `dotnet run --project NewVistas.WebServer`
3. `dotnet run --project NewVistas.BlazorWeb`

### Browser
- Open **https://localhost:7137** in Chrome or Edge (new InPrivate/Incognito window — no autofill clutter).
- Zoom 110–125% so nav labels and cover-sheet panels read cleanly on a 1080p capture.
- Close dev tools, notification panels, extension icons.

### Demo data — required before first take
Demo users seed automatically on WebServer startup. Clinical data does **not** — load it once:

- Option A (preferred, one command in a fourth terminal):
  ```bash
  curl -k -X POST https://localhost:7127/api/accesscontrol/demo/load
  curl -k -X POST "https://localhost:7127/api/scheduling/demo/load?patientId=4"
  curl -k -X POST "https://localhost:7127/api/lab/demo/load?patientId=4"
  curl -k -X POST "https://localhost:7127/api/outpatientpharmacy/demo/load?patientId=4"
  curl -k -X POST "https://localhost:7127/api/pce/demo/load?patientId=4"
  ```
- Option B: in the WPF UI use **Tools → ZWR Import** pointed at `exports/Fifty/` (50 patients, richest data).

### Credentials & patient
| Field | Value |
|---|---|
| Access Code | `DOCTOR1` |
| Verify Code | `smythVista1` |
| Display name that will appear in the header | SMITH,JOHN A — Internal Medicine |
| Demo patient ID | `4` |

Have the patient ID `4` on the clipboard before you start recording.

---

## Script (target 4:00)

Each block lists **[TIME]**, what the viewer sees, and the narrator line.
Narrator lines are conversational — tweak to your own cadence. Stick to the timing.

---

### 0:00 – 0:20 — Title / framing *(still card or simple intro)*

**On screen:** Title card — "NewVistas — Modern Clinical Information System" over the repo logo or the login page blurred in the background.

> "NewVistas is a modern electronic health record inspired by the VA's VistA system —
> rebuilt on .NET 10 and Microsoft Orleans. It covers the full clinician workflow:
> problems, orders, labs, meds, consults — the whole chart. Here's a four-minute tour."

---

### 0:20 – 0:40 — Sign in

**Action:**
1. Browser is on `/login`. Show the Sign-In card for a beat.
2. Type **`DOCTOR1`** into **Access Code**.
3. Type **`smythVista1`** into **Verify Code**.
4. Click **Sign In**.

**On screen:** Login card → brief flash of header showing "SMITH,JOHN A" top-right.

> "Authentication uses VistA-style access and verify codes.
> We're signing in as Dr. Smith, an Internal Medicine provider.
> Role-based security controls which menu items Dr. Smith can even see."

---

### 0:40 – 1:00 — Sidebar / breadth

**Action:** Let the home page render. Slowly hover-scroll the **left sidebar** from top to bottom so the viewer sees Clinical, Pharmacy, Administrative, Financial, Reference, Dashboards sections go by. Do **not** click anything yet.

> "The left rail is the whole product at a glance —
> every clinical domain you'd expect in a full EHR,
> plus pharmacy, registration, billing, and reference data."

---

### 1:00 – 1:15 — Open the Cover Sheet

**Action:**
1. Click **Cover Sheet** in the sidebar (📋).
2. In the **Patient ID** field at the top, paste or type **`4`**.
3. Press **Enter** (or click **Load**).

**On screen:** Page reloads into the patient banner + 8-panel grid.

> "This is the Cover Sheet — modeled on VistA CPRS.
> One patient, one screen, everything that matters clinically."

---

### 1:15 – 2:00 — Cover Sheet walkthrough *(the money shot — linger here)*

**Action:** Mouse-over each panel as you name it. Don't click in. Let the viewer read.

1. Point to the **Patient Banner** at top — name, sex, age, any SC% badge, CWAD flags.
2. **Active Problems** panel — hover for ~2s.
3. **Allergies** — hover for ~2s (call out any red "severe" row).
4. **Active Medications** — hover.
5. **Recent Labs** — hover; if any value is red, point at it.
6. **Recent Vitals** — hover.
7. **Appointments** — hover.
8. **Active Orders** — hover.

> "Eight live panels, all driven by the same patient record.
> Problems, allergies, active medications, reminders, labs, vitals, appointments, and orders —
> with abnormal values flagged in red, right where a clinician expects them.
> Each panel is powered by its own virtual-actor grain behind the scenes —
> so when a lab result posts or an order is signed, the right panel updates in isolation."

---

### 2:00 – 2:35 — Book a follow-up appointment *(prove it's interactive, not a mockup)*

**Action:**
1. In the **Appointments** panel header, click **+ Follow-Up**.
2. The form expands inline. Click **Load Clinics** if the dropdown is empty.
3. **Clinic:** pick the first active clinic (e.g., PRIMARY CARE).
4. **Date / Time:** leave the default (seven days out, 9:00 AM).
5. **Duration:** 30 min.
6. **Purpose:** type "Follow-up hypertension management".
7. Click **Book**.
8. Wait for the green "Follow-up booked…" confirmation.
9. Point to the new row that just appeared in the Appointments panel.

> "Booking a follow-up — pick a clinic, time, duration, reason, submit.
> The scheduler checks for conflicts against every other appointment in that clinic
> and the patient's own calendar, then writes the booking.
> No page reload — the appointment is back in the panel a beat later."

---

### 2:35 – 3:10 — Jump to Orders from the cover sheet

**Action:**
1. Click the **New Medication** button in the Workflows bar under the banner.
2. You land on **Orders** with patient 4 already loaded and the New Order tab selected. Pause ~2s.
3. Click **Medications** in the sidebar (💊) to show the active meds list for patient 4.

> "From the cover sheet, every workflow the provider needs is one click away —
> new order, new medication, new lab, new consult — and the patient context travels with them.
> Here are Dr. Smith's patient's active prescriptions,
> the same list that fed the Active Medications panel we just saw."

---

### 3:10 – 3:35 — One more domain to show breadth *(pick ONE — Labs is recommended)*

**Action:**
1. Click **Labs** in the sidebar (🔬).
2. Patient 4 is still in context — let the lab results grid render.
3. Scroll briefly if the grid is long. Point at any flagged value.

> "Labs — same patient, now the full results history.
> Under the hood, results are stored in time-partitioned batches,
> so a patient with twenty years of lab history still loads instantly."

---

### 3:35 – 3:55 — Back out to the architecture pitch *(no clicks needed)*

**Action:** Return to the Cover Sheet (browser back, or click **Cover Sheet** in the sidebar). Let the 8 panels sit on screen.

> "Everything you just saw runs on Orleans virtual actors —
> every patient, every lab batch, every order is its own stateful grain.
> That's what lets NewVistas scale horizontally across thousands of concurrent users
> without the single-database bottleneck of a traditional EHR."

---

### 3:55 – 4:00 — Close

**On screen:** Cover Sheet still visible, or cut to a closing card with repo/contact info.

> "That's NewVistas. Full clinical workflow, modern stack, production-ready architecture."

---

## Recording Notes

- **One take per section, not one take total.** Record in the eight sections above, then cut.
- If the follow-up booking throws a conflict error (you ran the demo twice), change the time — the error path is real behavior, but not something you want on camera.
- If the Recent Labs panel is empty on patient 4, run the `api/lab/demo/load?patientId=4` curl again before rolling.
- Cursor visibility: keep a subtle highlight effect on; don't use a giant yellow spotlight.
- Audio: record narration separately over the screen capture — you'll get cleaner timing than narrating live.

## Fallbacks

| If this breaks… | Do this |
|---|---|
| Login fails | Confirm WebServer is running; passwords are `smythVista1` exactly (case-sensitive). |
| Cover Sheet loads but all panels empty | Run the `demo/load` curls above for patient 4, then reload. |
| Clinics dropdown empty in follow-up form | Run `api/scheduling/demo/load?patientId=4`. |
| Sidebar missing sections | `DOCTOR1` only has Provider + OrderEntry roles — that's expected. For wider coverage sign in as `ADMIN1`. |

---

## Reusability

This folder pattern — `DemoScripts/<UI>/<NN>-<topic>-<duration>.md` —
scales to future demos. Suggested next files:

- `DemoScripts/Blazor/02-Pharmacy-Verification-3min.md` (login as `PHARM1`)
- `DemoScripts/Blazor/03-Registration-and-Means-Test-3min.md` (login as `CLERK1`)
- `DemoScripts/Wpf/01-Product-Overview-4min.md` (WPF equivalent of this script)
- `DemoScripts/CharUI/01-Terminal-UI-Tour-3min.md` (character-mode UI — VistA traditionalists)
- `DemoScripts/PatientPortal/01-Patient-Self-Service-3min.md`
