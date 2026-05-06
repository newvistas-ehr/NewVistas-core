-- ============================================================================
-- CDC Views: Parse Orleans Grain State JSON into flat relational rows
--
-- Orleans ADO.NET storage writes grain state into an OrleansStorage table:
--   GrainIdHash, GrainIdN0, GrainIdN1, GrainTypeHash, GrainTypeString,
--   PayloadBinary (binary) or PayloadJson (nvarchar(max))
--
-- These views use OPENJSON (SQL Server 2016+) to extract fields from the
-- JSON state payload. Each view represents one grain type's state as a
-- flat relational projection — the CDC materialization procedures (script 003)
-- read from these views to upsert into the star schema fact tables.
--
-- For CockroachDB: replace OPENJSON with jsonb operators (->>, #>>)
-- For PostgreSQL:  replace OPENJSON with json_populate_record or ->>
-- ============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- Helper: resolve Orleans grain key from GrainId columns
-- Orleans stores the string key in GrainIdExtensionString for IGrainWithStringKey
-- ─────────────────────────────────────────────────────────────────────────────

-- NOTE: Orleans ADO.NET provider stores grain state in table [OrleansStorage].
-- The PayloadJson column contains the serialized grain state when JSON serializer
-- is configured. With binary serializer, PayloadBinary is used instead and these
-- views will need a deserialization step (handled by the C# CDC service).
--
-- The views below assume JSON payload mode. When showing this to the VA analyst,
-- note that the production pipeline may use a C# CDC service to deserialize
-- binary payloads and write directly to the fact tables instead.

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_Patient: Patient demographics from PatientGrain state
-- VistA DPT file #2 → CDW SPatient
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_Patient AS
SELECT
    s.GrainIdExtensionString                        AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientName')      AS PatientName,
    JSON_VALUE(s.PayloadJson, '$.sex')              AS Sex,
    JSON_VALUE(s.PayloadJson, '$.dateOfBirth')      AS DateOfBirth,
    -- Mask SSN: only last 4 digits for reporting
    RIGHT(JSON_VALUE(s.PayloadJson, '$.socialSecurityNumber'), 4) AS SSNLast4,
    JSON_VALUE(s.PayloadJson, '$.veteranStatus')    AS IsVeteran,
    JSON_VALUE(s.PayloadJson, '$.serviceBranch')    AS ServiceBranch,
    JSON_VALUE(s.PayloadJson, '$.serviceEra')       AS ServiceEra,
    JSON_VALUE(s.PayloadJson, '$.isServiceConnected') AS IsServiceConnected,
    JSON_VALUE(s.PayloadJson, '$.scPercent')        AS SCPercent,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate') AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%PatientGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_LabTest: Lab test orders and results from LabTestGrain state
-- VistA LR file #63 → CDW Chem.PatientLabChem
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_LabTest AS
SELECT
    s.GrainIdExtensionString                                AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.testName')                 AS TestName,
    JSON_VALUE(s.PayloadJson, '$.testId')                   AS TestId,
    JSON_VALUE(s.PayloadJson, '$.testCode')                 AS LoincCode,
    JSON_VALUE(s.PayloadJson, '$.category')                 AS Category,
    JSON_VALUE(s.PayloadJson, '$.status')                   AS [Status],
    JSON_VALUE(s.PayloadJson, '$.specimenType')             AS SpecimenType,
    JSON_VALUE(s.PayloadJson, '$.collectionSample')         AS CollectionSample,
    JSON_VALUE(s.PayloadJson, '$.performingLab')            AS PerformingLab,
    JSON_VALUE(s.PayloadJson, '$.orderingProviderId')       AS OrderingProviderId,
    JSON_VALUE(s.PayloadJson, '$.orderingProviderName')     AS OrderingProviderName,
    -- Results
    JSON_VALUE(s.PayloadJson, '$.resultValue')              AS ResultValue,
    JSON_VALUE(s.PayloadJson, '$.resultUnit')               AS ResultUnit,
    JSON_VALUE(s.PayloadJson, '$.referenceLow')             AS ReferenceLow,
    JSON_VALUE(s.PayloadJson, '$.referenceHigh')            AS ReferenceHigh,
    JSON_VALUE(s.PayloadJson, '$.abnormalFlag')             AS AbnormalFlag,
    -- Timestamps
    JSON_VALUE(s.PayloadJson, '$.orderDateTime')            AS OrderDateTime,
    JSON_VALUE(s.PayloadJson, '$.collectionDateTime')       AS CollectionDateTime,
    JSON_VALUE(s.PayloadJson, '$.resultDateTime')           AS ResultDateTime,
    JSON_VALUE(s.PayloadJson, '$.verifiedDateTime')         AS VerifiedDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')         AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%LabTestGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_Order: CPRS orders from OrderGrain state
-- VistA OR file #100 → CDW CPRSOrder
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_Order AS
SELECT
    s.GrainIdExtensionString                                AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.orderType')                AS OrderType,
    JSON_VALUE(s.PayloadJson, '$.orderText')                AS OrderText,
    JSON_VALUE(s.PayloadJson, '$.status')                   AS [Status],
    JSON_VALUE(s.PayloadJson, '$.urgency')                  AS Urgency,
    JSON_VALUE(s.PayloadJson, '$.orderingProviderId')       AS OrderingProviderId,
    JSON_VALUE(s.PayloadJson, '$.orderingProviderName')     AS OrderingProviderName,
    JSON_VALUE(s.PayloadJson, '$.locationId')               AS LocationId,
    JSON_VALUE(s.PayloadJson, '$.locationName')             AS LocationName,
    JSON_VALUE(s.PayloadJson, '$.orderDateTime')            AS OrderDateTime,
    JSON_VALUE(s.PayloadJson, '$.startDateTime')            AS StartDateTime,
    JSON_VALUE(s.PayloadJson, '$.stopDateTime')             AS StopDateTime,
    JSON_VALUE(s.PayloadJson, '$.signedDateTime')           AS SignedDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')         AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%OrderGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_Prescription: Pharmacy prescriptions from PharmacyGrain state
-- VistA PS file #52 → CDW RxOut.RxOutpat
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_Prescription AS
SELECT
    s.GrainIdExtensionString                                AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.drugName')                 AS DrugName,
    JSON_VALUE(s.PayloadJson, '$.drugId')                   AS DrugId,
    JSON_VALUE(s.PayloadJson, '$.dosage')                   AS Dosage,
    JSON_VALUE(s.PayloadJson, '$.route')                    AS [Route],
    JSON_VALUE(s.PayloadJson, '$.schedule')                 AS Schedule,
    JSON_VALUE(s.PayloadJson, '$.daysSupply')               AS DaysSupply,
    JSON_VALUE(s.PayloadJson, '$.quantity')                 AS Quantity,
    JSON_VALUE(s.PayloadJson, '$.maxRefills')               AS MaxRefills,
    JSON_VALUE(s.PayloadJson, '$.refillsRemaining')         AS RefillsRemaining,
    JSON_VALUE(s.PayloadJson, '$.status')                   AS [Status],
    JSON_VALUE(s.PayloadJson, '$.prescriberId')             AS PrescriberId,
    JSON_VALUE(s.PayloadJson, '$.prescriberName')           AS PrescriberName,
    JSON_VALUE(s.PayloadJson, '$.locationId')               AS LocationId,
    JSON_VALUE(s.PayloadJson, '$.locationName')             AS LocationName,
    JSON_VALUE(s.PayloadJson, '$.issueDate')                AS IssueDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastFillDate')             AS LastFillDateTime,
    JSON_VALUE(s.PayloadJson, '$.expirationDate')           AS ExpirationDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')         AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%PharmacyGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_TiuDocument: Progress notes from TiuDocumentGrain state
-- VistA TIU file #8925 → CDW TIU.TIUDocument
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_TiuDocument AS
SELECT
    s.GrainIdExtensionString                                AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.documentType')             AS DocumentType,
    JSON_VALUE(s.PayloadJson, '$.status')                   AS [Status],
    JSON_VALUE(s.PayloadJson, '$.subject')                  AS [Subject],
    JSON_VALUE(s.PayloadJson, '$.authorId')                 AS AuthorId,
    JSON_VALUE(s.PayloadJson, '$.authorName')               AS AuthorName,
    JSON_VALUE(s.PayloadJson, '$.locationId')               AS LocationId,
    JSON_VALUE(s.PayloadJson, '$.locationName')             AS LocationName,
    LEN(JSON_VALUE(s.PayloadJson, '$.reportText'))          AS TextLength,
    -- Addenda
    (SELECT COUNT(*) FROM OPENJSON(s.PayloadJson, '$.addendumIds')) AS AddendumCount,
    -- Timestamps
    JSON_VALUE(s.PayloadJson, '$.referenceDate')            AS ReferenceDateTime,
    JSON_VALUE(s.PayloadJson, '$.entryDate')                AS EntryDateTime,
    JSON_VALUE(s.PayloadJson, '$.signedDateTime')           AS SignedDateTime,
    JSON_VALUE(s.PayloadJson, '$.cosignedDateTime')         AS CosignedDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')         AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%TiuDocumentGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_Consult: Consult requests from ConsultGrain state
-- VistA GMRC file #123 → CDW Con.Consult
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_Consult AS
SELECT
    s.GrainIdExtensionString                                    AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                    AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.toService')                    AS ToService,
    JSON_VALUE(s.PayloadJson, '$.fromService')                  AS FromService,
    JSON_VALUE(s.PayloadJson, '$.urgency')                      AS Urgency,
    JSON_VALUE(s.PayloadJson, '$.status')                       AS [Status],
    JSON_VALUE(s.PayloadJson, '$.requestingProviderId')         AS RequestingProviderId,
    JSON_VALUE(s.PayloadJson, '$.requestingProviderName')       AS RequestingProviderName,
    JSON_VALUE(s.PayloadJson, '$.locationId')                   AS LocationId,
    JSON_VALUE(s.PayloadJson, '$.locationName')                 AS LocationName,
    JSON_VALUE(s.PayloadJson, '$.requestDateTime')              AS RequestDateTime,
    JSON_VALUE(s.PayloadJson, '$.scheduledDateTime')            AS ScheduledDateTime,
    JSON_VALUE(s.PayloadJson, '$.completedDateTime')            AS CompletedDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')             AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%ConsultGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_AuditEvent: Audit trail from AuditEventGrain state
-- VistA AUDIT file #1.1 — no CDW equivalent (NewVistas-only)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_AuditEvent AS
SELECT
    s.GrainIdExtensionString                                AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.domain')                   AS Domain,
    JSON_VALUE(s.PayloadJson, '$.action')                   AS [Action],
    JSON_VALUE(s.PayloadJson, '$.entityType')               AS EntityType,
    JSON_VALUE(s.PayloadJson, '$.entityId')                 AS EntityId,
    JSON_VALUE(s.PayloadJson, '$.userId')                   AS UserId,
    JSON_VALUE(s.PayloadJson, '$.userName')                 AS UserName,
    JSON_VALUE(s.PayloadJson, '$.locationId')               AS LocationId,
    JSON_VALUE(s.PayloadJson, '$.locationName')             AS LocationName,
    JSON_VALUE(s.PayloadJson, '$.details')                  AS Details,
    JSON_VALUE(s.PayloadJson, '$.oldValue')                 AS OldValue,
    JSON_VALUE(s.PayloadJson, '$.newValue')                 AS NewValue,
    JSON_VALUE(s.PayloadJson, '$.timestamp')                AS EventDateTime,
    JSON_VALUE(s.PayloadJson, '$.createdDate')              AS CreatedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%AuditEventGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_AdtMovement: ADT movements from AdtGrain state
-- VistA MAS file #405 → CDW Inpat.PatientMovement
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_AdtMovement AS
SELECT
    s.GrainIdExtensionString                                    AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                    AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.movementType')                 AS MovementType,
    JSON_VALUE(s.PayloadJson, '$.wardLocationName')             AS WardName,
    JSON_VALUE(s.PayloadJson, '$.wardLocationId')               AS WardLocationId,
    JSON_VALUE(s.PayloadJson, '$.roomBed')                      AS RoomBed,
    JSON_VALUE(s.PayloadJson, '$.treatingSpecialtyName')        AS TreatingSpecialty,
    JSON_VALUE(s.PayloadJson, '$.attendingPhysicianId')         AS AttendingProviderId,
    JSON_VALUE(s.PayloadJson, '$.attendingPhysicianName')       AS AttendingProviderName,
    JSON_VALUE(s.PayloadJson, '$.disposition')                  AS Disposition,
    JSON_VALUE(s.PayloadJson, '$.movementDateTime')             AS MovementDateTime,
    JSON_VALUE(s.PayloadJson, '$.dischargeDateTime')            AS DischargeDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')             AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%AdtGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- vw_CDC_Bcma: Medication administration from BcmaGrain state
-- VistA PSB file #53.79 → CDW BCMA.BCMAMedicationLog
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER VIEW rpt.vw_CDC_Bcma AS
SELECT
    s.GrainIdExtensionString                                    AS GrainKey,
    JSON_VALUE(s.PayloadJson, '$.patientId')                    AS PatientId,
    JSON_VALUE(s.PayloadJson, '$.drugName')                     AS DrugName,
    JSON_VALUE(s.PayloadJson, '$.drugId')                       AS DrugId,
    JSON_VALUE(s.PayloadJson, '$.dosage')                       AS Dosage,
    JSON_VALUE(s.PayloadJson, '$.route')                        AS [Route],
    JSON_VALUE(s.PayloadJson, '$.actionStatus')                 AS ActionStatus,
    JSON_VALUE(s.PayloadJson, '$.injectionSite')                AS InjectionSite,
    JSON_VALUE(s.PayloadJson, '$.administeredById')             AS AdminProviderId,
    JSON_VALUE(s.PayloadJson, '$.administeredByName')           AS AdminProviderName,
    JSON_VALUE(s.PayloadJson, '$.scheduledDateTime')            AS ScheduledDateTime,
    JSON_VALUE(s.PayloadJson, '$.administrationDateTime')       AS AdminDateTime,
    JSON_VALUE(s.PayloadJson, '$.lastModifiedDate')             AS LastModifiedDate
FROM OrleansStorage s
WHERE s.GrainTypeString LIKE '%BcmaGrain%'
  AND s.PayloadJson IS NOT NULL;
GO

PRINT '✓ CDC views created: 9 views over OrleansStorage JSON payloads';
GO
