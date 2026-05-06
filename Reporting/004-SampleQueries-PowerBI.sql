-- ============================================================================
-- Sample Analytical Queries for Power BI / SSRS
--
-- These are the kinds of queries that VA CDW analysts run daily.
-- Each query runs against the rpt.* star schema — NEVER against OrleansStorage.
-- This is the whole point: analysts hammer the reporting schema while
-- clinical operations continue unaffected on the Orleans side.
--
-- Organized by the request types that VA CDW analysts actually submit:
--   1. Population Health / Quality Measures
--   2. Operational Metrics (wait times, throughput)
--   3. Pharmacy & Controlled Substance Monitoring
--   4. Lab Results Trending
--   5. Provider Productivity
--   6. Audit & Compliance
-- ============================================================================


-- ═══════════════════════════════════════════════════════════════════════════════
-- 1. POPULATION HEALTH / QUALITY MEASURES
--    "How are our patients doing across the system?"
--    Equivalent to VA HEDIS / SAIL measure queries
-- ═══════════════════════════════════════════════════════════════════════════════

-- 1a. HbA1c control: Diabetic patients with most recent HbA1c > 9.0
--     This is a core VA SAIL quality measure (DM-2)
SELECT
    dp.PatientId,
    dp.PatientName,
    lt.TestName,
    fl.ResultNumeric           AS LastHbA1c,
    fl.ResultDateTime,
    fl.AbnormalFlag
FROM rpt.FactLabResult fl
JOIN rpt.DimPatient dp ON dp.PatientSK = fl.PatientSK
LEFT JOIN rpt.DimLabTest lt ON lt.LabTestSK = fl.LabTestSK
WHERE lt.LoincCode = '4548-4'  -- LOINC for HbA1c
  AND fl.ResultNumeric > 9.0
  AND fl.ResultDateKey >= CAST(FORMAT(DATEADD(MONTH, -12, GETUTCDATE()), 'yyyyMMdd') AS INT)
ORDER BY fl.ResultNumeric DESC;

-- 1b. Patients with critical lab values in the last 7 days
--     Equivalent to what VistA LRVER1 ALERT fires on
SELECT
    dp.PatientName,
    lt.TestName,
    fl.ResultValue,
    fl.ResultUnit,
    fl.AbnormalFlag,
    fl.ResultDateTime,
    fl.IsAbnormal,
    fl.IsCritical
FROM rpt.FactLabResult fl
JOIN rpt.DimPatient dp ON dp.PatientSK = fl.PatientSK
LEFT JOIN rpt.DimLabTest lt ON lt.LabTestSK = fl.LabTestSK
WHERE fl.IsCritical = 1
  AND fl.ResultDateKey >= CAST(FORMAT(DATEADD(DAY, -7, GETUTCDATE()), 'yyyyMMdd') AS INT)
ORDER BY fl.ResultDateTime DESC;


-- ═══════════════════════════════════════════════════════════════════════════════
-- 2. OPERATIONAL METRICS
--    "How efficient are our clinics and processes?"
--    These replace manual VistA fileman extracts that take hours
-- ═══════════════════════════════════════════════════════════════════════════════

-- 2a. Average wait time by clinic (appointment scheduled → checked in)
--     Power BI: bar chart by location, drill down by month
SELECT
    dl.LocationName             AS Clinic,
    dd.[MonthName],
    dd.[Year],
    COUNT(*)                    AS EncounterCount,
    AVG(fe.WaitTimeMinutes)     AS AvgWaitMinutes,
    MAX(fe.WaitTimeMinutes)     AS MaxWaitMinutes,
    -- % seen within 30 minutes of scheduled time
    CAST(SUM(CASE WHEN fe.WaitTimeMinutes <= 30 THEN 1 ELSE 0 END) AS FLOAT)
        / NULLIF(COUNT(*), 0) * 100 AS PctWithin30Min
FROM rpt.FactEncounter fe
JOIN rpt.DimLocation dl ON dl.LocationSK = fe.LocationSK
JOIN rpt.DimDate dd ON dd.DateKey = fe.EncounterDateKey
WHERE fe.WaitTimeMinutes IS NOT NULL
  AND dd.[Year] = YEAR(GETUTCDATE())
GROUP BY dl.LocationName, dd.[MonthName], dd.[Month], dd.[Year]
ORDER BY dl.LocationName, dd.[Month];

-- 2b. Unsigned orders aging report
--     Power BI: KPI card showing orders > 24 hours unsigned
SELECT
    fo.[Status],
    fo.OrderType,
    fo.OrderText,
    dp.PatientName,
    fo.OrderDateTime,
    DATEDIFF(HOUR, fo.OrderDateTime, GETUTCDATE()) AS HoursUnsigned
