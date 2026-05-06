# Clinical Reminders and Health Factors

This guide covers the two modules that support preventive care and health maintenance tracking in NewVistas: **Clinical Reminders** for rule-based preventive care alerts and **Health Factors** for documenting patient health behaviors and risk factors. Together, these modules ensure that clinicians are prompted to address recommended screenings, immunizations, and follow-up actions at each patient encounter.

---

## Clinical Reminders

**Route:** `/reminders`

The Clinical Reminders module provides rule-based clinical decision support alerts for preventive care and health maintenance. Each reminder evaluates the patient's clinical data (diagnoses, age, sex, lab results, immunization history, prior screenings) against evidence-based guidelines to determine whether a specific preventive action is due.

![Clinical reminders list showing status badges for due, done, and not applicable reminders](screenshots/clinical-reminders-list.png)

### Reminders List

The reminders list displays all clinical reminders applicable to the selected patient. Each row shows:

| Column | Description |
|---|---|
| **Reminder** | Name of the clinical reminder (e.g., "Colorectal Cancer Screening", "HbA1c Monitoring") |
| **Status** | Current status of the reminder: Due, Applicable, Not Applicable, or Done |
| **Last Completed** | Date the reminder was last satisfied (if ever) |
| **Due Date** | Date by which the action should be completed (for Due reminders) |
| **Category** | Clinical category grouping (e.g., Cancer Screening, Diabetes Management, Immunization) |
| **Actions** | Action button to resolve or document the reminder |

### Reminder Status Badges

| Status | Badge Color | Description |
|---|---|---|
| **Due** | Red | The preventive action is currently recommended and should be addressed at this encounter. The reminder criteria are met and the required interval has elapsed since the last completion. |
| **Applicable** | Yellow | The reminder applies to this patient based on their demographics and clinical profile, but is not yet due. It will become Due when the required interval elapses. |
| **Not Applicable** | Gray | The reminder does not apply to this patient based on current clinical criteria (e.g., a cervical cancer screening reminder for a male patient). |
| **Done** | Green | The preventive action has been completed and the reminder is satisfied for the current interval. |

### Supported Clinical Reminders

NewVistas includes the following categories of clinical reminders:

#### Cancer Screenings

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **Colorectal Cancer Screening** | Adults age 45--75 | USPSTF recommendation. Colonoscopy, FIT, or FIT-DNA per guideline. | Colonoscopy every 10 years, FIT annually, FIT-DNA every 1--3 years |
| **Breast Cancer Screening (Mammography)** | Women age 40+ | USPSTF recommendation for biennial screening mammography. | Every 2 years (age 50--74) or based on shared decision-making (age 40--49) |
| **Cervical Cancer Screening** | Women age 21--65 | USPSTF recommendation. Pap smear, HPV testing, or co-testing. | Pap every 3 years (age 21--29), Pap + HPV co-test every 5 years (age 30--65) |
| **Lung Cancer Screening** | Adults age 50--80 with 20+ pack-year smoking history | USPSTF recommendation for annual low-dose CT in current or former smokers. | Annually |

#### Diabetes Management

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **HbA1c Monitoring** | Patients with diabetes | ADA Standards of Care. HbA1c at least twice yearly for stable patients, quarterly if not at goal. | Every 3--6 months |
| **Diabetic Eye Exam** | Patients with diabetes | ADA recommendation for annual dilated eye examination. | Annually |
| **Diabetic Foot Exam** | Patients with diabetes | ADA recommendation for annual comprehensive foot examination. | Annually |

#### Cardiovascular Risk

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **Lipid Panel** | Adults based on age and risk factors | USPSTF/ACC/AHA guidelines for cardiovascular risk assessment. | Every 5 years (low risk) or per clinical judgment |
| **Blood Pressure Screening** | All adults | USPSTF recommendation for screening. | At every clinical encounter |
| **Cardiovascular Risk Assessment** | Adults age 40--75 | ACC/AHA pooled cohort equations for 10-year ASCVD risk. | Every 5 years or when risk factors change |

#### Immunization Reminders

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **Annual Influenza Vaccine** | All adults | CDC ACIP recommendation for annual influenza vaccination. | Annually (seasonal) |
| **Pneumococcal Vaccine** | Adults age 65+ or with qualifying conditions | CDC ACIP recommendation. | Per series schedule |
| **Tdap/Td Booster** | All adults | CDC ACIP recommendation for Td or Tdap every 10 years. | Every 10 years |
| **Zoster (Shingles) Vaccine** | Adults age 50+ | CDC ACIP recommendation for RZV. | 2-dose series |
| **COVID-19 Vaccine** | All adults | CDC ACIP recommendation per current guidance. | Per current schedule |

#### Mental Health Screenings

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **Depression Screening (PHQ-2/PHQ-9)** | All adults | USPSTF recommendation for depression screening in the general adult population. | Annually |
| **PTSD Screening (PC-PTSD-5)** | Veterans | VA/DoD recommendation for annual PTSD screening. | Annually |
| **Alcohol Misuse Screening (AUDIT-C)** | All adults | USPSTF recommendation for alcohol misuse screening. | Annually |

#### Tobacco Cessation

| Reminder | Target Population | Guideline | Interval |
|---|---|---|---|
| **Tobacco Use Screening** | All adults | USPSTF recommendation to ask about tobacco use and provide cessation interventions. | At every clinical encounter |
| **Tobacco Cessation Counseling** | Current tobacco users | USPSTF recommendation for cessation counseling and pharmacotherapy. | At every clinical encounter with a current tobacco user |

### Resolving a Clinical Reminder

To resolve (satisfy) a clinical reminder:

