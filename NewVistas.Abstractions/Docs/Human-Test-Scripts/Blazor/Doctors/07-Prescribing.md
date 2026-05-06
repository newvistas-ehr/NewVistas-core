# Outpatient Prescribing -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR2 / Password: smythVista1
- Patient: 30
- Pre-conditions: Demo data loaded. Load demo prescriptions first: navigate to `/outpatientpharmacy`, enter patient 30, click **Load Demo**. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Write New Prescription for Lisinopril (Happy Path)

### Steps
1. Log in as **DOCTOR2** (CHEN,MICHAEL L / Family Medicine)
2. Navigate to `/outpatientpharmacy`
3. Enter Patient ID: `30`
4. Click **Load Prescriptions**
5. Review existing prescriptions in the table (columns: Drug, Dosage, Status, Priority, Refills Left, Expires, Verified, Counsel, Provider)
6. Note: New prescriptions for outpatient pharmacy are typically placed via the Orders page. Navigate to `/orders`
7. Enter Patient ID: `30`, click **Load**
8. Click the **New Order** tab
9. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `LISINOPRIL 10MG TAB DAILY`
   - Urgency: **ROUTINE**
   - Instructions: `Take one tablet by mouth daily in the morning for blood pressure. Quantity: 30, Days Supply: 30, Refills: 3`
   - Provider: `CHEN,MICHAEL L`
10. Click **Place Order**

### Expected Result
- Green success: "Order placed: LISINOPRIL 10MG TAB DAILY"
- Order appears in Active Orders with Type: "Pharmacy", Status: "Pending"

### Steps (continued)
11. Click the **Sign** button on the new order

### Expected Result
- Order status changes to "Active"
- The pharmacy order is now ready for filling

---

## Scenario 2: View Prescription Detail on Outpatient Pharmacy Page

### Steps
1. Navigate to `/outpatientpharmacy`
2. Enter Patient ID: `30`
3. Click **Load Prescriptions** (or **Load Demo** if no prescriptions exist)
4. Click on a prescription row in the table

### Expected Result
- A **detail panel** appears below the table showing:
  - Drug name with status badge (ACTIVE green, DISCONTINUED red, HOLD yellow)
  - Detail grid with: Rx ID, Rx Number, SIG, Route, Schedule, Days Supply, Quantity, Priority, Provider, Pharmacy
  - If verified: "Verified by: [name] on [date]"
  - If label printed: "Label printed: [date]"
  - If counseling required: "Patient counseling required" flag (orange)
- **Action buttons** (if status is ACTIVE):
  - Fill, Refill, Discontinue, Hold, Verify, Print Label, View Label, Record Dispense
  - If counseling required and not completed: Complete Counseling
  - Toggle counseling button: "Require Counseling" / "Clear Counseling"
- **Refill Eligibility** section (if fill date exists):
  - Eligible/Not Eligible badge
  - Refills remaining count
  - Earliest refill date
  - Percent consumed

---

## Scenario 3: Prescribe Controlled Substance -- Schedule II (No Refills)

### Steps
1. Navigate to `/orders`
2. Enter Patient ID: `30`, click **Load**
3. Click **New Order** tab
4. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `OXYCODONE 5MG TAB Q6H PRN PAIN`
   - Urgency: **ROUTINE**
   - Instructions: `Schedule II controlled substance. NO REFILLS ALLOWED. Quantity: 20, Days Supply: 5, Refills: 0. Patient counseled on safe storage and disposal.`
   - Provider: `CHEN,MICHAEL L`
5. Click **Check Order** (to see if drug interaction or DUR warnings fire)
6. Click **Place Order**

### Expected Result
- Order placed successfully
- Note: Schedule II controlled substances cannot have refills per DEA regulations. The system should enforce Refills: 0.

---

## Scenario 4: Prescribe Schedule IV Medication (Limited Refills)

### Steps
1. Click **New Order** tab
2. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `LORAZEPAM 0.5MG TAB BID PRN ANXIETY`
   - Urgency: **ROUTINE**
   - Instructions: `Schedule IV. Max 5 refills in 6 months per DEA. Quantity: 60, Days Supply: 30, Refills: 5.`
   - Provider: `CHEN,MICHAEL L`
3. Click **Place Order**

### Expected Result
- Order placed successfully with Type: Pharmacy

---

## Scenario 5: DUR Trigger -- Duplicate Drug Detected

### Steps
1. With patient 30 who already has a LISINOPRIL prescription
2. Click **New Order** tab
3. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `LISINOPRIL 20MG TAB DAILY`
   - Provider: `CHEN,MICHAEL L`
4. Click **Check Order**

### Expected Result
- **Order Check Warnings** section appears
- Warning shows:
  - Check Type: "DUPLICATE_ORDER" (or "DUPLICATE_DRUG")
  - Severity: Moderate or High
  - Message: Indicates duplicate medication in active orders

### Steps (continued)
5. Click **Place Order** to override

### Expected Result
- Order is placed despite the warning (physician override)

---

## Scenario 6: DUR Trigger -- Drug-Allergy Contraindication

### Steps
1. First, document a Penicillin allergy for patient 30:
   - Navigate to `/allergies?patientId=30`
   - Click **Record Allergy** tab
   - Allergen: `Penicillin`
   - Allergen Type: **Drug**
   - Reactions: `Anaphylaxis, Urticaria`
   - Severity: **Severe**
   - Observed / Historical: **Observed**
   - Click **Record Allergy**
2. Navigate to `/orders`, load patient 30
3. Click **New Order** tab
4. Fill in:
   - Order Type: **Pharmacy**
   - Order Text: `AMOXICILLIN 500MG CAP TID`
   - Provider: `CHEN,MICHAEL L`
5. Click **Check Order**

### Expected Result
- **Order Check Warnings** section appears
- Warning shows:
  - Check Type: "DRUG_ALLERGY"
  - Severity: **High** (red text)
  - Message: Indicates potential allergic reaction due to Penicillin cross-reactivity

---

## Scenario 7: Discontinue a Prescription

### Steps
1. Navigate to `/outpatientpharmacy`, load patient 30
2. Click on an ACTIVE prescription row
3. Click the **Discontinue** button (red)
4. A text input row appears: "Discontinue reason"
5. Enter: `Medication changed to alternative agent`
6. Click **Confirm D/C**

### Expected Result
- Green action message: "Prescription discontinued."
- Status changes to DISCONTINUED (red badge)
- The D/C reason appears in detail: "D/C reason: Medication changed to alternative agent"
- Action buttons disappear

---

## Scenario 8: Hold and Resume a Prescription

### Steps
1. Click on an ACTIVE prescription
2. Click **Hold** (orange button)

### Expected Result
- Status changes to HOLD (yellow badge)
- A **Resume** button appears
- Hold reason displays in detail

### Steps (continued)
3. Click **Resume**

### Expected Result
- Status returns to ACTIVE (green badge)
- Normal action buttons reappear

---

## Scenario 9: Verify and Print Label

### Steps
1. Click on an ACTIVE, unverified prescription (Verified column is empty)
2. Click **Verify**

### Expected Result
- Action message confirms verification
- Verified column now shows checkmark
- Detail shows "Verified by: [pharmacist] on [date]"

### Steps (continued)
3. Click **Print Label**

### Expected Result
- Action message confirms label printed
- Detail shows "Label printed: [date]"
- **View Label** button becomes available
