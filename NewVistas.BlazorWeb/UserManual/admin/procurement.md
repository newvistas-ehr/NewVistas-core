# Procurement and Facilities

This section covers the procurement (IFCAP) and facilities engineering tools in NewVistas. These modules support the full procurement lifecycle from budget allocation through receiving, as well as facility work order management and building/room directory maintenance.

**Routes:** /ifcap, /engineering

**Primary Roles:** Contracting Officers, Fiscal Service Staff, Control Point Officials, Purchasing Agents, Receiving Clerks, Engineering Officers, Facilities Managers, Maintenance Supervisors

---

## IFCAP (/ifcap)

IFCAP (Integrated Funds Distribution, Control Point Activity, Accounting and Procurement) is the VA's two-stage fund accounting system. It manages the lifecycle of procurement from budget allocation through payment, ensuring that funds are properly committed before purchases are made and properly expended when goods are received.

![IFCAP control point showing budget allocation and remaining balance](screenshots/ifcap-control-point-balance.png)

### Control Points

Control points are budget allocation units that represent the organizational units authorized to obligate funds. Each control point has a defined budget and tracks all financial activity against that budget.

#### Control Point Fields

- **Control Point Number** -- Unique identifier for the budget unit
- **Name** -- Descriptive name of the control point (e.g., "Prosthetics Service", "Laboratory", "Pharmacy")
- **Fiscal Year** -- The federal fiscal year (October 1 through September 30) the allocation covers
- **Status** -- Current operational status

#### Control Point Status

| Status | Description |
|--------|-------------|
| **ACTIVE** | Control point is operational and can be used for new purchase requests |
| **FROZEN** | Control point is temporarily suspended; no new obligations can be created, but existing obligations remain valid |
| **CLOSED** | Control point is closed for the fiscal year; no further activity is permitted |

> **Warning:** When a control point is FROZEN, no new purchase requests can be submitted against it. Existing purchase orders in progress will continue, but new requests will be rejected. Contact the Fiscal Service to request unfreezing a control point.

#### Financial Tracking

Each control point tracks four key financial figures:

| Field | Description |
|-------|-------------|
| **Total Allocation** | The total budget amount allocated to the control point for the fiscal year |
| **Obligations** | The total amount of funds committed through approved purchase orders (Stage 1) |
| **Expenditures** | The total amount of funds expended through completed receiving reports (Stage 2) |
| **Remaining Balance** | Total Allocation minus Obligations. This is the amount available for new purchase requests |

The relationship between these figures:

```
Remaining Balance = Total Allocation - Obligations
Unliquidated Obligations = Obligations - Expenditures
```

> **Note:** The Remaining Balance represents funds that have not yet been committed. Once a purchase order is approved, the funds move from Remaining Balance to Obligations. When goods are received and the receiving report is completed, the funds move from Obligations to Expenditures.

#### Creating a Control Point

1. Navigate to the IFCAP page (/ifcap).
2. Click **New Control Point**.
3. Enter the control point number, name, and fiscal year.
4. Enter the **Total Allocation** amount.
5. Click **Save**.

### Purchase Requests

A purchase request initiates the procurement process. It identifies what needs to be purchased, the estimated cost, and the control point that will fund the purchase.

#### Purchase Request Workflow

```
DRAFT → SUBMITTED → APPROVED → OBLIGATED
                 ↘ REJECTED
                 ↘ CANCELLED
```

- **DRAFT** -- The request has been created but not yet submitted for approval. Can be edited freely.
- **SUBMITTED** -- The request has been submitted for review and approval by the control point official or approving authority.
- **APPROVED** -- The request has been approved. A purchase order can now be generated.
- **OBLIGATED** -- Funds have been committed against the control point. The purchase order has been created.
- **REJECTED** -- The request was not approved. The rejection reason is documented for the requester.
- **CANCELLED** -- The request was cancelled by the requester or administrator before approval.

#### Purchase Request Fields

- **Request Number** -- System-generated unique identifier
- **Date** -- Date the request was created
- **Requester** -- The person initiating the request
- **Control Point** -- The budget unit funding the purchase
- **Item Description** -- Detailed description of the item or service being requested
- **Quantity** -- Number of items requested
- **Estimated Unit Cost** -- Estimated cost per unit
- **Estimated Total Cost** -- Calculated total (quantity x estimated unit cost)
- **Justification** -- Business justification for the purchase
- **Priority** -- Urgency of the request (Routine, Urgent, Emergency)
- **Suggested Vendor** -- Optionally, a preferred vendor

