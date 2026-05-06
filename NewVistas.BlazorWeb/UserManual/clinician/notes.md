# Clinical Documentation (Notes)

**Route:** `/notes`

The Notes page is the primary interface for clinical documentation in NewVistas. It implements the TIU (Text Integration Utilities) document framework, mapping to VistA File #8925. From this page, you can view recent notes, create new progress notes and other document types, sign notes with your electronic signature, add addenda to completed notes, and search note history.

This page also covers related documentation modules: Encounter Forms (`/encounter-forms`), Patient Care Encounter (`/pce`), and Health Summary (`/health-summary`).

![Recent Notes list with status badges and document types](screenshots/notes-recent-list.png)

---

## Loading Notes

1. Navigate to `/notes`.
2. Enter the **Patient ID** in the lookup bar.
3. Click **Load Notes** (or press **Enter**).

The system retrieves the patient's most recent notes (up to 100 by default) and displays them in a table. Three buttons are available in the lookup bar:

- **Load Notes** -- loads or refreshes the note list for the current patient.
- **+ New Note** -- opens the new note creation form (see Writing a New Note below).
- **Note History** -- toggles the history search panel for date-range searching.

---

## Recent Notes Table

The notes list displays all loaded notes in a table format. Click any row to view the full note detail.

| Column | Description |
|---|---|
| **Date** | Reference date of the note in MM/DD/YYYY HH:MM format |
| **Type** | Document type (e.g., PROGRESS NOTE, DISCHARGE SUMMARY, CONSULT NOTE) |
| **Subject** | Brief subject line; notes with addenda show a **[+]** indicator after the subject |
| **Author** | Name of the note author |
| **Status** | Current note status displayed as a colored badge (see Note Statuses below) |
| **Location** | Clinic or ward where the note was written |

---

## Note Statuses

Notes in NewVistas follow a defined lifecycle with the following statuses:

