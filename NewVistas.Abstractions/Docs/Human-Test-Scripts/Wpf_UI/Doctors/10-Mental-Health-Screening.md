# Mental Health Screening -- Physician Human Test Script -- WPF UI

## Prerequisites
- Login: DOCTOR5 / Password: smythVista1
- Patient: 35
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) are running.

---

## Scenario 1: PHQ-9 Depression Screen -- Score 12 (Moderate)

### Part A: Record the Screen

#### Steps
1. Log in as **DOCTOR5** (JACKSON,WILLIAM R / Neurology)
2. In the Navigation Panel, select **Mental Health**
3. Enter Patient ID in the toolbar: `35`
4. Click **Load** (or press Enter)
5. Note the three TabItems: **Screenings**, **Screening Detail**, **New Screening**
6. Click the **New Screening** TabItem
7. Fill in the "Record Mental Health Screen" form:
   - Instrument *: **PHQ-9** (ComboBox; options: PHQ-2, PHQ-9, GAD-7, PC-PTSD-5, AUDIT-C, Columbia Suicide Severity)
   - Date Administered: Today's date and time
   - Total Score: `12`
   - Score Interpretation: **MODERATE** (ComboBox; options: MINIMAL, MILD, MODERATE, MODERATELY SEVERE, SEVERE)
   - Positive Screen?: **Yes** (ComboBox; options: Yes, No)
   - Administered By: `JACKSON,WILLIAM R`
   - Location: `NEUROLOGY CLINIC`
   - Comments: `Patient referred by PCP for depression screening during neurology follow-up. Reports low mood x 4 weeks.`
8. Click **Record**

#### Expected Result
- A green notification appears in the status bar: "Screen recorded."
- View switches to Screenings TabItem
- New screening appears in the DataGrid with:
  - Date: today
  - Instrument: PHQ-9
  - Score: 12
  - Interpretation: MODERATE
  - Positive?: YES
  - Risk: "--" (not yet assessed)
  - Status: status indicator (red because IsPositiveScreen = true)

### Part B: Enter Individual Item Responses

#### Steps
1. Click the PHQ-9 screening row to open the **Screening Detail** TabItem
2. Detail shows all recorded information
3. Scroll to the **Item Responses** section
4. Enter each PHQ-9 question response:

   **Question 1:**
   - Item #: `1`
   - Response Value: `1`
   - Question Text: `Little interest or pleasure in doing things`
   - Response Text: `Several days`
   - Click **Add Response**

   **Question 2:**
   - Item #: `2`
   - Response Value: `2`
   - Question Text: `Feeling down, depressed, or hopeless`
   - Response Text: `More than half the days`

   **Question 3:**
   - Item #: `3`
   - Response Value: `2`
   - Question Text: `Trouble falling or staying asleep, or sleeping too much`
   - Response Text: `More than half the days`

   **Question 4:**
   - Item #: `4`
   - Response Value: `1`
   - Question Text: `Feeling tired or having little energy`
   - Response Text: `Several days`

   **Question 5:**
   - Item #: `5`
   - Response Value: `1`
   - Question Text: `Poor appetite or overeating`
   - Response Text: `Several days`

   **Question 6:**
   - Item #: `6`
   - Response Value: `2`
   - Question Text: `Feeling bad about yourself or that you are a failure`
   - Response Text: `More than half the days`

   **Question 7:**
   - Item #: `7`
   - Response Value: `1`
   - Question Text: `Trouble concentrating on things`
   - Response Text: `Several days`

   **Question 8:**
   - Item #: `8`
   - Response Value: `1`
   - Question Text: `Moving or speaking so slowly that others noticed, or being fidgety/restless`
   - Response Text: `Several days`

   **Question 9:**
   - Item #: `9`
   - Response Value: `1`
   - Question Text: `Thoughts that you would be better off dead or of hurting yourself`
   - Response Text: `Several days`

#### Expected Result
- After each response, a green notification appears in the status bar: "Item response added."
- The Item Responses DataGrid populates with 9 rows
- Each shows: #, Question, Value, Response Text
- Item # auto-increments after each add
- Total of response values: 1+2+2+1+1+2+1+1+1 = 12 (matches total score)

### Part C: Auto-Score

#### Steps
1. Click the **Auto-Score** button in the Score Trending section

#### Expected Result
- A green notification appears in the status bar: "Instrument scored."
- Total Score updates based on sum of item responses

### Part D: Record Risk Assessment

#### Steps
1. Scroll to the **Risk Assessment** section
2. Fill in:
   - Risk Level *: **2 - Moderate** (ComboBox; options: 0 - None, 1 - Low, 2 - Moderate, 3 - High, 4 - Imminent)
   - Risk Assessment Notes: `Patient endorses passive SI (item 9 = 1). No plan, intent, or access to means. Support system in place. Recommend mental health referral.`
3. Click **Record Risk Assessment**

#### Expected Result
- A green notification appears in the status bar: "Risk assessment recorded."
- Risk status indicator shows: "Risk: Moderate" (orange)

### Part E: Set Follow-Up

#### Steps
1. Scroll to the **Follow-Up** section
2. Fill in:
   - Requires Follow-Up: **Yes**
   - Due Date: 2 weeks from today using the DatePicker
   - Follow-Up Plan: `Refer to Mental Health for therapy evaluation. Repeat PHQ-9 in 2 weeks. Consider SSRI if score does not improve.`
3. Click **Set Follow-Up**

