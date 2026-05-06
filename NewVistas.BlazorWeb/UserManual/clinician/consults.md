# Consults

**Route:** `/consults`

The Consults page manages the requesting, tracking, and completion of clinical consult requests in NewVistas. It maps to VistA File #123 and the GMRCACTM.m MUMPS routine. Consults are the formal mechanism by which one provider or service requests the clinical opinion, evaluation, or procedure of another service or specialist.

![Consult list with urgency and status badges, showing pending and active consults](screenshots/consults-list.png)

---

## Consult Status Workflow

Consults in NewVistas follow a defined lifecycle:

```
PENDING ──> ACTIVE (via Accept) ──> SCHEDULED ──> COMPLETE (via Complete)
   │                      │                            
   │                      └──> COMPLETE (via Complete, skipping Schedule)
   │
   ├──> CANCELLED (with documented reason)
   └──> DISCONTINUED (with documented reason)
```

| Status | Description |
|---|---|
| **PENDING** | Consult has been requested and is awaiting acceptance by the consulting service |
| **ACTIVE** | Consult has been accepted by the consulting service and is being worked |
| **SCHEDULED** | A date, time, and location have been assigned for the consult appointment |
| **COMPLETE** | The consulting service has completed their evaluation and documented findings |
| **CANCELLED** | The consult was cancelled before completion (requires a documented reason) |
| **DISCONTINUED** | The consult was discontinued after being accepted (requires a documented reason) |

> **Note:** A consult can be completed directly from ACTIVE status without being scheduled first, or it can go through the SCHEDULED intermediate step. The path depends on the nature of the consult and the consulting service's workflow.

---

## Consult List

### Loading Consults

1. Enter the **Patient ID** in the lookup bar.
2. Click **Load Consults** (or press **Enter**).
3. To create a new consult, click **+ New Consult**.

### Consult Table

The consult list table displays all consults for the patient. Click any row to open the Consult Detail View.

| Column | Description |
|---|---|
| **Date** | Request date in MM/DD/YYYY format |
| **To Service** | The consulting service (e.g., "Cardiology", "Orthopedics", "Neurology") |
| **From** | The requesting service (e.g., "Primary Care") |
| **Type** | Consult type (CONSULT, PROCEDURE, INTERFACILITY, COMMUNITY_CARE) |
| **Urgency** | Urgency level displayed as a colored badge (see Urgency Reference below) |
| **Status** | Current status displayed as a colored badge |
| **Requesting** | Name of the requesting provider |
| **Diagnosis** | Provisional diagnosis; consults with a result note show "[Note]" |

---

## Requesting a Consult

![New Consult form with To Service, Urgency, and Reason for Request fields](screenshots/consults-new-form.png)

Follow these steps to request a new consult:

1. **Enter the Patient ID** and click **Load Consults** to establish the patient context. Then click **+ New Consult**.

2. **Complete the consult request form.** The form fields are:

| Field | Required | Description |
|---|---|---|
| **To Service** | Yes | The service you are requesting a consult from (e.g., "Cardiology", "Orthopedics") |
| **From Service** | No | Your service or department (e.g., "Primary Care", "Internal Medicine") |
| **Urgency** | Yes | Select from: ROUTINE, URGENT, STAT (defaults to ROUTINE) |
| **Requesting Provider** | No | Your name as the requesting provider |
| **Attention** | No | A specific provider within the consulting service, if you have a preference |
| **Provisional Diagnosis** | No | The working diagnosis that prompted the consult request |
| **Reason for Request** | No | A free-text clinical narrative explaining why the consult is needed, what clinical questions you want answered, and any relevant history |

3. **Click Submit Request** to create the consult. The consult is created in **PENDING** status and appears in the consult list.

> **Tip:** A well-written Reason for Request significantly improves the quality and timeliness of the consult response. Include the specific clinical question you want answered, relevant history, pertinent positive and negative findings, and what you have already tried.

---

## Consult Detail View

![Consult detail view showing header with urgency/status badges, metadata, and action buttons](screenshots/consults-detail-view.png)

Clicking a consult in the list opens the Consult Detail View, which provides a comprehensive view of the consult and its current state.

### Header

The header displays:
- **Urgency badge** -- colored by urgency level (blue for ROUTINE, amber for URGENT, red for STAT)
- **Status badge** -- colored by current status
- **Type badge** -- if a consult type has been set (CONSULT, PROCEDURE, INTERFACILITY, COMMUNITY_CARE)
- **To Service** -- the consulting service name in bold

### Metadata

The metadata section shows:
- **From** -- the requesting service
- **Requested** -- the request date and time
- **Requesting** -- the requesting provider's name
- **Attention** -- the specific provider requested (if any)
- **Consulting Provider** -- the provider who accepted/is working the consult (once assigned)
- **Provisional Diagnosis** -- the working diagnosis
- **Reason for Request** -- the clinical narrative
- **Clinical History** -- additional clinical history (if added)
- **Follow-Up Recommendation** -- follow-up recommendations (if added after completion)

### Interfacility Information

If the consult is marked as interfacility, an **INTERFACILITY** badge appears with the external facility name.

### Schedule Information

If the consult has been scheduled, the scheduled date/time and clinic name are displayed.

### Acceptance Information

If the consult has been accepted, the acceptance date/time and accepting provider name are shown.

### Completion Information

If the consult has been completed, the completion date and result note reference are displayed in a green banner.

### Action Buttons

The action buttons available depend on the consult's current status:

