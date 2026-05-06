# Patient Selection and Notifications -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 4 and 9 (used in various scenarios)
- Pre-conditions: Demo data loaded with at least some unsigned notes, pending orders, and abnormal lab results to generate notifications. SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Select a Patient via Type-Ahead Search (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Click in the **Patient ID** field in the toolbar
3. Begin typing the first few characters of a patient name (e.g., `SMI`)
4. Observe the type-ahead dropdown that appears below the field

### Expected Result
- As characters are typed, a dropdown list appears showing matching patients
- Each row in the dropdown shows: Patient ID, Name (Last, First), SSN (last 4), DOB
- Matches highlight the typed characters in bold
- The list narrows as more characters are typed
- Typing a numeric ID (e.g., `4`) matches by patient ID directly

### Steps (continued)
5. Select a patient from the dropdown list by clicking on it (or use arrow keys and press Enter)

### Expected Result
- The Patient ID field populates with the selected patient's ID
- The current view automatically loads data for the selected patient
- The **Patient Banner** at the top of the view updates with the selected patient's demographics

---

## Scenario 2: View Patient Notifications/Alerts

### Steps
1. With DOCTOR1 logged in, locate the **Notifications** button in the toolbar (bell icon with a count status indicator)
2. Click the **Notifications** button
3. A **Notifications** panel slides open (or a dialog window appears)

### Expected Result
- The Notifications panel contains a DataGrid with columns:
  - **Info** -- icon indicating alert type (lab flask, document, order arrow, flag)
  - **Patient** -- patient name and ID
  - **Location** -- clinic or ward where the alert originated
  - **Urgency** -- status indicator: High (red), Moderate (orange), Low (blue)
  - **Alert Date/Time** -- when the notification was generated
  - **Message** -- brief description (e.g., "Unsigned note requires signature", "STAT lab result: Potassium 6.2 mEq/L", "Order requires cosignature")
- Notifications are sorted by date/time descending (newest first)
- The count status indicator on the toolbar button matches the number of unprocessed alerts
- If no notifications exist: "No pending notifications" is displayed

---

## Scenario 3: Process a Notification

### Steps
1. In the Notifications panel, locate a notification with message containing "Unsigned note" or "abnormal lab result"
2. Click the notification row to select it
3. Click the **Process** button (or double-click the row)

### Expected Result
- The application navigates to the relevant view for the notification type:
  - "Unsigned note" notification opens the **Notes** view with the specific note pre-loaded and ready to sign
  - "Abnormal lab result" notification opens the **Labs** view with the specific result highlighted
  - "Order requires cosignature" notification opens the **Orders** view with the order selected
- The patient is automatically loaded in the destination view
- After processing, the notification is removed from the unprocessed list
- The count status indicator on the Notifications button decrements by 1

---

## Scenario 4: Forward a Notification

### Steps
1. Open the Notifications panel
2. Select a notification (e.g., a pending consult notification)
3. Click the **Forward** button
4. A **Forward Notification** dialog window appears:
   - Recipient: ComboBox with provider search (type-ahead) -- type `CHEN` and select `CHEN,MICHAEL L`
   - Comment: TextBox -- enter `Please review this consult request while I am on leave.`
   - Urgency: ComboBox (High, Moderate, Low) -- select **Moderate**
5. Click **Forward** in the dialog

### Expected Result
- A green notification appears in the status bar: "Notification forwarded to CHEN,MICHAEL L."
- The forwarded notification is removed from DOCTOR1's notification list
- The count status indicator decrements

---

## Scenario 5: Remove a Notification

### Steps
1. Open the Notifications panel
2. Select a notification that has been reviewed but does not require further action
3. Click the **Remove** button (or right-click and select **Remove**)
4. A confirmation dialog appears: "Remove this notification? This cannot be undone."
5. Click **Yes**

### Expected Result
- The notification is removed from the list
- A green notification appears in the status bar: "Notification removed."
- The count status indicator decrements
- The notification does not reappear on subsequent loads

---

## Scenario 6: Defer a Notification

### Steps
1. Open the Notifications panel
2. Select a notification
3. Click the **Defer** button
4. A **Defer Notification** dialog window appears:
   - Defer Until: DatePicker with time -- select tomorrow's date, 08:00
   - Reason (optional): TextBox -- enter `Will address during morning rounds`
5. Click **Defer** in the dialog

### Expected Result
- A green notification appears in the status bar: "Notification deferred until [date/time]."
- The notification is removed from the active notifications list
- It will reappear in the notifications list after the deferred date/time
- A subtle indicator (clock icon) shows it was deferred if viewed in a "Deferred" tab or filter

### Steps (continued)
6. In the Notifications panel, look for a **Show Deferred** CheckBox or toggle
7. Check **Show Deferred**

### Expected Result
- The deferred notification reappears in the list with a clock icon and the deferred date shown in the Alert Date/Time column
- The message column appends: "(Deferred until [date])"

---

## Scenario 7: Duplicate Patient Warning (Disambiguation)

### Steps
1. In the Patient ID field in the toolbar, type a name that matches multiple patients (e.g., `SMITH`)
2. The type-ahead dropdown shows multiple matches
3. If two patients have very similar names and DOB, a **Patient Disambiguation** dialog window appears (triggered when selected patient has known duplicates)

### Expected Result
- The disambiguation dialog shows a DataGrid with potentially matching patients:
  - Columns: DFN (internal ID), Full Name, Date of Birth, SSN (last 4), Sex, Veteran Status
  - Rows are highlighted to show differences (e.g., differing DOB or SSN)
- A warning message at the top: "Multiple patients match. Please verify the correct patient."
- The user must click a specific row and then click **Select** to confirm

### Steps (continued)
4. Click the correct patient row
5. Click **Select**

### Expected Result
- The dialog closes
- The selected patient loads into the current view
- The Patient Banner updates with the confirmed patient's demographics

---

## Scenario 8: Patient Banner Verification

### Steps
1. In the Navigation Panel, select **Cover Sheet**
2. Enter Patient ID in the toolbar: `9`
3. Click **Load**
4. Examine the Patient Banner at the top of the view

### Expected Result
- The Patient Banner displays the following information:
  - **Name**: bold, navy font (e.g., "DOE,JANE M")
  - **SSN**: last 4 digits only (e.g., "***-**-1234")
  - **DOB/Age**: formatted date and calculated age (e.g., "03/15/1958 (67)")
  - **Sex**: M or F
  - **Location**: current clinic or ward (if admitted, shows ward/room/bed)
  - **Primary Care Provider**: provider name (e.g., "PCP: SMITH,JOHN A")
  - **Primary Care Team**: team name if assigned
  - **Attending**: attending physician name (if inpatient)
  - **CWAD Indicators**: red status indicators for any active flags:
    - **C** = Crisis note on file
    - **W** = Warning flag active
    - **A** = Allergy documented
    - **D** = Advance Directive on file
  - **Postings**: additional status indicators if applicable (e.g., DNR, behavioral flag)
  - **Service Connected**: green status indicator if service-connected (e.g., "SC 30%")
  - **Combat Veteran**: status indicator if combat veteran status is active

### Steps (continued)
5. Load patient `4` and verify the banner updates

### Expected Result
- All banner fields update to reflect patient 4's demographics
- CWAD indicators change based on patient 4's documented flags
- If patient 4 is admitted, the Location field shows the ward assignment
- If patient 4 has no CWAD data, those indicators are absent (not shown as empty placeholders)