1. Navigate to the Clinical Reminders module at `/reminders`.
2. Locate the reminder with a **Due** (red) status.
3. Click the **Resolve** action button on the reminder row.
4. The system will present the appropriate resolution form based on the reminder type:
   - **Screening reminders** -- Document that the screening was performed, ordered, or declined.
   - **Lab-based reminders** -- Link to a recent lab result that satisfies the reminder, or order the appropriate lab test.
   - **Immunization reminders** -- Document the vaccine administration or link to the immunization record.
   - **Counseling reminders** -- Document that counseling was provided.
5. Complete the resolution form and click **Save**.
6. The reminder status will change from **Due** to **Done**, and the Last Completed date will update.

> **Tip:** Address all Due clinical reminders during each patient encounter. This systematic approach ensures that preventive care is not overlooked, even when the visit is focused on a different clinical concern.

> **Note:** Some reminders are automatically resolved when the underlying clinical action is documented elsewhere in the system. For example, when a lab result for HbA1c is entered in the Labs module, the HbA1c Monitoring reminder will automatically update to Done if the result falls within the reminder's evaluation window.

---

## Health Factors

**Route:** `/health-factors`

The Health Factors module documents patient health behaviors, lifestyle factors, and social determinants of health that influence clinical decision-making and preventive care recommendations. Health factors are structured data elements that can be referenced by clinical reminder rules.

### Health Factor Categories

Health factors are organized into the following categories:

| Category | Examples |
|---|---|
| **Smoking Status** | Current smoker, former smoker, never smoker, pack-years, quit date |
| **Exercise** | Exercise frequency, type of activity, minutes per week |
| **Alcohol Use** | Current use, frequency, quantity, history of alcohol use disorder |
| **Substance Use** | Current or former use of illicit substances, type, frequency |
| **Diet/Nutrition** | Dietary patterns, nutritional risk factors, dietary restrictions |
| **Weight Management** | BMI category, weight loss/gain trends, bariatric surgery history |
| **Sexual Health** | Sexually active, number of partners, contraception use, STI screening |
| **Social Determinants** | Housing status, food security, transportation access, social isolation, employment |
| **Safety** | Seatbelt use, firearm access, domestic violence screening, fall risk |
| **Mental Health** | Stress level, sleep quality, social support, resilience factors |

![Health factors documentation form with category selection](screenshots/health-factors-form.png)

### Documenting Health Factors

To document a health factor for a patient:

1. Navigate to the Health Factors module at `/health-factors`.
2. Click **Add Health Factor**.
3. Select the **Category** for the health factor being documented.
4. Select or enter the specific **Health Factor** within the category (e.g., within Smoking Status, select "Current Smoker" or "Former Smoker").
5. Enter the **Date** the health factor was assessed. Defaults to today.
6. Add any additional **Comments** or context (e.g., "Patient reports smoking 1 pack per day for 20 years. Interested in cessation.").
7. Click **Save** to add the health factor to the patient's record.

### Viewing Health Factor History

The health factor history displays all documented health factors for the patient, organized by category. Each entry shows:

| Column | Description |
|---|---|
| **Date** | Date the health factor was documented |
| **Category** | Health factor category |
| **Factor** | Specific health factor documented |
| **Provider** | Clinician who documented the factor |
| **Comments** | Additional context or notes |

### Relationship to Clinical Reminders

Health factors directly influence clinical reminder evaluation. For example:

- Documenting **"Current Smoker"** activates the Tobacco Cessation Counseling reminder and, if the patient meets age and pack-year criteria, the Lung Cancer Screening reminder.
- Documenting **"Sexually Active"** may activate STI screening reminders.
- Documenting **"Former Smoker"** with a quit date may deactivate the Tobacco Cessation reminder while keeping the Lung Cancer Screening reminder active if pack-year criteria are met.

> **Tip:** Keep health factors up to date at each primary care visit. Accurate health factor documentation ensures that clinical reminders are appropriately triggered and that preventive care recommendations are tailored to the individual patient.

---

## Common Workflows

### Addressing Clinical Reminders at a Primary Care Visit

1. Open the patient's chart and navigate to `/reminders`.
2. Review all reminders with a **Due** (red) status.
3. For each Due reminder, determine the appropriate action:
   - **Order a test** (e.g., order a colonoscopy for colorectal cancer screening)
   - **Perform a screening** (e.g., administer a PHQ-2 for depression screening)
   - **Provide counseling** (e.g., tobacco cessation counseling)
   - **Administer a vaccine** (e.g., annual influenza vaccine)
   - **Document declination** (if the patient declines a recommended action)
4. Resolve each reminder using the appropriate resolution method.
5. Update health factors as needed based on information gathered during the visit.

### Updating Smoking Status

1. Navigate to `/health-factors`.
2. Click **Add Health Factor**.
3. Select the **Smoking Status** category.
4. Select the appropriate factor (e.g., "Current Smoker", "Former Smoker", "Never Smoker").
5. For current smokers, document pack-years in the Comments field.
6. For former smokers, document the quit date in the Comments field.
7. Click **Save**.
8. Navigate to `/reminders` to verify that tobacco-related reminders have updated appropriately.

---

## Related Modules

- **[Immunizations](immunizations.md)** -- Immunization reminders link to the Immunization History and Forecast modules.
- **[Laboratory](labs.md)** -- Lab-based reminders (HbA1c, lipid panel) are resolved by lab results entered in the Labs module.
- **[Mental Health](mental-health.md)** -- Mental health screening reminders link to the Mental Health Screening module.
- **[Cover Sheet](cover-sheet.md)** -- Due clinical reminders are highlighted on the Cover Sheet for quick reference.
- **[Vitals](vitals.md)** -- Blood pressure and BMI data documented in Vitals support cardiovascular and weight management reminders.
