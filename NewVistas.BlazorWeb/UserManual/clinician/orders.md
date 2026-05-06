# Orders

**Route:** `/orders`

The Orders page is the central hub for Computerized Provider Order Entry (CPOE) in NewVistas. It maps to VistA File #100 (Orders) and the ORWDX.m / ORWORR.m MUMPS routines. From this page, you can view existing orders, place new orders, execute predefined order sets, and review complete order history.

![Active Orders tab with status filters and action buttons](screenshots/orders-active-orders.png)

---

## Tabs

The Orders page is organized into four tabs:

### Active Orders Tab

The Active Orders tab displays orders for the selected patient, filtered by status. This is the default view when you open the Orders page.

#### Status Filter

A dropdown filter at the top of the tab lets you control which orders are displayed:

| Filter | Description |
|---|---|
| **Current (Active/Pending/Hold)** | Shows orders that are currently active, pending signature, or on hold. This is the default filter. |
| **All Orders** | Shows all orders regardless of status. |
| **Discontinued** | Shows only orders that have been discontinued. |
| **Completed/Expired** | Shows orders that have been completed or have passed their expiration date. |
| **Pending** | Shows only orders awaiting signature or processing. |
| **Unsigned** | Shows only orders that have not yet been signed by the ordering provider. |

#### Table Columns

| Column | Description |
|---|---|
| **Order** | The order text describing what was ordered (e.g., "CBC WITH DIFFERENTIAL") |
| **Type** | The order type: Lab, Pharmacy, Radiology, Consult, Nursing, Diet, or Vitals |
| **Status** | Current status displayed as a colored badge (see Order Status Workflow below) |
| **Start** | The order start date and time in MM/DD/YYYY HH:MM format |
| **Provider** | Name of the ordering provider |
| **Actions** | Action buttons available for the order based on its current status |

#### Row Highlighting

Order rows are visually highlighted based on status to draw attention to orders requiring action:

- **Yellow background** -- orders on Hold
- **Blue/amber background** -- orders in Pending status

#### Actions

The action buttons available for each order depend on its current status:

| Action | Available When | Description |
|---|---|---|
| **Sign** | Pending or Active | Applies your electronic signature to authorize the order |
| **Hold** | Active | Places the order on hold (temporarily suspends execution) |
| **DC** | Active | Discontinues the order with a documented reason |
| **Release** | Hold | Releases a held order back to Active status |

### New Order Tab

![New Order form with order type selection and fields](screenshots/orders-new-order-form.png)

The New Order tab provides a form for placing new orders. The form fields are:

| Field | Required | Description |
|---|---|---|
| **Order Type** | Yes | Select from: Lab, Pharmacy, Radiology, Consult, Nursing, Diet, Vitals |
| **Order Text** | Yes | Descriptive text for the order (e.g., "CBC WITH DIFFERENTIAL", "LISINOPRIL 10MG TAB") |
| **Urgency** | Yes | Select from: Routine, Urgent, STAT |
| **Instructions** | No | Additional instructions or comments for the order |
| **Clinic / Location** | No | The clinic or location where the order should be processed. Click "Load Clinics" to populate the dropdown with active clinics. |
| **Provider** | Yes | Name of the ordering provider |

New orders are created in **PENDING** status and must be signed before they become active.

Two buttons are available at the bottom of the form:

- **Check Order** -- runs order checks (drug-allergy, duplicate, drug-drug interaction) without placing the order. Results appear inline.
- **Place Order** -- places the order and returns to the Active Orders tab.

### Order Sets Tab

Order Sets are predefined groups of orders designed for common clinical scenarios (e.g., "Admission Orders," "Pre-Op Labs," "Diabetic Management"). They allow you to place multiple related orders at once.

1. Click **Load Order Sets** to retrieve the list of available order sets.
2. Browse the grid of order set cards, each showing the set name, category, service section, and item count.
3. Click an order set card to select it and view its details, including the list of individual orders with sequence number, orderable item, type, and urgency.
4. Enter a **Provider** name in the provider field.
5. Click **Execute Order Set** to place all orders in the set at once.

After execution, the system reports how many orders were created and switches to the Active Orders tab.

### Order History Tab

The Order History tab provides a searchable archive of all orders for the patient.

#### History Filters