#### Creating a Purchase Request

1. Navigate to the IFCAP page (/ifcap).
2. Click **New Purchase Request**.
3. Select the **Control Point** that will fund the purchase.
4. Enter the item description, quantity, and estimated unit cost.
5. Provide a **Justification** explaining the business need.
6. Set the **Priority** level.
7. Optionally, suggest a vendor.
8. Click **Save as Draft** to continue editing later, or **Submit** to send for approval.

> **Tip:** Verify the control point's remaining balance before submitting a purchase request. If the estimated total exceeds the remaining balance, the request will be rejected or delayed until additional funds are allocated.

![Purchase request form with item details and control point selection](screenshots/ifcap-purchase-request.png)

#### Approving a Purchase Request

1. Navigate to the IFCAP page and filter for requests with status **SUBMITTED**.
2. Click on the request to review the details.
3. Verify the justification, estimated cost, and control point balance.
4. Click **Approve** to authorize the purchase, or **Reject** with a documented reason.

### Purchase Orders

A purchase order (PO) is the formal authorization to a vendor to provide goods or services. Purchase orders are created from approved purchase requests.

#### Purchase Order Workflow

```
CREATED → SENT → ACKNOWLEDGED → PARTIALLY_RECEIVED → RECEIVED → CLOSED
                                                                ↘ CANCELLED
```

- **CREATED** -- The PO has been generated from an approved purchase request but has not yet been sent to the vendor.
- **SENT** -- The PO has been transmitted to the vendor.
- **ACKNOWLEDGED** -- The vendor has acknowledged receipt of the PO.
- **PARTIALLY_RECEIVED** -- Some items on the PO have been received but the order is not yet complete.
- **RECEIVED** -- All items on the PO have been received and inspected.
- **CLOSED** -- The PO is complete and all financial transactions have been reconciled.
- **CANCELLED** -- The PO was cancelled before completion. Any obligated funds are de-obligated and returned to the control point's remaining balance.

#### Purchase Order Fields

- **PO Number** -- System-generated unique identifier
- **Date** -- Date the PO was created
- **Vendor** -- The vendor who will supply the goods or services
- **Control Point** -- The budget unit funding the purchase
- **Line Items** -- Individual items with description, quantity, unit cost, and total cost
- **Total Amount** -- Sum of all line items
- **Terms** -- Payment terms and delivery expectations
- **Status** -- Current PO status

#### Creating a Purchase Order

1. From an approved purchase request, click **Create Purchase Order**.
2. Select or confirm the **Vendor**.
3. Review and adjust the line items if needed (quantities, costs).
4. Enter the payment **Terms** and expected delivery date.
5. Click **Create PO**.
6. The system obligates the PO amount against the control point balance (Stage 1).
7. Click **Send to Vendor** to transmit the PO.

### Two-Stage Fund Accounting

IFCAP uses a two-stage fund accounting model to ensure fiscal integrity:

#### Stage 1: Obligation

When a purchase order is approved and created, the funds are **obligated** (committed) against the control point's allocation. This ensures that the funds are reserved for the specific purchase and cannot be used for another purpose.

- Obligation occurs when the PO is created, not when it is sent to the vendor.
- The obligated amount reduces the control point's remaining balance.
- If the PO is cancelled, the obligation is reversed and the funds are returned to the remaining balance.

> **Note:** Obligation prevents over-commitment of funds. The system will not allow a PO to be created if the amount exceeds the control point's remaining balance (unless explicitly overridden by an authorized fiscal official).

#### Stage 2: Expenditure

When goods are received and a receiving report is completed, the funds move from obligation to **expenditure**. This confirms that the goods or services have been delivered and triggers the payment process.

- Expenditure occurs when the receiving report is finalized.
- The expenditure amount reduces the unliquidated obligation balance.
- Partial receipts create partial expenditures; the remaining obligation stays in place until the order is fully received.

The two-stage model can be summarized as:

| Event | Financial Impact |
|-------|-----------------|
| PO Created | Funds move from Remaining Balance to Obligations |
| PO Cancelled | Funds return from Obligations to Remaining Balance |
| Goods Partially Received | Partial amount moves from Obligations to Expenditures |
| Goods Fully Received | Remaining obligation moves to Expenditures |

### Vendors

Manage the vendor directory for procurement activities.

#### Vendor Fields

