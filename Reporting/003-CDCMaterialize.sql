-- ============================================================================
-- CDC Materialization: Incremental upsert from CDC views into star schema
--
-- These procedures read from the rpt.vw_CDC_* views (which parse live Orleans
-- grain state JSON) and merge changes into the star schema fact/dimension tables.
--
-- Designed to run:
--   - Every 1-5 minutes via SQL Agent job (near real-time)
--   - Or triggered by CockroachDB changefeed / Cosmos change feed
--   - Or called from a C# BackgroundService in the Orleans silo
--
-- Each procedure tracks its own high-water mark via rpt.CDCWatermark so it only
-- processes grains modified since the last run.
--
-- Architecture:
--   OrleansStorage → vw_CDC_* views → sp_CDC_Materialize_* → rpt.Fact*/rpt.Dim*
--     (clinical)     (JSON parse)     (incremental MERGE)    (reporting)
-- ============================================================================

-- ─── CDC Watermark tracking table ────────────────────────────────────────────
CREATE TABLE rpt.CDCWatermark (
    EntityName          VARCHAR(100)    NOT NULL PRIMARY KEY,
    LastProcessedAt     DATETIME2       NOT NULL DEFAULT '2000-01-01',
    LastRowCount        INT             NOT NULL DEFAULT 0,
    LastRunDurationMs   INT             NULL
);
GO

