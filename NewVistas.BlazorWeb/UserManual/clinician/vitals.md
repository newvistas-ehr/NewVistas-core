# Vitals

**Route:** `/vitals`

The Vitals page manages the recording, viewing, and historical search of patient vital signs in NewVistas. It maps to VistA File #120.5 (GMRV Vital Measurement). Vital signs are fundamental clinical observations that provide a snapshot of a patient's physiologic status at a point in time.

![View Vitals tab showing recent measurements with date/time stamps](screenshots/vitals-view-tab.png)

---

## Tabs

The Vitals page is organized into three tabs: View, Record, and History.

### View Tab

The View tab displays the patient's most recent vital sign measurements from the hot cache. This is the default tab when you open the Vitals page.

#### Loading Vitals

1. Enter the **Patient ID** in the lookup bar.
2. Click **Load** (or press **Enter**).

The system retrieves the patient's latest vitals via `PatientWorkflowGrain.GetLatestVitalsAsync()`. If the patient ID is available from the patient context service (e.g., if you navigated from the Cover Sheet), it will be pre-populated automatically.

#### Vitals Table

| Column | Description |
|---|---|
| **Vital** | Type of vital sign (e.g., "TEMPERATURE", "PULSE", "BLOOD PRESSURE") |
| **Value** | Measured value; abnormal values appear in bold red |
| **Units** | Units of measurement (e.g., "F", "bpm", "mmHg") |
| **Flag** | Abnormality flag (H, L, HH, LL) displayed in red if present |
| **Date/Time** | Date and time the measurement was taken in MM/DD/YYYY HH:MM format |

If no vitals are recorded for the patient, the tab displays "No vitals recorded."

The number of vitals displayed is configurable via Site Parameters. The default display count is **10** most recent measurements.

### Record Tab

![Record Vitals form showing context fields and vital measurement grid](screenshots/vitals-record-form.png)

The Record tab provides a form for entering new vital sign measurements.

#### Context Fields

These fields establish the context for the vital sign measurements being recorded:

| Field | Description |
|---|---|
| **Date/Time Taken** | The date and time when the vital signs were measured (defaults to the current date/time) |
| **Location** | The clinic, ward, or location where the measurements were taken (e.g., "Clinic 3B", "Ward 4A") |
| **Entered By** | The name of the person recording the vital signs |

#### Vital Measurement Fields

All measurement fields are optional. Leave a field blank to skip that vital sign. Enter only the measurements that were actually taken.

| Field | Unit | Placeholder | Description |
|---|---|---|---|
| **Temperature** | F (Fahrenheit) | 98.6 | Body temperature |
| **Pulse** | bpm (beats per minute) | 72 | Heart rate |
| **Respiration** | breaths/min | 16 | Respiratory rate |
| **Blood Pressure** | mmHg | 120/80 | Systolic/diastolic blood pressure (enter as "systolic/diastolic") |
| **Weight** | lbs (pounds) | 170 | Body weight |
| **Height** | in (inches) | 70 | Body height/stature |
| **Pain** | 0-10 scale | 0 | Pain assessment on a numeric rating scale (0 = no pain, 10 = worst possible pain) |
| **Pulse Oximetry** | % (percent) | 98 | Oxygen saturation (SpO2) |

> **Note:** Vital signs in NewVistas are **write-once**. Once a set of vitals is recorded, it cannot be edited or deleted. If an error is made, record a new set of vitals with the correct values and document the correction in a progress note.

### History Tab

The History tab provides full historical search capability for the patient's vital sign records.

![Vital history search with date range filter and vital type selection](screenshots/vitals-history.png)

#### History Filters

| Filter | Default | Description |
|---|---|---|
| **From** | 30 days ago | Start date/time for the search range |
| **To** | Today | End date/time for the search range |
| **Vital Type** | ALL | Filter to a specific vital type, or leave as ALL to show all types. Options: TEMPERATURE, PULSE, RESPIRATION, BLOOD PRESSURE, WEIGHT, HEIGHT, PAIN, PULSE OXIMETRY |
| **Max Results** | 50 | Maximum number of results to return (1-500) |

