# Getting Started

This guide covers system requirements, login, navigation, and common UI patterns shared by all NewVistas roles.

---

## System Requirements

### Browser Requirements

| Requirement | Minimum |
|---|---|
| **Browser** | Microsoft Edge 120+, Google Chrome 120+, Mozilla Firefox 120+, or Safari 17+ |
| **JavaScript** | Must be enabled (Blazor Server requires WebSocket connectivity) |
| **Screen Resolution** | 1280 x 800 minimum; 1920 x 1080 recommended |
| **Network** | Persistent network connection required (Blazor Server uses SignalR WebSockets) |

### Network Access

NewVistas runs as a server-rendered application. All data processing occurs on the server; the browser serves as a thin display client. A stable network connection is required at all times. If the connection is interrupted, a **reconnection modal** will appear automatically and attempt to re-establish the session.

> **Warning:** If the reconnection modal persists for more than 30 seconds, refresh the browser page. Any unsaved form data will be lost. Save your work frequently.

---

## Logging In

NewVistas uses a VistA-style authentication model with Access Codes and Verify Codes.

**Navigation:** Open your browser and navigate to the NewVistas application URL. If you are not already authenticated, you will be redirected to `/login`.

![Login page](screenshots/login-page.png)

1. **Enter your Access Code** — Type your username (Access Code) in the **Access Code (Username)** field. This is the identifier assigned to you by your site administrator.
2. **Enter your Verify Code** — Type your password (Verify Code) in the **Verify Code (Password)** field. Verify codes are case-sensitive.
3. **Click Sign In** — Click the **Sign In** button or press **Enter** to authenticate. A "Signing in..." indicator will appear while the system validates your credentials.
4. **Successful Login** — Upon successful authentication, you will be redirected to the **Home** page (`/`). Your username appears in the upper-right corner of the header bar alongside the **Sign Out** button.

![Successful login — home page with username in header](screenshots/login-success.png)

> **Note:** If you receive a "Sign-in failed" error, verify that both your Access Code and Verify Code are entered correctly. After multiple failed attempts, your account may be temporarily locked. Contact your site administrator or IRM support to unlock your account.

### Signing Out

Click the **Sign Out** button in the upper-right corner of the header bar at any time. You will be returned to the login page. Always sign out when leaving a shared workstation.

---

## Navigation Overview

The NewVistas interface consists of three main areas:

1. **Header Bar** — Displays the NewVistas logo, system title ("Clinical Information System"), your username, and the Sign Out button.
2. **Sidebar Navigation** — A persistent left-hand sidebar organized into functional sections. Click any item to navigate to that module.
3. **Main Content Area** — The central workspace where the selected page is rendered.

![Navigation layout — header bar, sidebar, and main content area](screenshots/navigation-layout.png)

### Sidebar Sections

#### Top-Level

| Item | Route | Description |
|---|---|---|
| Home | `/` | Application home page |
| Patient Lookup | `/patient-lookup` | Search and select patients |

#### Clinical

Core clinical modules for direct patient care:

| Item | Route | Description |
|---|---|---|
| Cover Sheet | `/cover-sheet` | CPRS-style patient overview dashboard |
| Problems | `/problems` | Problem list management (VistA File #9000011) |
| Medications | `/medications` | Active medication review |
| Orders | `/orders` | Order entry and results reporting (VistA File #100) |
| Notes | `/notes` | TIU clinical documentation (VistA File #8925) |
| Consults | `/consults` | Consultation requests and tracking (VistA File #123) |
| Ext. Referrals | `/external-referrals` | External/interfacility referrals |
| Labs | `/labs` | Laboratory orders and results (VistA File #63) |
| Vitals | `/vitals` | Vital signs recording and review |
| Allergies | `/allergies` | Allergy and adverse reaction tracking |
| Immunizations | `/immunizations` | Immunization history |
| Imm. Forecast | `/immunization-forecast` | CDC-based immunization forecasting |
| Surgery | `/surgery` | Surgical case management (VistA File #130) |
| Radiology | `/radiology` | Radiology orders and results (VistA File #75.1) |
| Imaging | `/imaging` | Clinical imaging (VistA File #2005) |
| BCMA | `/bcma` | Barcode Medication Administration |
| Mental Health | `/mental-health` | Mental health assessments and screening |
| Dietetics | `/dietetics` | Nutrition and diet orders |
| Reminders | `/reminders` | Clinical reminders and preventive care |
| Health Factors | `/health-factors` | Health factor documentation |
| Health Summary | `/health-summary` | Health summary report generation |

#### Pharmacy

| Item | Route | Description |
|---|---|---|
| Pharmacy Hub | `/pharmacy` | Pharmacy operations dashboard |
| Outpatient Rx | `/outpatientpharmacy` | Outpatient prescription processing |
| Inpatient Meds | `/inpatientpharmacy` | Inpatient medication management |
| Drug Accountability | `/drugaccountability` | Drug inventory and accountability |
| Benefits & PA | `/pharmacybenefits` | Pharmacy benefits and prior authorization |

#### Administrative

| Item | Route | Description |
|---|---|---|
| ADT | `/adt` | Admission, Discharge, Transfer |
| Means Test | `/means-test` | Financial eligibility screening |
| SC Conditions | `/service-connected` | Service-connected conditions |
| Prosthetics | `/prosthetics` | Prosthetic item tracking |
| Site Parameters | `/site-parameters` | System-wide configuration |
| Patient Merge | `/patient-merge` | Duplicate patient record merging |

#### Financial

| Item | Route | Description |
|---|---|---|
| Accounts Receivable | `/accounts-receivable` | AR debt management |
| Integrated Billing | `/integrated-billing` | Insurance billing |
| EDI Billing | `/edi-billing` | Electronic claims submission |
| Agent Cashier | `/agent-cashier` | Cash collections |
| Fee Basis | `/fee-basis` | Fee-basis (community care) payments |
| IFCAP | `/ifcap` | Procurement and fund control |

#### Reference

| Item | Route | Description |
|---|---|---|
| ICD-10 Codes | `/icd10` | ICD-10 diagnosis code browser |
| NDF Formulary | `/drugformulary` | National Drug File formulary lookup |
| Drug File | `/drugfile` | Drug file maintenance |
| Voluntary Service | `/voluntary-service` | Volunteer management |

#### Dashboards

| Item | Route | Description |
|---|---|---|
| iCare Dashboard | `/icare-dashboard` | Clinical intelligence dashboard |

#### Patient Portal

| Item | Route | Description |
|---|---|---|
| Patient Portal | `/patient-portal` | Patient-facing portal |

> **Tip:** Additional modules not listed in the sidebar (such as Nursing, Dental, Oncology, Blood Bank, Spinal Cord Injury, and many others) are accessible via direct URL navigation or through links on related pages. See the role-specific guides for the full list of modules relevant to your workflow.

---

## Patient Context

Most clinical pages in NewVistas require a **patient context** — you must specify which patient's record you are viewing or modifying.

### Selecting a Patient

![Patient Lookup page](screenshots/patient-lookup.png)

1. **Navigate to Patient Lookup** — Click **Patient Lookup** in the sidebar or navigate to `/patient-lookup`.
2. **Search for the patient** — Enter the patient's name (Last, First), Social Security Number (last 4 digits), or Patient ID in the search field. Click **Search** or press Enter.
3. **Select from results** — Review the search results table showing matching patients with their Name, SSN (masked), Date of Birth, and Patient ID. Click on the desired patient row to select them.
4. **Patient ID on clinical pages** — On individual clinical pages (Problems, Orders, Notes, Vitals, etc.), each page has a **Patient ID** input field at the top. Enter the patient's ID and click **Load** to retrieve that patient's data.

![Patient search results table](screenshots/patient-search-results.png)

> **Note:** Patient IDs may contain special characters. The system automatically URL-encodes patient identifiers when making API calls to ensure reliable data retrieval.

---

## Common UI Patterns

### Tab Navigation

Many pages use a **tab bar** to organize content into logical sections. Tabs appear as a horizontal row of buttons below the page header. The currently active tab is visually highlighted. Click any tab to switch views. Common patterns include:

- **2-tab pages**: View/list + Add/create (e.g., Problems: "Problem List" | "Add Problem")
- **3-tab pages**: Multiple list views + actions (e.g., Labs: "Results" | "Current Summary" | "Order / Submit")
- **4-tab pages**: Extended functionality (e.g., Orders: "Active Orders" | "New Order" | "Order Sets" | "Order History")

![Tab navigation example](screenshots/tab-navigation.png)

### Status Badges

Clinical items display color-coded status badges throughout the system:

| Badge Style | Typical Statuses |
|---|---|
| **Green/Active** | Active, Completed, Verified, Signed |
| **Yellow/Pending** | Pending, Ordered, Hold, Draft, Unsigned |
| **Red/Alert** | Discontinued, Cancelled, Expired, Abnormal, STAT |
| **Gray/Inactive** | Inactive, Archived, Historical |

![Status badge examples](screenshots/status-badges.png)

### Loading States

When data is being retrieved from the server, buttons display a "Loading..." label and are disabled to prevent duplicate submissions. Tables show a loading message or spinner until data arrives.

### Error Messages

Errors appear as red alert banners at the top of the content area with a description of the issue. Errors are typically transient and clear when the next successful action is performed.

### Success Messages

Successful actions (saving a record, signing a note, etc.) display a green alert banner confirming the operation completed.

### Data Tables

Clinical data is presented in sortable, striped tables with column headers. Row highlighting indicates special states:

- **Yellow rows** — Items requiring attention (due medications, pending orders)
- **Green rows** — Signed/completed items
- **Red text** — Abnormal lab values, critical alerts

### Electronic Signature Modal

Certain actions require electronic signature verification, including:

- Signing clinical notes (TIU documents)
- Signing orders
- Signing nursing assessments
- Completing surgical cases
- Finalizing anesthesia records

When prompted, you must enter your **Electronic Signature Code** (typically your Verify Code) to authenticate the action. This creates an auditable record of who performed the action and when.

![Electronic signature modal](screenshots/electronic-signature-modal.png)

> **Warning:** Never share your Electronic Signature Code. Each signed action creates a legal record attributable to your credentials. Signing on behalf of another user is a policy violation.

### Form Patterns

Data entry forms use a consistent layout:

- **Required fields** are marked with a red asterisk (*).
- **Dropdown selects** provide predefined options for coded fields (status, type, priority).
- **Date fields** accept `MM/dd/yyyy` format or use a date picker.
- **Text areas** are used for narrative/free-text fields (clinical notes, comments).
- **Submit buttons** are disabled until all required fields are populated.

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| **Enter** | Submit the current form or search (when focus is on an input field) |
| **Tab** | Move focus to the next form field |
| **Shift+Tab** | Move focus to the previous form field |
| **Escape** | Close modal dialogs |

---

## Session Management

NewVistas uses JWT (JSON Web Token) authentication with server-side session management.

### Session Timeout

Sessions have a configurable inactivity timeout. When your session expires:

1. The application will display a reconnection modal.
2. You will be redirected to the login page.
3. Any unsaved form data will be lost.

> **Tip:** Save your work frequently. If you are entering a long clinical note, periodically save it as a draft (UNSIGNED status) to prevent data loss from session timeouts or network interruptions.

### Connection Recovery

NewVistas uses Blazor Server with SignalR WebSockets. If your network connection is briefly interrupted:

1. A **reconnection modal** appears with a "Reconnecting..." message.
2. The system automatically attempts to re-establish the connection.
3. If reconnection succeeds, you return to your previous state with no data loss.
4. If reconnection fails after several attempts, click the **Reload** link to refresh the page.

![Reconnection modal](screenshots/reconnection-modal.png)
