# Graphing and Trending -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded with lab results and vitals over multiple dates (run lab demo load and record vitals on several occasions). SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: Open Graphing Tool from Tools Menu

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Enter Patient ID in the toolbar: `9`
3. Click **Load** (or press Enter) on any view to establish the active patient
4. From the menu bar, click **Tools** > **Graphing**

### Expected Result
- A **Graphing** view opens (either as a new TabItem in the main content area or as a separate window)
- The view contains:
  - Left pane: **Available Items** panel with a TreeView listing graphable data categories:
    - **Lab Tests** (expandable to show individual tests: Hemoglobin, WBC, Glucose, Creatinine, etc.)
    - **Vital Signs** (expandable: Blood Pressure Systolic, Blood Pressure Diastolic, Heart Rate, Temperature, Weight, Respiratory Rate, SpO2, Pain)
  - Right pane: empty chart area with placeholder text: "Select items to graph"
  - Bottom toolbar: Date Range ComboBox, **Graph** button, **Clear** button, **Settings** button
- The Date Range ComboBox defaults to **6 Months** with options: 1 Week, 1 Month, 3 Months, 6 Months, 1 Year, 2 Years, All, Custom

---

## Scenario 2: Select Lab Tests for Graphing

### Steps
1. In the Available Items TreeView, expand **Lab Tests**
2. Check the CheckBox next to **Hemoglobin**
3. Check the CheckBox next to **WBC**
4. Click the **Graph** button (or double-click the item to add and graph immediately)

### Expected Result
- The chart area displays a line graph with:
  - X-axis: dates (collection dates for the selected date range)
  - Y-axis: values (auto-scaled to fit both Hemoglobin and WBC ranges)
  - Two colored series lines, each with data point markers:
    - Hemoglobin (e.g., blue line) with reference range shaded band (e.g., 12.0-16.0 g/dL)
    - WBC (e.g., red line) with reference range shaded band (e.g., 4.5-11.0 K/uL)
  - A legend in the top-right corner showing series name, color, and units
- Hovering over a data point shows a tooltip: "Hemoglobin: 14.2 g/dL -- 03/15/2026"
- Data points outside reference ranges are displayed as filled circles (normal) vs. triangles (abnormal)
- API calls: `GET /api/lab/9/results` filtered by test type and date range

---

## Scenario 3: Graph Vital Signs Over Time

### Steps
1. Uncheck **Hemoglobin** and **WBC** in the Available Items TreeView (or click **Clear** to reset)
2. Expand **Vital Signs**
3. Check **Blood Pressure Systolic**
4. Check **Blood Pressure Diastolic**
5. Check **Heart Rate**
6. Click **Graph**

### Expected Result
- The chart displays three series:
  - Blood Pressure Systolic (e.g., red line) with reference band (90-140 mmHg)
  - Blood Pressure Diastolic (e.g., blue line) with reference band (60-90 mmHg)
  - Heart Rate (e.g., green line) with reference band (60-100 bpm)
- The Y-axis auto-scales to encompass all three series
- Each data point corresponds to a vitals measurement date
- Blood pressure readings that exceed the reference range are highlighted with triangle markers
- API call: `GET /api/patient/9/vitals`

---

## Scenario 4: Dual View Graphs (Labs on Top, Vitals on Bottom)

### Steps
1. Click the **Settings** button in the bottom toolbar
2. A **Graph Settings** dialog window appears
3. Under Layout, select **Dual View** RadioButton (options: Single View, Dual View)
4. Click **OK** to close Settings
5. In the Available Items TreeView:
   - Check **Glucose** under Lab Tests
   - Check **Weight** under Vital Signs
6. Click **Graph**

### Expected Result
- The chart area splits into two vertically stacked panels:
  - **Top panel**: Lab data -- Glucose trend line with reference range band (70-100 mg/dL fasting)
  - **Bottom panel**: Vital data -- Weight trend line
- Both panels share the same X-axis (date range) and are time-aligned
- Each panel has its own Y-axis label and scale
- A thin horizontal splitter separates the two panels (draggable to resize)

---

## Scenario 5: Change Date Range on Graph

### Steps
1. With a graph displayed (e.g., Glucose from Scenario 4)
2. In the Date Range ComboBox at the bottom, change from **6 Months** to **1 Year**

