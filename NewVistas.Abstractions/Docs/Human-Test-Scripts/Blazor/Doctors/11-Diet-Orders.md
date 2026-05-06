# Diet Orders (Dietetics) -- Physician Human Test Script

## Prerequisites
- Login: DOCTOR1 / Password: smythVista1
- Patient: 9
- Pre-conditions: Demo data loaded. SiloHost, WebServer, and BlazorWeb running.

---

## Scenario 1: Order a Regular Diet (Happy Path)

### Steps
1. Log in as **DOCTOR1** (SMITH,JOHN A / Internal Medicine)
2. Navigate to `/dietetics`
3. Enter Patient ID: `9`
4. Click **Load** (or press Enter)
5. Note any existing diet orders in the Diet Orders tab
6. Click the **+ Create Diet Order** button (green)
7. The "Create Diet Order" form appears
8. Fill in:
   - Diet Type *: **REGULAR** (dropdown; options: REGULAR, CARDIAC, RENAL, DIABETIC, CLEAR LIQUID, FULL LIQUID, NPO)
   - Current Diet: `Regular diet`
   - Texture: **REGULAR** (dropdown; options: REGULAR, MECHANICAL SOFT, PUREED, GROUND)
   - Fluid Consistency: **THIN** (dropdown; options: THIN, NECTAR THICK, HONEY THICK, PUDDING THICK)
   - Calorie Level: `2000 kcal`
   - Modifications (comma-sep.): (leave empty for regular diet)
   - Provider: `SMITH,JOHN A`
   - Special Instructions: `Regular diet, no known food allergies`
9. Click **Create Order**

### Expected Result
- Green success: "Diet order created."
- Form closes
- Diet Orders list reloads
- New order appears in the table:
  - Diet Type: REGULAR
  - Texture: REGULAR
  - Calories: (may show if set)
  - Modifications: "--"
  - Status: badge "ACTIVE" (green)
  - Action button: **D/C**

---

## Scenario 2: Cardiac Diet with Sodium Restriction

### Steps
1. Click **+ Create Diet Order**
2. Fill in:
   - Diet Type *: **CARDIAC**
   - Current Diet: `Cardiac diet`
   - Texture: **REGULAR**
   - Fluid Consistency: **THIN**
   - Calorie Level: `1800 kcal`
   - Modifications: `LOW SODIUM, LOW FAT, LOW CHOLESTEROL`
   - Provider: `SMITH,JOHN A`
   - Special Instructions: `Sodium restriction < 2g/day. Low saturated fat. Heart-healthy diet.`
3. Click **Create Order**

### Expected Result
- Diet order created with Type: CARDIAC
- Modifications column shows: "LOW SODIUM, LOW FAT, LOW CHOLESTEROL"

### Steps (continued -- Set Nutrition Goals)
4. Click the new CARDIAC diet row to view the **Detail** tab
5. Scroll to the **Nutrition Goals** section
6. Fill in:
   - Calorie Target: `1800`
   - Target Weight (kg): `82.0`
7. Click **Set Nutrition Goals**

### Expected Result
- Green success: "Nutrition goals set."
- Detail refreshes -- Calorie Target and Target Weight now visible

### Steps (continued -- Set Fluid Restriction)
8. Scroll to the **Fluid Restriction** section
9. Fill in:
   - Fluid Restriction (mL): `1500`
10. Click **Set Fluid Restriction**

### Expected Result
- Green success: "Fluid restriction set."
- Detail shows: "Fluid Restriction: 1500 mL"

---

## Scenario 3: NPO (Nothing by Mouth) for Pre-Surgery

### Steps
1. Click **+ Create Diet Order**
2. Fill in:
   - Diet Type *: **NPO**
   - Current Diet: `NPO`
   - Provider: `SMITH,JOHN A`
   - Special Instructions: `NPO after midnight for scheduled surgery. Ice chips only until 2 hours pre-op.`
