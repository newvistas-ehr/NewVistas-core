// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Substance Abuse Treatment — RPMS CDMIS (File #9002170-9002174).
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class SubstanceAbuseTreatmentWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private ISiteParametersGrain GetSiteParams()
        => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp()
    {
        // Enable the SUBSTANCE_ABUSE_TREATMENT feature flag for all tests
        await GetSiteParams().EnableFeatureAsync("SUBSTANCE_ABUSE_TREATMENT");
    }

    // ── Episode creation ────────────────────────────────────────────────────────

    [Test]
    public async Task CreateEpisode_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient,
            SubstanceType.Alcohol,
            secondarySubstances: null,
            intakeDate: new DateTime(2025, 3, 1),
            lastUseDate: new DateTime(2025, 2, 28),
            sobrietyDate: new DateTime(2025, 3, 1),
            programName: "OUTPT-ALCOHOL",
            treatmentGoals: new List<string> { "Maintain sobriety" },
            providerId: "PROV-001", providerName: "Dr. Smith",
            locationId: "LOC-001", locationName: "SA Clinic",
            notes: "Initial intake");

        Assert.That(episodeId, Does.StartWith("SA-EPISODE:"));

        List<SATreatmentEpisodeIndexEntry> all = await wf.GetSATreatmentEpisodesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].EpisodeId,        Is.EqualTo(episodeId));
        Assert.That(all[0].Status,           Is.EqualTo(SATreatmentStatus.Active));
        Assert.That(all[0].PrimarySubstance, Is.EqualTo(SubstanceType.Alcohol));
        Assert.That(all[0].Modality,         Is.EqualTo(SATreatmentModality.Outpatient));
    }

    [Test]
    public async Task CreateEpisode_OpioidMAT_FullSetup()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted,
            SubstanceType.Opioids,
            new List<SubstanceType> { SubstanceType.Benzodiazepines },
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-3), null,
            "MAT-OPIOID", new List<string> { "Taper off opioids", "Start MAT" },
            null, "Dr. Addiction Med",
            null, null, null);

        await wf.AddSAMATEntryAsync(episodeId, new MATEntry
        {
            EntryId        = $"MAT-{Guid.NewGuid()}",
            Medication     = MATMedication.BuprenorphineNaloxone,
            Dosage         = "8mg/2mg sublingual BID",
            StartDate      = DateTime.UtcNow,
            PrescriberId   = "PROV-MAT-001",
            PrescriberName = "Dr. Addiction Med",
            IsActive       = true,
        });

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.Modality,         Is.EqualTo(SATreatmentModality.MedicationAssisted));
        Assert.That(state.PrimarySubstance, Is.EqualTo(SubstanceType.Opioids));
        Assert.That(state.SecondarySubstances, Has.Count.EqualTo(1));
        Assert.That(state.MATEntries,       Has.Count.EqualTo(1));
        Assert.That(state.MATEntries[0].Medication, Is.EqualTo(MATMedication.BuprenorphineNaloxone));
        Assert.That(state.MATEntries[0].IsActive, Is.True);
    }

    // ── Active episode ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetActiveEpisode_ReturnsActiveOrReopened()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Create and discharge an episode
        string ep1 = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Detoxification, SubstanceType.Alcohol, null,
            DateTime.UtcNow.AddDays(-90), null, null,
            null, null, null, null, null, null, null);
        await wf.DischargeSATreatmentAsync(ep1, DateTime.UtcNow.AddDays(-60),
            SADischargeDisposition.CompletedTreatment, null);

        // Create a new active episode
        string ep2 = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        SATreatmentEpisodeIndexEntry? active = await wf.GetActiveSATreatmentAsync();
        Assert.That(active, Is.Not.Null);
        Assert.That(active!.EpisodeId, Is.EqualTo(ep2));
        Assert.That(active.Status, Is.EqualTo(SATreatmentStatus.Active));
    }

    // ── Episode detail ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetEpisodeDetail_ReturnsFullState()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.ResidentialInpatient,
            SubstanceType.Polysubstance,
            new List<SubstanceType> { SubstanceType.Opioids, SubstanceType.Stimulants },
            new DateTime(2025, 5, 1),
            new DateTime(2025, 4, 30), null,
            "RES-POLY", new List<string> { "Detox stabilization", "Long-term recovery plan" },
            "PROV-005", "Dr. Residential",
            "LOC-005", "Residential Treatment Facility",
            "Polysubstance use disorder — residential admission.");

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);

        Assert.That(state.PatientId,          Is.EqualTo(patientId));
        Assert.That(state.Modality,           Is.EqualTo(SATreatmentModality.ResidentialInpatient));
        Assert.That(state.PrimarySubstance,   Is.EqualTo(SubstanceType.Polysubstance));
        Assert.That(state.SecondarySubstances, Has.Count.EqualTo(2));
        Assert.That(state.ProgramName,        Is.EqualTo("RES-POLY"));
        Assert.That(state.TreatmentGoals,     Has.Count.EqualTo(2));
        Assert.That(state.ProviderName,       Is.EqualTo("Dr. Residential"));
        Assert.That(state.LocationName,       Is.EqualTo("Residential Treatment Facility"));
        Assert.That(state.Notes,              Does.Contain("Polysubstance"));
    }

    // ── MAT management ──────────────────────────────────────────────────────────

    [Test]
    public async Task AddMATEntry_AppearsOnEpisode()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string matId = $"MAT-{Guid.NewGuid()}";
        await wf.AddSAMATEntryAsync(episodeId, new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.Methadone,
            Dosage     = "40mg daily",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        });

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.MATEntries, Has.Count.EqualTo(1));
        Assert.That(state.MATEntries[0].EntryId,    Is.EqualTo(matId));
        Assert.That(state.MATEntries[0].Medication, Is.EqualTo(MATMedication.Methadone));
    }

    [Test]
    public async Task StopMATEntry_MarksInactive()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string matId = $"MAT-{Guid.NewGuid()}";
        await wf.AddSAMATEntryAsync(episodeId, new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.NaltrexoneExtendedRelease,
            Dosage     = "380mg IM monthly",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        });

        DateTime endDate = DateTime.UtcNow.AddDays(60);
        await wf.StopSAMATEntryAsync(episodeId, matId, endDate);

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.MATEntries[0].IsActive, Is.False);
        Assert.That(state.MATEntries[0].EndDate,  Is.EqualTo(endDate));
    }

    // ── Treatment goals ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddTreatmentGoal_AppearsOnEpisode()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Cannabis, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await wf.AddSATreatmentGoalAsync(episodeId, "Reduce cannabis use");
        await wf.AddSATreatmentGoalAsync(episodeId, "Improve sleep hygiene");

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.TreatmentGoals, Has.Count.EqualTo(2));
        Assert.That(state.TreatmentGoals, Contains.Item("Reduce cannabis use"));
        Assert.That(state.TreatmentGoals, Contains.Item("Improve sleep hygiene"));
    }

    // ── Discharge & reopen ──────────────────────────────────────────────────────

    [Test]
    public async Task DischargeEpisode_SyncsIndex_StopsMAT()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await wf.AddSAMATEntryAsync(episodeId, new MATEntry
        {
            EntryId    = $"MAT-{Guid.NewGuid()}",
            Medication = MATMedication.Buprenorphine,
            Dosage     = "16mg daily",
            StartDate  = DateTime.UtcNow,
            IsActive   = true,
        });

        DateTime dischargeDate = DateTime.UtcNow.AddDays(90);
        await wf.DischargeSATreatmentAsync(episodeId, dischargeDate,
            SADischargeDisposition.CompletedTreatment, "Successfully completed program.");

        // Verify grain state
        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.Status,               Is.EqualTo(SATreatmentStatus.Discharged));
        Assert.That(state.DischargeDate,        Is.EqualTo(dischargeDate));
        Assert.That(state.DischargeDisposition, Is.EqualTo(SADischargeDisposition.CompletedTreatment));
        Assert.That(state.MATEntries[0].IsActive, Is.False);

        // Verify index synced
        List<SATreatmentEpisodeIndexEntry> index = await wf.GetSATreatmentEpisodesAsync();
        Assert.That(index[0].Status,        Is.EqualTo(SATreatmentStatus.Discharged));
        Assert.That(index[0].DischargeDate, Is.EqualTo(dischargeDate));

        SATreatmentEpisodeIndexEntry? active = await wf.GetActiveSATreatmentAsync();
        Assert.That(active, Is.Null);
    }

    [Test]
    public async Task ReopenEpisode_ClearsDischarge_SyncsIndex()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Stimulants, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await wf.DischargeSATreatmentAsync(episodeId, DateTime.UtcNow,
            SADischargeDisposition.PatientDroppedOut, null);

        await wf.ReopenSATreatmentAsync(episodeId, "Patient returned for continued treatment.");

        SATreatmentEpisodeState state = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(state.Status,               Is.EqualTo(SATreatmentStatus.Reopened));
        Assert.That(state.DischargeDate,        Is.Null);
        Assert.That(state.DischargeDisposition, Is.Null);

        List<SATreatmentEpisodeIndexEntry> index = await wf.GetSATreatmentEpisodesAsync();
        Assert.That(index[0].Status,        Is.EqualTo(SATreatmentStatus.Reopened));
        Assert.That(index[0].DischargeDate, Is.Null);

        SATreatmentEpisodeIndexEntry? active = await wf.GetActiveSATreatmentAsync();
        Assert.That(active, Is.Not.Null);
        Assert.That(active!.Status, Is.EqualTo(SATreatmentStatus.Reopened));
    }

    // ── Visit workflows ─────────────────────────────────────────────────────────

    [Test]
    public async Task CreateSAVisit_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string visitId = await wf.CreateSAVisitAsync(
            episodeId, DateTime.UtcNow,
            SAVisitType.Individual, 60,
            null, null, 14, 4,
            "PROV-V01", "Dr. Counselor", "Initial individual session");

        Assert.That(visitId, Does.StartWith("SA-VISIT:"));

        List<SAVisitIndexEntry> visits = await wf.GetSAVisitsAsync(episodeId);
        Assert.That(visits, Has.Count.EqualTo(1));
        Assert.That(visits[0].VisitId,   Is.EqualTo(visitId));
        Assert.That(visits[0].VisitType, Is.EqualTo(SAVisitType.Individual));
    }

    [Test]
    public async Task CreateMultipleVisits_OrderedNewestFirst()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.IntensiveOutpatient, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string v1 = await wf.CreateSAVisitAsync(
            episodeId, new DateTime(2025, 3, 1),
            SAVisitType.Individual, 60,
            null, null, null, null, null, "Dr. A", null);

        string v2 = await wf.CreateSAVisitAsync(
            episodeId, new DateTime(2025, 3, 8),
            SAVisitType.Group, 90,
            null, null, null, null, null, "Dr. B", null);

        string v3 = await wf.CreateSAVisitAsync(
            episodeId, new DateTime(2025, 3, 15),
            SAVisitType.UrineDrugScreen, 15,
            "NEGATIVE", null, null, null, null, null, null);

        List<SAVisitIndexEntry> visits = await wf.GetSAVisitsAsync(episodeId);
        Assert.That(visits, Has.Count.EqualTo(3));
        Assert.That(visits[0].VisitId, Is.EqualTo(v3));
        Assert.That(visits[2].VisitId, Is.EqualTo(v1));
    }

    [Test]
    public async Task GetVisitDetail_ReturnsFullState()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        DateTime visitDate = new DateTime(2025, 4, 10, 10, 0, 0);
        string visitId = await wf.CreateSAVisitAsync(
            episodeId, visitDate,
            SAVisitType.Family, 90,
            null, null, 60, 2,
            "PROV-FAM", "Dr. Family Therapist",
            "Family session — improving communication.");

        SAVisitState state = await wf.GetSAVisitAsync(visitId);

        Assert.That(state.EpisodeId,       Is.EqualTo(episodeId));
        Assert.That(state.PatientId,       Is.EqualTo(patientId));
        Assert.That(state.VisitDate,       Is.EqualTo(visitDate));
        Assert.That(state.VisitType,       Is.EqualTo(SAVisitType.Family));
        Assert.That(state.DurationMinutes, Is.EqualTo(90));
        Assert.That(state.DaysSinceLastUse, Is.EqualTo(60));
        Assert.That(state.CravingLevel,    Is.EqualTo(2));
        Assert.That(state.ProviderName,    Is.EqualTo("Dr. Family Therapist"));
    }

    [Test]
    public async Task GetVisitCount_ReturnsCorrectCount()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        Assert.That(await wf.GetSAVisitCountAsync(episodeId), Is.EqualTo(0));

        await wf.CreateSAVisitAsync(episodeId, DateTime.UtcNow,
            SAVisitType.Individual, 60, null, null, null, null, null, null, null);
        await wf.CreateSAVisitAsync(episodeId, DateTime.UtcNow.AddDays(7),
            SAVisitType.Group, 90, null, null, null, null, null, null, null);

        Assert.That(await wf.GetSAVisitCountAsync(episodeId), Is.EqualTo(2));
    }

    // ── UDS visit tracking ──────────────────────────────────────────────────────

    [Test]
    public async Task UDSVisit_TracksSubstancesDetected()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        string visitId = await wf.CreateSAVisitAsync(
            episodeId, DateTime.UtcNow,
            SAVisitType.UrineDrugScreen, 15,
            "POSITIVE",
            new List<string> { "Opioids", "Amphetamines" },
            null, null,
            null, "Lab Tech", "Unexpected positive — provider notified.");

        SAVisitState state = await wf.GetSAVisitAsync(visitId);

        Assert.That(state.VisitType,             Is.EqualTo(SAVisitType.UrineDrugScreen));
        Assert.That(state.UdsResult,             Is.EqualTo("POSITIVE"));
        Assert.That(state.UdsSubstancesDetected, Has.Count.EqualTo(2));
        Assert.That(state.UdsSubstancesDetected, Contains.Item("Opioids"));
        Assert.That(state.UdsSubstancesDetected, Contains.Item("Amphetamines"));
    }

    // ── Independent patients ────────────────────────────────────────────────────

    [Test]
    public async Task MultiplePatients_IndependentRecords()
    {
        string p1 = $"SA-PAT-{Guid.NewGuid()}";
        string p2 = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Outpatient, SubstanceType.Alcohol, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await wf2.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.MedicationAssisted, SubstanceType.Opioids, null,
            DateTime.UtcNow, null, null,
            null, null, null, null, null, null, null);

        await wf2.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.Detoxification, SubstanceType.Stimulants, null,
            DateTime.UtcNow.AddDays(-30), null, null,
            null, null, null, null, null, null, null);

        List<SATreatmentEpisodeIndexEntry> p1Episodes = await wf1.GetSATreatmentEpisodesAsync();
        List<SATreatmentEpisodeIndexEntry> p2Episodes = await wf2.GetSATreatmentEpisodesAsync();

        Assert.That(p1Episodes, Has.Count.EqualTo(1));
        Assert.That(p2Episodes, Has.Count.EqualTo(2));
    }

    // ── Full lifecycle ──────────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_IntakeToDischarge()
    {
        string patientId = $"SA-PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // 1. Create episode
        string episodeId = await wf.CreateSATreatmentEpisodeAsync(
            SATreatmentModality.IntensiveOutpatient,
            SubstanceType.Opioids,
            new List<SubstanceType> { SubstanceType.Benzodiazepines },
            new DateTime(2025, 1, 1),
            new DateTime(2024, 12, 31), null,
            "IOP-OPIOID", null,
            "PROV-LC", "Dr. Lifecycle",
            "LOC-LC", "IOP Center", null);

        // 2. Add MAT
        string matId = $"MAT-{Guid.NewGuid()}";
        await wf.AddSAMATEntryAsync(episodeId, new MATEntry
        {
            EntryId    = matId,
            Medication = MATMedication.BuprenorphineNaloxone,
            Dosage     = "8mg/2mg sublingual daily",
            StartDate  = new DateTime(2025, 1, 2),
            IsActive   = true,
        });

        // 3. Add treatment goals
        await wf.AddSATreatmentGoalAsync(episodeId, "Achieve 30-day sobriety");
        await wf.AddSATreatmentGoalAsync(episodeId, "Complete IOP curriculum");

        // 4. Record visits
        await wf.CreateSAVisitAsync(episodeId, new DateTime(2025, 1, 3),
            SAVisitType.Assessment, 90, null, null, 3, 8,
            null, "Dr. Lifecycle", "Initial assessment");

        await wf.CreateSAVisitAsync(episodeId, new DateTime(2025, 1, 10),
            SAVisitType.Individual, 60, null, null, 10, 5,
            null, "Dr. Lifecycle", null);

        await wf.CreateSAVisitAsync(episodeId, new DateTime(2025, 1, 17),
            SAVisitType.UrineDrugScreen, 15,
            "NEGATIVE", null, null, null, null, null, "Clean UDS");

        // 5. Verify mid-treatment state
        SATreatmentEpisodeState midState = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(midState.Status,         Is.EqualTo(SATreatmentStatus.Active));
        Assert.That(midState.MATEntries,     Has.Count.EqualTo(1));
        Assert.That(midState.TreatmentGoals, Has.Count.EqualTo(2));

        int visitCount = await wf.GetSAVisitCountAsync(episodeId);
        Assert.That(visitCount, Is.EqualTo(3));

        SATreatmentEpisodeIndexEntry? active = await wf.GetActiveSATreatmentAsync();
        Assert.That(active, Is.Not.Null);

        // 6. Discharge
        DateTime dischargeDate = new DateTime(2025, 4, 1);
        await wf.DischargeSATreatmentAsync(episodeId, dischargeDate,
            SADischargeDisposition.CompletedTreatment,
            "Successfully completed IOP — 90 days sober.");

        SATreatmentEpisodeState finalState = await wf.GetSATreatmentEpisodeAsync(episodeId);
        Assert.That(finalState.Status,               Is.EqualTo(SATreatmentStatus.Discharged));
        Assert.That(finalState.DischargeDate,        Is.EqualTo(dischargeDate));
        Assert.That(finalState.DischargeDisposition, Is.EqualTo(SADischargeDisposition.CompletedTreatment));
        Assert.That(finalState.MATEntries[0].IsActive, Is.False);
        Assert.That(finalState.Notes,                Does.Contain("90 days sober"));

        // No active episode after discharge
        SATreatmentEpisodeIndexEntry? postDischarge = await wf.GetActiveSATreatmentAsync();
        Assert.That(postDischarge, Is.Null);
    }
}