-- Seed watermarks for each entity
INSERT INTO rpt.CDCWatermark (EntityName) VALUES
    ('Patient'), ('LabTest'), ('Order'), ('Prescription'),
    ('TiuDocument'), ('Consult'), ('AdtMovement'), ('AuditEvent'),
    ('Bcma');
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- DimPatient upsert: SCD Type 2 for demographics changes
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE rpt.sp_CDC_Materialize_DimPatient
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start DATETIME2 = SYSUTCDATETIME();
    DECLARE @watermark DATETIME2;
    DECLARE @count INT = 0;

    SELECT @watermark = LastProcessedAt FROM rpt.CDCWatermark WHERE EntityName = 'Patient';

    -- Upsert current patients (SCD Type 1 for simplicity; upgrade to Type 2 if needed)
    MERGE rpt.DimPatient AS tgt
    USING (
        SELECT
            GrainKey, PatientName, Sex, DateOfBirth, SSNLast4,
            IsVeteran, ServiceBranch, ServiceEra, IsServiceConnected, SCPercent
        FROM rpt.vw_CDC_Patient
        WHERE LastModifiedDate > @watermark
           OR @watermark = '2000-01-01'
    ) AS src ON tgt.PatientId = src.GrainKey AND tgt.IsCurrent = 1
    WHEN MATCHED THEN
        UPDATE SET
            PatientName = src.PatientName,
            Sex = src.Sex,
            DateOfBirth = TRY_CAST(src.DateOfBirth AS DATE),
            SSNLast4 = src.SSNLast4,
            IsVeteran = TRY_CAST(src.IsVeteran AS BIT),
            ServiceBranch = src.ServiceBranch,
            ServiceEra = src.ServiceEra,
            IsServiceConnected = TRY_CAST(src.IsServiceConnected AS BIT),
            SCPercent = TRY_CAST(src.SCPercent AS INT),
            LastCDCTimestamp = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (PatientId, PatientName, Sex, DateOfBirth, SSNLast4,
                IsVeteran, ServiceBranch, ServiceEra, IsServiceConnected, SCPercent,
                SourceGrainKey, LastCDCTimestamp)
        VALUES (src.GrainKey, src.PatientName, src.Sex, TRY_CAST(src.DateOfBirth AS DATE), src.SSNLast4,
                TRY_CAST(src.IsVeteran AS BIT), src.ServiceBranch, src.ServiceEra,
                TRY_CAST(src.IsServiceConnected AS BIT), TRY_CAST(src.SCPercent AS INT),
                src.GrainKey, SYSUTCDATETIME());

    SET @count = @@ROWCOUNT;
    UPDATE rpt.CDCWatermark
    SET LastProcessedAt = SYSUTCDATETIME(), LastRowCount = @count,
        LastRunDurationMs = DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME())
    WHERE EntityName = 'Patient';
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- FactLabResult upsert
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE rpt.sp_CDC_Materialize_FactLabResult
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start DATETIME2 = SYSUTCDATETIME();
    DECLARE @watermark DATETIME2;
    DECLARE @count INT = 0;

    SELECT @watermark = LastProcessedAt FROM rpt.CDCWatermark WHERE EntityName = 'LabTest';

    MERGE rpt.FactLabResult AS tgt
    USING (
        SELECT
            cdc.GrainKey,
            dp.PatientSK,
            cdc.[Status],
            cdc.SpecimenType,
            cdc.PerformingLab,
            cdc.ResultValue,
            TRY_CAST(cdc.ResultValue AS DECIMAL(18,4))  AS ResultNumeric,
            cdc.ResultUnit,
            cdc.ReferenceLow,
            cdc.ReferenceHigh,
            cdc.AbnormalFlag,
            CASE WHEN cdc.AbnormalFlag IN ('H','L','HH','LL','A','AA') THEN 1 ELSE 0 END AS IsAbnormal,
            CASE WHEN cdc.AbnormalFlag IN ('HH','LL','AA') THEN 1 ELSE 0 END AS IsCritical,
            TRY_CAST(cdc.CollectionDateTime AS DATETIME2) AS CollectionDateTime,
            TRY_CAST(cdc.ResultDateTime AS DATETIME2)     AS ResultDateTime,
            TRY_CAST(cdc.VerifiedDateTime AS DATETIME2)   AS VerifiedDateTime,
            CAST(FORMAT(TRY_CAST(cdc.CollectionDateTime AS DATE), 'yyyyMMdd') AS INT) AS CollectionDateKey,
            CAST(FORMAT(TRY_CAST(cdc.ResultDateTime AS DATE), 'yyyyMMdd') AS INT)     AS ResultDateKey
        FROM rpt.vw_CDC_LabTest cdc
        LEFT JOIN rpt.DimPatient dp ON dp.PatientId = cdc.PatientId AND dp.IsCurrent = 1
        WHERE cdc.LastModifiedDate > @watermark
           OR @watermark = '2000-01-01'
    ) AS src ON tgt.LabTestGrainKey = src.GrainKey
    WHEN MATCHED THEN
        UPDATE SET
            [Status] = src.[Status],
            ResultValue = src.ResultValue,
            ResultNumeric = src.ResultNumeric,
            ResultUnit = src.ResultUnit,
            ReferenceLow = src.ReferenceLow,
            ReferenceHigh = src.ReferenceHigh,
            AbnormalFlag = src.AbnormalFlag,
            IsAbnormal = src.IsAbnormal,
            IsCritical = src.IsCritical,
            CollectionDateTime = src.CollectionDateTime,
            CollectionDateKey = src.CollectionDateKey,
            ResultDateTime = src.ResultDateTime,
            ResultDateKey = src.ResultDateKey,
            VerifiedDateTime = src.VerifiedDateTime,
            CDCTimestamp = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (LabTestGrainKey, PatientSK, [Status], SpecimenType, PerformingLab,
                ResultValue, ResultNumeric, ResultUnit, ReferenceLow, ReferenceHigh,
                AbnormalFlag, IsAbnormal, IsCritical,
                CollectionDateTime, CollectionDateKey, ResultDateTime, ResultDateKey, VerifiedDateTime)
        VALUES (src.GrainKey, src.PatientSK, src.[Status], src.SpecimenType, src.PerformingLab,
                src.ResultValue, src.ResultNumeric, src.ResultUnit, src.ReferenceLow, src.ReferenceHigh,
                src.AbnormalFlag, src.IsAbnormal, src.IsCritical,
                src.CollectionDateTime, src.CollectionDateKey, src.ResultDateTime, src.ResultDateKey, src.VerifiedDateTime);

    SET @count = @@ROWCOUNT;
    UPDATE rpt.CDCWatermark
    SET LastProcessedAt = SYSUTCDATETIME(), LastRowCount = @count,
        LastRunDurationMs = DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME())
    WHERE EntityName = 'LabTest';
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- FactOrder upsert
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE rpt.sp_CDC_Materialize_FactOrder
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start DATETIME2 = SYSUTCDATETIME();
    DECLARE @watermark DATETIME2;
    DECLARE @count INT = 0;

    SELECT @watermark = LastProcessedAt FROM rpt.CDCWatermark WHERE EntityName = 'Order';

    MERGE rpt.FactOrder AS tgt
    USING (
        SELECT
            cdc.GrainKey,
            dp.PatientSK,
            cdc.OrderType,
            cdc.OrderText,
            cdc.[Status],
            cdc.Urgency,
            TRY_CAST(cdc.OrderDateTime AS DATETIME2)    AS OrderDateTime,
            TRY_CAST(cdc.StartDateTime AS DATETIME2)    AS StartDateTime,
            TRY_CAST(cdc.StopDateTime AS DATETIME2)     AS StopDateTime,
            TRY_CAST(cdc.SignedDateTime AS DATETIME2)    AS SignedDateTime,
            CAST(FORMAT(TRY_CAST(cdc.OrderDateTime AS DATE), 'yyyyMMdd') AS INT) AS OrderDateKey,
            DATEDIFF(DAY,
                TRY_CAST(cdc.OrderDateTime AS DATETIME2),
                TRY_CAST(cdc.SignedDateTime AS DATETIME2)) AS DaysToSign,
            DATEDIFF(DAY,
                TRY_CAST(cdc.StartDateTime AS DATETIME2),
                TRY_CAST(cdc.StopDateTime AS DATETIME2))  AS DaysActive
        FROM rpt.vw_CDC_Order cdc
        LEFT JOIN rpt.DimPatient dp ON dp.PatientId = cdc.PatientId AND dp.IsCurrent = 1
        WHERE cdc.LastModifiedDate > @watermark
           OR @watermark = '2000-01-01'
    ) AS src ON tgt.OrderGrainKey = src.GrainKey
    WHEN MATCHED THEN
        UPDATE SET
            [Status] = src.[Status],
            SignedDateTime = src.SignedDateTime,
            StopDateTime = src.StopDateTime,
            DaysToSign = src.DaysToSign,
            DaysActive = src.DaysActive,
            CDCTimestamp = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (OrderGrainKey, PatientSK, OrderType, OrderText, [Status], Urgency,
                OrderDateTime, StartDateTime, StopDateTime, SignedDateTime,
                OrderDateKey, DaysToSign, DaysActive)
        VALUES (src.GrainKey, src.PatientSK, src.OrderType, src.OrderText, src.[Status], src.Urgency,
                src.OrderDateTime, src.StartDateTime, src.StopDateTime, src.SignedDateTime,
                src.OrderDateKey, src.DaysToSign, src.DaysActive);

    SET @count = @@ROWCOUNT;
    UPDATE rpt.CDCWatermark
    SET LastProcessedAt = SYSUTCDATETIME(), LastRowCount = @count,
        LastRunDurationMs = DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME())
    WHERE EntityName = 'Order';
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- FactAuditEvent upsert (append-only — audit events never update)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE rpt.sp_CDC_Materialize_FactAuditEvent
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @start DATETIME2 = SYSUTCDATETIME();
    DECLARE @watermark DATETIME2;
    DECLARE @count INT = 0;

    SELECT @watermark = LastProcessedAt FROM rpt.CDCWatermark WHERE EntityName = 'AuditEvent';

    INSERT INTO rpt.FactAuditEvent (
        AuditGrainKey, PatientSK, EventDateKey,
        Domain, [Action], EntityType, EntityId, Details, OldValue, NewValue,
        EventDateTime, CDCTimestamp
    )
    SELECT
        cdc.GrainKey,
        dp.PatientSK,
        CAST(FORMAT(TRY_CAST(cdc.EventDateTime AS DATE), 'yyyyMMdd') AS INT),
        cdc.Domain,
        cdc.[Action],
        cdc.EntityType,
        cdc.EntityId,
        cdc.Details,
        cdc.OldValue,
        cdc.NewValue,
        TRY_CAST(cdc.EventDateTime AS DATETIME2),
        SYSUTCDATETIME()
    FROM rpt.vw_CDC_AuditEvent cdc
    LEFT JOIN rpt.DimPatient dp ON dp.PatientId = cdc.PatientId AND dp.IsCurrent = 1
    WHERE cdc.CreatedDate > @watermark
       OR @watermark = '2000-01-01'
    -- Skip events already materialized (append-only idempotency)
    AND NOT EXISTS (
        SELECT 1 FROM rpt.FactAuditEvent f WHERE f.AuditGrainKey = cdc.GrainKey
    );

    SET @count = @@ROWCOUNT;
    UPDATE rpt.CDCWatermark
    SET LastProcessedAt = SYSUTCDATETIME(), LastRowCount = @count,
        LastRunDurationMs = DATEDIFF(MILLISECOND, @start, SYSUTCDATETIME())
    WHERE EntityName = 'AuditEvent';
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- Master CDC runner: executes all materialization procedures in order
-- Schedule this via SQL Agent every 1-5 minutes
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE rpt.sp_CDC_MaterializeAll
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @totalStart DATETIME2 = SYSUTCDATETIME();

    PRINT '═══ NewVistas CDC Materialization Run: ' + CONVERT(VARCHAR, SYSUTCDATETIME(), 120) + ' ═══';

    EXEC rpt.sp_CDC_Materialize_DimPatient;
    PRINT '  ✓ DimPatient';

    EXEC rpt.sp_CDC_Materialize_FactLabResult;
    PRINT '  ✓ FactLabResult';

    EXEC rpt.sp_CDC_Materialize_FactOrder;
    PRINT '  ✓ FactOrder';

    EXEC rpt.sp_CDC_Materialize_FactAuditEvent;
    PRINT '  ✓ FactAuditEvent';

    -- TODO: Add remaining materializers as needed:
    -- EXEC rpt.sp_CDC_Materialize_FactPrescription;
    -- EXEC rpt.sp_CDC_Materialize_FactEncounter;
    -- EXEC rpt.sp_CDC_Materialize_FactVital;
    -- EXEC rpt.sp_CDC_Materialize_FactNote;
    -- EXEC rpt.sp_CDC_Materialize_FactConsult;
    -- EXEC rpt.sp_CDC_Materialize_FactAdtMovement;
    -- EXEC rpt.sp_CDC_Materialize_FactMedAdmin;

    PRINT '═══ Done in ' + CAST(DATEDIFF(MILLISECOND, @totalStart, SYSUTCDATETIME()) AS VARCHAR) + 'ms ═══';

    -- Return watermark status for monitoring
    SELECT EntityName, LastProcessedAt, LastRowCount, LastRunDurationMs
    FROM rpt.CDCWatermark
    ORDER BY EntityName;
END;
GO

PRINT '✓ CDC materialization procedures created (4 active + master runner)';
GO
