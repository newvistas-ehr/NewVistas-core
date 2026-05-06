# User Options and Preferences -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: (none required for most scenarios; patient 9 used for verification)
- Pre-conditions: SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Open User Options Dialog (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. From the menu bar, click **Tools** > **Options** (or press Ctrl+Shift+O)

### Expected Result
- A **User Options** dialog window opens
- The dialog contains a **TabControl** with the following tabs:
  - **Chart Defaults**
  - **Patient Selection**
  - **Notes / Titles**
  - **Reminders**
  - **Reports**
  - **Surrogate**
  - **Notifications**
  - **Other**
- The **Chart Defaults** tab is selected by default
- A **Save** button and **Cancel** button are at the bottom of the dialog
- A **Restore Defaults** button is in the bottom-left corner

---

## Scenario 2: Set Chart Defaults (Date Ranges per Tab)

### Steps
1. On the **Chart Defaults** tab in the User Options dialog
2. The tab displays a list of views with a date range ComboBox for each:
   - Orders: ComboBox (1 Week, 2 Weeks, 1 Month, 3 Months, 6 Months, 1 Year, 2 Years, All)
   - Labs: ComboBox (same options)
   - Notes: ComboBox (same options)
   - Consults: ComboBox (same options)
   - D/C Summaries: ComboBox (same options)
   - Reports: ComboBox (same options)
   - Appointments: ComboBox (1 Week, 2 Weeks, 1 Month, 3 Months, 6 Months)
3. Set the following:
   - Orders: **1 Month**
   - Labs: **6 Months**
   - Notes: **1 Year**
   - Consults: **3 Months**
   - D/C Summaries: **1 Year**
   - Reports: **6 Months**
   - Appointments: **3 Months**
4. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."
- The dialog remains open (save does not close)

### Steps (verification)
5. Click **Cancel** to close the dialog
6. In the Navigation Panel, select **Orders**
7. Enter Patient ID: `9`, click **Load**

### Expected Result
- The Orders DataGrid loads with data filtered to the past 1 month (per the new default)
- The date filter indicator (if shown) displays: "Last 1 Month"

---

## Scenario 3: Configure Patient Selection Defaults

### Steps
1. Open **Tools** > **Options**
2. Click the **Patient Selection** tab
3. The tab displays:
   - Default Patient List Source: ComboBox with options:
     - Provider (shows the logged-in provider's patients)
     - Team (shows patients from the provider's team)
     - Ward (shows patients on a specific ward)
     - Clinic (shows patients from a specific clinic)
     - All
   - Default Clinic: ComboBox (enabled when Source = Clinic) -- lists available clinics
   - Default Team: ComboBox (enabled when Source = Team) -- lists available teams
   - Default Ward: ComboBox (enabled when Source = Ward) -- lists available wards
   - Initial Sort: ComboBox (Name A-Z, Name Z-A, Last Visit, Room/Bed)
   - Auto-Select Last Patient: CheckBox (if checked, automatically loads the last patient viewed on login)
4. Set:
   - Default Patient List Source: **Clinic**
   - Default Clinic: select **PRIMARY CARE CLINIC A** from the ComboBox
   - Initial Sort: **Name A-Z**
   - Auto-Select Last Patient: check the CheckBox
5. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."

### Steps (verification)
6. Close the Options dialog
7. Close and relaunch the WPF Application
8. Log in as **DOCTOR1**

### Expected Result
- The patient selection area (if a patient list is displayed) defaults to showing patients from PRIMARY CARE CLINIC A
- The list is sorted by Name A-Z
- The last patient viewed (e.g., patient 9) is automatically loaded in the toolbar

---

## Scenario 4: Set Note Defaults

### Steps
1. Open **Tools** > **Options**
2. Click the **Notes / Titles** tab
3. The tab displays:
   - Default Note Title: ComboBox (PROGRESS NOTE, DISCHARGE SUMMARY, CONSULT NOTE, SURGICAL NOTE, CRISIS NOTE, ADVANCE DIRECTIVE)
   - Default Cosigner: TextBox with type-ahead (optional; leave blank for no default cosigner)
   - Auto-Save Interval: ComboBox (Off, 1 minute, 2 minutes, 5 minutes, 10 minutes)
   - Default Location: ComboBox (lists clinics/wards)
   - Ask for Visit at Note Creation: CheckBox (if checked, prompts for visit linkage when creating a note)
4. Set:
   - Default Note Title: **PROGRESS NOTE**
   - Default Cosigner: leave blank
   - Auto-Save Interval: **5 minutes**
   - Default Location: **PRIMARY CARE CLINIC A**
   - Ask for Visit at Note Creation: check the CheckBox
5. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."

### Steps (verification)
6. Close the Options dialog
7. In the Navigation Panel, select **Notes**
8. Load patient 9 and click **+ New Note**

### Expected Result
- The New Note form pre-populates:
  - Document Type: PROGRESS NOTE (from default)
  - Location: PRIMARY CARE CLINIC A (from default)
- A dialog prompts: "Link this note to a visit?" with **Select Visit** / **Skip** buttons (because Ask for Visit at Note Creation is checked)

---

## Scenario 5: Configure Reminder Display

### Steps
1. Open **Tools** > **Options**
2. Click the **Reminders** tab
3. The tab displays:
   - A DataGrid listing available clinical reminders with columns:
     - Reminder Name (e.g., "Influenza Vaccination", "Colorectal Cancer Screening", "Depression Screening PHQ-9")
     - Display: CheckBox (checked = show on Cover Sheet)
     - Evaluation Frequency: ComboBox (Every Visit, Daily, Weekly, Monthly, Quarterly)
   - A **Select All** / **Deselect All** button pair above the grid
4. Uncheck **Display** for "Influenza Vaccination" (hide it from the cover sheet)
5. For "Depression Screening PHQ-9", change Evaluation Frequency to **Every Visit**
6. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."

### Steps (verification)
7. Close the Options dialog
8. Navigate to the **Cover Sheet** for patient 9

### Expected Result
- The Clinical Reminders panel on the Cover Sheet does not show "Influenza Vaccination"
- Other reminders remain visible
- "Depression Screening PHQ-9" evaluates based on the "Every Visit" frequency setting

---

## Scenario 6: Set Report Defaults

### Steps
1. Open **Tools** > **Options**
2. Click the **Reports** tab
3. The tab displays:
   - Default Report: ComboBox listing available reports (Comprehensive Health Summary, Lab Results by Date, Active Problems, etc.)
   - Default Date Range: ComboBox (1 Week, 1 Month, 3 Months, 6 Months, 1 Year, 2 Years, All)
   - Auto-Generate on Patient Load: CheckBox (if checked, the default report auto-runs when a patient is selected on the Reports view)
4. Set:
   - Default Report: **Comprehensive Health Summary**
   - Default Date Range: **6 Months**
   - Auto-Generate on Patient Load: check the CheckBox
5. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."

### Steps (verification)
6. Close the Options dialog
7. In the Navigation Panel, select **Reports**
8. Enter Patient ID: `9`, click **Load**

### Expected Result
- The Reports view opens with "Comprehensive Health Summary" already selected in the TreeView
- The report auto-generates and displays in the right pane (because Auto-Generate is checked)
- The Date Range is set to 6 Months

---

## Scenario 7: Manage Surrogate (Designate a Cover Provider)

### Steps
1. Open **Tools** > **Options**
2. Click the **Surrogate** tab
3. The tab displays:
   - Current Surrogate: label showing "None" (or a previously designated provider)
   - Designate Surrogate: TextBox with type-ahead for provider search
   - Start Date: DatePicker
   - End Date: DatePicker
   - Reason (optional): TextBox
   - **Set Surrogate** button
   - **Remove Surrogate** button (disabled if no surrogate is set)
4. Fill in:
   - Designate Surrogate: type `CHEN` -- select **CHEN,MICHAEL L**
   - Start Date: select tomorrow's date
   - End Date: select a date 7 days from tomorrow
   - Reason: `Annual leave -- Dr. Chen covering primary care panel`
5. Click **Set Surrogate**

### Expected Result
- A green notification appears in the status bar: "Surrogate designated: CHEN,MICHAEL L"
- The Current Surrogate label updates to: "CHEN,MICHAEL L (from [start] to [end])"
- The **Remove Surrogate** button becomes enabled
- During the surrogate period, notifications addressed to DOCTOR1 will also be visible to DOCTOR2 (CHEN)

### Steps (continued)
6. Click **Remove Surrogate**
7. A confirmation dialog appears: "Remove surrogate designation for CHEN,MICHAEL L?"
8. Click **Yes**

### Expected Result
- A green notification appears in the status bar: "Surrogate removed."
- The Current Surrogate label returns to "None"
- The Remove Surrogate button is disabled again

---

## Scenario 8: Configure Alert Notifications

### Steps
1. Open **Tools** > **Options**
2. Click the **Notifications** tab
3. The tab displays:
   - A DataGrid of alert types with columns:
     - Alert Type (e.g., "Abnormal Lab Result", "Unsigned Note Reminder", "Consult Response", "Order Requires Cosignature", "Imaging Result Available", "Flagged Order Expiring", "Appointment Reminder")
     - Receive: CheckBox (checked = receive this alert type)
     - Urgency Override: ComboBox (Default, High, Moderate, Low)
   - Alert Display Range: ComboBox (Last 24 Hours, Last 3 Days, Last 7 Days, Last 30 Days, All)
   - Enable Desktop Notifications: CheckBox (if checked, shows Windows toast notifications for high-urgency alerts)
   - Notification Sound: CheckBox (if checked, plays a sound for new alerts)
4. Set:
   - Uncheck **Receive** for "Appointment Reminder" (disable appointment reminders)
   - Set "Abnormal Lab Result" Urgency Override to **High**
   - Alert Display Range: **Last 7 Days**
   - Enable Desktop Notifications: check the CheckBox
   - Notification Sound: check the CheckBox
5. Click **Save**

### Expected Result
- A green notification appears in the status bar: "User options saved."

### Steps (verification)
6. Close the Options dialog
7. Click the **Notifications** button (bell icon) in the toolbar

### Expected Result
- The Notifications panel shows alerts from the last 7 days only (per the display range setting)
- No "Appointment Reminder" alerts appear (since that type was disabled)
- Abnormal lab result alerts are displayed with **High** urgency indicator (red) regardless of their original urgency

### Steps (continued)
8. Reopen **Tools** > **Options** > **Notifications** tab
9. Click **Restore Defaults** at the bottom of the dialog
10. A confirmation dialog appears: "Restore all notification settings to defaults?"
11. Click **Yes**

### Expected Result
- All alert types are re-checked (all enabled)
- Urgency overrides reset to "Default"
- Alert Display Range resets to "Last 30 Days"
- Desktop Notifications and Sound checkboxes reset to unchecked
- A green notification appears in the status bar: "Settings restored to defaults."