FROM rpt.FactOrder fo
JOIN rpt.DimPatient dp ON dp.PatientSK = fo.PatientSK
WHERE fo.[Status] = 'Pending'
  AND fo.SignedDateTime IS NULL
  AND fo.OrderDateTime < DATEADD(HOUR, -24, GETUTCDATE())
ORDER BY fo.OrderDateTime;

-- 2c. Consult completion timeliness by service
--     VA measures this as "consult completion within 90 days"
SELECT
    fc.ToService,
    COUNT(*)                        AS TotalConsults,
    AVG(fc.DaysToComplete)          AS AvgDaysToComplete,
    SUM(CASE WHEN fc.DaysToComplete <= 30 THEN 1 ELSE 0 END)  AS CompletedIn30,
    SUM(CASE WHEN fc.DaysToComplete <= 90 THEN 1 ELSE 0 END)  AS CompletedIn90,
    SUM(CASE WHEN fc.[Status] = 'Pending' THEN 1 ELSE 0 END)  AS StillPending,
    -- % completed within 90 days (SAIL-like measure)
    CAST(SUM(CASE WHEN fc.DaysToComplete <= 90 THEN 1 ELSE 0 END) AS FLOAT)
        / NULLIF(SUM(CASE WHEN fc.[Status] = 'Complete' THEN 1 ELSE 0 END), 0) * 100
        AS PctIn90Days
FROM rpt.FactConsult fc
JOIN rpt.DimDate dd ON dd.DateKey = fc.RequestDateKey
WHERE dd.FiscalYear = CASE WHEN MONTH(GETUTCDATE()) >= 10 THEN YEAR(GETUTCDATE()) + 1 ELSE YEAR(GETUTCDATE()) END
GROUP BY fc.ToService
ORDER BY AvgDaysToComplete DESC;


-- ═══════════════════════════════════════════════════════════════════════════════
-- 3. PHARMACY & CONTROLLED SUBSTANCE MONITORING
--    "Who's prescribing what, and are there red flags?"
--    These are the queries that PBM and patient safety teams run
-- ═══════════════════════════════════════════════════════════════════════════════

-- 3a. Top 20 prescribed medications by volume (current fiscal year)
SELECT TOP 20
    dd.DrugName,
    dd.DrugClass,
    COUNT(*)                    AS PrescriptionCount,
    COUNT(DISTINCT fp.PatientSK) AS UniquePatients,
    SUM(CAST(fp.DaysSupply AS INT)) AS TotalDaysSupply
FROM rpt.FactPrescription fp
JOIN rpt.DimDrug dd ON dd.DrugSK = fp.DrugSK
JOIN rpt.DimDate dt ON dt.DateKey = fp.IssueDateKey
WHERE fp.[Status] IN ('Active', 'Discontinued', 'Expired')
  AND dt.FiscalYear = CASE WHEN MONTH(GETUTCDATE()) >= 10 THEN YEAR(GETUTCDATE()) + 1 ELSE YEAR(GETUTCDATE()) END
GROUP BY dd.DrugName, dd.DrugClass
ORDER BY PrescriptionCount DESC;

-- 3b. Controlled substance prescribing patterns (DEA Schedule II-V)
--     Red flag: high prescribers or patients with multiple providers
SELECT
    dprov.ProviderName,
    dd.DEASchedule,
    dd.DrugName,
    COUNT(*)                    AS RxCount,
    COUNT(DISTINCT fp.PatientSK) AS UniquePatients,
    AVG(CAST(fp.DaysSupply AS INT)) AS AvgDaysSupply
FROM rpt.FactPrescription fp
JOIN rpt.DimDrug dd ON dd.DrugSK = fp.DrugSK
JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fp.PrescriberSK
WHERE dd.DEASchedule IN ('II', 'III', 'IV', 'V')
  AND fp.IssueDateKey >= CAST(FORMAT(DATEADD(MONTH, -6, GETUTCDATE()), 'yyyyMMdd') AS INT)
GROUP BY dprov.ProviderName, dd.DEASchedule, dd.DrugName
HAVING COUNT(*) > 10
ORDER BY RxCount DESC;

-- 3c. BCMA medication administration variance: doses given > 60 min from scheduled
SELECT
    dp.PatientName,
    fm.DrugSK,
    fm.ActionStatus,
    fm.ScheduledDateTime,
    fm.AdminDateTime,
    fm.VarianceMinutes,
    dprov.ProviderName          AS AdministeredBy
FROM rpt.FactMedAdmin fm
JOIN rpt.DimPatient dp ON dp.PatientSK = fm.PatientSK
LEFT JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fm.AdminProviderSK
WHERE ABS(fm.VarianceMinutes) > 60
  AND fm.AdminDateKey >= CAST(FORMAT(DATEADD(DAY, -30, GETUTCDATE()), 'yyyyMMdd') AS INT)
