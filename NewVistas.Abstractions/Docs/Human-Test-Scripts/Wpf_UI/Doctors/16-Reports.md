# Clinical Reports -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded (patients with lab results, notes, consults, radiology reports). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Navigate Report Categories (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Reports**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load** (or press Enter)

### Expected Result
- The view title shows "Reports"
- A **Patient Banner** appears at the top with patient name, SSN (last 4), age, and location
- The left pane contains a **TreeView** with expandable report categories:
  - **Clinical Reports**
    - Cover Sheet Summary
    - Active Problems
    - Active Medications
    - Crisis Notes
    - CWAD Summary
  - **Health Summary**
    - Comprehensive Health Summary
    - Brief Health Summary
  - **Lab Reports**
    - Most Recent Lab Results
    - Cumulative Lab Report
    - Lab Results by Date
    - Abnormal Results
    - Microbiology
    - Blood Bank
  - **Radiology**
    - Radiology Reports
    - Imaging Status
  - **Consult Reports**
    - All Consults
    - Pending Consults
    - Completed Consults
  - **Surgery Reports**
    - Surgical Case Reports
    - Anesthesia Reports
  - **Discharge Summaries**
    - All Discharge Summaries
    - Recent Discharge Summaries
  - **Progress Notes**
    - All Notes
    - Notes by Author
    - Unsigned Notes
- The right pane shows italic placeholder text: "Select a report to view"
- Below the TreeView, a **Date Range** group with RadioButton options:
  - 1 Week, 1 Month, 6 Months, 1 Year, 2 Years, All Results, Today, Custom

---

## Scenario 2: View a Clinical Report

### Steps
1. With patient 9 loaded on the Reports view
2. Expand the **Clinical Reports** category in the TreeView
3. Click **Active Problems**

### Expected Result
- The right pane displays the Active Problems report in a formatted text block:
  - Header: "Active Problems Report -- Patient: [Name] -- Generated: [date/time]"
  - A list of active problems with columns: Problem #, ICD-10 Code, Description, Onset Date, Status
  - Footer: "End of Report -- [count] active problem(s)"
- The report text is selectable (can highlight and copy)

### Steps (continued)
4. Click **Active Medications** under Clinical Reports

### Expected Result
- The right pane refreshes to show the Active Medications report:
  - Header with patient name and generation date
  - Sections for Outpatient Medications, Inpatient Medications, Non-VA Medications
  - Each medication shows: Drug, Sig, Status, Start Date, Prescriber

---

## Scenario 3: View a Lab Report with Date Range

### Steps
1. Expand the **Lab Reports** category in the TreeView
2. Click **Cumulative Lab Report**
3. In the Date Range group, select the **1 Month** RadioButton
4. The report generates automatically on date range change

### Expected Result
- The right pane shows a cumulative lab report for the past month:
  - Header: "Cumulative Lab Report -- [date range]"
  - A tabular layout with test names as rows and collection dates as columns
  - Abnormal values displayed with an asterisk (*) and bold formatting
  - Reference ranges shown in parentheses next to each value
  - Tests with no results in the period show "--" placeholders
- API call: `GET /api/lab/9/results` (filtered by date range)

### Steps (continued)
5. Change the Date Range to **6 Months**

### Expected Result
- The report regenerates with the wider date range
- More columns appear (more collection dates)
- A horizontal scrollbar appears if the report is wider than the pane

---

## Scenario 4: View Health Summary

### Steps
1. Expand the **Health Summary** category in the TreeView
2. Click **Comprehensive Health Summary**

### Expected Result
- The right pane displays a comprehensive summary with sections:
  - **Demographics** -- Name, SSN, DOB, Age, Sex, Address, Phone
  - **Active Problems** -- Current problem list
  - **Allergies/ADR** -- All documented allergies with severity and reactions
  - **Active Medications** -- Outpatient and inpatient medications
  - **Recent Vitals** -- Most recent set of vital signs
  - **Recent Labs** -- Last 5 lab results with abnormal flagging
  - **Immunizations** -- Documented immunizations with dates
  - **Upcoming Appointments** -- Next 5 scheduled appointments
  - **Clinical Reminders** -- Due and overdue reminders
- Each section has a bold header with a horizontal rule separator
- Sections with no data show "None documented" or "No data available"

---

## Scenario 5: Print a Report

### Steps
1. With any report displayed in the right pane (e.g., Comprehensive Health Summary from Scenario 4)
2. Click the **Print** button in the report toolbar (or press Ctrl+P)
3. A **Print** dialog window appears:
   - Device: ComboBox listing available printers / "Win Printer" / "PDF Export"
   - Copies: numeric spinner (default 1)
   - Preview: CheckBox (checked by default)
4. Select Device: **Win Printer**
5. Ensure Preview is checked
6. Click **Print**

### Expected Result
- A print preview window opens showing the formatted report
- The preview includes:
  - Page header: patient name, SSN (last 4), report title, page number
  - Page footer: facility name, print date/time, "CONFIDENTIAL"
- The user can close the preview or proceed to print
- If printing, the standard Windows print dialog appears for printer selection

---

## Scenario 6: Copy Report to Clipboard

### Steps
1. With any report displayed in the right pane
2. Right-click anywhere in the report text
3. A context menu appears with options: **Copy All**, **Copy Selection**, **Print**
4. Click **Copy All** (or press Ctrl+A then Ctrl+C)

### Expected Result
- A green notification appears in the status bar: "Report copied to clipboard."
- The full report text is on the system clipboard
- Paste into a text editor (Notepad) to verify the content is plain text with formatting preserved via spacing

### Steps (continued)
5. Highlight a portion of the report text with the mouse
6. Right-click and select **Copy Selection**

### Expected Result
- Only the highlighted text is copied to the clipboard
- A green notification appears: "Selection copied to clipboard."

---

## Scenario 7: View Remote Data Report

### Steps
1. Expand the **Health Summary** category (or a top-level **Remote Data** category if present)
2. Click **Remote Data** (or **Remote Patient Data**)
3. If the patient has MPI correlations to other facilities, a facility selection list appears

### Expected Result
- If remote data is available (MPI correlations exist):
  - A list of treating facilities appears with columns: Facility Name, Last Seen Date, Station Number
  - Clicking a facility loads remote data summary from that site
  - Data displayed is read-only with a banner: "REMOTE DATA -- [Facility Name]"
- If no remote data is available:
  - The right pane shows: "No remote data available for this patient. No treating facility correlations found in MPI."
- API call: `GET /api/mpi/{icn}/treatingfacilities` (where ICN is the patient's integration control number)

---

## Scenario 8: Custom Date Range Report

### Steps
1. Expand the **Lab Reports** category and click **Lab Results by Date**
2. In the Date Range group, select the **Custom** RadioButton
3. Two DatePicker fields appear:
   - From Date: select `01/01/2026`
   - To Date: select `03/30/2026`
4. Click **Generate** (or the report auto-generates on date selection)

### Expected Result
- The right pane shows lab results filtered to the custom date range:
  - Header: "Lab Results -- 01/01/2026 to 03/30/2026"
  - Results listed chronologically with: Collection Date, Test Name, Result, Units, Reference Range, Flag
  - Abnormal values shown with bold text and asterisk
  - A count summary at the bottom: "[N] result(s) in date range ([M] abnormal)"
- Changing either date and pressing Enter (or clicking Generate) refreshes the report
- If no results exist in the range: "No lab results found for the selected date range."
