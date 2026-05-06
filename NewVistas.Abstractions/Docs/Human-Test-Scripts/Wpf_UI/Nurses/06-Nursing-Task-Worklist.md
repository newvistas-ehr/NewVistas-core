# Nursing Task Worklist -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE2 / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Nursing Tasks**.
  3. Enter Patient ID `4` in the Patient ID field in the toolbar.
  4. Click **Refresh Worklist** to auto-generate tasks from existing orders and care plan.
  5. Alternatively, click **Load** to view previously generated tasks.

---

## Scenario 1: View Due Tasks and Complete a Task (Happy Path)

### Steps

1. In the Navigation Panel, select **Nursing Tasks**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Refresh Worklist** (this regenerates tasks from the patient's active orders).
4. The **Due Now** TabItem is active by default. Observe the TabItem header which shows the count of due tasks (e.g., "Due Now (3)").
5. The DataGrid displays tasks with columns: Priority, Category, Description, Due, Status, Actions.
6. Locate a task with Status **Due** or **Overdue**.
7. Click the **Done** button on a task (e.g., a Vital Signs task) (or right-click and select **Done**).

### Expected Result

- After clicking **Done**, the worklist reloads.
- The completed task no longer appears on the **Due Now** TabItem (it has Status: Completed).
- Switch to the **All Tasks** TabItem to verify the task now shows:
  - Status: **Completed** (green status indicator)
  - The Done and Defer buttons are no longer visible for that task.
- The Due Now count in the TabItem header decreases by 1.

---

## Scenario 2: Defer a Task with Reason

### Steps

1. On the **Due Now** TabItem, locate a task with Status **Due** (e.g., an Assessment task).
2. Click the **Defer** button on that task (or right-click and select **Defer**).

### Expected Result

- The worklist reloads.
- The deferred task no longer appears on the **Due Now** TabItem.
- Switch to the **All Tasks** TabItem. The task shows:
  - Status: **Deferred** (blue/info status indicator)
  - The Done and Defer buttons are no longer visible for that task.
- The Due Now count decreases.

---

## Scenario 3: Add an Ad-Hoc Task

### Steps

1. Click the **Add Task** TabItem.
2. Fill in:
   - Category ComboBox: `Wound`
   - Priority ComboBox: `Urgent`
   - Description TextBox: `Dressing change on right abdominal surgical wound -- due by 1400`
3. Click **Add Task**.

### Expected Result

- The worklist reloads (view may switch to the task list).
- Click **Load** or **Refresh Worklist** to see all tasks.
- On the **All Tasks** TabItem, the new task appears:
  - Priority: **Urgent** (yellow/warning status indicator)
  - Category: **Wound** (status indicator)
  - Description: Dressing change on right abdominal surgical wound -- due by 1400
  - Due: current time
  - Status: Due (if within window) or as created
- On the **Due Now** TabItem, the task appears if its due time is within the current window.

---

## Scenario 4: Add Multiple Ad-Hoc Tasks of Different Categories

### Steps

1. Click the **Add Task** TabItem.
2. Add the following tasks one at a time:

   **Task A:**
   - Category ComboBox: `VitalSigns`
   - Priority ComboBox: `Routine`
   - Description TextBox: `Post-transfusion vital signs -- 15 minute check`
   - Click **Add Task**.

   **Task B:**
   - Category ComboBox: `PatientEducation`
   - Priority ComboBox: `Routine`
   - Description TextBox: `Insulin self-injection teaching for discharge`
   - Click **Add Task**.

   **Task C:**
   - Category ComboBox: `Assessment`
   - Priority ComboBox: `STAT`
   - Description TextBox: `Neuro checks Q15min -- new onset altered mental status`
   - Click **Add Task**.

3. Click **Load** to view all tasks.

### Expected Result

- The **All Tasks** TabItem shows all added tasks plus any system-generated tasks.
- STAT tasks appear first in the DataGrid with red row highlighting.
- The priority status indicators show:
  - STAT: red/danger status indicator
  - Urgent: yellow/warning status indicator
  - Routine: gray/secondary status indicator
- The category status indicators show appropriate colors:
  - VitalSigns: blue/info
  - Assessment: yellow/warning
  - PatientEducation: gray/secondary
  - Wound: gray/secondary

---

## Scenario 5: Refresh Worklist to See New Orders

### Steps

1. First, ensure Patient 4 has active inpatient medication orders (from BCMA demo data or inpatient pharmacy).
2. On the task worklist view with Patient ID `4`, click **Refresh Worklist**.

### Expected Result

- The worklist regenerates from the patient's current active orders and care plan.
- A "Last refreshed" timestamp appears at the bottom of the task list (e.g., "Last refreshed: 2026-03-29 14:32:15").
- Any newly created orders since the last refresh will generate corresponding medication tasks.
- Previously completed tasks remain with Completed status.

---

## Scenario 6: Complete All Due Tasks

### Steps

1. On the **Due Now** TabItem, click **Done** on each task one by one until no tasks remain.

### Expected Result

- After all tasks are completed, the **Due Now** TabItem shows: "No tasks due right now."
- The TabItem header shows "Due Now" with no count (or count of 0).
- On the **All Tasks** TabItem, all tasks show Status: Completed (green status indicators).

---

## Scenario 7: Add a Discharge Task

### Steps

1. Click the **Add Task** TabItem.
2. Fill in:
   - Category ComboBox: `Discharge`
   - Priority ComboBox: `Routine`
   - Description TextBox: `Complete discharge checklist -- patient education, follow-up appointments, medication reconciliation`
3. Click **Add Task**.

### Expected Result

- The task appears on the All Tasks TabItem with:
  - Category: Discharge status indicator
  - Priority: Routine
  - Status: Due

---

## Reference: Task Categories and Priorities

### Task Categories (NursingTaskCategory enum)

| Category         | Description                        | Indicator Color |
|------------------|------------------------------------|-----------------|
| Medication       | Medication administration tasks     | Blue/Primary    |
| VitalSigns       | Vital sign monitoring tasks         | Blue/Info       |
| Intervention     | Nursing intervention tasks          | Green/Success   |
| Assessment       | Nursing assessment tasks            | Yellow/Warning  |
| Wound            | Wound care tasks                    | Gray/Secondary  |
| PatientEducation | Patient/family education tasks      | Gray/Secondary  |
| Discharge        | Discharge-related tasks             | Gray/Secondary  |
| Other            | Miscellaneous tasks                 | Gray/Secondary  |

### Task Priorities (NursingTaskPriority enum)

| Priority | Description           | Indicator Color  | Row Highlighting |
|----------|-----------------------|------------------|------------------|
| STAT     | Immediate action      | Red/Danger       | Red row          |
| Urgent   | Urgent action needed  | Yellow/Warning   | Yellow row       |
| Routine  | Standard timing       | Gray/Secondary   | none             |

### Task Statuses (NursingTaskStatus enum)

| Status    | Description            | Indicator Color |
|-----------|------------------------|-----------------|
| Due       | Task is due now        | Yellow/Warning  |
| Overdue   | Task is past due       | Red/Danger      |
| Completed | Task has been done     | Green/Success   |
| Deferred  | Task was deferred      | Blue/Info       |
