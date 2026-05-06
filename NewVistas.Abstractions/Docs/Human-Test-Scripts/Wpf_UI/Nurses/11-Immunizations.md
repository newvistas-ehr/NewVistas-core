# Immunizations -- Human Test Script -- WPF UI

## Prerequisites

- **Login:** NURSE1 / Password: `smythVista1`
- **Patient:** 4
- **Pre-conditions:**
  1. The SiloHost, WebServer, and WPF Application (NewVistas.WpfClient) must all be running.
  2. In the Navigation Panel, select **Immunizations**.
  3. Enter Patient ID `4` in the Patient ID field in the toolbar and click **Load**.
  4. If no immunizations exist yet, the Immunization History TabItem shows "No immunizations recorded."
  5. To seed demo data, use the API endpoint: `POST /api/patient/4/immunizations` to add baseline records, or proceed directly to Scenario 2 to record new immunizations.

---

## Scenario 1: View Immunization History

### Steps

1. In the Navigation Panel, select **Immunizations**.
2. Enter Patient ID: `4` in the Patient ID field in the toolbar.
3. Click **Load**.
4. Verify the **Immunization History** TabItem is active.
5. Observe the DataGrid columns.

### Expected Result

- The DataGrid displays columns: Immunization Name, Date Administered, Series, Reaction, Facility.
- If immunizations exist, each row shows one immunization event with:
  - Immunization Name (e.g., "Influenza, Seasonal Injectable")
  - Date Administered in date/time format
  - Series (e.g., "1", "C" for Complete, "B" for Booster)
  - Reaction (blank if none, or "Local", "Systemic", "Anaphylaxis")
  - Facility (location name where administered)
- Rows are sorted by Date Administered, newest first.
- If no immunizations exist, a message reads "No immunizations recorded."

---

## Scenario 2: Record New Immunization (Happy Path)

### Steps

1. Click the **Record Immunization** TabItem.
2. Fill in the following fields:
   - Immunization Name ComboBox: `Influenza, Seasonal Injectable`
   - CVX Code: `158`
   - Date Administered DatePicker: (leave as current date/time)
   - Lot Number TextBox: `FL2024-1892`
   - Manufacturer TextBox: `SANOFI PASTEUR`
   - Expiration Date DatePicker: `06/30/2027`
   - Site of Administration ComboBox: `LEFT DELTOID`
   - Route ComboBox: `INTRAMUSCULAR`
   - Dose TextBox: `0.5 mL`
   - Administered By TextBox: `JOHNSON,MARY R`
   - Series TextBox: `1`
   - Location TextBox: `Primary Care Clinic`
   - Comments TextBox: `Annual influenza vaccination. VIS provided and reviewed with patient.`
3. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Immunization recorded."
- The form fields are cleared (reset to defaults).
- Switch to the **Immunization History** TabItem. The newest entry shows:
  - Immunization Name: Influenza, Seasonal Injectable
  - Date Administered: current date/time
  - Series: 1
  - Reaction: (blank)
  - Facility: Primary Care Clinic
- The API endpoint `POST /api/patient/4/immunizations` was called with the entered data.

---

## Scenario 3: Record Immunization Reaction

### Steps

1. On the **Immunization History** TabItem, locate the Influenza immunization recorded in Scenario 2.
2. Click the row to select it, then click the **Record Reaction** button (or right-click and select **Record Reaction**).
3. In the dialog window "Record Immunization Reaction", select:
   - Reaction ComboBox: `Local`
   - Comments TextBox: `Mild redness and swelling at injection site. No systemic symptoms. Patient advised to apply cold compress.`
4. Click **Save**.

### Expected Result

- A green notification appears in the status bar: "Reaction recorded."
- The dialog window closes.
- The Immunization History DataGrid refreshes. The Influenza row now shows:
  - Reaction column: `Local`
- The reaction was saved via the API. Verify by calling: `GET /api/patient/4/immunizations` -- the record includes the reaction text.

---

## Scenario 4: Record Skin Test (PPD/TB Test)

### Steps

1. Click the **Skin Test** TabItem.
2. Fill in the following fields:
   - Test Type ComboBox: `PPD (Tuberculin)`
   - Date Placed DatePicker: (leave as current date/time)
   - Placed By TextBox: `JOHNSON,MARY R`
   - Site of Administration ComboBox: `LEFT FOREARM`
   - Reading: (leave blank -- test has not been read yet)
   - Result: (leave blank)
   - Comments TextBox: `Annual TB screening per facility policy. Patient instructed to return in 48-72 hours for reading.`
3. Click **Record**.

### Expected Result

- A green notification appears in the status bar: "Skin test recorded."
- The Skin Test DataGrid shows the new entry with:
  - Test Type: PPD (Tuberculin)
  - Date Placed: current date/time
  - Date Read: (blank -- pending)
  - Result: PENDING
  - Reading: (blank)
- The skin test is recorded as a health factor via the API: `POST /api/patient/4/health-factors` with category "SCREENING" and health factor name "TB SKIN TEST PLACED".

---

## Scenario 5: Record Skin Test Reading

### Steps

