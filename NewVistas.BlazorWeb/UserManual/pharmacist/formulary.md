# Drug Formulary and Drug File

This document covers two related pharmacy modules: the Drug Formulary (National Drug File search and reference) and the Drug File (facility drug file maintenance).

---

## Drug Formulary

**Route:** `/drugformulary`

The Drug Formulary module provides access to the National Drug File (NDF), which is the comprehensive drug reference database used throughout the VA healthcare system and adapted for NewVistas. Use this module to search for drug information, check formulary status, identify generic equivalents, and review drug classifications.

### Search

The Drug Formulary search supports multiple search strategies:

- **Drug name search** -- Enter a brand name (e.g., "Lipitor"), generic name (e.g., "atorvastatin"), or partial name (e.g., "atorva") to find matching entries.
- **NDC code search** -- Enter a National Drug Code (NDC) to find the specific product.
- **Drug class search** -- Enter a VA drug class code (e.g., "CN103") or class description (e.g., "Opioid Analgesics") to find all drugs in that class.

### Search Workflow

Follow these three steps to find drug information.

1. **Enter search criteria** -- Type a drug name (brand or generic), NDC code, or drug class in the search field. Partial names are supported (e.g., "metop" will match "metoprolol"). Click **Search** or press Enter.

2. **Review results** -- The search results table displays matching entries:

   | Column | Description |
   |--------|-------------|
   | Drug Name | Generic name of the drug |
   | Brand Name | Brand/trade name(s) |
   | Formulary Status | Current formulary status (see Formulary Status section below) |
   | Drug Class | VA drug class code and description |
   | Dosage Forms | Available dosage forms (TAB, CAP, INJ, SOLN, SUSP, etc.) |
   | DEA Schedule | DEA schedule if controlled (II, III, IV, V), blank if non-controlled |

3. **Click entry for full details** -- Select any result row to view the complete product detail, including all NDC codes, unit dose packaging information, manufacturer data, and any formulary restrictions or criteria for use.

![Drug formulary search results](screenshots/formulary-search-results.png)

### Product Detail

When you select a drug from the search results, the full product detail is displayed.

| Field | Description |
|-------|-------------|
| Name | Full drug name (generic) |
| Generic Name | INN or USAN generic name |
| Brand Name | Proprietary/trade name(s) |
| NDC | National Drug Code (11-digit format: labeler-product-package) |
| Strength | Drug strength and units (e.g., 10 mg, 500 mg, 20 mg/mL) |
| Dosage Form | Physical form (tablet, capsule, injection, solution, suspension, cream, ointment, patch, etc.) |
| Manufacturer | Drug manufacturer or labeler name |
| DEA Schedule | DEA schedule (II, III, IV, V) or blank for non-controlled |
| Formulary Status | Current formulary status (see below) |
| VA Drug Class | VA classification code and description |

![Drug detail view](screenshots/formulary-drug-detail.png)

### Generics

The formulary links all branded products to their generic entity. When viewing a product detail, you can see all products (brand and generic) that share the same active ingredient and strength. This supports generic substitution decisions and formulary compliance.

### Drug Classes

Drugs are organized into VA drug classification codes. Each code represents a therapeutic category. Examples:

| Code | Description |
|------|-------------|
| CN103 | Opioid Analgesics |
| CN101 | Non-Opioid Analgesics |
| CV100 | Beta Blockers |
| CV200 | Calcium Channel Blockers |
| CV350 | HMG-CoA Reductase Inhibitors (Statins) |
| CV800 | ACE Inhibitors |
| HS502 | Insulins |
| AM650 | Penicillins |
| GU600 | Phosphodiesterase 5 (PDE5) Inhibitors |

### Formulary Status

Each drug product has a formulary status that determines its availability and any restrictions.

| Status | Description |
|--------|-------------|
| **Formulary** | Approved for use. Available for prescribing and dispensing without restrictions. |
| **Non-Formulary** | Not on the approved formulary. May require special authorization or non-formulary request. |
| **Restricted** | On the formulary but with restrictions. Specific criteria must be met (e.g., specialist prescribing only, specific indications, prior therapy failure required). |
| **Criteria for Use** | On the formulary with defined criteria for use. The criteria documentation specifies the clinical conditions and requirements for appropriate use. |

> **Note:** Non-formulary medications typically require prior authorization or a non-formulary request approved by the Pharmacy and Therapeutics (P&T) Committee or a designated clinical pharmacist. See [Pharmacy Benefits](benefits-pos.md) for prior authorization workflows.

---

## Drug File

**Route:** `/drugfile`

The Drug File module provides facility-level drug file maintenance. This is where drugs are configured for use in the facility's ordering and dispensing systems. Changes to the Drug File affect how medications appear in CPOE (Computerized Provider Order Entry), pharmacy dispensing, and BCMA.

