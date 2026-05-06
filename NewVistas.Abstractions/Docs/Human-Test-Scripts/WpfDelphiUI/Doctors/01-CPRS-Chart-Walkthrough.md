# NewVistas.WpfDelphiUI -- CPRS Chart Walkthrough (Provider Human Test Script)

**Purpose:** Validate the CPRS-style WPF frontend (`NewVistas.WpfDelphiUI`)
end-to-end for a clinician's typical chart review session. This script
covers the 10 standard chart tabs (Cover Sheet, Problems, Meds, Orders,
Notes, Consults, Labs, Vitals, Surgery, Reports) plus the IHS / tribal
extensions added in the recent RPMS-frontend refresh:

- **Diabetes Registry panel** on the Cover Sheet (visible only when the
  patient is enrolled — see [Doctors/16-Diabetes-Registry.md](../../Blazor/Doctors/16-Diabetes-Registry.md) Part G)
- **External Referrals + CHS authorization** on the Consults tab (lower pane;
  CHS action bar gated by `CanAuthorizeChs` — see [Admin/11-CHS-Authorization.md](../../Blazor/Admin/11-CHS-Authorization.md) Part G-alt)
- **GPRA reporting** on the Reports tab (facility-wide, in addition to the
  per-patient radiology reports — see [Admin/12-GPRA-Submission.md](../../Blazor/Admin/12-GPRA-Submission.md) Part H)

This is the WPF analogue of [Blazor/Doctors/01-Cover-Sheet-Review.md](../../Blazor/Doctors/01-Cover-Sheet-Review.md);
the UI mimics the Delphi CPRS GUI's classic look (clBtnFace controls,
navy headers, MS Sans Serif feel via Segoe UI) and is suitable for users
migrating from RPMS GUI / VistA CPRS.

---

## Prerequisites

- **Login (provider):** `DOCTOR1` / Password: `smythVista1`
- **Login (CHS coordinator, for Part F):** `ADMIN1` / Password: `smythVista1`
  (must hold `CanAuthorizeChs`).
- **Site profile:** any deployment that includes the chart-tab modules
  (`IhsTribalSiteProfile` recommended — pre-enables `DIABETES_REGISTRY`,
  `EXTERNAL_REFERRAL_TRACKING`, `GPRA_REPORTING`, etc.)
- **A patient with a rich chart** — the demo seed (`exports/Fifty/` or
  `exports/TribalDemo/`) provides patients with problems, meds, orders,
  labs, vitals, etc.
- **A diabetic patient** for Part E — use the tribal demo or enroll one
  per [Doctors/16-Diabetes-Registry.md](../../Blazor/Doctors/16-Diabetes-Registry.md) Part A first.
- **A patient with at least one CHS-flagged referral** for Part F — use
  the demo seed or create one per [Admin/11-CHS-Authorization.md](../../Blazor/Admin/11-CHS-Authorization.md) Part B.
- **Servers running:** `NewVistas.SiloHost`, `NewVistas.WebServer` (default
  port 7127, the URL the WpfDelphiUI is hard-wired to).

---

## Part A: Launch + Login

### Scenario 1: Application launches and authenticates

### Steps

1. Build with `dotnet build NewVistas.WpfDelphiUI/NewVistas.WpfDelphiUI.csproj`.
2. Run the app (`dotnet run --project NewVistas.WpfDelphiUI` or via Visual Studio).
3. Log in as `DOCTOR1` / `smythVista1`.

### Expected Result

- The login dialog accepts the credentials and the main window appears.
- Window title: "CPRS Chart -- NewVistas".
- Status bar shows "Ready" and the user name on the right.
- Patient toolbar is empty; the "No patient selected" overlay is shown
  with the prompt "Use File → Select New Patient or the button above."

---

## Part B: Patient Selection

### Scenario 2: Select a patient via the patient-selection dialog

### Steps

1. Click **Select Patient** (or `File → Select New Patient...` or `Ctrl+P`).
2. Search by name (e.g., "DOE") or ICN.
3. Pick a patient from the result list and click OK.

### Expected Result

- The dialog closes and the main window's patient toolbar populates:
  - Patient name (clickable -- re-opens the dialog).
  - DOB / SSN / Age / Sex info line.
  - Visit location / provider, primary / attending provider.
  - Reminders count (color-coded by severity).