1. On the **Skin Test** TabItem, locate the PPD test recorded in Scenario 4 (status: PENDING).
2. Click the row to select it, then click the **Record Reading** button (or right-click and select **Record Reading**).
3. In the dialog window "Record Skin Test Reading", fill in:
   - Date Read DatePicker: (set to 48 hours after the placement date)
   - Induration (mm) TextBox: `3`
   - Result ComboBox: `Negative`
   - Read By TextBox: `THOMPSON,PATRICIA A`
   - Comments TextBox: `3mm induration measured. Less than 5mm cutoff for this patient risk category. Result: Negative.`
4. Click **Save**.

### Expected Result

- A green notification appears in the status bar: "Skin test reading recorded."
- The dialog window closes.
- The Skin Test DataGrid refreshes. The PPD row now shows:
  - Date Read: 48 hours after placement
  - Result: NEGATIVE
  - Reading: 3 mm
- The reading is recorded as a health factor update via the API: `POST /api/patient/4/health-factors/{healthFactorId}/value` with value "3mm" and magnitude "3".

---

## Scenario 6: View Immunization Contraindications

### Steps

1. On the **Record Immunization** TabItem, select an immunization from the Immunization Name ComboBox: `Influenza, Seasonal Injectable`.
2. Before recording, click the **Check Contraindications** button.
3. The system queries the patient's allergy list: `GET /api/patient/4/allergies`.

### Expected Result

- A dialog window "Contraindication Check" appears showing:
  - A list of the patient's documented allergies.
  - If a known contraindication exists (e.g., egg allergy for influenza), the dialog displays a warning in red text: "ALERT: Patient has documented allergy that may contraindicate this vaccine."
  - If no contraindications are found, the dialog shows: "No known contraindications for this immunization."
- The nurse can click **Proceed** to continue recording the immunization, or **Cancel** to abort.
- If the nurse proceeds despite a contraindication, the IsContraindicated flag is set to `true` on the immunization record.

---

## Scenario 7: Verify Lot Number and Expiration

### Steps

1. On the **Record Immunization** TabItem, fill in:
   - Immunization Name ComboBox: `Tdap (Tetanus, Diphtheria, Pertussis)`
   - CVX Code: `115`
   - Lot Number TextBox: `TD2023-0044`
   - Expiration Date DatePicker: `01/15/2025` (a date in the past)
2. Click **Record**.

### Expected Result

- A red error notification appears in the status bar: "Lot expiration date has passed. Cannot administer expired vaccine."
- The immunization is NOT recorded.
- The form fields remain populated so the nurse can correct the expiration date or select a different lot.

---

## Scenario 8: Print Immunization Record

### Steps

1. On the **Immunization History** TabItem, verify that at least one immunization is displayed.
2. Click the **Print Record** button in the toolbar above the DataGrid.
3. A print preview dialog window appears.

### Expected Result

- The print preview shows a formatted immunization record containing:
  - Patient name and ID at the top
  - A table of all immunizations with columns: Vaccine Name, Date Given, Lot Number, Manufacturer, Site, Route, Series, Administered By
  - A footer with facility name and date printed
- Clicking **Print** sends the document to the selected printer.
- Clicking **Cancel** closes the preview without printing.
- The printed record is suitable for the patient to provide to schools, employers, or other healthcare facilities.

---

## Reference: API Endpoints

| Action                | Method | Endpoint                                                  |
|-----------------------|--------|-----------------------------------------------------------|
| List immunizations    | GET    | `/api/patient/{patientId}/immunizations`                  |
| Record immunization   | POST   | `/api/patient/{patientId}/immunizations`                  |
| Mark as historical    | POST   | `/api/patient/{patientId}/immunizations/{id}/historical`  |
| Record VIS            | POST   | `/api/patient/{patientId}/immunizations/{id}/vis`         |
| Set series info       | POST   | `/api/patient/{patientId}/immunizations/{id}/series-info` |
| Set admin details     | POST   | `/api/patient/{patientId}/immunizations/{id}/administration-details` |
| Set vaccine group     | POST   | `/api/patient/{patientId}/immunizations/{id}/vaccine-group`|
| Set manufacturer      | POST   | `/api/patient/{patientId}/immunizations/{id}/manufacturer` |
| Update registry status| POST   | `/api/patient/{patientId}/immunizations/{id}/registry-status` |
| Add comment           | POST   | `/api/patient/{patientId}/immunizations/{id}/comments`    |
| Get comments          | GET    | `/api/patient/{patientId}/immunizations/{id}/comments`    |
| Generate forecast     | POST   | `/api/immunizationforecast/{patientId}/forecast`          |
| Get forecast          | GET    | `/api/immunizationforecast/{patientId}/forecast`          |

## Reference: Common CVX Codes

| CVX Code | Vaccine Name                                |
|----------|---------------------------------------------|
| 158      | Influenza, injectable, quadrivalent          |
| 115      | Tdap (Tetanus, Diphtheria, Pertussis)        |
| 21       | Varicella (Chickenpox)                       |
| 33       | Pneumococcal polysaccharide PPV23            |
| 113      | Td (adult) preservative-free                 |
| 08       | Hepatitis B, adolescent or pediatric         |
| 03       | MMR (Measles, Mumps, Rubella)                |
| 207      | COVID-19 mRNA, Moderna                       |
| 208      | COVID-19 mRNA, Pfizer-BioNTech               |
| 187      | Recombinant Zoster (Shingrix)                |