### Expected Result
- The graph refreshes with the wider date range
- More data points appear if results exist beyond 6 months
- The X-axis labels adjust (e.g., monthly labels instead of weekly)

### Steps (continued)
3. Change Date Range to **1 Week**

### Expected Result
- The graph narrows to the past 7 days
- If only 1-2 data points exist, the chart shows those points with wider spacing
- If no data exists in the past week: "No data available for the selected date range" appears in the chart area

### Steps (continued)
4. Change Date Range to **Custom**
5. Two DatePicker fields appear inline:
   - From: select `01/01/2026`
   - To: select `03/30/2026`
6. Click **Apply** (or the graph auto-refreshes on date selection)

### Expected Result
- The graph shows only data within the custom date range
- The Date Range ComboBox shows "Custom: 01/01/2026 - 03/30/2026"

---

## Scenario 6: Graph Settings (3D, Legend, Grid Lines, Value Markers)

### Steps
1. With a graph displayed, click the **Settings** button
2. The **Graph Settings** dialog window contains:
   - **3D Effect**: CheckBox (default unchecked)
   - **Show Legend**: CheckBox (default checked)
   - **Show Grid Lines**: CheckBox (default checked)
   - **Show Value Markers**: CheckBox (default checked) -- shows data point values on the chart
   - **Line Thickness**: ComboBox (1px, 2px, 3px; default 2px)
   - **Layout**: RadioButton group (Single View, Dual View)
3. Check **3D Effect**
4. Uncheck **Show Grid Lines**
5. Click **OK**

### Expected Result
- The graph re-renders with a subtle 3D perspective effect on the chart area
- Grid lines are removed, leaving only axis lines
- Value markers and legend remain visible
- The settings persist for the current session

### Steps (continued)
6. Reopen Settings, uncheck **Show Legend**, click **OK**

### Expected Result
- The legend disappears from the chart
- Series are still identifiable by color; hovering a line shows the series name in the tooltip

---

## Scenario 7: Print/Export Graph

### Steps
1. With a graph displayed showing at least two series
2. Right-click anywhere on the chart area
3. A context menu appears with: **Print**, **Copy to Clipboard**, **Export as PNG**, **Export as PDF**
4. Click **Export as PNG**
5. A file save dialog appears -- choose a location and filename
6. Click **Save**

### Expected Result
- The graph is saved as a PNG image file at the selected location
- A green notification appears in the status bar: "Graph exported to [filename].png"

### Steps (continued)
7. Right-click the chart again and select **Print**
8. A **Print** dialog window appears with:
   - Device: ComboBox (Win Printer, PDF Export)
   - Include Data Table: CheckBox (includes a table of values below the graph)
   - Preview: CheckBox (checked by default)
9. Check **Include Data Table** and **Preview**
10. Click **Print**

### Expected Result
- A print preview window shows:
  - Patient header (name, SSN last 4)
  - The chart image
  - A data table below the chart with columns: Date, and one column per graphed item, showing numeric values
  - Footer with date/time and "CONFIDENTIAL"

---

## Scenario 8: Graph Multiple Lab Values with Reference Lines

### Steps
1. Click **Clear** to reset the graph
2. In the Available Items TreeView, expand **Lab Tests** and check:
   - **Hemoglobin**
   - **Hematocrit**
   - **MCV**
   - **Platelet Count**
3. Set Date Range to **1 Year**
4. Click **Graph**

### Expected Result
- The chart displays four series with distinct colors and a legend
- Each series has its own reference range displayed as a semi-transparent horizontal band:
  - Hemoglobin: 12.0-16.0 g/dL
  - Hematocrit: 36-46%
  - MCV: 80-100 fL
  - Platelet Count: 150-400 K/uL
- Reference bands are color-matched to their series (lighter shade of the series color)
- Values outside reference ranges have triangle markers and bold tooltip text: "ABNORMAL: Hemoglobin 10.8 g/dL (Low) -- 02/10/2026"
- The Y-axis auto-scales to accommodate all four series; if ranges differ greatly, the axis uses a normalized scale or dual Y-axes
- Clicking a data point in the chart selects it and shows a detail panel below the chart: Test Name, Value, Units, Reference Range, Flag, Collection Date