#### Expected Result
- A green notification appears in the status bar: "Follow-up set."
- Follow-Up section shows:
  - Requires Follow-Up: "YES" (orange status indicator)
  - Due Date: the selected date
  - Plan text

---

## Scenario 2: PHQ-2 Quick Screen (Positive -- Escalate to PHQ-9)

### Steps
1. Click the **New Screening** TabItem
2. Fill in:
   - Instrument: **PHQ-2**
   - Total Score: `4` (score >= 3 is positive for PHQ-2)
   - Score Interpretation: **MODERATE**
   - Positive Screen?: **Yes**
   - Administered By: `JACKSON,WILLIAM R`
   - Location: `NEUROLOGY CLINIC`
   - Comments: `Positive PHQ-2. Will administer full PHQ-9.`
3. Click **Record**

### Expected Result
- Screen recorded with PHQ-2, Score 4, Positive: YES
- The tester should then create a follow-up PHQ-9 screen (Scenario 1) to document the escalation

---

## Scenario 3: GAD-7 Anxiety Screen

### Steps
1. Click the **New Screening** TabItem
2. Fill in:
   - Instrument: **GAD-7**
   - Total Score: `15` (range: 0-21; 15+ = Severe anxiety)
   - Score Interpretation: **SEVERE**
   - Positive Screen?: **Yes**
   - Administered By: `JACKSON,WILLIAM R`
   - Location: `NEUROLOGY CLINIC`
   - Comments: `Patient reports significant worry interfering with daily activities.`
3. Click **Record**
4. Click the new screening to view detail
5. Enter the 7 GAD-7 item responses:

   | # | Question | Value | Response |
   |---|----------|-------|----------|
   | 1 | Feeling nervous, anxious, or on edge | 3 | Nearly every day |
   | 2 | Not being able to stop or control worrying | 2 | More than half the days |
   | 3 | Worrying too much about different things | 2 | More than half the days |
   | 4 | Trouble relaxing | 2 | More than half the days |
   | 5 | Being so restless that it is hard to sit still | 2 | More than half the days |
   | 6 | Becoming easily annoyed or irritable | 2 | More than half the days |
   | 7 | Feeling afraid as if something awful might happen | 2 | More than half the days |

6. Click **Auto-Score**

### Expected Result
- Score calculated from responses: 3+2+2+2+2+2+2 = 15
- Interpretation: SEVERE

---

## Scenario 4: AUDIT-C Alcohol Screen (Positive)

### Steps
1. Click the **New Screening** TabItem
2. Fill in:
   - Instrument: **AUDIT-C**
   - Total Score: `7` (range: 0-12; men >= 4 is positive)
   - Score Interpretation: (leave as "--")
   - Positive Screen?: **Yes**
   - Administered By: `JACKSON,WILLIAM R`
   - Location: `NEUROLOGY CLINIC`
   - Comments: `Positive AUDIT-C. Patient reports drinking 4-5 drinks per occasion, 3-4 times per week.`
3. Click **Record**
4. View detail and enter 3 AUDIT-C responses:

   | # | Question | Value | Response |
   |---|----------|-------|----------|
   | 1 | How often do you have a drink containing alcohol? | 3 | 2-3 times per week |
   | 2 | How many standard drinks on a typical day when drinking? | 2 | 3 or 4 |
   | 3 | How often do you have 6 or more drinks on one occasion? | 2 | Monthly |

### Expected Result
- Screen recorded: AUDIT-C, Score 7, Positive: YES
- Sum of responses: 3+2+2 = 7

---

## Scenario 5: Columbia Suicide Severity Rating Scale (High Risk)

### Steps
1. Click the **New Screening** TabItem
2. Fill in:
   - Instrument: **Columbia Suicide Severity**
   - Total Score: `4` (ideation intensity score)
   - Score Interpretation: **SEVERE**
   - Positive Screen?: **Yes**
   - Administered By: `JACKSON,WILLIAM R`
   - Location: `NEUROLOGY CLINIC`
   - Comments: `Patient endorses active suicidal ideation with plan. Immediate safety intervention required.`
3. Click **Record**
4. View detail and record Risk Assessment:
   - Risk Level: **4 - Imminent**
   - Risk Assessment Notes: `Active suicidal ideation with plan to overdose on medications. Has access to large supply of prescription opioids. No protective factors identified at this time. 1:1 observation initiated. Psychiatry STAT consult placed. All medications secured.`
5. Click **Record Risk Assessment**

### Expected Result
- Risk status indicator: "Risk: Imminent" (red)

### Steps (continued -- Set Follow-Up)
6. Set Follow-Up:
   - Requires Follow-Up: **Yes**
   - Due Date: Tomorrow using the DatePicker
   - Follow-Up Plan: `Immediate psychiatric evaluation. Consider inpatient admission. Lethal means restriction counseling. Safety plan development. Contact crisis hotline number provided to patient.`
7. Click **Set Follow-Up**

### Expected Result
- Follow-up set with urgent due date

---

## Scenario 6: Score Change Calculation

### Steps
1. Record a second PHQ-9 screen for the same patient (patient 35 should already have one from Scenario 1)
2. New screen: Instrument PHQ-9, Total Score: `8` (improvement from 12)
3. Record it, then view the detail
4. In the **Score Trending** section, click **Calculate Change**

### Expected Result
- A success notification appears: "Score change: -4.0"
- Change display shows: "-4.0" in green text (decrease = improvement)
- Previous Score shows: 12
- Current Score: 8
