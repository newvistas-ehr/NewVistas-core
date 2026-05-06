# Immunizations and Forecast

This guide covers the two immunization modules in NewVistas: **Immunization History** for maintaining a complete vaccination record and **Immunization Forecast** for CDC-based vaccine schedule evaluation and recommendation. Together, these modules ensure that patients receive recommended immunizations on time and that their complete vaccination history is documented in the clinical record.

---

## Immunization History

**Route:** `/immunizations`

The Immunization History module maintains a chronological record of all vaccinations administered to the patient, including immunizations given at the facility and those reported from external sources.

![Immunization history table showing administered vaccines with dates and lot numbers](screenshots/immunization-history-table.png)

### Immunization Record Table

The immunization history is displayed as a reverse-chronological table. Each row represents a single immunization event and shows:

| Column | Description |
|---|---|
| **Vaccine Name** | Name of the vaccine administered (e.g., "Influenza, Injectable, Quadrivalent", "Tdap", "COVID-19 mRNA Bivalent") |
| **Date Administered** | Date the vaccine was given |
| **Lot Number** | Manufacturer's lot number for the vaccine vial used |
| **Route/Site** | Route of administration and anatomic site (e.g., "IM, Left Deltoid", "SC, Right Upper Arm", "PO") |
| **Administering Provider** | Clinician who administered the vaccine |
| **Reaction** | Any documented adverse reaction following the immunization (e.g., "None", "Local redness/swelling", "Fever", "Anaphylaxis") |

### Adding an Immunization Record

To document a newly administered vaccination:

1. Navigate to the Immunization History module at `/immunizations`.
2. Click the **Add Immunization** button.
3. Complete the immunization form:

| Field | Required | Description |
|---|---|---|
| **Vaccine Name** | Yes | Select from the vaccine catalog or type to search. The catalog includes all CDC-recognized vaccines with CVX codes. |
| **Date Administered** | Yes | Date the vaccine was given. Defaults to today for vaccines administered at the current visit. |
| **Lot Number** | No | Manufacturer's lot number printed on the vaccine vial. Recommended for all vaccines for recall tracking. |
| **Route** | No | Route of administration: IM (intramuscular), SC (subcutaneous), PO (oral), ID (intradermal), IN (intranasal) |
| **Site** | No | Anatomic site: Left Deltoid, Right Deltoid, Left Thigh, Right Thigh, Left Upper Arm, Right Upper Arm, Other |
| **Administering Provider** | No | Defaults to the currently signed-in user. Change if documenting a vaccine given by another provider. |
| **Reaction** | No | Document any adverse reaction. Select "None" if no reaction occurred. |
| **Comments** | No | Additional notes (e.g., "Patient reported history -- vaccine given at outside facility", "VIS provided and reviewed") |

4. Click **Save** to add the immunization to the patient's record.

> **Note:** For vaccines administered at outside facilities or reported by the patient, enter the information as accurately as possible. If the lot number or administering provider is unknown, those fields may be left blank. Document the source of the information in the Comments field.

### Viewing Immunization Details

Click any row in the immunization history table to view the full details of that immunization event, including all fields listed above plus:

- **CVX Code** -- CDC vaccine code
- **Manufacturer** -- Vaccine manufacturer name
- **Expiration Date** -- Vaccine expiration date (if recorded)
- **VIS Date** -- Date of the Vaccine Information Statement provided to the patient
- **Series Number** -- Dose number within a multi-dose series (e.g., "Dose 2 of 3")
- **Source** -- Whether the immunization was administered at this facility, reported by the patient, or imported from an immunization registry

### Documenting Adverse Reactions

If a patient experiences an adverse reaction to an immunization:

1. Locate the immunization record in the history table.
2. Click the record to open the detail view.
3. Click **Edit** and update the **Reaction** field with the observed reaction.
4. Document the severity, onset time, and any treatment provided in the Comments field.

> **Warning:** Serious adverse reactions (anaphylaxis, hospitalization, disability, or death) must be reported to the Vaccine Adverse Event Reporting System (VAERS). Contact your facility's immunization coordinator for VAERS reporting procedures.

---

## Immunization Forecast

**Route:** `/immunization-forecast`

The Immunization Forecast module evaluates the patient's immunization history against current CDC immunization schedules and generates patient-specific vaccine recommendations. This helps clinicians identify which vaccines are due, overdue, upcoming, or complete.

![Immunization forecast showing due, overdue, and complete vaccines](screenshots/immunization-forecast-due-overdue.png)

### How the Forecast Works

The immunization forecast engine performs the following evaluation:

1. **Retrieves** the patient's complete immunization history from the Immunization History module.
2. **Identifies** applicable vaccine series based on the patient's age, risk factors, and clinical conditions.
3. **Evaluates** each vaccine series against CDC-recommended schedules, including minimum intervals between doses and age-specific recommendations.
4. **Categorizes** each vaccine series into one of four forecast categories.
5. **Displays** the results in a color-coded forecast table.