- **Name** -- Full legal name of the vendor
- **Contact** -- Primary contact person at the vendor
- **Phone** -- Vendor phone number
- **Email** -- Vendor email address
- **Tax ID** -- Vendor's tax identification number (EIN or SSN)
- **Contract Number** -- Associated contract number (if applicable)
- **Payment Terms** -- Standard payment terms (e.g., Net 30, Net 60, Upon Receipt)

#### Adding a Vendor

1. Navigate to the vendor section of the IFCAP page.
2. Click **Add Vendor**.
3. Enter the vendor's name, contact information, tax ID, and payment terms.
4. If there is an existing contract, enter the **Contract Number**.
5. Click **Save**.

> **Note:** Verify the vendor's tax ID and debarment status before creating purchase orders. Use the System for Award Management (SAM.gov) to check vendor eligibility for government contracts.

### Receiving Reports

Receiving reports document the receipt of goods or services against a purchase order. They trigger Stage 2 (expenditure) of the fund accounting process.

#### Receiving Report Fields

- **Receiving Report Number** -- System-generated unique identifier
- **Purchase Order** -- The PO against which the goods were received
- **Receipt Date** -- Date the goods were received at the facility
- **Received By** -- Name of the person who inspected and received the goods
- **Quantity Received** -- Number of items received (may be less than ordered for partial receipts)
- **Condition** -- Condition of the received goods

#### Condition Assessment

| Condition | Description | Action |
|-----------|-------------|--------|
| **SATISFACTORY** | Goods received in acceptable condition, matching the PO specifications | Process for payment |
| **DAMAGED** | Goods received but damaged during shipping or defective | Document damage; contact vendor for replacement or credit |
| **PARTIAL** | Only a portion of the ordered goods were received | Create a partial receiving report; track remaining items |

#### Creating a Receiving Report

1. Navigate to the receiving section of the IFCAP page.
2. Click **New Receiving Report**.
3. Select the **Purchase Order** from the list of open POs.
4. Enter the **Receipt Date**.
5. For each line item, enter the **Quantity Received** and assess the **Condition**.
6. Enter the name of the person who physically received and inspected the goods.
7. Click **Submit Receiving Report**.
8. The system moves the corresponding funds from Obligations to Expenditures (Stage 2).

> **Warning:** Verify that the goods received match the purchase order specifications before submitting the receiving report. Once submitted, the receiving report triggers the payment process. Discrepancies should be documented and resolved with the vendor before the report is finalized.

---

## Engineering (/engineering)

The Engineering page manages facility maintenance through work orders and a comprehensive building/room directory. It is organized into two tabs.

### Tab 1: Work Orders

Work orders track maintenance requests, repair activities, and preventive maintenance across the facility.

![Work order list showing priorities and statuses](screenshots/engineering-work-order-list.png)

#### Work Order Fields

- **WO Number** -- System-generated unique work order identifier
- **Title** -- Brief descriptive title of the work needed
- **Description** -- Detailed description of the problem, requested work, or maintenance activity
- **Location** -- Building, floor, and room where the work is needed
- **Requester** -- Person who submitted the work order
- **Priority** -- Urgency classification
- **Status** -- Current work order status
- **Category** -- Type of work required
- **Assigned To** -- Engineering staff member or crew assigned to the work
- **Date Submitted** -- When the work order was created
- **Date Completed** -- When the work was finished (if applicable)

#### Priority Levels

| Priority | Description | Expected Response Time |
|----------|-------------|----------------------|
| **EMERGENCY** | Immediate life safety concern or critical infrastructure failure (e.g., power outage in patient care area, water main break, fire alarm malfunction) | Immediate response; crew dispatched within minutes |
| **URGENT** | Significant operational impact that needs prompt attention but is not an immediate safety hazard (e.g., HVAC failure in occupied area, elevator outage, broken door lock in secure area) | Within 24 hours |
| **ROUTINE** | Standard maintenance or repair request that does not significantly impact operations (e.g., leaking faucet, broken light fixture, paint touch-up) | Within 1-2 weeks |
| **SCHEDULED** | Planned preventive maintenance or scheduled improvement project | Per the established maintenance schedule |

> **Tip:** Use EMERGENCY priority only for genuine life safety and critical infrastructure issues. Overuse of emergency priority delays response to true emergencies and disrupts preventive maintenance schedules.

#### Work Order Status

```
SUBMITTED → ASSIGNED → IN_PROGRESS → COMPLETED → CLOSED
                          ↘ ON_HOLD
                                                ↘ CANCELLED
```

