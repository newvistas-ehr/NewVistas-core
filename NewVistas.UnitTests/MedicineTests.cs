// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// MedProcedureGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class MedProcedureGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IMedProcedureGrain NewProc() =>
        _cluster.GrainFactory.GetGrain<IMedProcedureGrain>($"MED-PROC:{Guid.NewGuid()}");

    // ── Order / Basic ─────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_CanOrderProcedure()
    {
        IMedProcedureGrain grain = NewProc();
        DateTime ordered = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.OrderProcedureAsync(
            "PAT-001", MedProcedureCategory.Electrocardiogram,
            "93000", "Routine ECG, 12-lead", ordered,
            "PROV-001", "Dr. Heart", "LOC-CARD", "Cardiology Clinic", "Chest pain evaluation");

        MedProcedureState state = await grain.GetProcedureAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Category, Is.EqualTo(MedProcedureCategory.Electrocardiogram));
        Assert.That(state.ProcedureCode, Is.EqualTo("93000"));
        Assert.That(state.ProcedureDescription, Is.EqualTo("Routine ECG, 12-lead"));
        Assert.That(state.OrderedDate, Is.EqualTo(ordered));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Heart"));
        Assert.That(state.LocationName, Is.EqualTo("Cardiology Clinic"));
        Assert.That(state.Indication, Is.EqualTo("Chest pain evaluation"));
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Ordered));
    }

    [Test]
    public async Task ProcedureGrain_ProcedureId_MatchesGrainKey()
    {
        string key = $"MED-PROC:{Guid.NewGuid()}";
        IMedProcedureGrain grain = _cluster.GrainFactory.GetGrain<IMedProcedureGrain>(key);
        await grain.OrderProcedureAsync(
            "PAT-002", MedProcedureCategory.PulmonaryFunction,
            "94010", "Spirometry", DateTime.UtcNow,
            null, null, null, null, null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.ProcedureId, Is.EqualTo(key));
    }

    [Test]
    public async Task ProcedureGrain_DefaultStatus_IsOrdered()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-003", MedProcedureCategory.GIEndoscopy,
            "45378", "Colonoscopy", DateTime.UtcNow,
            null, null, null, null, null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Ordered));
    }

    // ── Schedule ─────────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_ScheduleProcedure_SetsScheduledDateAndStatus()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-004", MedProcedureCategory.Cardiology,
            "93306", "Echocardiography, complete", DateTime.UtcNow,
            null, null, null, null, null);

        DateTime sched = new DateTime(2025, 7, 15, 9, 0, 0, DateTimeKind.Utc);
        await grain.ScheduleProcedureAsync(sched);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Scheduled));
        Assert.That(state.ScheduledDate, Is.EqualTo(sched));
    }

    // ── Complete ─────────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_CompleteProcedure_SetsStatusAndFields()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-005", MedProcedureCategory.Electrocardiogram,
            "93000", "ECG 12-lead", DateTime.UtcNow,
            null, null, null, null, null);

        DateTime performed = new DateTime(2025, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        await grain.CompleteProcedureAsync(performed, "Normal sinus rhythm.", "ECG within normal limits.", null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Completed));
        Assert.That(state.PerformedDate, Is.EqualTo(performed));
        Assert.That(state.Findings, Is.EqualTo("Normal sinus rhythm."));
        Assert.That(state.Impression, Is.EqualTo("ECG within normal limits."));
    }

    // ── Cancel ───────────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_CancelProcedure_SetsStatusAndReason()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-006", MedProcedureCategory.GIEndoscopy,
            "45378", "Colonoscopy", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.CancelProcedureAsync("Patient declined procedure.");

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient declined procedure."));
    }

    [Test]
    public async Task ProcedureGrain_CancelProcedure_NullReasonIsAllowed()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-007", MedProcedureCategory.Other,
            "99213", "Office visit", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.CancelProcedureAsync(null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.Status, Is.EqualTo(MedProcedureStatus.Cancelled));
        Assert.That(state.CancellationReason, Is.Null);
    }

    // ── ECG Results ──────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordEcgResults_StoresAllFields()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-008", MedProcedureCategory.Electrocardiogram,
            "93000", "ECG 12-lead", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEcgResultsAsync(
            rate: 72,
            rhythm: CardiacRhythm.Normal,
            prIntervalMs: 160,
            qrsDurationMs: 88,
            qtcMs: 420,
            axisDegrees: 45,
            interpretation: "Normal sinus rhythm. No ischemic changes.",
            isNormal: true);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EcgRate, Is.EqualTo(72));
        Assert.That(state.EcgRhythm, Is.EqualTo(CardiacRhythm.Normal));
        Assert.That(state.EcgPrIntervalMs, Is.EqualTo(160));
        Assert.That(state.EcgQrsDurationMs, Is.EqualTo(88));
        Assert.That(state.EcgQtcMs, Is.EqualTo(420));
        Assert.That(state.EcgAxisDegrees, Is.EqualTo(45));
        Assert.That(state.EcgInterpretation, Is.EqualTo("Normal sinus rhythm. No ischemic changes."));
        Assert.That(state.EcgIsNormal, Is.True);
    }

    [Test]
    public async Task ProcedureGrain_RecordEcgResults_AtrialFibrillation()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-009", MedProcedureCategory.Electrocardiogram,
            "93000", "ECG 12-lead", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEcgResultsAsync(
            rate: 110, rhythm: CardiacRhythm.AtrialFibrillation,
            prIntervalMs: null, qrsDurationMs: 90, qtcMs: 430,
            axisDegrees: null, interpretation: "Atrial fibrillation with rapid ventricular response.", isNormal: false);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EcgRhythm, Is.EqualTo(CardiacRhythm.AtrialFibrillation));
        Assert.That(state.EcgIsNormal, Is.False);
        Assert.That(state.EcgPrIntervalMs, Is.Null);
    }

    // ── Echo Results ─────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordEchoResults_StoresLvef()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-010", MedProcedureCategory.Cardiology,
            "93306", "Echocardiography, complete", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEchoResultsAsync(
            lvEjectionFraction: 60m,
            lvDiastolicFunction: "Grade I diastolic dysfunction",
            valvularFindings: "Mild mitral regurgitation");

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.LvEjectionFraction, Is.EqualTo(60m));
        Assert.That(state.LvDiastolicFunction, Is.EqualTo("Grade I diastolic dysfunction"));
        Assert.That(state.ValvularFindings, Is.EqualTo("Mild mitral regurgitation"));
    }

    [Test]
    public async Task ProcedureGrain_RecordEchoResults_NullFieldsAllowed()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-011", MedProcedureCategory.Cardiology,
            "93306", "Echo", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEchoResultsAsync(55m, null, null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.LvEjectionFraction, Is.EqualTo(55m));
        Assert.That(state.LvDiastolicFunction, Is.Null);
    }

    // ── Stress Test Results ───────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordStressTestResults_StoresFields()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-012", MedProcedureCategory.Cardiology,
            "93015", "Cardiovascular stress test", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordStressTestResultsAsync(peakMets: 9.2m, targetHeartRatePct: 94m, inducibleIschemia: false);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.PeakMets, Is.EqualTo(9.2m));
        Assert.That(state.TargetHeartRatePct, Is.EqualTo(94m));
        Assert.That(state.InducibleIschemia, Is.False);
    }

    [Test]
    public async Task ProcedureGrain_RecordStressTestResults_PositiveIschemia()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-013", MedProcedureCategory.Cardiology,
            "93015", "Stress test", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordStressTestResultsAsync(5.1m, 78m, true);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.InducibleIschemia, Is.True);
        Assert.That(state.PeakMets, Is.EqualTo(5.1m));
    }

    // ── PFT Results ──────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordPftResults_StoresSpirometryFields()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-014", MedProcedureCategory.PulmonaryFunction,
            "94010", "Spirometry", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordPftResultsAsync(
            fev1: 2.8m, fev1PctPredicted: 88m,
            fvc: 3.5m, fvcPctPredicted: 91m,
            fev1FvcRatio: 0.80m,
            dlco: 22m, dlcoPctPredicted: 85m,
            tlc: 5.1m, rv: 1.6m,
            obstructive: false, restrictive: false, bronchodilatorResponse: false);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.PftFev1, Is.EqualTo(2.8m));
        Assert.That(state.PftFev1PctPredicted, Is.EqualTo(88m));
        Assert.That(state.PftFvc, Is.EqualTo(3.5m));
        Assert.That(state.PftFev1FvcRatio, Is.EqualTo(0.80m));
        Assert.That(state.PftDlco, Is.EqualTo(22m));
        Assert.That(state.PftObstructive, Is.False);
        Assert.That(state.PftRestrictive, Is.False);
    }

    [Test]
    public async Task ProcedureGrain_RecordPftResults_ObstructivePattern()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-015", MedProcedureCategory.PulmonaryFunction,
            "94010", "Spirometry", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordPftResultsAsync(
            fev1: 1.2m, fev1PctPredicted: 42m,
            fvc: 2.8m, fvcPctPredicted: 76m,
            fev1FvcRatio: 0.43m,
            dlco: null, dlcoPctPredicted: null,
            tlc: null, rv: null,
            obstructive: true, restrictive: false, bronchodilatorResponse: true);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.PftObstructive, Is.True);
        Assert.That(state.PftBronchodilatorResponse, Is.True);
        Assert.That(state.PftFev1FvcRatio, Is.EqualTo(0.43m));
    }

    // ── ABG Results ──────────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordAbgResults_StoresAllValues()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-016", MedProcedureCategory.PulmonaryFunction,
            "82803", "Arterial blood gas", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordAbgResultsAsync(ph: 7.40m, pao2: 95m, paco2: 40m, hco3: 24m, sao2: 97m);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.AbgPh, Is.EqualTo(7.40m));
        Assert.That(state.AbgPao2, Is.EqualTo(95m));
        Assert.That(state.AbgPaco2, Is.EqualTo(40m));
        Assert.That(state.AbgHco3, Is.EqualTo(24m));
        Assert.That(state.AbgSao2, Is.EqualTo(97m));
    }

    [Test]
    public async Task ProcedureGrain_RecordAbgResults_AcidemiaHypoxia()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-017", MedProcedureCategory.PulmonaryFunction,
            "82803", "ABG", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordAbgResultsAsync(7.28m, 52m, 55m, 25m, 86m);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.AbgPh, Is.LessThan(7.35m));
        Assert.That(state.AbgPao2, Is.LessThan(60m));
    }

    // ── Endoscopy Results ────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_RecordEndoscopyResults_Colonoscopy()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-018", MedProcedureCategory.GIEndoscopy,
            "45378", "Colonoscopy", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEndoscopyResultsAsync(
            endoscopyType: EndoscopyType.Colonoscopy,
            bowelPrepQuality: BowelPrepQuality.Good,
            cecumReached: true,
            scopeAdvancedCm: 150,
            biopsyTaken: true,
            biopsySites: new List<string> { "Sigmoid polyp", "Cecal polyp" },
            polypCount: 2,
            polypDescriptions: new List<string> { "5mm pedunculated sigmoid", "4mm sessile cecal" },
            endoscopicInterventions: new List<string> { "Polypectomy x2" });

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EndoscopyType, Is.EqualTo(EndoscopyType.Colonoscopy));
        Assert.That(state.BowelPrepQuality, Is.EqualTo(BowelPrepQuality.Good));
        Assert.That(state.CecumReached, Is.True);
        Assert.That(state.BiopsyTaken, Is.True);
        Assert.That(state.BiopsySites, Has.Count.EqualTo(2));
        Assert.That(state.PolypCount, Is.EqualTo(2));
        Assert.That(state.PolypDescriptions, Has.Count.EqualTo(2));
        Assert.That(state.EndoscopicInterventions, Contains.Item("Polypectomy x2"));
    }

    [Test]
    public async Task ProcedureGrain_RecordEndoscopyResults_EGD_NoPolyps()
    {
        IMedProcedureGrain grain = NewProc();
        await grain.OrderProcedureAsync(
            "PAT-019", MedProcedureCategory.GIEndoscopy,
            "43239", "EGD", DateTime.UtcNow,
            null, null, null, null, null);

        await grain.RecordEndoscopyResultsAsync(
            EndoscopyType.EGD, null, null, null, false, null, 0, null, null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.EndoscopyType, Is.EqualTo(EndoscopyType.EGD));
        Assert.That(state.BiopsyTaken, Is.False);
        Assert.That(state.PolypCount, Is.EqualTo(0));
        Assert.That(state.BiopsySites, Is.Empty);
    }

    // ── LastModifiedDate ─────────────────────────────────────────────────

    [Test]
    public async Task ProcedureGrain_LastModifiedDate_UpdatedOnEveryWrite()
    {
        IMedProcedureGrain grain = NewProc();
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        await grain.OrderProcedureAsync(
            "PAT-020", MedProcedureCategory.Electrocardiogram,
            "93000", "ECG", DateTime.UtcNow,
            null, null, null, null, null);

        MedProcedureState state = await grain.GetProcedureAsync();
        Assert.That(state.LastModifiedDate, Is.GreaterThanOrEqualTo(before));
        Assert.That(state.CreatedDate, Is.GreaterThanOrEqualTo(before));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MedProcedureIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class MedProcedureIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IMedProcedureIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IMedProcedureIndexGrain>($"MED-PROC-IDX:{Guid.NewGuid()}");

    private static MedProcedureIndexEntry MakeEntry(
        string procedureId,
        MedProcedureCategory category = MedProcedureCategory.Electrocardiogram,
        MedProcedureStatus status = MedProcedureStatus.Ordered,
        DateTime? orderedDate = null) => new()
        {
            ProcedureId          = procedureId,
            Category             = category,
            ProcedureCode        = "93000",
            ProcedureDescription = "ECG 12-lead",
            Status               = status,
            OrderedDate          = orderedDate ?? DateTime.UtcNow,
            PerformedDate        = status == MedProcedureStatus.Completed ? DateTime.UtcNow : null,
            ProviderName         = "Dr. Test",
            LocationName         = "Cardiology",
            Impression           = status == MedProcedureStatus.Completed ? "Normal" : null
        };

    [Test]
    public async Task IndexGrain_EmptyIndex_ReturnsEmptyList()
    {
        IMedProcedureIndexGrain index = NewIndex();
        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_UpsertProcedure_AppearsInGetAll()
    {
        IMedProcedureIndexGrain index = NewIndex();
        string procId = $"MED-PROC:{Guid.NewGuid()}";
        await index.UpsertProcedureAsync(MakeEntry(procId));

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ProcedureId, Is.EqualTo(procId));
    }

    [Test]
    public async Task IndexGrain_UpsertProcedure_UpdatesExistingEntry()
    {
        IMedProcedureIndexGrain index = NewIndex();
        string procId = $"MED-PROC:{Guid.NewGuid()}";
        await index.UpsertProcedureAsync(MakeEntry(procId, status: MedProcedureStatus.Ordered));
        await index.UpsertProcedureAsync(MakeEntry(procId, status: MedProcedureStatus.Completed));

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(MedProcedureStatus.Completed));
    }

    [Test]
    public async Task IndexGrain_GetProceduresByCategory_FiltersCorrectly()
    {
        IMedProcedureIndexGrain index = NewIndex();
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.Electrocardiogram));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.PulmonaryFunction));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.Electrocardiogram));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.GIEndoscopy));

        List<MedProcedureIndexEntry> ecgs = await index.GetProceduresByCategoryAsync(MedProcedureCategory.Electrocardiogram);
        Assert.That(ecgs, Has.Count.EqualTo(2));
        Assert.That(ecgs.All(p => p.Category == MedProcedureCategory.Electrocardiogram), Is.True);

        List<MedProcedureIndexEntry> pfts = await index.GetProceduresByCategoryAsync(MedProcedureCategory.PulmonaryFunction);
        Assert.That(pfts, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_GetCompletedProcedures_FiltersCorrectly()
    {
        IMedProcedureIndexGrain index = NewIndex();
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", status: MedProcedureStatus.Completed));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", status: MedProcedureStatus.Ordered));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", status: MedProcedureStatus.Scheduled));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", status: MedProcedureStatus.Completed));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", status: MedProcedureStatus.Cancelled));

        List<MedProcedureIndexEntry> completed = await index.GetCompletedProceduresAsync();
        Assert.That(completed, Has.Count.EqualTo(2));
        Assert.That(completed.All(p => p.Status == MedProcedureStatus.Completed), Is.True);
    }

    [Test]
    public async Task IndexGrain_GetAllProcedures_OrderedByOrderedDateDescending()
    {
        IMedProcedureIndexGrain index = NewIndex();
        DateTime older = DateTime.UtcNow.AddMonths(-3);
        DateTime newer = DateTime.UtcNow.AddMonths(-1);
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", orderedDate: older));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", orderedDate: newer));

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all[0].OrderedDate, Is.EqualTo(newer).Within(TimeSpan.FromSeconds(1)));
        Assert.That(all[1].OrderedDate, Is.EqualTo(older).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task IndexGrain_RemoveProcedure_RemovesFromIndex()
    {
        IMedProcedureIndexGrain index = NewIndex();
        string procId = $"MED-PROC:{Guid.NewGuid()}";
        await index.UpsertProcedureAsync(MakeEntry(procId));
        await index.RemoveProcedureAsync(procId);

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task IndexGrain_RemoveNonExistentProcedure_IsIdempotent()
    {
        IMedProcedureIndexGrain index = NewIndex();
        string procId = $"MED-PROC:{Guid.NewGuid()}";
        await index.UpsertProcedureAsync(MakeEntry(procId));

        Assert.DoesNotThrowAsync(() => index.RemoveProcedureAsync($"MED-PROC:{Guid.NewGuid()}"));

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_MultipleProceduresAndCategories_AllReturnedInGetAll()
    {
        IMedProcedureIndexGrain index = NewIndex();
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.Electrocardiogram));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.Cardiology));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.PulmonaryFunction));
        await index.UpsertProcedureAsync(MakeEntry($"MED-PROC:{Guid.NewGuid()}", MedProcedureCategory.GIEndoscopy));

        List<MedProcedureIndexEntry> all = await index.GetAllProceduresAsync();
        Assert.That(all, Has.Count.EqualTo(4));
    }
}
