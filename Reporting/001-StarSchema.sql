-- ============================================================================
-- NewVistas Reporting Star Schema
-- Equivalent to VA Corporate Data Warehouse (CDW) — isolated from clinical ops
--
-- Compatible with: SQL Server 2019+, CockroachDB 23.1+, PostgreSQL 15+
-- Intended consumer: Power BI, SSRS, Tableau, ad-hoc SQL analytics
--
-- VistA CDW mapping:
--   DimPatient        → CDW.SPatient.SPatient
--   DimProvider        → CDW.SStaff.SStaff
--   DimLocation        → CDW.Dim.Location
--   DimDate            → CDW.Dim.DimDate  (standard date dimension)
--   FactLabResult      → CDW.Chem.PatientLabChem
--   FactOrder          → CDW.CPRSOrder.CPRSOrder
--   FactPrescription   → CDW.RxOut.RxOutpat / CDW.RxIn.RxIn
--   FactEncounter      → CDW.Outpat.Visit / CDW.Inpat.Inpatient
--   FactVital          → CDW.Vital.VitalSign
--   FactNote           → CDW.TIU.TIUDocument
--   FactConsult        → CDW.Con.Consult
--   FactAdtMovement    → CDW.Inpat.PatientMovement
--   FactAuditEvent     → CDW.Audit.AuditTrail (new — VistA CDW doesn't have this)
-- ============================================================================

-- Schema isolation: all reporting objects live in their own schema
-- so they never collide with Orleans operational tables
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rpt')
    EXEC('CREATE SCHEMA rpt');
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- DIMENSION TABLES
-- ─────────────────────────────────────────────────────────────────────────────

-- DimDate: standard date dimension, pre-populated from 2020-01-01 to 2035-12-31
CREATE TABLE rpt.DimDate (
    DateKey             INT             NOT NULL PRIMARY KEY,  -- YYYYMMDD
    FullDate            DATE            NOT NULL,
    [Year]              INT             NOT NULL,
    [Quarter]           INT             NOT NULL,
    [Month]             INT             NOT NULL,
    [MonthName]         VARCHAR(20)     NOT NULL,
    [Week]              INT             NOT NULL,
    [DayOfWeek]         INT             NOT NULL,
    [DayName]           VARCHAR(20)     NOT NULL,
    [DayOfMonth]        INT             NOT NULL,
    [DayOfYear]         INT             NOT NULL,
    IsWeekend           BIT             NOT NULL,
    FiscalYear          INT             NOT NULL,  -- VA fiscal year (Oct start)
    FiscalQuarter       INT             NOT NULL
);

-- DimPatient: slowly changing dimension (SCD Type 2)
-- Mirrors CDW SPatient with VistA DFN/ICN identifiers
CREATE TABLE rpt.DimPatient (
    PatientSK           BIGINT          IDENTITY(1,1) PRIMARY KEY,
    PatientId           VARCHAR(200)    NOT NULL,  -- Orleans grain key
    PatientName         NVARCHAR(200)   NULL,
    Sex                 CHAR(1)         NULL,
    DateOfBirth         DATE            NULL,
    SSNLast4            CHAR(4)         NULL,      -- last 4 only for privacy
    IsVeteran           BIT             NULL,
    ServiceBranch       VARCHAR(50)     NULL,
    ServiceEra          VARCHAR(50)     NULL,
    IsServiceConnected  BIT             NULL,
    SCPercent           INT             NULL,
    -- SCD tracking
    EffectiveFrom       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    EffectiveTo         DATETIME2       NULL,
    IsCurrent           BIT             NOT NULL DEFAULT 1,
    -- Metadata
    SourceGrainKey      VARCHAR(200)    NOT NULL,
    LastCDCTimestamp     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_DimPatient_PatientId ON rpt.DimPatient (PatientId) WHERE IsCurrent = 1;

-- DimProvider: clinicians, nurses, pharmacists
-- Mirrors CDW SStaff with VistA NEW PERSON file (#200) attributes
CREATE TABLE rpt.DimProvider (
    ProviderSK          BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ProviderId          VARCHAR(200)    NOT NULL,
    ProviderName        NVARCHAR(200)   NULL,
    ProviderType        VARCHAR(50)     NULL,      -- Physician, Nurse, Pharmacist, etc.
    Specialty           VARCHAR(100)    NULL,
    Division            VARCHAR(100)    NULL,
    IsCurrent           BIT             NOT NULL DEFAULT 1,
    LastCDCTimestamp     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_DimProvider_ProviderId ON rpt.DimProvider (ProviderId) WHERE IsCurrent = 1;

-- DimLocation: clinics, wards, facilities
-- Mirrors CDW Dim.Location with VistA HOSPITAL LOCATION (#44)
CREATE TABLE rpt.DimLocation (
    LocationSK          BIGINT          IDENTITY(1,1) PRIMARY KEY,
    LocationId          VARCHAR(200)    NOT NULL,
    LocationName        NVARCHAR(200)   NULL,
    LocationType        VARCHAR(50)     NULL,      -- Clinic, Ward, OR, ER, etc.
    Division            VARCHAR(100)    NULL,
    StationNumber       VARCHAR(10)     NULL,      -- VA station number (e.g., 688)
    IsCurrent           BIT             NOT NULL DEFAULT 1,
    LastCDCTimestamp     DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_DimLocation_LocationId ON rpt.DimLocation (LocationId) WHERE IsCurrent = 1;

-- DimDiagnosis: ICD-10-CM codes
-- Mirrors CDW Dim.ICD10 with VistA ICD DIAGNOSIS file (#80)
CREATE TABLE rpt.DimDiagnosis (
    DiagnosisSK         BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ICD10Code           VARCHAR(10)     NOT NULL,
    Description         NVARCHAR(500)   NULL,
    Category            VARCHAR(200)    NULL,      -- Chapter / block grouping
    IsCurrent           BIT             NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IX_DimDiagnosis_Code ON rpt.DimDiagnosis (ICD10Code) WHERE IsCurrent = 1;

-- DimDrug: medications from National Drug File
-- Mirrors CDW Dim.NationalDrug with VistA NDF (#50.6/#50.68)
CREATE TABLE rpt.DimDrug (
    DrugSK              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    DrugId              VARCHAR(200)    NOT NULL,   -- VA Product IEN or grain key
    DrugName            NVARCHAR(300)   NULL,
    GenericName         NVARCHAR(300)   NULL,
    DrugClass           VARCHAR(100)    NULL,       -- VA Drug Class (e.g., AM110)
    DEASchedule         VARCHAR(10)     NULL,       -- II, III, IV, V, or NULL
    Route               VARCHAR(50)     NULL,
    DoseForm            VARCHAR(100)    NULL,
    IsCurrent           BIT             NOT NULL DEFAULT 1
);

-- DimLabTest: lab test catalog
-- Mirrors CDW Dim.LabChemTest
CREATE TABLE rpt.DimLabTest (
    LabTestSK           BIGINT          IDENTITY(1,1) PRIMARY KEY,
    TestId              VARCHAR(200)    NOT NULL,
    TestName            NVARCHAR(200)   NULL,
    LoincCode           VARCHAR(20)     NULL,
    Category            VARCHAR(50)     NULL,       -- Chemistry, Hematology, Micro, etc.
    Units               VARCHAR(50)     NULL,
    ReferenceLow        VARCHAR(50)     NULL,
    ReferenceHigh       VARCHAR(50)     NULL,
    IsCurrent           BIT             NOT NULL DEFAULT 1
);

-- ─────────────────────────────────────────────────────────────────────────────
-- FACT TABLES
-- ─────────────────────────────────────────────────────────────────────────────

-- FactLabResult: one row per lab result
-- Mirrors CDW Chem.PatientLabChem — the most queried table in CDW
CREATE TABLE rpt.FactLabResult (
    LabResultSK         BIGINT          IDENTITY(1,1) PRIMARY KEY,
    LabTestGrainKey     VARCHAR(200)    NOT NULL,   -- Orleans grain key for traceability
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    LabTestSK           BIGINT          NULL        REFERENCES rpt.DimLabTest(LabTestSK),
    OrderingProviderSK  BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    CollectionDateKey   INT             NULL        REFERENCES rpt.DimDate(DateKey),
    ResultDateKey       INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Measures
    ResultValue         VARCHAR(200)    NULL,
    ResultNumeric       DECIMAL(18,4)   NULL,       -- parsed numeric for aggregation
    ResultUnit          VARCHAR(50)     NULL,
    ReferenceLow        VARCHAR(50)     NULL,
    ReferenceHigh       VARCHAR(50)     NULL,
    AbnormalFlag        VARCHAR(20)     NULL,       -- H, L, HH, LL, A, etc.
    IsAbnormal          BIT             NOT NULL DEFAULT 0,
    IsCritical          BIT             NOT NULL DEFAULT 0,
    -- Status
    [Status]            VARCHAR(30)     NULL,       -- Ordered, Collected, Resulted, Verified
    SpecimenType        VARCHAR(100)    NULL,
    PerformingLab       VARCHAR(100)    NULL,
    -- Timestamps (full precision for drill-down)
    CollectionDateTime  DATETIME2       NULL,
    ResultDateTime      DATETIME2       NULL,
    VerifiedDateTime    DATETIME2       NULL,
    -- CDC metadata
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactLab_Patient ON rpt.FactLabResult (PatientSK, ResultDateKey);
CREATE INDEX IX_FactLab_Test    ON rpt.FactLabResult (LabTestSK, ResultDateKey);

-- FactOrder: all CPRS orders
-- Mirrors CDW CPRSOrder.CPRSOrder
CREATE TABLE rpt.FactOrder (
    OrderSK             BIGINT          IDENTITY(1,1) PRIMARY KEY,
    OrderGrainKey       VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    OrderingProviderSK  BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    OrderDateKey        INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    OrderType           VARCHAR(50)     NULL,       -- Lab, Pharmacy, Radiology, Consult, Diet, etc.
    OrderText           NVARCHAR(1000)  NULL,
    Urgency             VARCHAR(30)     NULL,
    [Status]            VARCHAR(30)     NULL,       -- Pending, Active, Completed, DC, Expired
    -- Timestamps
    OrderDateTime       DATETIME2       NULL,
    StartDateTime       DATETIME2       NULL,
    StopDateTime        DATETIME2       NULL,
    SignedDateTime      DATETIME2       NULL,
    -- Measures
    DaysToSign          INT             NULL,       -- OrderDateTime → SignedDateTime
    DaysActive          INT             NULL,       -- StartDateTime → StopDateTime
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactOrder_Patient ON rpt.FactOrder (PatientSK, OrderDateKey);
CREATE INDEX IX_FactOrder_Status  ON rpt.FactOrder ([Status], OrderDateKey);

-- FactPrescription: outpatient + inpatient pharmacy
-- Mirrors CDW RxOut.RxOutpat + RxIn.RxIn
CREATE TABLE rpt.FactPrescription (
    PrescriptionSK      BIGINT          IDENTITY(1,1) PRIMARY KEY,
    PrescriptionGrainKey VARCHAR(200)   NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    PrescriberSK        BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    DrugSK              BIGINT          NULL        REFERENCES rpt.DimDrug(DrugSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    IssueDateKey        INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    PrescriptionType    VARCHAR(20)     NULL,       -- Outpatient, Inpatient, IV
    Dosage              VARCHAR(100)    NULL,
    [Route]             VARCHAR(50)     NULL,
    Schedule            VARCHAR(100)    NULL,
    DaysSupply          INT             NULL,
    Quantity            DECIMAL(10,2)   NULL,
    Refills             INT             NULL,
    RefillsRemaining    INT             NULL,
    [Status]            VARCHAR(30)     NULL,       -- Active, Discontinued, Expired, Hold
    -- Timestamps
    IssueDateTime       DATETIME2       NULL,
    LastFillDateTime    DATETIME2       NULL,
    ExpirationDateTime  DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactRx_Patient ON rpt.FactPrescription (PatientSK, IssueDateKey);
CREATE INDEX IX_FactRx_Drug    ON rpt.FactPrescription (DrugSK, IssueDateKey);

-- FactEncounter: outpatient visits + inpatient admissions
-- Mirrors CDW Outpat.Visit + Inpat.Inpatient
CREATE TABLE rpt.FactEncounter (
    EncounterSK         BIGINT          IDENTITY(1,1) PRIMARY KEY,
    EncounterGrainKey   VARCHAR(200)    NOT NULL,   -- Visit or Appointment grain key
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    PrimaryProviderSK   BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    EncounterDateKey    INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    EncounterType       VARCHAR(30)     NULL,       -- Outpatient, Inpatient, Telehealth, ER
    ServiceCategory     VARCHAR(50)     NULL,       -- Primary Care, Mental Health, Surgery, etc.
    StopCode            VARCHAR(10)     NULL,       -- VA stop code (e.g., 323 = Primary Care)
    [Status]            VARCHAR(30)     NULL,       -- Scheduled, CheckedIn, CheckedOut, Cancelled
    PrimaryDiagnosisCode VARCHAR(10)    NULL,
    -- Measures
    DurationMinutes     INT             NULL,
    ProcedureCount      INT             NULL,
    DiagnosisCount      INT             NULL,
    WaitTimeMinutes     INT             NULL,       -- ScheduledTime → CheckInTime
    -- Timestamps
    EncounterDateTime   DATETIME2       NULL,
    CheckInDateTime     DATETIME2       NULL,
    CheckOutDateTime    DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactEncounter_Patient  ON rpt.FactEncounter (PatientSK, EncounterDateKey);
CREATE INDEX IX_FactEncounter_Location ON rpt.FactEncounter (LocationSK, EncounterDateKey);

-- FactVital: vital sign measurements
-- Mirrors CDW Vital.VitalSign
CREATE TABLE rpt.FactVital (
    VitalSK             BIGINT          IDENTITY(1,1) PRIMARY KEY,
    VitalGrainKey       VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    EnteredBySK         BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    VitalDateKey        INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    VitalType           VARCHAR(30)     NOT NULL,   -- Temperature, Pulse, Respiration, BP, SpO2, etc.
    -- Measures
    ResultValue         VARCHAR(50)     NULL,       -- Original string (e.g., "120/80")
    ResultNumeric       DECIMAL(10,2)   NULL,       -- Parsed numeric (systolic for BP)
    ResultNumeric2      DECIMAL(10,2)   NULL,       -- Secondary (diastolic for BP)
    Unit                VARCHAR(20)     NULL,
    -- Qualifiers
    Qualifier           VARCHAR(200)    NULL,       -- Lying, Sitting, Standing, etc.
    -- Timestamps
    VitalDateTime       DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactVital_Patient ON rpt.FactVital (PatientSK, VitalDateKey);

-- FactNote: TIU documents (progress notes, discharge summaries, consult notes)
-- Mirrors CDW TIU.TIUDocument
CREATE TABLE rpt.FactNote (
    NoteSK              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    NoteGrainKey        VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    AuthorSK            BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    ReferenceDateKey    INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    DocumentType        VARCHAR(50)     NULL,       -- Progress Note, Discharge Summary, etc.
    [Subject]           NVARCHAR(500)   NULL,
    [Status]            VARCHAR(30)     NULL,       -- Unsigned, Uncosigned, Completed, Retracted
    HasAddenda          BIT             NOT NULL DEFAULT 0,
    AddendumCount       INT             NOT NULL DEFAULT 0,
    -- Measures
    TextLength          INT             NULL,       -- Character count of note body
    HoursToSign         DECIMAL(10,2)   NULL,       -- EntryDate → SignedDateTime
    -- Timestamps
    ReferenceDateTime   DATETIME2       NULL,
    EntryDateTime       DATETIME2       NULL,
    SignedDateTime      DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactNote_Patient ON rpt.FactNote (PatientSK, ReferenceDateKey);
CREATE INDEX IX_FactNote_Author  ON rpt.FactNote (AuthorSK, ReferenceDateKey);

-- FactConsult: consult requests
-- Mirrors CDW Con.Consult
CREATE TABLE rpt.FactConsult (
    ConsultSK           BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ConsultGrainKey     VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    RequestingProviderSK BIGINT         NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    RequestDateKey      INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    ToService           VARCHAR(200)    NULL,
    FromService         VARCHAR(200)    NULL,
    Urgency             VARCHAR(30)     NULL,
    [Status]            VARCHAR(30)     NULL,       -- Pending, Active, Scheduled, Complete, Cancelled
    -- Measures
    DaysToComplete      INT             NULL,       -- RequestDate → CompletionDate
    DaysToSchedule      INT             NULL,       -- RequestDate → ScheduledDate
    -- Timestamps
    RequestDateTime     DATETIME2       NULL,
    ScheduledDateTime   DATETIME2       NULL,
    CompletedDateTime   DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactConsult_Patient ON rpt.FactConsult (PatientSK, RequestDateKey);

-- FactAdtMovement: admission, discharge, transfer events
-- Mirrors CDW Inpat.PatientMovement
CREATE TABLE rpt.FactAdtMovement (
    MovementSK          BIGINT          IDENTITY(1,1) PRIMARY KEY,
    MovementGrainKey    VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    AttendingProviderSK BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    MovementDateKey     INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    MovementType        VARCHAR(30)     NULL,       -- Admission, Transfer, Discharge
    WardName            NVARCHAR(200)   NULL,
    RoomBed             VARCHAR(50)     NULL,
    TreatingSpecialty   VARCHAR(100)    NULL,
    Disposition         VARCHAR(100)    NULL,       -- Discharge only
    -- Measures
    LengthOfStayDays    INT             NULL,       -- Admission → Discharge
    -- Timestamps
    MovementDateTime    DATETIME2       NULL,
    DischargeDateTime   DATETIME2       NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactAdt_Patient ON rpt.FactAdtMovement (PatientSK, MovementDateKey);

-- FactAuditEvent: audit trail (no CDW equivalent — this is a NewVistas advantage)
CREATE TABLE rpt.FactAuditEvent (
    AuditSK             BIGINT          IDENTITY(1,1) PRIMARY KEY,
    AuditGrainKey       VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    UserSK              BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    LocationSK          BIGINT          NULL        REFERENCES rpt.DimLocation(LocationSK),
    EventDateKey        INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    Domain              VARCHAR(30)     NOT NULL,
    [Action]            VARCHAR(30)     NOT NULL,
    EntityType          VARCHAR(100)    NULL,
    EntityId            VARCHAR(200)    NULL,
    Details             NVARCHAR(1000)  NULL,
    OldValue            NVARCHAR(MAX)   NULL,
    NewValue            NVARCHAR(MAX)   NULL,
    -- Timestamps
    EventDateTime       DATETIME2       NOT NULL,
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactAudit_Patient ON rpt.FactAuditEvent (PatientSK, EventDateKey);
CREATE INDEX IX_FactAudit_Domain  ON rpt.FactAuditEvent (Domain, EventDateKey);
CREATE INDEX IX_FactAudit_User    ON rpt.FactAuditEvent (UserSK, EventDateKey);

-- FactMedAdmin: BCMA medication administration events
-- Mirrors CDW BCMA.BCMAMedicationLog
CREATE TABLE rpt.FactMedAdmin (
    MedAdminSK          BIGINT          IDENTITY(1,1) PRIMARY KEY,
    BcmaGrainKey        VARCHAR(200)    NOT NULL,
    PatientSK           BIGINT          NOT NULL REFERENCES rpt.DimPatient(PatientSK),
    AdminProviderSK     BIGINT          NULL        REFERENCES rpt.DimProvider(ProviderSK),
    DrugSK              BIGINT          NULL        REFERENCES rpt.DimDrug(DrugSK),
    AdminDateKey        INT             NULL        REFERENCES rpt.DimDate(DateKey),
    -- Attributes
    ActionStatus        VARCHAR(30)     NULL,       -- Given, Held, Refused, Missing Dose
    Dosage              VARCHAR(100)    NULL,
    [Route]             VARCHAR(50)     NULL,
    InjectionSite       VARCHAR(100)    NULL,
    -- Timestamps
    ScheduledDateTime   DATETIME2       NULL,
    AdminDateTime       DATETIME2       NULL,
    VarianceMinutes     INT             NULL,       -- Scheduled → Actual
    CDCTimestamp        DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_FactMedAdmin_Patient ON rpt.FactMedAdmin (PatientSK, AdminDateKey);

GO

-- ─────────────────────────────────────────────────────────────────────────────
-- Populate DimDate: 2020-01-01 through 2035-12-31
-- ─────────────────────────────────────────────────────────────────────────────
;WITH DateCTE AS (
    SELECT CAST('2020-01-01' AS DATE) AS d
    UNION ALL
    SELECT DATEADD(DAY, 1, d) FROM DateCTE WHERE d < '2035-12-31'
)
INSERT INTO rpt.DimDate (
    DateKey, FullDate, [Year], [Quarter], [Month], [MonthName],
    [Week], [DayOfWeek], [DayName], [DayOfMonth], [DayOfYear],
    IsWeekend, FiscalYear, FiscalQuarter
)
SELECT
    CAST(FORMAT(d, 'yyyyMMdd') AS INT),
    d,
    YEAR(d),
    DATEPART(QUARTER, d),
    MONTH(d),
    DATENAME(MONTH, d),
    DATEPART(WEEK, d),
    DATEPART(WEEKDAY, d),
    DATENAME(WEEKDAY, d),
    DAY(d),
    DATEPART(DAYOFYEAR, d),
    CASE WHEN DATEPART(WEEKDAY, d) IN (1, 7) THEN 1 ELSE 0 END,
    -- VA fiscal year starts October 1
    CASE WHEN MONTH(d) >= 10 THEN YEAR(d) + 1 ELSE YEAR(d) END,
    CASE
        WHEN MONTH(d) IN (10, 11, 12) THEN 1
        WHEN MONTH(d) IN (1, 2, 3) THEN 2
        WHEN MONTH(d) IN (4, 5, 6) THEN 3
        ELSE 4
    END
FROM DateCTE
OPTION (MAXRECURSION 10000);
GO

PRINT '✓ Star schema created: 7 dimensions, 9 fact tables, DimDate populated';
GO