ORDER BY ABS(fm.VarianceMinutes) DESC;


-- ═══════════════════════════════════════════════════════════════════════════════
-- 4. LAB RESULTS TRENDING
--    "Show me how a patient's labs have changed over time"
--    Power BI: line chart with date on X axis, value on Y
-- ═══════════════════════════════════════════════════════════════════════════════

-- 4a. Single patient lab trend (e.g., creatinine over 2 years)
--     Power BI parameter: @PatientId, @LoincCode
DECLARE @TrendPatientId VARCHAR(200) = 'PATIENT-001';  -- parameterize in Power BI
DECLARE @TrendLoincCode VARCHAR(20)  = '2160-0';       -- Creatinine

SELECT
    fl.ResultDateTime,
    fl.ResultNumeric,
    fl.ResultUnit,
    fl.ReferenceLow,
    fl.ReferenceHigh,
    fl.AbnormalFlag,
    fl.IsAbnormal
FROM rpt.FactLabResult fl
JOIN rpt.DimPatient dp ON dp.PatientSK = fl.PatientSK
LEFT JOIN rpt.DimLabTest lt ON lt.LabTestSK = fl.LabTestSK
WHERE dp.PatientId = @TrendPatientId
  AND lt.LoincCode = @TrendLoincCode
ORDER BY fl.ResultDateTime;

-- 4b. Facility-wide abnormal rate by test type (monthly)
--     Power BI: heat map with test on Y, month on X, color by abnormal %
SELECT
    lt.TestName,
    dd.[Year],
    dd.[Month],
    dd.[MonthName],
    COUNT(*)                    AS TotalResults,
    SUM(CAST(fl.IsAbnormal AS INT)) AS AbnormalCount,
    CAST(SUM(CAST(fl.IsAbnormal AS INT)) AS FLOAT) / NULLIF(COUNT(*), 0) * 100 AS AbnormalPct
FROM rpt.FactLabResult fl
LEFT JOIN rpt.DimLabTest lt ON lt.LabTestSK = fl.LabTestSK
JOIN rpt.DimDate dd ON dd.DateKey = fl.ResultDateKey
WHERE dd.[Year] >= YEAR(GETUTCDATE()) - 1
GROUP BY lt.TestName, dd.[Year], dd.[Month], dd.[MonthName]
HAVING COUNT(*) >= 10  -- exclude low-volume tests
ORDER BY lt.TestName, dd.[Year], dd.[Month];


-- ═══════════════════════════════════════════════════════════════════════════════
-- 5. PROVIDER PRODUCTIVITY
--    "How much work is each clinician doing?"
--    Common VA management report
-- ═══════════════════════════════════════════════════════════════════════════════

-- 5a. Notes authored per provider per month with avg time to sign
SELECT
    dprov.ProviderName,
    dd.[Year],
    dd.[MonthName],
    COUNT(*)                    AS NotesAuthored,
    SUM(CASE WHEN fn.[Status] = 'Completed' THEN 1 ELSE 0 END) AS NotesSigned,
    SUM(CASE WHEN fn.[Status] = 'UNSIGNED' THEN 1 ELSE 0 END)  AS NotesUnsigned,
    AVG(fn.HoursToSign)        AS AvgHoursToSign,
    AVG(fn.TextLength)         AS AvgNoteLength
FROM rpt.FactNote fn
JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fn.AuthorSK
JOIN rpt.DimDate dd ON dd.DateKey = fn.ReferenceDateKey
WHERE dd.[Year] = YEAR(GETUTCDATE())
GROUP BY dprov.ProviderName, dd.[Year], dd.[MonthName], dd.[Month]
ORDER BY dprov.ProviderName, dd.[Month];

-- 5b. Encounters per provider per stop code (workload credit)
--     This is how VA measures RVUs / FTEE productivity
SELECT
    dprov.ProviderName,
    fe.StopCode,
    fe.ServiceCategory,
    COUNT(*)                    AS EncounterCount,
    COUNT(DISTINCT fe.PatientSK) AS UniquePatients,
    AVG(fe.DurationMinutes)     AS AvgDurationMinutes
FROM rpt.FactEncounter fe
JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fe.PrimaryProviderSK
JOIN rpt.DimDate dd ON dd.DateKey = fe.EncounterDateKey
WHERE dd.FiscalYear = CASE WHEN MONTH(GETUTCDATE()) >= 10 THEN YEAR(GETUTCDATE()) + 1 ELSE YEAR(GETUTCDATE()) END
  AND fe.[Status] = 'CheckedOut'
GROUP BY dprov.ProviderName, fe.StopCode, fe.ServiceCategory
ORDER BY dprov.ProviderName, EncounterCount DESC;