### Forecast Categories

Each vaccine series in the forecast is assigned one of four categories:

| Category | Badge Color | Description |
|---|---|---|
| **Due** | Yellow | The vaccine is currently recommended based on the CDC schedule. The patient is within the recommended administration window. |
| **Overdue** | Red | The vaccine is past the recommended administration date. The patient should receive this vaccine as soon as possible. |
| **Upcoming** | Blue | The vaccine will be recommended in the future but is not yet due. The next dose date is shown. |
| **Complete** | Green | The patient has completed the full vaccine series. No additional doses are needed. |

### Forecast Table

The forecast table displays one row per vaccine series:

| Column | Description |
|---|---|
| **Vaccine Series** | Name of the vaccine series (e.g., "Influenza (Annual)", "Td/Tdap", "Hepatitis B", "Pneumococcal") |
| **Forecast Status** | Category badge: Due (yellow), Overdue (red), Upcoming (blue), or Complete (green) |
| **Doses Given** | Number of valid doses the patient has received in this series |
| **Doses Required** | Total number of doses required to complete the series |
| **Last Dose Date** | Date the most recent dose was administered |
| **Next Dose Due** | Recommended date for the next dose (for Due, Overdue, and Upcoming categories) |
| **Earliest Date** | Earliest acceptable date for the next dose based on minimum intervals |
| **Notes** | Additional context, such as age-based recommendations or risk-factor indications |

### Using the Forecast

To review and act on immunization recommendations:

1. Navigate to the Immunization Forecast module at `/immunization-forecast`.
2. Review the forecast table. Focus first on **Overdue** (red) vaccines, then **Due** (yellow) vaccines.
3. For each vaccine that is Due or Overdue, determine whether the patient should receive the vaccine at the current visit.
4. If administering a vaccine, navigate to the Immunization History module at `/immunizations` and document the immunization using the **Add Immunization** workflow.
5. After documenting the immunization, return to the forecast to verify that the series status has updated appropriately.

> **Tip:** Review the immunization forecast at every primary care visit. Addressing due and overdue immunizations opportunistically during routine visits improves vaccination rates significantly.

### CDC Schedule Coverage

The forecast engine evaluates the following vaccine schedules:

#### Adult Immunization Schedule

- **Influenza** -- Annual vaccination recommended for all adults
- **Td/Tdap** -- Tdap once if not previously received, then Td or Tdap booster every 10 years
- **Zoster (Shingles)** -- Recombinant zoster vaccine (RZV), 2-dose series for adults age 50 and older
- **Pneumococcal** -- PCV20 or PCV15 followed by PPSV23, based on age and risk factors
- **Hepatitis B** -- 2- or 3-dose series for unvaccinated adults
- **Hepatitis A** -- 2-dose series for at-risk individuals
- **MMR** -- For adults born after 1956 without evidence of immunity
- **Varicella** -- 2-dose series for adults without evidence of immunity
- **HPV** -- Through age 26 routinely, shared clinical decision-making through age 45
- **COVID-19** -- Per current CDC recommendations

#### Veteran-Specific Considerations

Certain immunizations may be particularly relevant for veteran populations:

- **Hepatitis A and B** -- Recommended for veterans with occupational exposure history, travel history, or chronic liver disease
- **Pneumococcal** -- Recommended earlier for veterans with chronic conditions common in the VA population (COPD, diabetes, heart failure)
- **Influenza** -- Emphasized for veterans with chronic medical conditions

> **Note:** The immunization forecast is a clinical decision support tool. The forecast provides recommendations based on CDC schedules, but the final decision to administer a vaccine rests with the clinician based on the individual patient's clinical circumstances, contraindications, and preferences.

---

## Common Workflows

### Routine Immunization Review at a Primary Care Visit

1. Navigate to `/immunization-forecast` to review the patient's immunization forecast.
2. Identify any vaccines that are **Due** or **Overdue**.
3. Discuss recommended vaccines with the patient, addressing questions and concerns.
4. For each vaccine to be administered, verify there are no contraindications.
5. Provide the Vaccine Information Statement (VIS) to the patient.
6. Administer the vaccine.
7. Navigate to `/immunizations` and document the immunization with vaccine name, date, lot number, route, site, and provider.
8. Return to `/immunization-forecast` to confirm the forecast has updated.

### Documenting Historical Immunizations

1. Navigate to `/immunizations`.
2. Click **Add Immunization**.
3. Enter the vaccine name, date administered, and any available details (lot number, provider, site).
4. In the Comments field, note the source of the information (e.g., "Per patient report", "From outside immunization record dated 2024-03-15").
5. Click **Save**.

---

## Related Modules

- **[Clinical Reminders](reminders.md)** -- Immunization-related clinical reminders trigger when vaccines are due or overdue.
- **[Cover Sheet](cover-sheet.md)** -- Immunization status may appear on the Cover Sheet overview for quick reference.
- **[Health Summary](health-summary.md)** -- Immunization history can be included as a component in generated health summaries.