- The chart tabs become visible and enabled (`ShowClinicalTabs` is true
  for `DOCTOR1` because they hold the `PROVIDER` key — see
  [`MenuAccessMap.ClinicalKeys`](../../../../Security/MenuAccessMap.cs)).
- The Cover Sheet tab is selected by default.
- Window title updates to "CPRS Chart -- {patient display name}".

---

## Part C: Cover Sheet (8 standard panels)

### Scenario 3: Standard 8-panel cover sheet

### Steps

1. Stay on the **Cover Sheet** tab.
2. Verify each panel populates:
   - Active Problems (panel 1)
   - Allergies / Adverse Reactions (panel 2)
   - Patient Record Flags / Postings (panel 3)
   - Pending Orders (panel 4)
   - Upcoming Appointments (panel 5)
   - Latest Vitals (panel 6) -- abnormal values in red
   - Recent Lab Results (panel 7) -- H/L flags in red
   - Clinical Reminders (panel 8) -- due dates in red

### Expected Result

- All 8 panels load with data from the corresponding `api/patient/{id}/...`
  endpoints (or empty if the patient has none in that category).
- GridSplitters between panels are draggable.
- The status-bar text remains "Patient: {name}" (no error).

---

## Part D: Walk Each Chart Tab

### Scenario 4: Tab-by-tab smoke

### Steps

1. Click each tab in turn and verify it loads without error:
   - **Problems** -- list of active/inactive problems
   - **Meds** -- prescription list
   - **Orders** -- order list with filter
   - **Notes** -- TIU document list, signed/unsigned
   - **Consults** -- internal consults list (top); external referrals (bottom -- see Part F)
   - **Labs** -- recent lab results
   - **Vitals** -- vitals history
   - **Surgery** -- scheduled / completed surgical cases
   - **Reports** -- radiology reports (top); GPRA reports (bottom -- see Part G)
2. Use **F5** or the **Refresh** button to reload the active tab.
3. Switch back to **Cover Sheet** and use `File → Select New Patient` to pick
   a different patient. All tabs should re-load against the new patient.

### Expected Result

- No exceptions thrown; no error strip shown on any tab.
- Each tab's data refreshes when patient context changes (drives off
  `PatientContext.PatientId` `PropertyChanged`).
- Switching back to a previously visited tab does not re-fetch (singleton
  ViewModel state is preserved per session).

---

## Part E: Cover Sheet -- Diabetes Registry Panel

### Scenario 5: Enrolled patient surfaces the diabetes panel

### Steps

1. Select a diabetic patient (one enrolled in `IDiabetesRegistryGrain` per
   [Doctors/16-Diabetes-Registry.md](../../Blazor/Doctors/16-Diabetes-Registry.md) Part A).
2. Stay on the **Cover Sheet** tab.

### Expected Result

- A 9th panel appears below the standard 8-panel grid headed
  "Diabetes Registry -- {Type-1|Type-2|...}".
- **Left column** (snapshot):
  - HbA1c value + control label, color-coded:
    - **Maroon** for Poor (HbA1c ≥ 9.0)
    - **Navy** for Good (< 7.0) or AtTarget (7.0--8.9)
    - Grey when no data
  - Kidney function: eGFR + label, color-coded by status (Severe → red,
    Reduced → amber, Normal → navy).
  - Annual exams: foot / eye / ACR each labelled "up to date / due /
    overdue / never recorded".