3. Click **Create Order**
4. Click the NPO diet row to view detail
5. Scroll to the **NPO Status** section
6. Fill in:
   - NPO: **Yes**
   - Start Date: Tonight at midnight (select today's date)
   - End Date: Tomorrow (select next day)
7. Click **Set NPO**

### Expected Result
- Green success: "NPO status set."
- Detail shows: NPO: Yes
- Start and end dates displayed

---

## Scenario 4: Diabetic Diet with Calorie Level

### Steps
1. Click **+ Create Diet Order**
2. Fill in:
   - Diet Type *: **DIABETIC**
   - Current Diet: `Diabetic diet`
   - Texture: **REGULAR**
   - Fluid Consistency: **THIN**
   - Calorie Level: `1600 kcal`
   - Modifications: `CONSISTENT CARBOHYDRATE, NO CONCENTRATED SWEETS`
   - Provider: `SMITH,JOHN A`
   - Special Instructions: `Consistent carbohydrate diet. 3 meals + 1 bedtime snack. CHO counting: ~45-60g per meal.`
3. Click **Create Order**
4. View the detail
5. Set Nutrition Goals:
   - Calorie Target: `1600`
   - Target Weight: `75.0`
6. Record BMI:
   - BMI: `28.5`
   - Click **Record BMI**
7. Set Allergy Considerations:
   - Text: `Lactose intolerant. Use lactose-free dairy alternatives.`
   - Click **Save**

### Expected Result
- Diet created with all modifications
- BMI shows: 28.5
- Allergy Considerations displayed in detail

---

## Scenario 5: Mechanical Soft Texture Modification

### Steps
1. Click **+ Create Diet Order**
2. Fill in:
   - Diet Type *: **REGULAR**
   - Current Diet: `Mechanical soft diet`
   - Texture: **MECHANICAL SOFT**
   - Fluid Consistency: **NECTAR THICK**
   - Calorie Level: `2000 kcal`
   - Provider: `SMITH,JOHN A`
   - Special Instructions: `Dysphagia diet per SLP evaluation. Mechanical soft texture, nectar-thick liquids. Supervised meals. Upright positioning 30 min after meals.`
3. Click **Create Order**
4. View detail and set texture consistency:
   - Texture: **MECHANICAL_SOFT** (dropdown; options: REGULAR, MECHANICAL_SOFT, PUREED, LIQUID)
   - Click **Set Texture**

### Expected Result
- Diet created with Texture: MECHANICAL SOFT, Fluid: NECTAR THICK
- Texture Consistency field updated

---

## Scenario 6: Discontinue a Diet Order

### Steps
1. In the Diet Orders list, locate an ACTIVE diet order
2. Click the **D/C** button (red) on that row

### Expected Result
- Green success: "Diet order discontinued."
- Status changes to badge (red, not ACTIVE)
- D/C button disappears

---

## Scenario 7: Record Nutrition Assessment

### Steps
1. View an active diet order detail
2. Scroll to the **Nutrition Assessment** section
3. Fill in:
   - Score *: `6.5`
   - Assessed By *: `SMITH,JOHN A`
4. Click **Record Assessment**

### Expected Result
- Green success: "Nutrition assessment recorded."
- Assessment Score visible in detail: 6.5

---

## Scenario 8: Set Meal Preferences

### Steps
1. View an active diet order detail
2. Scroll to the **Meal Preferences** section
3. Fill in:
   - Preferences: `No pork products. Prefers kosher meals when available. Prefers warm breakfast. No coffee, prefers tea.`
4. Click **Save Preferences**

### Expected Result
- Green success: "Meal preferences saved."
- Meal Preferences text visible in detail

---

## Scenario 9: Tube Feeding Order

### Steps
1. View an active diet order detail
2. Scroll to the **Tube Feeding** section
3. Fill in:
   - Tube Feeding: **Yes**
   - Formula: `Jevity 1.5`
   - Rate (mL/hr): `65.0`
4. Click **Set Tube Feeding**

### Expected Result
- Green success: "Tube feeding set."
- Detail shows:
  - Tube Feeding: Yes
  - Formula: Jevity 1.5
  - Rate: 65.0 mL/hr

---

## Scenario 10: View Modification History

### Steps
1. View any diet order detail
2. Scroll to the **Modification History** section

### Expected Result
- If changes have been made, a table shows: Date, Change, By
- If no history, shows: "No modification history."