- **SUBMITTED** -- The work order has been created and is awaiting review by Engineering.
- **ASSIGNED** -- An engineering staff member or crew has been assigned to the work.
- **IN_PROGRESS** -- Work has begun on the request.
- **ON_HOLD** -- Work is paused, typically waiting for parts, materials, or contractor availability.
- **COMPLETED** -- The work has been finished and is awaiting verification and closure.
- **CLOSED** -- The work order has been verified as complete and is closed.
- **CANCELLED** -- The work order was cancelled before completion (reason documented).

#### Work Order Categories

| Category | Description | Examples |
|----------|-------------|---------|
| **ELECTRICAL** | Electrical systems and components | Power outages, lighting repair, outlet installation, wiring issues, generator maintenance |
| **PLUMBING** | Water supply, drainage, and fixtures | Leaks, clogs, toilet repair, water heater maintenance, backflow prevention |
| **HVAC** | Heating, ventilation, and air conditioning systems | Temperature control, air quality, filter replacement, chiller maintenance |
| **STRUCTURAL** | Building structure and architectural elements | Wall damage, floor repair, ceiling tiles, door and window issues |
| **SAFETY** | Fire protection, alarms, and safety systems | Fire alarm testing, sprinkler maintenance, emergency exit signage, AED inspection |
| **BIOMEDICAL** | Medical equipment maintenance and repair | Equipment calibration, preventive maintenance, repair of clinical devices |
| **GROUNDS** | Exterior maintenance and landscaping | Lawn care, snow removal, parking lot repair, exterior lighting, signage |
| **OTHER** | Requests not covered by the above categories | Furniture moves, key requests, signage installation, special projects |

#### Submitting a Work Order

1. Navigate to the Engineering page (/engineering) and click the **Work Orders** tab.
2. Click **New Work Order**.
3. Enter a descriptive **Title** and detailed **Description** of the problem or requested work.
4. Select the **Location** (building, floor, room) from the facilities directory.
5. Set the **Priority** level based on the urgency and impact.
6. Select the **Category** that best describes the type of work needed.
7. Click **Submit**.

> **Note:** When submitting a work order, provide as much detail as possible in the description. Include the specific nature of the problem, when it was first noticed, any intermittent patterns, and the impact on operations. Detailed descriptions help engineering staff diagnose and resolve issues more quickly.

#### Managing Work Orders

1. **Review submitted work orders** -- Filter for SUBMITTED status and review each request.
2. **Assign the work order** -- Select the appropriate engineering staff member or crew and change the status to ASSIGNED.
3. **Begin work** -- When work starts, update the status to IN_PROGRESS.
4. **Document progress** -- Add notes to the work order as work progresses, especially if parts are ordered or the work is placed ON_HOLD.
5. **Complete the work** -- When finished, update the status to COMPLETED and document what was done.
6. **Close the work order** -- After verification, change the status to CLOSED.

### Tab 2: Facilities

The Facilities tab maintains the comprehensive directory of buildings, floors, and rooms across the facility campus. This directory supports work order location tracking, space assignment, and maintenance scheduling.

![Facilities directory showing buildings, rooms, and departments](screenshots/engineering-facilities-directory.png)

#### Facility Record Fields

- **Building** -- Building name or number (e.g., "Building 1", "Main Hospital", "Research Annex")
- **Floor** -- Floor level (e.g., "Ground", "1st Floor", "2nd Floor", "Basement")
- **Room** -- Room number or identifier (e.g., "101A", "OR-3", "Lab 204")
- **Department** -- Department assigned to the room (e.g., "Primary Care", "Radiology", "Engineering")
- **Room Type** -- Classification of the room's purpose

#### Room Types

| Room Type | Description |
|-----------|-------------|
| **OFFICE** | Administrative or staff office space |
| **EXAM_ROOM** | Clinical examination room |
| **WARD** | Inpatient ward or patient bed area |
| **LAB** | Laboratory space (clinical or research) |
| **PHARMACY** | Pharmacy dispensing or storage area |
| **OR** | Operating room or surgical suite |
| **STORAGE** | General or specialized storage area |
| **MECHANICAL** | Mechanical, utility, or infrastructure space (HVAC, electrical, plumbing) |

#### Facility Status

| Status | Description |
|--------|-------------|
| **ACTIVE** | Room is currently operational and in use |
| **UNDER_RENOVATION** | Room is undergoing renovation or remodeling; temporarily unavailable for use |
| **DECOMMISSIONED** | Room has been permanently taken out of service |