> **Warning:** Drug File changes affect the entire facility's ordering and dispensing workflows. Coordinate all changes with the Pharmacy and Therapeutics (P&T) Committee and pharmacy informatics staff before making modifications. Incorrect drug file entries can lead to ordering errors, dispensing errors, and patient safety events.

### Drug Entry Management

Each drug in the Drug File has a status that controls its availability in the system.

| Status | Description |
|--------|-------------|
| **ACTIVE** | The drug is available for ordering and dispensing. It appears in CPOE search results and pharmacy dispensing workflows. |
| **INACTIVE** | The drug is not available for new orders. Existing active orders may continue, but no new orders can be placed. |
| **PENDING** | The drug entry is being set up or modified and is not yet available for ordering. |

### Key Functions

#### Drug File Search

Search the facility drug file by drug name or VA drug class to find existing entries.

1. Enter a drug name (full or partial) or drug class code.
2. Click **Search**.
3. Results display the drug name, status (ACTIVE/INACTIVE/PENDING), drug class, and available dosage forms.

#### Orderable Items

Orderable items link drugs to the CPOE system. Each orderable item defines how a drug appears when a provider places an order, including default values.

| Field | Description |
|-------|-------------|
| Drug Name | The name as it appears in CPOE |
| Default Route | Default route of administration |
| Default Dose | Default dose and units |
| Default Schedule | Default dosing schedule |
| Formulary Status | Linked formulary status |

> **Note:** Orderable item configuration directly affects provider ordering efficiency and accuracy. Well-configured defaults reduce order entry time and minimize errors.

#### Routes

The Drug File maintains a master list of available routes of administration. Common routes include:

| Abbreviation | Full Name |
|-------------|-----------|
| PO | Oral (by mouth) |
| IV | Intravenous |
| IM | Intramuscular |
| SC/SQ | Subcutaneous |
| SL | Sublingual |
| TOP | Topical |
| PR | Rectal |
| INH | Inhalation |
| IVPB | Intravenous Piggyback |
| NG | Nasogastric |
| OPH | Ophthalmic |
| OT | Otic (ear) |
| NAS | Nasal |
| VAG | Vaginal |
| TD | Transdermal |

#### Dose Units

The Drug File maintains a master list of available dose units.

| Abbreviation | Full Name |
|-------------|-----------|
| mg | Milligrams |
| mL | Milliliters |
| mcg | Micrograms |
| units | Units (e.g., insulin units, heparin units) |
| mEq | Milliequivalents |
| TAB | Tablet(s) |
| CAP | Capsule(s) |
| PUFF | Puff(s) (for inhalers) |
| DROP | Drop(s) |
| PATCH | Patch(es) |
| SUPP | Suppository(ies) |

#### Drug Classes

VA drug classification codes categorize medications by therapeutic use. The Drug File maintains the complete VA drug class hierarchy.

- Drug classes are used for formulary management, utilization review, and clinical screening.
- Each drug entry must be assigned to at least one VA drug class.
- Drug class codes follow the format: two-letter category + three-digit number (e.g., CN103, CV350, AM650).

#### Synonyms

Drug synonyms allow alternate names, abbreviations, and common misspellings to be linked to the official drug file entry. This improves search results and order entry efficiency.

Examples:

| Official Name | Synonym |
|--------------|---------|
| Acetaminophen | Tylenol, APAP |
| Atorvastatin | Lipitor |
| Metoprolol Succinate | Toprol XL |
| Furosemide | Lasix |
| Lisinopril | Prinivil, Zestril |

> **Tip:** When providers report difficulty finding a medication in CPOE, check the Drug File to ensure appropriate synonyms are configured. Adding common brand names and abbreviations as synonyms improves ordering efficiency.

![Drug File entry form](screenshots/drugfile-entry-form.png)

---

## Common Tasks

### Adding a New Drug to the Formulary

1. Obtain P&T Committee approval for the new formulary addition.
2. Search the National Drug File (`/drugformulary`) to find the NDF entry for the drug.
3. Create a new Drug File entry (`/drugfile`) with PENDING status:
   - Enter the drug name, strength, dosage form, and route.
   - Assign the VA drug class.
   - Configure the formulary status (Formulary, Restricted, or Criteria for Use).
   - Set up orderable items with appropriate defaults for CPOE.
   - Add synonyms for common brand names and abbreviations.
4. Test the entry by searching for it in a test CPOE session.
5. Change the status to ACTIVE when ready for facility-wide use.

### Inactivating a Drug

1. Confirm with the P&T Committee or pharmacy management that the drug should be inactivated.
2. Search the Drug File for the entry.
3. Change the status from ACTIVE to INACTIVE.
4. Review active orders for the drug and coordinate with prescribers for therapeutic alternatives.
5. Document the reason for inactivation.

> **Note:** Inactivating a drug does not automatically discontinue existing orders. Existing active orders continue until they expire or are manually discontinued. Coordinate with prescribers to transition patients to alternative medications.