-- ═══════════════════════════════════════════════════════════════════════════════
-- 6. AUDIT & COMPLIANCE
--    "Who accessed what, and when?"
--    These queries have NO equivalent in VA CDW today — this is new capability
-- ═══════════════════════════════════════════════════════════════════════════════

-- 6a. Audit events by domain — volume dashboard
--     Power BI: donut chart by domain, trend line by day
SELECT
    fa.Domain,
    dd.[Year],
    dd.[Month],
    dd.[MonthName],
    COUNT(*)                    AS EventCount,
    COUNT(DISTINCT fa.PatientSK) AS UniquePatientsAccessed,
    COUNT(DISTINCT fa.UserSK)    AS UniqueUsers
FROM rpt.FactAuditEvent fa
JOIN rpt.DimDate dd ON dd.DateKey = fa.EventDateKey
WHERE dd.[Year] = YEAR(GETUTCDATE())
GROUP BY fa.Domain, dd.[Year], dd.[Month], dd.[MonthName]
ORDER BY fa.Domain, dd.[Month];

-- 6b. Suspicious access patterns: users viewing patients outside their clinic
--     Privacy officer query — look for potential HIPAA violations
SELECT
    dprov.ProviderName,
    fa.Domain,
    fa.[Action],
    COUNT(*)                        AS AccessCount,
    COUNT(DISTINCT fa.PatientSK)    AS UniquePatientsAccessed,
    STRING_AGG(DISTINCT fa.Details, '; ') AS SampleDetails
FROM rpt.FactAuditEvent fa
LEFT JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fa.UserSK
WHERE fa.[Action] = 'VIEW'
  AND fa.EventDateKey >= CAST(FORMAT(DATEADD(DAY, -30, GETUTCDATE()), 'yyyyMMdd') AS INT)
GROUP BY dprov.ProviderName, fa.Domain, fa.[Action]
HAVING COUNT(DISTINCT fa.PatientSK) > 50  -- threshold for review
ORDER BY UniquePatientsAccessed DESC;

-- 6c. Full audit trail for a specific patient (HIPAA access log)
--     Required for patient requests under HIPAA Right of Access
DECLARE @AuditPatientId VARCHAR(200) = 'PATIENT-001';  -- parameterize

SELECT
    fa.EventDateTime,
    fa.Domain,
    fa.[Action],
    fa.EntityType,
    fa.EntityId,
    dprov.ProviderName          AS AccessedBy,
    dl.LocationName             AS FromLocation,
    fa.Details,
    fa.OldValue,
    fa.NewValue
FROM rpt.FactAuditEvent fa
JOIN rpt.DimPatient dp ON dp.PatientSK = fa.PatientSK
LEFT JOIN rpt.DimProvider dprov ON dprov.ProviderSK = fa.UserSK
LEFT JOIN rpt.DimLocation dl ON dl.LocationSK = fa.LocationSK
WHERE dp.PatientId = @AuditPatientId
ORDER BY fa.EventDateTime DESC;


-- ═══════════════════════════════════════════════════════════════════════════════
-- 7. EXECUTIVE DASHBOARD SUMMARY
--    "Give me the one-page view of the entire facility"
--    Power BI: single page with KPI tiles
-- ═══════════════════════════════════════════════════════════════════════════════

SELECT
    (SELECT COUNT(DISTINCT PatientSK) FROM rpt.FactEncounter
     WHERE EncounterDateKey >= CAST(FORMAT(DATEADD(DAY, -30, GETUTCDATE()), 'yyyyMMdd') AS INT))
        AS PatientsSeenLast30Days,

    (SELECT COUNT(*) FROM rpt.FactOrder WHERE [Status] = 'Pending' AND SignedDateTime IS NULL)
        AS UnsignedOrders,

    (SELECT COUNT(*) FROM rpt.FactLabResult WHERE IsCritical = 1
     AND ResultDateKey >= CAST(FORMAT(DATEADD(DAY, -7, GETUTCDATE()), 'yyyyMMdd') AS INT))
        AS CriticalLabsLast7Days,

    (SELECT COUNT(*) FROM rpt.FactConsult WHERE [Status] = 'Pending')
        AS PendingConsults,

    (SELECT AVG(WaitTimeMinutes) FROM rpt.FactEncounter
     WHERE EncounterDateKey >= CAST(FORMAT(DATEADD(DAY, -30, GETUTCDATE()), 'yyyyMMdd') AS INT)
       AND WaitTimeMinutes IS NOT NULL)
        AS AvgWaitMinutes30Days,

    (SELECT COUNT(*) FROM rpt.FactNote WHERE [Status] = 'UNSIGNED')
        AS UnsignedNotes,

    (SELECT COUNT(DISTINCT PatientSK) FROM rpt.FactAdtMovement
     WHERE MovementType = 'Admission' AND DischargeDateTime IS NULL)
        AS CurrentInpatientCensus;