| Status | Badge Color | Description |
|---|---|---|
| **UNSIGNED** | Amber | Note has been created but not yet signed by the author. Appears in Action Items. |
| **UNCOSIGNED** | Orange | Note has been signed by the author but requires a cosignature (e.g., attending cosigning a resident's note). |
| **COMPLETED** | Green | Note has been signed (and cosigned if required). This is the final status for most notes. |
| **AMENDED** | Blue | A completed note that has been amended. The original text is preserved and the amendment is appended. |
| **RETRACTED** | Red | A note that has been administratively retracted. Retracted notes remain in the record but are marked as retracted. |

### Status Transitions

```
UNSIGNED ──> COMPLETED (via Sign)
UNSIGNED ──> UNCOSIGNED (via Sign when cosignature required)
UNCOSIGNED ──> COMPLETED (via Cosign)
COMPLETED ──> AMENDED (via Add Addendum)
Any ──> RETRACTED (administrative action only)
```

> **Note:** Only notes in UNSIGNED status can be edited. Once a note is signed (COMPLETED), the original text is locked. To add information to a completed note, you must add an addendum.

---

## Writing a New Note

![New Note form with document type selection and text area](screenshots/notes-new-note-form.png)

Follow these steps to create a new clinical note:

1. **Enter the Patient ID** and click **Load Notes** to establish the patient context. Then click **+ New Note** to open the note creation form.

2. **Select the Document Type** from the dropdown menu. Available types include:
   - **Progress Note** -- standard clinical encounter note (most common)
   - **Discharge Summary** -- summary written at the time of discharge
   - **Consult Note** -- documentation of a consult response
   - **Surgical Note** -- operative or procedural note
   - **Crisis Note** -- documentation of a crisis intervention (sets the "C" CWAD flag)
   - **Advance Directive** -- documentation of patient's advance directive (sets the "D" CWAD flag)

3. **Enter the note metadata.** Fill in the following fields:
   - **Subject** -- a brief title for the note (e.g., "Follow-up for Diabetes Management")
   - **Author** -- the name of the note author (your name)
   - **Location** -- the clinic or ward where the encounter occurred

4. **Compose the note body.** Enter the full text of your clinical note in the large text area. The text area supports free-text entry with monospace font for consistent formatting.

5. **Save or Sign the note.** You have two options:
   - Click **Save Note** to save the note in UNSIGNED status. You can return later to sign it.
   - After saving, the note appears in the notes list. Click the note to view it, then click **Sign** to open the Electronic Signature modal and sign it immediately.

> **Tip:** Save your note frequently during long documentation sessions. An unsaved note will be lost if you navigate away from the page.

---

## Electronic Signature

![Electronic signature modal with Signer ID and Signature Code fields](screenshots/notes-electronic-signature.png)

The Electronic Signature modal appears when you click **Sign** on an unsigned note or **Cosign** on an uncosigned note.

The modal contains:

| Field | Description |
|---|---|
| **Signer ID** | Your user ID in the system |
| **Electronic Signature Code** | Your personal electronic signature code (entered in a password field for security) |

To sign a note:

1. Enter your **Signer ID**.
2. Enter your **Electronic Signature Code**.
3. Click the **sign** (or **cosign**) button.

The system verifies your credentials and, upon success:
- Changes the note status from UNSIGNED to COMPLETED (or from UNCOSIGNED to COMPLETED for cosignatures).
- Creates an audit trail record documenting who signed, when, and from what workstation.
- Removes the note from your unsigned notes action items.

> **Warning:** Your electronic signature is legally binding and equivalent to a handwritten signature for clinical documentation purposes. Only sign notes that you have personally reviewed and for which you accept clinical responsibility. Never share your electronic signature code with another user.

If the signature verification fails, an error message appears within the modal. Re-enter your credentials and try again.

---

## Note Detail View

![Note detail view showing header, metadata, body text, and addendum indicator](screenshots/notes-detail-view.png)

Clicking a note in the notes list opens the Note Detail View, which displays:

### Header
- **Document Type** badge (blue, e.g., "PROGRESS NOTE")
- **Status** badge (colored by status)
- **Subject** in bold text

### Metadata
- **Author** -- name of the note author
- **Date** -- reference date in MM/DD/YYYY HH:MM format
- **Location** -- clinic or ward

### Action Buttons
- **Sign** -- appears only for notes in UNSIGNED status
- **Cosign** -- appears only for notes in UNCOSIGNED status
- **Close** -- closes the detail view and returns to the notes list

### Note Body
The full text of the note displayed in a readable, pre-formatted layout with word wrapping.

### Addenda Indicator
If the note has addenda, an amber banner appears at the bottom: "Addenda (N)" where N is the number of addenda attached to this note.

---

## Addenda

Addenda allow you to append additional information to a completed note without modifying the original signed text. This is the proper way to add information, corrections, or updates to a note after it has been signed.

Key points about addenda:

- Addenda are appended to the parent note and tracked via the `AddendumIds` list on the parent note state.
- The parent note displays `[+]` in the Subject column of the notes list and shows an addenda count banner in the detail view.
- Each addendum is itself a TIU document with its own `IsAddendum` flag set to true.
- Addenda do not modify the original note text -- they are separate, linked documents.
- The `HasAddenda` flag on the parent note is set to true when the first addendum is attached.

> **Note:** To add an addendum, open the completed note, then use the addendum workflow. The addendum will be linked to the parent note and visible in the note detail view.

---

## Note History

The Note History panel provides date-range searching for the patient's complete note archive.

1. Click **Note History** in the lookup bar to toggle the history search panel.
2. Set the search filters:

| Filter | Default | Description |
|---|---|---|
| **From** | 90 days ago | Start date/time of the search range |
| **To** | Today | End date/time of the search range |
| **Max Results** | 100 | Maximum number of notes to return (1-500) |

3. Click **Load History** to execute the search.

Results appear in a table identical to the Recent Notes table. Click any row to view the full note.

---

## Encounter Forms

**Route:** `/encounter-forms`

The Encounter Forms page provides structured templates for clinical documentation. It is organized into three tabs:

### Templates Tab
Browse and select from available encounter form templates. Templates provide pre-structured formats for common encounter types, reducing documentation time and ensuring completeness.

### Patient Forms Tab
View encounter forms that have been completed or are in progress for the current patient.

### Dashboard Tab
An overview of encounter form activity, including completion rates and pending forms.

---

## Patient Care Encounter (PCE)

**Route:** `/pce`

The Patient Care Encounter page documents the clinical details of a patient visit. It is organized into three tabs:

### Encounter List Tab
Displays all documented encounters for the patient with date, type, and status.

### Encounter Detail Tab
Shows the full details of a selected encounter, including:
- Visit type and date
- Procedures performed (with CPT codes)
- Diagnoses documented (with ICD-10 codes)
- Health factors assessed
- Provider information

### New Encounter Tab
A form for creating a new encounter record, documenting:
- Visit type (office visit, telephone, telehealth, etc.)
- Procedures performed
- Diagnoses addressed
- Health factors identified
- Service connection relevance

> **Tip:** PCE data is used for workload reporting, billing, and clinical quality measurement. Ensure that all procedures and diagnoses are accurately coded.

---

## Health Summary

**Route:** `/health-summary`

The Health Summary page provides a configurable, comprehensive summary of a patient's clinical record. It aggregates data from multiple clinical domains into a single printable report. Health summaries can be configured to include specific sections based on clinical need.

---

## Tips for Effective Documentation

- **Sign your notes promptly.** Unsigned notes appear in your Action Items and may delay care coordination. Aim to sign all notes before the end of each clinical session.
- **Use the Subject field meaningfully.** A descriptive subject line (e.g., "Annual Physical - Diabetes Management") makes it easier to find notes later.
- **Choose the correct Document Type.** The document type affects how the note is categorized, indexed, and displayed. Crisis Notes and Advance Directives automatically set CWAD flags on the patient record.
- **Add addenda rather than creating new notes** when you need to supplement an existing signed note.
- **Use Note History** with date range filters to review a patient's documentation history before an encounter.
