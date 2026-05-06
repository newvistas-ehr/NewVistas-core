# NewVistas Reporting Prototype

## Architecture: Isolated Analytical Workload

NewVistas separates clinical write workloads from analytical read workloads. The clinical path (Orleans grains backed by `OrleansStorage`) handles patient care. A parallel star-schema reporting database is fed incrementally from grain state via change-data-capture (CDC) views and materialization procedures.

```
Clinical Operations              Reporting / Analytics
┌──────────────────────┐         ┌──────────────────────┐
│  Orleans Grains       │         │  Star Schema (rpt.*)  │
│  (write path)         │         │  (read-only)          │
│                       │         │                       │
│  OrleansStorage table │──CDC──► │  DimPatient           │
│  (JSON grain state)   │  1-5    │  FactLabResult        │
│                       │  min    │  FactOrder ...        │
└──────────────────────┘         └──────────────────────┘
         │                                │
    Blazor / API                   Power BI / SSRS
    (patient care)                 (population analytics)
```

**Design goal:** keep analytical queries from contending with the clinical write path while still giving analysts near real-time (1–5 minute) data. The CDC pipeline is incremental and watermark-driven, not a nightly full-table ETL.

---

## Scripts

### 001-StarSchema.sql — The Data Warehouse

Creates the `rpt` schema with 7 dimension tables and 9 fact tables.

**Dimensions:**

| Table | Notes |
|-------|-------|
| DimDate | Pre-populated 2020–2035, VA fiscal year (Oct start) |
| DimPatient | SSN masked to last-4 only, SCD tracking |
| DimProvider | Physicians, nurses, pharmacists |
| DimLocation | Clinics, wards, station number |
| DimDiagnosis | ICD-10-CM codes |
| DimDrug | National drug file with drug class, DEA schedule |
| DimLabTest | LOINC-coded test catalog |

**Facts:**

| Table | Key Measures |
|-------|-------------|
| FactLabResult | ResultNumeric, AbnormalFlag, IsCritical |
| FactOrder | DaysToSign, DaysActive, Status |
| FactPrescription | DaysSupply, RefillsRemaining |
| FactEncounter | WaitTimeMinutes, StopCode, DurationMinutes |
| FactVital | BP split systolic/diastolic, qualifiers |
| FactNote | TextLength, HoursToSign, AddendumCount |
| FactConsult | DaysToComplete, DaysToSchedule |
| FactAdtMovement | LengthOfStayDays, Disposition |
| FactMedAdmin | VarianceMinutes (scheduled vs actual) |
| FactAuditEvent | Domain, Action, OldValue/NewValue |

`FactAuditEvent` projects the audit trail captured in grain state into the analytical schema, making compliance reporting first-class alongside clinical reporting.

---

### 002-CDCViews.sql — The Bridge

9 SQL views that parse Orleans grain state JSON via `OPENJSON` into flat relational rows. Each view maps one grain type to a relational projection:

| View | Source Grain | VistA File |
|------|-------------|-----------|
| vw_CDC_Patient | PatientGrain | DPT #2 |
| vw_CDC_LabTest | LabTestGrain | LR #63 |
| vw_CDC_Order | OrderGrain | OR #100 |
| vw_CDC_Prescription | PharmacyGrain | PS #52 |
| vw_CDC_TiuDocument | TiuDocumentGrain | TIU #8925 |
| vw_CDC_Consult | ConsultGrain | GMRC #123 |
| vw_CDC_AuditEvent | AuditEventGrain | AUDIT #1.1 |
| vw_CDC_AdtMovement | AdtGrain | MAS #405 |
| vw_CDC_Bcma | BcmaGrain | PSB #53.79 |

These views read from `OrleansStorage` directly. The materialization procedures consume them.

**Database portability:**
- SQL Server: `OPENJSON` / `JSON_VALUE` (used in scripts)
- CockroachDB: `jsonb` operators (`->>`, `#>>`)
- PostgreSQL: `json_populate_record` or `->>`

---

### 003-CDCMaterialize.sql — The Pipeline

Incremental materialization using `MERGE` upserts with watermark tracking.

**Components:**

- `rpt.CDCWatermark` — tracks last-processed timestamp per entity type
- `rpt.sp_CDC_Materialize_DimPatient` — SCD Type 1 upsert for demographics
- `rpt.sp_CDC_Materialize_FactLabResult` — MERGE with numeric parsing and abnormal flag detection
- `rpt.sp_CDC_Materialize_FactOrder` — MERGE with computed DaysToSign/DaysActive
- `rpt.sp_CDC_Materialize_FactAuditEvent` — append-only INSERT with idempotency check
- `rpt.sp_CDC_MaterializeAll` — master runner that executes all procedures in order

**Scheduling:** Run `rpt.sp_CDC_MaterializeAll` via SQL Agent every 1–5 minutes. Each procedure only processes grains modified since its last watermark — no full table scans.

**Monitoring:** The master runner returns the CDCWatermark table at the end of each run, showing row counts and duration per entity.

---

### 004-SampleQueries-PowerBI.sql — What Analysts Actually Run

Representative analytical queries organized by category:

**1. Population Health / Quality Measures**
- HbA1c control for diabetic patients (SAIL DM-2 measure)
- Critical lab values in the last 7 days

**2. Operational Metrics**
- Average wait time by clinic with % seen within 30 minutes
- Unsigned orders aging report (orders > 24 hours without signature)
- Consult completion timeliness by service (% completed within 90 days)

**3. Pharmacy & Controlled Substance Monitoring**
- Top 20 prescribed medications by volume
- Controlled substance prescribing patterns (DEA Schedule II-V) with red flag thresholds
- BCMA medication administration timing variance (doses > 60 min from scheduled)

**4. Lab Results Trending**
- Single patient lab trend (parameterized for Power BI line chart)
- Facility-wide abnormal rate by test type per month (heat map)

**5. Provider Productivity**
- Notes authored per provider per month with avg hours to sign
- Encounters per provider per stop code (workload/RVU model)

**6. Audit & Compliance**
- Audit event volume by domain (donut chart + trend)
- Suspicious access patterns: users viewing > 50 unique patients in 30 days
- Full HIPAA Right of Access log for a specific patient

**7. Executive Dashboard**
- Single query returning all KPI tiles: patients seen, unsigned orders, critical labs, pending consults, avg wait time, unsigned notes, inpatient census

---

## Design Choices

| Aspect | Choice |
|--------|--------|
| Latency | 1–5 minute incremental CDC (watermark-driven) |
| Audit data | First-class fact table with old/new values |
| ETL | SQL MERGE against JSON views on grain state |
| Schema | Star schema — 7 dimensions, 9 facts |
| Source of truth | Orleans grain state in `OrleansStorage` |
| Database portability | SQL Server, CockroachDB, or PostgreSQL |

---

## Deployment Options

**Option A: Same database, different schema** (simplest)
- Star schema lives in `rpt.*` on the same SQL Server
- CDC procedures read from `OrleansStorage` in the same database
- Use Resource Governor to throttle reporting queries

**Option B: Read replica** (recommended for production)
- SQL Server Always On readable secondary
- CockroachDB follower reads
- CDC procedures run on the replica, not the primary

**Option C: Dedicated analytics database** (enterprise)
- Separate database/server for reporting
- CDC pushes via linked server, Service Broker, or external pipeline (Kafka, EventHub)
- Maximum isolation — clinical ops completely untouched