| Button | Available When | Description |
|---|---|---|
| **Accept** | PENDING | Accept the consult on behalf of the consulting service |
| **Cancel** | PENDING | Cancel the consult (records a cancellation reason) |
| **Schedule** | ACTIVE | Schedule a date, time, and location for the consult |
| **Complete** | ACTIVE or SCHEDULED | Complete the consult with findings and recommendations |
| **Close** | Always | Close the detail view and return to the consult list |

---

## Key Actions

### Accept

Accepting a consult transitions it from PENDING to ACTIVE status, indicating that the consulting service has received and acknowledged the request.

1. Click **Accept** in the Consult Detail View.
2. The Accept form opens. Enter:
   - **Accepted By ID** -- your provider ID
   - **Accepted By Name** -- your name (required)
3. Click **Accept** to confirm. The consult status changes to ACTIVE.

### Schedule

Scheduling assigns a specific date, time, and location for the consult appointment.

1. Click **Schedule** in the Consult Detail View (available when status is ACTIVE).
2. The Schedule form opens. Enter:
   - **Scheduled Date/Time** -- the appointment date and time (required; defaults to 7 days from today)
   - **Clinic ID** -- the clinic identifier (optional)
   - **Clinic Name** -- the name of the clinic where the consult will occur (optional)
3. Click **Schedule** to confirm. The consult status changes to SCHEDULED and the schedule details are recorded.

### Complete

Completing a consult documents the consulting provider's findings, recommendations, and any follow-up needs.

1. Click **Complete** in the Consult Detail View (available when status is ACTIVE or SCHEDULED).
2. The Complete form opens. Enter:
   - **Author** -- the name of the completing provider
   - **Result Note** -- a free-text narrative documenting the consult findings, assessment, and recommendations
3. Click **Complete Consult** to finalize. The consult status changes to COMPLETE and a TIU document is created as the result note.

### Add Comment (Tracking Comments)

Tracking comments provide a running log of communications and actions related to the consult. They are visible in the Consult Detail View under the "Tracking Comments" section.

1. Scroll to the **Tracking Comments** section in the Consult Detail View.
2. Enter:
   - **Author ID** -- your user ID (optional, defaults to "USER-1")
   - **Author Name** -- your name (required)
   - **Action Taken** -- select from: FORWARDED, UPDATED, ACCEPTED, SCHEDULED, REVIEWED (optional)
   - **Comment** -- the comment text (required)
3. Click **Add Comment** to save. The comment appears in the tracking list with a timestamp.

### Update Type

Set or change the consult type:

1. In the **Consult Type** section of the Consult Detail View, select the type from the dropdown: CONSULT, PROCEDURE, INTERFACILITY, COMMUNITY_CARE.
2. Click **Set Type** to save.

### Set Clinical History

Add or update the clinical history for the consult:

1. In the **Clinical History** section, enter the relevant clinical history text.
2. Click **Set Clinical History** to save.

### Set Follow-Up Recommendation

Document follow-up recommendations (typically done after completing the consult):

1. In the **Follow-Up Recommendation** section, enter the follow-up plan.
2. Click **Set Follow-Up** to save.

### Set Consulting Provider

Assign or reassign the consulting provider:

1. In the **Consulting Provider** section, enter the Provider ID and Provider Name.
2. Click **Set Provider** to save.

### Cancel or Discontinue

Cancelling a PENDING consult or discontinuing an ACTIVE/SCHEDULED consult removes it from the active workflow.

- **Cancel** (available for PENDING consults) -- click the **Cancel** button. A cancellation reason is recorded automatically.
- **Discontinue** -- available through the workflow grain for ACTIVE or SCHEDULED consults, with a documented reason.

> **Note:** Cancelled and discontinued consults remain in the consult history for audit and review purposes. They are not deleted from the system.

---

## Urgency Reference

![Urgency badges showing ROUTINE (blue), URGENT (amber), and STAT (red)](screenshots/consults-urgency-badges.png)

| Urgency | Badge Color | Expected Response Time | Description |
|---|---|---|---|
| **ROUTINE** | Blue | Days to weeks | Standard consult request with no time-critical clinical need |
| **URGENT** | Amber | 24-72 hours | Clinical situation requires prompt attention but is not an emergency |
| **STAT** | Red | Immediate / same day | Emergent clinical need requiring immediate evaluation or intervention |

> **Warning:** STAT consults should be reserved for true emergencies. Overuse of STAT urgency may delay response to genuinely emergent requests. If the clinical situation requires immediate attention, also contact the consulting service directly by phone or page.

---

## Interfacility Consults

For consults that need to be sent to an external facility (e.g., a specialty center or community provider), NewVistas supports interfacility consult tracking.

1. Open the Consult Detail View for the consult.
2. Scroll to the **Interfacility** section.
3. Enter the **External Facility ID** and **External Facility Name**.
4. Click **Mark Interfacility** to flag the consult as interfacility.

Once marked, the consult displays an **INTERFACILITY** badge in the header and shows the external facility name in the metadata section. Interfacility consults follow the same status workflow as internal consults.

---

## Tips for Effective Consult Management

- **Write clear consult requests.** The Reason for Request should include the specific clinical question, relevant history, pertinent findings, and what has already been tried. Vague requests lead to delayed or incomplete responses.
- **Set the correct urgency.** Use ROUTINE for non-urgent consultations, URGENT for situations needing attention within days, and STAT only for true emergencies.
- **Track consults to completion.** Use the consult list and status badges to monitor the progress of your consult requests. Follow up on consults that remain in PENDING status beyond the expected response time.
- **Use tracking comments** to document communications with the consulting service, including phone calls, pages, and coordination details.
- **Review the result note** when a consult is completed. The consulting provider's findings and recommendations should be incorporated into your treatment plan and documented in your progress note.
- **Close the loop** by acknowledging completed consults and acting on recommendations in a timely manner.