#### Adding a Facility Record

1. Click the **Facilities** tab.
2. Click **Add Room**.
3. Select or enter the **Building** and **Floor**.
4. Enter the **Room** number or identifier.
5. Assign the **Department**.
6. Select the **Room Type**.
7. Set the **Status** (typically ACTIVE for new entries).
8. Click **Save**.

#### Maintenance Scheduling

The Facilities tab also supports scheduling and tracking preventive maintenance for facility systems.

- **Schedule maintenance** by room, floor, or building for systems such as HVAC, fire safety, elevators, plumbing, and electrical.
- **Set recurrence intervals** (monthly, quarterly, semi-annual, annual) based on manufacturer recommendations and regulatory requirements.
- **Track compliance** with a dashboard showing upcoming, overdue, and completed maintenance activities.

> **Note:** Preventive maintenance scheduling integrates with the work order system. When a scheduled maintenance date arrives, the system automatically generates a work order with SCHEDULED priority.

---

## Common Workflows

### Procurement Lifecycle (End-to-End)

1. **Identify the need** -- A service or department identifies a need for goods or services. Verify the control point has sufficient remaining balance.
2. **Create a purchase request** -- Submit a purchase request on the IFCAP page with the item description, quantity, estimated cost, and justification.
3. **Approve the request** -- The control point official or approving authority reviews and approves the request.
4. **Create the purchase order** -- Generate a PO from the approved request. The system obligates the funds (Stage 1).
5. **Send the PO to the vendor** -- Transmit the PO to the selected vendor.
6. **Receive the goods** -- When goods arrive, inspect them and create a receiving report. The system records the expenditure (Stage 2).
7. **Close the PO** -- Once all items are received and the receiving report is finalized, close the PO.

### Emergency Work Order Response

1. **Submit the emergency work order** -- Create a work order with EMERGENCY priority. Include the location and a clear description of the life safety or critical infrastructure issue.
2. **Dispatch engineering staff** -- Assign the work order to available staff immediately.
3. **Respond and mitigate** -- Engineering staff respond to the location and take immediate action to mitigate the safety concern.
4. **Document the resolution** -- Update the work order with findings, actions taken, and any follow-up needed.
5. **Complete and close** -- Mark the work order as COMPLETED and then CLOSED after verification.

### Fiscal Year-End Closeout

1. **Review open obligations** -- Before the end of the fiscal year (September 30), review all open POs and their remaining obligations.
2. **Expedite pending receipts** -- Contact vendors for any goods not yet received. Process all pending receiving reports.
3. **Cancel unused POs** -- Cancel any POs that will not be fulfilled. De-obligated funds return to the remaining balance.
4. **Close control points** -- After all fiscal year activity is reconciled, close the control points for the ending fiscal year.
5. **Open new fiscal year** -- Create new control points for the new fiscal year (beginning October 1) with the approved allocations.

---

## Tips and Best Practices

1. **Monitor control point balances regularly.** Check remaining balances at least weekly, especially in the second half of the fiscal year. Running out of funds mid-year disrupts patient care services.

2. **Submit purchase requests early.** The procurement process involves multiple approval steps. Submit requests well in advance of the date the goods or services are needed.

3. **Verify vendor eligibility.** Always check the vendor's status in SAM.gov before creating a purchase order. Contracting with debarred or suspended vendors is prohibited.

4. **Complete receiving reports promptly.** Delayed receiving reports hold funds in obligation status and can distort financial reports. Process receiving reports within 48 hours of goods receipt.

5. **Document everything.** Include justifications in purchase requests, notes on receiving reports, and detailed descriptions in work orders. Thorough documentation supports audits and dispute resolution.

6. **Use the correct work order priority.** Reserve EMERGENCY for true life safety issues. Misuse of emergency priority delays response to genuine emergencies and disrupts scheduled maintenance.

7. **Keep the facilities directory current.** Update room assignments, statuses, and department information promptly when spaces change. Accurate facility data supports work order routing and space planning.

8. **Schedule preventive maintenance proactively.** Preventive maintenance reduces emergency repairs and extends equipment life. Ensure all scheduled maintenance is completed on time and documented.

9. **Reconcile obligations monthly.** Compare the system's obligation records with accounting reports to identify discrepancies early. Investigate and resolve any differences promptly.

10. **Plan for fiscal year transitions.** Begin year-end closeout activities no later than August. This provides sufficient time to clear open obligations, cancel unused POs, and prepare new fiscal year allocations.