| Filter | Description |
|---|---|
| **From** | Start date/time for the search range |
| **To** | End date/time for the search range |
| **Max Results** | Maximum number of orders to return (1-500, default 100) |

Click **Search** to load the history. Results appear in a table with the same columns as the Active Orders tab (Order, Type, Status, Start, Provider) but without action buttons.

---

## Order Status Workflow

Orders in NewVistas follow a defined lifecycle with the following status transitions:

```
PENDING ──> ACTIVE ──> COMPLETED
                  ├──> EXPIRED
                  ├──> HOLD ──> ACTIVE (via Release)
                  └──> DISCONTINUED
```

| Status | Badge Color | Description |
|---|---|---|
| **Pending** | Amber | Order has been placed but not yet signed or activated |
| **Active** | Green | Order is signed and currently being executed |
| **Hold** | Orange | Order execution has been temporarily suspended |
| **Discontinued** | Red | Order has been permanently stopped by a provider |
| **Completed** | Blue/Purple | Order has been fully executed and fulfilled |
| **Expired** | Blue/Purple | Order has passed its stop date |

---

## Writing a New Order

Follow these steps to write and submit a new order:

1. **Enter the Patient ID** in the lookup bar and click **Load** to load the patient's existing orders. Then switch to the **New Order** tab.

2. **Select the Order Type** from the dropdown menu. Choose Lab, Pharmacy, Radiology, Consult, Nursing, Diet, or Vitals based on what you are ordering.

3. **Complete the order details.** Enter the Order Text (required), select the Urgency level, and optionally add Instructions and a Clinic/Location. Enter your name in the Provider field.

4. **Review Order Checks.** Click **Check Order** to run clinical decision support checks before placing the order. Review any warnings that appear (see Order Check Warnings below). You may proceed with placing the order even if warnings are present, but you should document your clinical rationale for overriding any warnings.

5. **Place and sign the order.** Click **Place Order** to submit the order. The order is created in PENDING status. Return to the Active Orders tab and click the **Sign** button on the new order to apply your electronic signature and activate it.

![Order Check warning dialog showing duplicate order and drug interaction warnings](screenshots/orders-order-checks.png)

> **Note:** Orders from the Cover Sheet's Workflow Actions bar (New Order, New Medication, New Lab Order) pre-navigate you to the New Order tab with the appropriate order type already selected.

---

## Order Check Warnings

When you click **Check Order** or place a new order, the system performs clinical decision support checks against the patient's current data. Warnings appear in a yellow highlighted area below the order form.

Each warning shows:

- **Check Type** -- the category of the warning
- **Severity** -- High (red), Moderate (amber), or Low (gray)
- **Message** -- a description of the clinical concern

### Warning Types

| Check Type | Description |
|---|---|
| **Duplicate Order** | An identical or similar order already exists for this patient |
| **Drug Interaction** | The ordered medication interacts with a medication the patient is already taking |
| **Allergy** | The ordered item matches an allergen in the patient's allergy list |
| **Critical Lab** | A recent lab result suggests the order may be contraindicated |
| **Age/Weight** | The order may not be appropriate for the patient's age or weight |

> **Warning:** Order checks are clinical decision support tools. A "High" severity warning such as a drug-allergy interaction should be carefully considered before overriding. If you choose to proceed despite a warning, ensure that your clinical reasoning is documented in the order instructions or in a progress note.

---

## Electronic Signature

![Electronic signature modal for order signing](screenshots/orders-electronic-signature.png)

When you click the **Sign** button on a Pending or Active order, the system records the signature action. In production environments, this generates an audit trail entry with your user identity and timestamp.

> **Note:** Electronic signatures in NewVistas are legally binding and create a permanent record in the audit trail. Only sign orders that you have personally reviewed and authorized.

---

## Tips for Effective Order Management

- **Review your unsigned orders daily.** Pending and unsigned orders appear in the Action Items panel on your Provider Dashboard.
- **Use Order Sets** for common clinical scenarios to save time and reduce errors.
- **Always run Order Checks** before placing medication orders to catch potential drug interactions and allergy conflicts.
- **Discontinue rather than delete.** If an order is no longer needed, use the DC (Discontinue) action rather than leaving it in an ambiguous state. Discontinued orders remain in the order history for audit purposes.
- **Use the Order History tab** to review a patient's complete ordering history when making new clinical decisions.