Click **Search** to execute the query. Results appear in a table identical to the View tab table (Vital, Value, Units, Flag, Date/Time).

If no results match the search criteria, the tab displays "No vitals found for the selected criteria."

---

## Recording Vitals

Follow these steps to record a new set of vital signs:

1. **Enter the Patient ID** in the lookup bar and click **Load** to establish the patient context.

2. **Switch to the Record tab** by clicking the "Record Vitals" tab button.

3. **Set the context fields.** Verify the Date/Time Taken (defaults to now), enter the Location, and enter your name in the Entered By field.

4. **Enter the vital measurements.** Fill in the values for each vital sign that was measured. Leave fields blank for measurements not taken. At least one vital measurement is required.

5. **Click Record Vitals** to save. The system records all entered measurements, displays a success message ("Vitals recorded successfully."), clears the form, and automatically switches to the View tab to show the updated vitals list.

> **Tip:** Enter all vitals taken at the same time in a single recording session. This preserves the temporal relationship between measurements (e.g., blood pressure and pulse taken simultaneously).

---

## Vital Sign Reference

| Vital | Unit | Normal Range (Adult) | Description |
|---|---|---|---|
| Temperature | F (Fahrenheit) or C (Celsius) | 97.8-99.1 F | Body core temperature |
| Pulse | bpm (beats per minute) | 60-100 bpm | Heart rate measured at a peripheral pulse point |
| Respiration | breaths/min | 12-20 breaths/min | Respiratory rate (count of breaths per minute) |
| Blood Pressure | mmHg (systolic/diastolic) | < 120/80 mmHg | Arterial blood pressure; enter as "systolic/diastolic" |
| Weight | lbs (pounds) or kg (kilograms) | Varies by individual | Body weight measured on a calibrated scale |
| Height | in (inches) or cm (centimeters) | Varies by individual | Body height/stature measured standing |
| Pain | 0-10 numeric rating scale | 0 (no pain) | Patient self-reported pain level on a standardized scale |
| Pulse Oximetry (SpO2) | % (percent) | 95-100% | Oxygen saturation measured by pulse oximeter |

> **Note:** Normal ranges are general adult guidelines. Pediatric, geriatric, and condition-specific ranges may differ. Always interpret vital signs in the context of the individual patient's baseline and clinical condition.

---

## Abnormality Flags

Vital sign values may be flagged when they fall outside expected ranges:

| Flag | Meaning | Description |
|---|---|---|
| **H** | High | Value is above the normal high threshold |
| **L** | Low | Value is below the normal low threshold |
| **HH** | Critical High | Value is critically elevated and requires immediate clinical attention |
| **LL** | Critical Low | Value is critically low and requires immediate clinical attention |

Flagged values appear in bold red text in both the View and History tabs, and on the Cover Sheet.

> **Warning:** Critical vital signs (HH or LL) may indicate a medical emergency. If you observe a critical vital sign, immediately notify the responsible provider and initiate appropriate clinical protocols.

---

## Supplemental Oxygen Documentation

When recording Pulse Oximetry (SpO2), it is important to document whether the patient is on supplemental oxygen and at what flow rate. While the basic vitals form captures the SpO2 percentage, supplemental oxygen details (flow rate in L/min) should be documented in the patient's progress note or entered through the supplemental oxygen field if available.

---

## Tips for Vital Sign Recording

- **Record vitals at every patient encounter** -- vitals provide essential clinical data for assessment and trend monitoring.
- **Use consistent technique** -- measure vital signs using standardized procedures (e.g., patient seated for 5 minutes before blood pressure, same arm each visit) to ensure comparability over time.
- **Document the context** -- the Location and Entered By fields create an audit trail and help identify the clinical setting of the measurement.
- **Use the History tab for trending** -- review vital sign trends over time to identify patterns (e.g., rising blood pressure, weight gain, declining oxygen saturation).
- **Pain assessment** -- use the 0-10 numeric rating scale consistently and document the patient's functional status in relation to their pain level in the progress note.
- **Blood pressure format** -- always enter blood pressure as "systolic/diastolic" (e.g., "120/80"), not as two separate numbers.
