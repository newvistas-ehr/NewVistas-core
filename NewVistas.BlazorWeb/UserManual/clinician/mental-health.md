# Mental Health

This guide covers the three mental health modules in NewVistas: **Mental Health Screening** for standardized clinical assessments, **Suicide Prevention** for high-risk patient management and safety planning, and **Substance Abuse Treatment** for addiction treatment coordination. Together, these modules support the continuum of behavioral health care within the VA clinical environment.

These modules map to VistA Mental Health files (#601 Mental Health Instruments, #601.2 Mental Health Results, #502 Substance Abuse Treatment).

---

## Mental Health Screening

**Route:** `/mental-health`
**VistA Files:** #601 (Mental Health Instruments), #601.2 (Mental Health Results)

The Mental Health Screening module administers, scores, and tracks standardized mental health screening instruments. It supports eight validated instruments covering depression, anxiety, PTSD, substance use, and suicidality assessment. Screening results are trended over time to help clinicians monitor treatment response.

![Mental health screening list showing completed assessments with scores](screenshots/mental-health-screening-list.png)

### Tab 1: Screenings List

The Screenings List tab displays all mental health screenings administered to the selected patient. Each row shows:

| Column | Description |
|---|---|
| **Date** | Date the screening was administered |
| **Instrument** | Name of the screening tool used (e.g., PHQ-9, GAD-7) |
| **Score** | Numeric score from the screening (or categorical result for C-SSRS) |
| **Interpretation** | Clinical interpretation of the score (e.g., "Moderate Depression") |
| **Positive?** | Whether the screening result meets the threshold for a positive screen. **YES** is displayed with a red badge; **No** is displayed with a green badge. |
| **Risk** | Assessed risk level based on the screening result |
| **Status** | Screening status (Completed, In Progress, Cancelled) |

---

### Supported Instruments

NewVistas supports the following eight standardized mental health screening instruments:

| Instrument | Full Name | Score Range | Positive Threshold | Domain |
|---|---|---|---|---|
| **PHQ-9** | Patient Health Questionnaire-9 | 0--27 | >= 10 | Depression |
| **GAD-7** | Generalized Anxiety Disorder-7 | 0--21 | >= 10 | Anxiety |
| **PCL-5** | PTSD Checklist for DSM-5 | 0--80 | >= 33 | PTSD |
| **AUDIT-C** | Alcohol Use Disorders Identification Test - Consumption | 0--12 | >= 4 (men) / >= 3 (women) | Alcohol Use |
| **PHQ-2** | Patient Health Questionnaire-2 | 0--6 | >= 3 | Depression (brief screen) |
| **C-SSRS** | Columbia Suicide Severity Rating Scale | Categorical | Categorical (any active ideation) | Suicidality |
| **DAST-10** | Drug Abuse Screening Test-10 | 0--10 | >= 3 | Drug Use |
| **PC-PTSD-5** | Primary Care PTSD Screen for DSM-5 | 0--5 | >= 3 | PTSD (brief screen) |

> **Note:** The AUDIT-C uses sex-specific positive thresholds. The threshold is >= 4 for men and >= 3 for women. The C-SSRS uses categorical assessment rather than a numeric threshold -- any endorsement of active suicidal ideation constitutes a positive screen.

---

### Score Interpretation Tables

#### PHQ-9 (Patient Health Questionnaire-9)

| Score Range | Severity | Interpretation |
|---|---|---|
| 0--4 | Minimal | Minimal or no depression. Monitoring recommended. |
| 5--9 | Mild | Mild depression. Watchful waiting; repeat PHQ-9 at follow-up. |
| 10--14 | Moderate | Moderate depression. Treatment plan should be considered (therapy, medication, or both). **Positive screen.** |
| 15--19 | Moderately Severe | Moderately severe depression. Active treatment with pharmacotherapy and/or psychotherapy warranted. **Positive screen.** |
| 20--27 | Severe | Severe depression. Immediate initiation of pharmacotherapy and, if severe impairment or poor response, referral to mental health specialist. **Positive screen.** |

> **Warning:** PHQ-9 Item 9 asks about thoughts of self-harm or being better off dead. Any score greater than 0 on Item 9 triggers an alert in the system requiring the clinician to assess suicide risk, regardless of the total PHQ-9 score.

#### GAD-7 (Generalized Anxiety Disorder-7)

| Score Range | Severity | Interpretation |
|---|---|---|
| 0--4 | Minimal | Minimal anxiety. No clinical intervention indicated. |
| 5--9 | Mild | Mild anxiety. Monitoring recommended. Consider reassessment if symptoms persist. |
| 10--14 | Moderate | Moderate anxiety. Further evaluation warranted. Consider therapy or pharmacotherapy. **Positive screen.** |
| 15--21 | Severe | Severe anxiety. Active treatment indicated. Consider referral to mental health specialist. **Positive screen.** |

#### PCL-5 (PTSD Checklist for DSM-5)

| Score Range | Interpretation |
|---|---|
| 0--32 | Below threshold. Does not meet probable PTSD criteria based on this screen. |
| 33--80 | Probable PTSD. Score meets or exceeds the clinical threshold. Further diagnostic evaluation recommended. **Positive screen.** |

---

### Tab 2: Screening Detail

Selecting a screening from the Screenings List opens the Screening Detail tab, which provides the full results and clinical context for a single screening administration.

![PHQ-9 screening detail showing score, interpretation, and trending](screenshots/mental-health-phq9-detail.png)

#### Score Information

| Field | Description |
|---|---|
| **Instrument** | Screening tool used |
| **Date Administered** | Date and time of administration |
| **Administered By** | Clinician who administered the screening |
| **Location** | Clinical location where the screening was conducted |
| **Total Score** | Numeric score (or categorical result for C-SSRS) |
| **Positive** | Whether the screen is positive based on instrument-specific thresholds |
| **Interpretation** | Severity category based on the score |

#### Score Trending

The Screening Detail view includes a trending section that compares the current screening result against previous administrations of the same instrument:

| Trending Field | Description |
|---|---|
| **Current Score** | The score from the selected screening |
| **Previous Score** | The score from the most recent prior administration of the same instrument |
| **Change** | Numeric difference between current and previous scores |
| **Direction** | Arrow indicator: upward arrow (score increased / worsening), downward arrow (score decreased / improving), or horizontal arrow (no change) |
| **Trend History** | Visual graph or sparkline showing score progression over the last several administrations |

> **Tip:** Score trending is one of the most valuable features for monitoring treatment response. A clinician can quickly see whether a patient's depression is improving (PHQ-9 scores declining) or worsening (scores increasing) over the course of treatment.

#### Risk Assessment

| Field | Description |
|---|---|
| **Risk Level** | Assessed risk level: None, Low, Moderate, High, or Imminent |
| **Risk Factors** | Identified risk factors contributing to the assessment |
| **Protective Factors** | Identified protective factors |
| **Clinical Judgment** | Provider's clinical assessment narrative |

#### PHQ-9 Item 9 Alert

When a PHQ-9 screening includes a score greater than 0 on Item 9 ("Thoughts that you would be better off dead, or of hurting yourself"), the system displays a prominent alert:

> **Warning:** This patient endorsed thoughts of self-harm on PHQ-9 Item 9. A suicide risk assessment is required. Document the risk assessment in the Risk Assessment section below and determine appropriate clinical response.

#### Follow-Up Section

| Field | Description |
|---|---|
| **Recommendation** | Recommended follow-up action (e.g., "Refer to Mental Health", "Schedule follow-up PHQ-9 in 4 weeks", "Safety plan indicated") |
| **Follow-Up Date** | Scheduled date for next screening or clinical follow-up |
| **Follow-Up Provider** | Provider responsible for the follow-up |
| **Comments** | Additional clinical notes about the screening results and plan |

---

### Tab 3: New Screening

The New Screening tab provides the form for administering a new mental health screening.

| Field | Required | Description |
|---|---|---|
| **Instrument** | Yes | Select the screening instrument from the dropdown (PHQ-9, GAD-7, PCL-5, AUDIT-C, PHQ-2, C-SSRS, DAST-10, PC-PTSD-5) |
| **Date** | No | Date of administration. Defaults to today. |
| **Administered By** | No | Provider administering the screen. Defaults to the currently signed-in user. |
| **Location** | No | Clinical location |
| **Score** | No | Total numeric score. Enter after the patient completes the instrument. |
| **Positive** | No | Whether the screen is positive. Auto-calculated based on the score and instrument thresholds, but can be overridden. |
| **Comments** | No | Clinical comments about the screening administration or circumstances |
| **Recommendation** | No | Recommended follow-up action based on results |

To administer a new screening:

1. Navigate to the Mental Health Screening module at `/mental-health`.
2. Click the **New Screening** tab.
3. Select the **Instrument** from the dropdown.
4. Administer the screening instrument to the patient (verbally, on paper, or via tablet).
5. Enter the **Score** once the patient has completed the instrument.
6. Review the auto-calculated Positive result and Interpretation.
7. Complete the Risk Assessment and Follow-Up sections as clinically indicated.
8. Click **Save Screening** to record the result.

---

## Suicide Prevention

**Route:** `/suicide-prevention`

The Suicide Prevention module supports the VA's comprehensive approach to suicide prevention. It provides three core functions: a high-risk patient roster for identifying and monitoring at-risk veterans, structured safety planning, and systematic follow-up tracking to ensure continuity of care for high-risk patients.

### Tab 1: High-Risk Roster

The High-Risk Roster is a facility-level view that lists patients who have been identified as being at elevated risk for suicide. Each row shows:

| Column | Description |
|---|---|
| **Patient** | Patient name and identifier |
| **Risk Level** | Current suicide risk designation, displayed with color-coded badges |
| **High-Risk Flag** | Whether the patient has been flagged as high-risk in their record (Yes/No) |
| **Last Contact** | Date of the most recent clinical contact with the patient |
| **Active Plans** | Number of active safety plans on file |
| **Detail** | Link to the patient's full suicide prevention record |

#### Risk Level Badges

Risk levels are displayed with distinct color badges for rapid visual identification:

| Risk Level | Badge Color | Description |
|---|---|---|
| **Not Assessed** | Gray | Patient has not yet been assessed for suicide risk |
| **Low** | Green | Low current risk based on assessment findings |
| **Moderate** | Yellow | Moderate risk requiring enhanced monitoring and safety planning |
| **High** | Orange | High risk requiring intensive monitoring, safety planning, and treatment |
| **Imminent** | Red | Imminent risk requiring immediate intervention (crisis response, inpatient admission, constant observation) |

![High-Risk Roster showing patients with color-coded risk level badges](screenshots/suicide-prevention-high-risk-roster.png)

#### Risk Designation Form

To set or update a patient's suicide risk level:

1. Locate the patient on the High-Risk Roster or navigate to their suicide prevention record.
2. Click **Set Risk Level**.
3. Complete the risk designation form:

| Field | Required | Description |
|---|---|---|
| **Patient** | Yes | Auto-populated with the selected patient |
| **Risk Level** | Yes | Select the assessed risk level: Not Assessed, Low, Moderate, High, or Imminent |
| **Provider** | No | Designating provider. Defaults to the currently signed-in user. |
| **Set High-Risk Flag** | No | Check this box to place a high-risk flag on the patient's record. The flag causes alerts to display whenever the patient's chart is opened. |

4. Click **Save** to update the risk designation.

> **Warning:** Setting a patient's risk level to High or Imminent requires immediate clinical action. Ensure that a safety plan is in place, follow-up contacts are scheduled, and the treatment team is notified.

---

### Tab 2: Safety Plans

The Safety Plans tab manages structured safety plans for patients at risk for suicide. Safety plans are collaborative documents created with the patient that outline specific steps the patient can take when experiencing suicidal thoughts.

A safety plan in NewVistas contains the following six standard components, based on the Stanley-Brown Safety Planning Intervention:

| Component | Description |
|---|---|
| **1. Warning Signs** | Personal warning signs that a crisis may be developing (thoughts, images, moods, situations, behaviors) |
| **2. Internal Coping Strategies** | Things the patient can do on their own to take their mind off problems without contacting another person (e.g., exercise, relaxation techniques, hobbies) |
| **3. Social Distractions** | People and social settings that provide distraction -- contact information for people the patient can reach out to for socializing (not necessarily to discuss the crisis) |
| **4. Support Contacts** | People the patient can ask for help -- family, friends, or others the patient trusts to discuss their feelings with |
| **5. Professional Contacts** | Clinicians, agencies, and crisis resources the patient can contact during a crisis, including the Veterans Crisis Line (988, press 1) |
| **6. Crisis Lines** | Emergency crisis contacts including 988 Suicide and Crisis Lifeline, Veterans Crisis Line, local emergency services (911), and crisis text line |

Additionally, the safety plan includes:

| Field | Description |
|---|---|
| **Means Restriction** | Steps to restrict access to lethal means (firearms, medications, sharp objects). Documents specific actions taken or planned. |
| **Reasons for Living** | Patient-identified reasons for living that can serve as motivation during a crisis |

#### Safety Plan Status

| Status | Description |
|---|---|
| **Active** | The safety plan is current and in effect. Displayed with a green badge. |
| **Draft** | The safety plan is being developed and has not yet been finalized with the patient. Displayed with a gray badge. |
| **Reviewed** | The safety plan has been reviewed and updated during a recent clinical encounter. Displayed with a blue badge. |
| **Inactive** | The safety plan is no longer in active use (superseded by a newer plan or no longer clinically indicated). Displayed with a gray badge. |

![Safety plan form showing the six standard components](screenshots/suicide-prevention-safety-plan.png)

> **Tip:** Safety plans should be developed collaboratively with the patient, not simply filled in by the clinician. Review and update the safety plan at every clinical contact with high-risk patients.

---

### Tab 3: Follow-Up Tracking

The Follow-Up Tracking tab manages the required cadence of clinical contacts with patients who have been identified as high-risk. Follow-up tracking ensures that no high-risk patient falls through the cracks.

Each follow-up entry shows:

| Column | Description |
|---|---|
| **Due Date** | Date the follow-up contact is due |
| **Contact Type** | Method of contact: Phone, In-Person, or Telehealth |
| **Status** | Current status of the follow-up: Scheduled, Completed, Overdue, or Missed |

#### Follow-Up Status Badges

| Status | Badge Color | Description |
|---|---|---|
| **Scheduled** | Blue | Follow-up is scheduled and not yet due |
| **Completed** | Green | Follow-up contact has been made and documented |
| **Overdue** | Orange | Follow-up is past due but has not yet been completed |
| **Missed** | Red | Follow-up was not completed and the window has passed |

#### Required Follow-Up Cadence for High-Risk Patients

Patients designated as **High** risk are subject to a mandatory follow-up schedule:

| Period | Frequency | Duration |
|---|---|---|
| **Weeks 1--4** | Weekly | 4 weekly contacts following high-risk designation |
| **Months 2--12** | Monthly | 11 monthly contacts following the initial weekly period |

This results in a minimum of **15 follow-up contacts** over the first 12 months following a high-risk designation.

> **Warning:** Overdue and Missed follow-up contacts generate alerts on the provider's dashboard and on the High-Risk Roster. These must be addressed immediately. If a patient cannot be reached, document the contact attempt and escalate per facility protocol.

![Follow-up tracking table showing scheduled, completed, and overdue contacts](screenshots/suicide-prevention-follow-up-tracking.png)

---

## Substance Abuse Treatment

**Route:** `/substance-abuse-treatment`
**VistA File:** #502

The Substance Abuse Treatment module supports the management of patients in substance use disorder (SUD) treatment programs. It covers the full treatment lifecycle from intake through discharge and integrates with mental health screening for validated substance use assessments.

> **Warning:** Substance abuse treatment records are subject to **42 CFR Part 2** federal confidentiality regulations, which impose stricter privacy protections than standard HIPAA requirements. These records cannot be disclosed without the patient's specific written consent, even to other treating providers. Violations carry criminal penalties. All users accessing this module should be familiar with their facility's 42 CFR Part 2 compliance policies.

### Treatment Phases

Substance abuse treatment follows a structured four-phase lifecycle:

| Phase | Description |
|---|---|
| **1. Intake** | Initial assessment, diagnosis, treatment history, substance use history, psychosocial evaluation, and treatment program assignment |
| **2. Active Treatment** | Ongoing therapy sessions, medication-assisted treatment (MAT), group therapy, individual counseling, and skills training |
| **3. Progress Monitoring** | Regular reassessment of treatment goals, screening instrument re-administration, relapse tracking, and treatment plan adjustments |
| **4. Discharge** | Treatment completion, discharge planning, continuing care referrals, relapse prevention planning, and outcome documentation |

### Key Features

#### Treatment Plans

Treatment plans document the patient's substance use diagnoses, treatment goals, and planned interventions. Each treatment plan includes:

- **Diagnosis** -- Substance use disorder diagnosis with ICD-10 code and severity (Mild, Moderate, Severe)
- **Treatment Goals** -- Measurable goals established with the patient (e.g., "Maintain abstinence from alcohol for 90 days")
- **Interventions** -- Planned treatment modalities (individual therapy, group therapy, MAT, peer support)
- **Target Dates** -- Expected dates for goal achievement and plan review
- **Status** -- Active, Completed, or Revised

#### Session Tracking

Individual and group therapy sessions are documented with:

- **Session Date** -- Date and time of the session
- **Session Type** -- Individual, Group, Family, or Couples
- **Modality** -- Treatment approach (CBT, MI, DBT, 12-Step Facilitation, Contingency Management, etc.)
- **Duration** -- Session length in minutes
- **Clinician** -- Treating provider
- **Notes** -- Session notes including topics covered, patient progress, and plan

#### Screening Integration

The Substance Abuse Treatment module integrates with the Mental Health Screening module for validated substance use screening instruments:

- **AUDIT-C** -- Alcohol use screening
- **DAST-10** -- Drug use screening
- **Additional instruments** -- Full AUDIT, CAGE, CIWA-Ar (alcohol withdrawal), COWS (opioid withdrawal)

Screening results are pulled from the Mental Health Screening module and displayed within the substance abuse treatment record for longitudinal tracking.

#### Medication-Assisted Treatment (MAT) Coordination

For patients receiving MAT, the module coordinates with pharmacy services to track:

- **MAT Medications** -- Buprenorphine, methadone, naltrexone, acamprosate, disulfiram
- **Prescribing Provider** -- Provider authorized to prescribe the MAT medication
- **Dosing** -- Current dose, dose adjustments, and schedule
- **Compliance** -- Adherence tracking, urine drug screen results, pill counts

#### Referral Tracking

Track referrals to and from the substance abuse treatment program:

- **Internal referrals** -- From primary care, mental health, emergency department
- **External referrals** -- To community programs, halfway houses, vocational rehabilitation, sober living facilities
- **Referral status** -- Pending, Accepted, Completed, Declined

#### Discharge Planning

When a patient completes treatment or is discharged, the module documents:

- **Discharge Date** -- Date of treatment completion or discharge
- **Discharge Type** -- Completed, Administrative, Patient Request, Transfer, Against Medical Advice
- **Discharge Summary** -- Summary of treatment course and outcomes
- **Continuing Care Plan** -- Referrals to ongoing treatment, support groups, community resources
- **Relapse Prevention Plan** -- Strategies, triggers, warning signs, and support resources

---

## Common Workflows

### New Patient Mental Health Evaluation

1. Navigate to the Mental Health Screening module at `/mental-health`.
2. Click the **New Screening** tab and select the appropriate initial screening instrument (typically PHQ-2 or PHQ-9 for depression, GAD-7 for anxiety).
3. Administer the screening instrument to the patient and enter the score.
4. Review the interpretation and positive/negative determination.
5. If the PHQ-2 is positive (score >= 3), administer the full PHQ-9 as a follow-up.
6. Complete the Risk Assessment section with the assessed risk level and contributing factors.
7. Document the clinical recommendation and schedule follow-up as indicated.

### Crisis Intervention

1. If a patient presents in crisis or endorses active suicidal ideation, navigate to the Suicide Prevention module at `/suicide-prevention`.
2. Complete the risk designation form, setting the risk level to **High** or **Imminent** as clinically determined.
3. Set the **High-Risk Flag** to ensure alerts display to all providers accessing the patient's record.
4. Create or update the **Safety Plan** collaboratively with the patient, completing all six components and the means restriction section.
5. Schedule the required follow-up contacts -- weekly for the first 4 weeks, then monthly for 12 months.
6. If the risk level is **Imminent**, initiate the facility's crisis response protocol (psychiatric evaluation, possible inpatient admission, constant observation).

---

## Related Modules

- **[Clinical Notes (TIU)](notes.md)** -- Mental health encounter notes, crisis intervention notes, and safety plan narratives are documented through the Notes module.
- **[Consults](consults.md)** -- Referrals to mental health services are tracked through the Consults module.
- **[Medications](medications.md)** -- Psychotropic medications and MAT prescriptions are managed through the Medications module.
- **[Clinical Reminders](reminders.md)** -- Mental health screening reminders (e.g., annual PHQ-2, AUDIT-C) are tracked through the Clinical Reminders module.
- **[Care Team](care-team.md)** -- Mental health providers assigned to the patient's care team are managed through the Care Team module.
