# Order Flagging -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. Patient 9 should have at least 2-3 active orders (use Orders view to place orders if needed). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Flag an Active Order (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. In the Navigation Panel, select **Orders**
3. Enter Patient ID in the toolbar: `9`
4. Click **Load** (or press Enter)
5. On the Active Orders TabItem, locate an active order in the DataGrid (e.g., "CBC WITH DIFFERENTIAL" with status "Active")
6. Right-click the order row and select **Flag for Follow-Up** (or click the **Flag** button in the row actions)
7. A **Flag Order** dialog window appears with:
   - Reason: ComboBox with options:
     - Clarification Needed
     - Follow-Up Required
     - Drug-Drug Interaction Review
     - Abnormal Result Expected
     - Patient Non-Compliance
     - Insurance/Formulary Issue
     - Other
   - Reason Detail: TextBox (multi-line) for free-text explanation
   - Alert Recipients: a provider search ComboBox with an **Add** button and a list of added recipients
   - Expiration Date: DatePicker (optional; default blank = no expiration)
8. Fill in:
   - Reason: **Follow-Up Required**
   - Reason Detail: `Need to review CBC results before next appointment. If WBC elevated, consider infectious workup.`
   - Alert Recipients: type `CHEN` in the search, select **CHEN,MICHAEL L**, click **Add**
   - Expiration Date: select a date 14 days from today
9. Click **Flag** in the dialog

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Order flagged for follow-up."
- The order row in the DataGrid now shows a **flag icon** (small red/orange flag) next to the order text
- The flag icon has a tooltip: "Flagged: Follow-Up Required (by SMITH,JOHN A)"
- API call: `POST /api/patient/9/orders/{orderId}/flag`

---

## Scenario 2: View Flagged Orders

### Steps
1. With patient 9 loaded on the Orders view
2. Locate the filter toolbar above the Active Orders DataGrid
3. Check the **Flagged Only** CheckBox (or click a **Filter: Flagged** toggle button)

### Expected Result
- The DataGrid filters to show only flagged orders
- Each row shows the flag icon, order text, status, and a "Flag Reason" column (visible when filter is active)
- The filter status indicator shows: "Showing flagged orders only ([N] of [total])"
- If no flagged orders exist: "No flagged orders for this patient."

### Steps (continued)
4. Uncheck the **Flagged Only** filter

### Expected Result
- The DataGrid returns to showing all active orders
- Flagged orders are still identifiable by the flag icon on their rows

---

## Scenario 3: Add Flag Comment

### Steps
1. With patient 9 loaded on the Orders view (unfiltered)
2. Locate the flagged order from Scenario 1 (identified by the flag icon)
3. Right-click the flagged order and select **Flag Details** (or click the flag icon)
4. A **Flag Details** panel opens (either inline below the row or as a dialog window) showing:
   - Flag Reason: "Follow-Up Required"
   - Reason Detail: the original text
   - Flagged By: SMITH,JOHN A
   - Flagged Date: date/time
   - Expiration: date from Scenario 1
   - Alert Recipients: CHEN,MICHAEL L
   - Comments: (empty list initially)
5. Click the **Add Comment** button
6. A comment TextBox appears
7. Enter: `03/30/2026 -- Checked CBC results. WBC normal at 7.2. No further workup needed. Will unflag after patient visit.`
8. Click **Save Comment**

### Expected Result
- The comment appears in the Comments section with timestamp and author:
  - "03/30/2026 14:30 -- SMITH,JOHN A: Checked CBC results. WBC normal at 7.2..."
- A green notification appears in the status bar: "Flag comment added."
- The flag icon remains on the order

---

## Scenario 4: Unflag an Order

### Steps
1. On the Orders view with patient 9 loaded
2. Locate the flagged order
3. Right-click and select **Unflag** (or open Flag Details and click **Unflag**)
4. An **Unflag Order** dialog window appears:
   - Resolution Note: TextBox (required) -- enter `CBC results reviewed. All values within normal limits. No further action required.`
5. Click **Unflag** in the dialog

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Order unflagged."
- The flag icon is removed from the order row in the DataGrid
- The order returns to its normal appearance
- If the Flagged Only filter was active, the order disappears from the filtered view

---

## Scenario 5: Flag with Multiple Recipients

### Steps
1. On the Active Orders DataGrid, locate another active order (e.g., a Pharmacy order)
2. Right-click and select **Flag for Follow-Up**
3. In the Flag Order dialog:
   - Reason: **Drug-Drug Interaction Review**
   - Reason Detail: `Patient started on Warfarin. Need pharmacy and cardiology to review potential interactions with current medication list.`
   - Alert Recipients:
     - Type `CHEN`, select **CHEN,MICHAEL L**, click **Add**
     - Type `JONES`, select **JONES,SARAH K** (if available), click **Add**
     - Type `PHARMACIST` or a known pharmacy user, click **Add**
   - Expiration Date: leave blank (no expiration)
4. Verify the recipients list shows all added providers
5. To remove a recipient, select them in the list and click **Remove**
6. Click **Flag**

### Expected Result
- The dialog closes
- A green notification appears in the status bar: "Order flagged for follow-up."
- The order shows the flag icon
- The flag tooltip shows: "Flagged: Drug-Drug Interaction Review (by SMITH,JOHN A)"
- Each recipient listed will receive a notification alert in their Notifications panel

---

## Scenario 6: View Flag Details (Comprehensive)

### Steps
1. On the Orders view, locate the flagged order from Scenario 5
2. Click the flag icon (or right-click and select **Flag Details**)

### Expected Result
- The Flag Details panel/dialog displays all flag metadata:
  - **Reason**: Drug-Drug Interaction Review
  - **Detail**: Full reason text entered in Scenario 5
  - **Flagged By**: SMITH,JOHN A
  - **Flagged Date/Time**: timestamp when the flag was created
  - **Expiration**: "No expiration" (since it was left blank)
  - **Alert Recipients**: list of all providers added (names with remove option for current user's flags)
  - **Comments**: list of all comments with timestamp and author (empty if none added yet)
  - **Status**: Active (green) or Expired (gray) if past expiration date
- The panel includes action buttons:
  - **Add Comment** -- opens comment entry
  - **Unflag** -- opens unflag dialog with resolution note
  - **Edit Flag** -- allows changing reason, detail, recipients, or expiration (only available to the flag creator)
  - **Close** -- closes the details panel

### Steps (continued)
3. Click **Edit Flag**
4. Change the Expiration Date to 30 days from today
5. Click **Save**

### Expected Result
- A green notification appears in the status bar: "Flag updated."
- The Flag Details panel refreshes to show the new expiration date