- **Right column** (pre-visit plan, today's date):
  - Three sub-sections: **Overdue** (red), **Due**, **Up to date** (grey).
  - Lists generated from `GetDiabetesPreVisitPlanAsync(today)`.

### Scenario 6: Non-enrolled patient -- panel hidden

### Steps

1. Select a patient who is NOT enrolled in the diabetes registry.

### Expected Result

- Cover Sheet shows the standard 8 panels only; the diabetes row has zero
  height (the conditional `Visibility="{Binding HasDiabetesRegistry}"` on
  its Border collapses the entire Auto-height row).
- No 404 spam in the API server log -- the WpfDelphiUI cover sheet
  swallows snapshot/pre-visit-plan errors so a disabled feature flag or
  missing registry record doesn't break the rest of the cover sheet load.

---

## Part F: Consults Tab -- External Referrals + CHS

### Scenario 7: Consults tab splits into Internal + External

### Steps

1. Select a patient and click the **Consults** tab.

### Expected Result

- The tab splits horizontally with a draggable GridSplitter:
  - **Top**: Internal Consults list (existing CPRS behavior + the
    "New Consult" form via the toolbar button).
  - **Bottom**: External Referrals list with columns Facility, Type,
    Status, CHS, Priority, Auth $, Date.

### Scenario 8: Select a CHS-flagged referral as a CHS coordinator

### Steps

1. Log out, log back in as `ADMIN1` (holds `CanAuthorizeChs`).
2. Select a patient with a CHS-flagged referral.
3. Open the Consults tab and select the CHS referral in the bottom list.

### Expected Result

- A pale-amber CHS action bar appears above the External Referrals list,
  showing the referral details (Priority Class, Authorized Amount,
  Alternate Resources Checked, decision date + coordinator).
- Three action buttons: **Request CHS Auth**, **Approve**, **Deny**.
- The action bar is hidden when:
  - No referral is selected, OR
  - The selected referral is `IsChsReferral = false`, OR
  - The user does NOT hold `CanAuthorizeChs`.

### Scenario 9: Submit a CHS approval

### Steps

1. With the CHS action bar visible, click **Approve**.
2. Enter authorized amount `$1500.00` and authorization # `CHS-2026-00099`.
3. Click **Submit**.

### Expected Result

- The referral list reloads; the selected referral's status updates and
  CHS columns reflect the new amount + decision date.
- Validation errors (non-numeric amount, missing denial reason for Deny)
  surface in the shared `ErrorText` strip without submitting the request.
- The action bar's "Decision" line updates to show the date + the
  coordinator's display name.

> Detailed CHS workflow scenarios (eligibility gate, denial flow,
> auth-gate negative tests) are in [Admin/11-CHS-Authorization.md](../../Blazor/Admin/11-CHS-Authorization.md);
> this Part F is just the WPF surface validation.

---

## Part G: Reports Tab -- Radiology + GPRA

### Scenario 10: Reports tab splits into Radiology + GPRA

### Steps

1. With a patient selected, click the **Reports** tab.

### Expected Result

- The tab splits horizontally:
  - **Top**: Radiology Reports list (per-patient).
  - **Bottom**: GPRA section, further split into:
    - **Left**: GPRA Reports list (facility-wide, FY + Period + Facility +
      Status + Indicator-count columns).
    - **Right**: Indicator drilldown for the selected GPRA report
      (Measure / Title / Category / Current % / Baseline % / Δ pp /
      Target Met).

### Scenario 11: Drill into a GPRA report

### Steps

1. Click any GPRA report in the bottom-left list.

### Expected Result

- The header above the right-side grid updates to
  "{Facility} -- FY{N} {Period} ({Status}, {N} indicators, AUP={N})".
- The right-side grid populates with that report's `Indicators` collection.
- The Δ (pp) column is color-coded:
  - **Green** when `IsImproved = true`
  - **Red** otherwise
- The Period and Status columns use the int → label converters
  (`ReportingPeriodLabelConverter`, `GpraStatusLabelConverter`,
  `GpraCategoryLabelConverter`).

### Scenario 12: No GPRA reports configured (graceful empty state)

### Steps

1. On a fresh memory-storage silo with no GPRA reports built yet, click
   the Reports tab.

### Expected Result

- Radiology list populates as expected (per-patient).
- GPRA list is empty -- no crash, no error strip. (`SafeGetGpraReportsAsync`
  swallows errors so a missing endpoint or empty index doesn't break the
  Reports tab load.)

---

## Part H: Menu Filtering

### Scenario 13: Clinical tabs visible for a Doctor

### Steps

1. Logged in as `DOCTOR1`, verify all 10 chart tabs are enabled.

### Expected Result

- `MainViewModel.ShowClinicalTabs` is true (DOCTOR1 holds `PROVIDER`).
- All 10 tabs are visible and enabled.

### Scenario 14: Reduced-key user sees fewer tabs

### Steps

1. Log out, log in as a user that holds only `LRLAB` (lab tech, no
   `PROVIDER` / `ORES` -- e.g., `LAB1` if seeded).

### Expected Result

- `ShowClinicalTabs` falls back to MenuAccessMap behavior; the user still
  has Clinical access (LRLAB is in `ClinicalKeys`), so tabs remain
  visible.
- The same MenuAccessMap entries that grant access to a key like
  `CanManageDiabetesRegistry` or `CanAuthorizeChs` keep the Clinical area
  unlocked even if the user holds *only* one of those new keys -- without
  this guarantee, the chart-tab UI for those features would be silently
  hidden.

---

## Part I: Verification Checklist

- [ ] Login dialog accepts valid credentials and returns a JWT
- [ ] Patient selection dialog returns hits and populates the toolbar
- [ ] Cover Sheet panels 1--8 load against the seeded demo patient
- [ ] All 10 chart tabs load without error (Problems, Meds, Orders, Notes,
      Consults, Labs, Vitals, Surgery, Reports, Cover Sheet)
- [ ] Refresh button (F5) re-pulls the active tab
- [ ] Selecting a different patient re-loads every visited tab
- [ ] Cover Sheet diabetes panel appears for enrolled patients only
- [ ] HbA1c color-coding matches control status (maroon Poor / navy Good)
- [ ] Pre-visit plan items split into Overdue / Due / Up-to-date columns
- [ ] Consults tab splits into Internal Consults (top) + External Referrals (bottom)
- [ ] CHS action bar visible only for CHS-flagged referrals when user holds `CanAuthorizeChs`
- [ ] CHS form branches by action mode (REQUEST / APPROVE / DENY) with shared Submit
- [ ] Reports tab splits into Radiology (top, per-patient) + GPRA (bottom, facility)
- [ ] GPRA indicator Δ column color-coded green for improved, red for not
- [ ] Period / Status / Category converters render readable labels
- [ ] Menu visibility honors `MenuAccessMap.ClinicalKeys` (including the new
      `CanManageDiabetesRegistry` and `CanAuthorizeChs` entries)

---

## Cross-References

- **Project:** [`NewVistas.WpfDelphiUI/`](../../../../../NewVistas.WpfDelphiUI/) -- WPF CPRS-style frontend.
- **Main view + viewmodel:** [`MainWindow.xaml`](../../../../../NewVistas.WpfDelphiUI/MainWindow.xaml), [`MainViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/MainViewModel.cs)
- **Cover Sheet (incl. diabetes panel):** [`CoverSheetView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/CoverSheetView.xaml), [`CoverSheetViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/CoverSheetViewModel.cs)
- **Consults (incl. CHS action bar):** [`ConsultsView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/ConsultsView.xaml), [`ConsultsViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/ConsultsViewModel.cs)
- **Reports (incl. GPRA section):** [`ReportsView.xaml`](../../../../../NewVistas.WpfDelphiUI/Views/ReportsView.xaml), [`ReportsViewModel.cs`](../../../../../NewVistas.WpfDelphiUI/ViewModels/ReportsViewModel.cs)
- **API client + DTOs:** [`ApiClient.cs`](../../../../../NewVistas.WpfDelphiUI/Services/ApiClient.cs)
- **Theme / converters:** [`Themes/CprsTheme.xaml`](../../../../../NewVistas.WpfDelphiUI/Themes/CprsTheme.xaml), [`Converters/`](../../../../../NewVistas.WpfDelphiUI/Converters/)
- **Backend controllers:**
  [`DiabetesRegistryController.cs`](../../../../../NewVistas.WebServer/Controllers/DiabetesRegistryController.cs),
  [`ExternalReferralController.cs`](../../../../../NewVistas.WebServer/Controllers/ExternalReferralController.cs) (CHS endpoints),
  [`GpraReportingController.cs`](../../../../../NewVistas.WebServer/Controllers/GpraReportingController.cs)
- **Menu access:** [`MenuAccessMap.cs`](../../../../Security/MenuAccessMap.cs) -- `ClinicalKeys` includes the new `CanManageDiabetesRegistry` and `CanAuthorizeChs` entries
- **Companion Blazor scripts (deeper feature coverage):**
  [Doctors/16-Diabetes-Registry.md](../../Blazor/Doctors/16-Diabetes-Registry.md),
  [Admin/11-CHS-Authorization.md](../../Blazor/Admin/11-CHS-Authorization.md),
  [Admin/12-GPRA-Submission.md](../../Blazor/Admin/12-GPRA-Submission.md)
