// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Polytrauma / TBI module.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end TBI screening and polytrauma registration workflows.
/// </summary>
[TestFixture]
public class PolytraumaTbiWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ITBIScreeningGrain GetScreeningGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>($"TBI-SCREEN:{id}");

    private ITBIScreeningIndexGrain GetScreeningIndex(string patientId) =>
        _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>($"TBI-SCREEN-IDX:{patientId}");

    private IPolytraumaRecordGrain GetRecordGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPolytraumaRecordGrain>($"PT-RECORD:{patientId}");

    private IPolytraumaRegistryIndexGrain GetRegistryIndex() =>
        _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>("PT-REGISTRY-IDX");

    private static List<TBIScreeningAnswer> CreateDVBICAnswers(bool q1, bool q2, bool q3, bool q4) =>
        new List<TBIScreeningAnswer>
        {
            new TBIScreeningAnswer { QuestionNumber = 1, QuestionText = "Were you injured in blast/explosion?", Answer = q1 },
            new TBIScreeningAnswer { QuestionNumber = 2, QuestionText = "Were you hit in the head?", Answer = q2 },
            new TBIScreeningAnswer { QuestionNumber = 3, QuestionText = "Did you lose consciousness?", Answer = q3 },
            new TBIScreeningAnswer { QuestionNumber = 4, QuestionText = "Do you have current symptoms?", Answer = q4 }
        };

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreening_Create_PersistsAllFields()
    {
        string screeningId = Guid.NewGuid().ToString("N");
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITBIScreeningGrain grain = GetScreeningGrain(screeningId);

        await grain.CreateScreeningAsync(
            patientId, "John Veteran",
            DateTime.UtcNow, "VA Primary Care",
            "PRV-001", "Dr. Smith",
            "Post-Deployment",
            CreateDVBICAnswers(true, true, false, true),
            "OEF/OIF veteran screening");

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("John Veteran"));
        Assert.That(state.ScreeningLocation, Is.EqualTo("VA Primary Care"));
        Assert.That(state.EncounterType, Is.EqualTo("Post-Deployment"));
        Assert.That(state.Answers, Has.Count.EqualTo(4));
        Assert.That(state.Notes, Is.EqualTo("OEF/OIF veteran screening"));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreening_Finalize_PositiveRequiresEval()
    {
        string screeningId = Guid.NewGuid().ToString("N");
        ITBIScreeningGrain grain = GetScreeningGrain(screeningId);

        await grain.CreateScreeningAsync(
            "PAT-001", "Test Patient",
            DateTime.UtcNow, "Clinic A",
            "PRV-001", "Dr. A", "Primary Care",
            CreateDVBICAnswers(true, true, true, true), null);

        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.Result, Is.EqualTo(TBIScreeningResult.PositiveRequiresEvaluation));
        Assert.That(state.TriggeredFullEvaluation, Is.True);
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreening_Finalize_Negative()
    {
        string screeningId = Guid.NewGuid().ToString("N");
        ITBIScreeningGrain grain = GetScreeningGrain(screeningId);

        await grain.CreateScreeningAsync(
            "PAT-002", "Negative Patient",
            DateTime.UtcNow, "Clinic B",
            "PRV-002", "Dr. B", "Primary Care",
            CreateDVBICAnswers(false, false, false, false), null);

        await grain.FinalizeScreeningAsync(TBIScreeningResult.Negative, false);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.Result, Is.EqualTo(TBIScreeningResult.Negative));
        Assert.That(state.TriggeredFullEvaluation, Is.False);
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreening_RecordFullEvaluation_PersistsDetails()
    {
        string screeningId = Guid.NewGuid().ToString("N");
        ITBIScreeningGrain grain = GetScreeningGrain(screeningId);

        await grain.CreateScreeningAsync(
            "PAT-003", "Eval Patient",
            DateTime.UtcNow, "Clinic C",
            "PRV-001", "Dr. Screener", "Post-Deployment",
            CreateDVBICAnswers(true, true, true, true), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        await grain.RecordFullEvaluationAsync(
            DateTime.UtcNow, "PRV-NEURO", "Dr. Neurologist", TBISeverity.Mild);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.FullEvaluationDate, Is.Not.Null);
        Assert.That(state.FullEvaluationProviderName, Is.EqualTo("Dr. Neurologist"));
        Assert.That(state.ConfirmedTBISeverity, Is.EqualTo(TBISeverity.Mild));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreeningIndex_UpsertAndGetAll()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITBIScreeningIndexGrain index = GetScreeningIndex(patientId);

        await index.UpsertScreeningAsync(new TBIScreeningSummaryEntry
        {
            ScreeningId = Guid.NewGuid().ToString("N"),
            PatientId = patientId,
            PatientName = "Index Patient",
            ScreeningDate = DateTime.UtcNow,
            Result = TBIScreeningResult.Negative,
            ScreenedById = "PRV-001",
            ScreenedByName = "Dr. A",
            TriggeredFullEvaluation = false
        });

        List<TBIScreeningSummaryEntry> all = await index.GetAllScreeningsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientName, Is.EqualTo("Index Patient"));
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task TBIScreeningIndex_GetPositive_FiltersCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ITBIScreeningIndexGrain index = GetScreeningIndex(patientId);

        await index.UpsertScreeningAsync(new TBIScreeningSummaryEntry
        {
            ScreeningId = Guid.NewGuid().ToString("N"), PatientId = patientId,
            PatientName = "Patient X", ScreeningDate = DateTime.UtcNow,
            Result = TBIScreeningResult.Negative, ScreenedById = "PRV-001",
            ScreenedByName = "Dr. A", TriggeredFullEvaluation = false
        });
        await index.UpsertScreeningAsync(new TBIScreeningSummaryEntry
        {
            ScreeningId = Guid.NewGuid().ToString("N"), PatientId = patientId,
            PatientName = "Patient X", ScreeningDate = DateTime.UtcNow,
            Result = TBIScreeningResult.PositiveRequiresEvaluation, ScreenedById = "PRV-002",
            ScreenedByName = "Dr. B", TriggeredFullEvaluation = true
        });

        List<TBIScreeningSummaryEntry> positive = await index.GetPositiveScreeningsAsync();
        Assert.That(positive, Has.Count.EqualTo(1));
        Assert.That(positive[0].Result, Is.EqualTo(TBIScreeningResult.PositiveRequiresEvaluation));
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PolytraumaRecord_Register_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPolytraumaRecordGrain grain = GetRecordGrain(patientId);

        await grain.RegisterPatientAsync(
            patientId, "Polytrauma Patient", new DateTime(1985, 4, 12),
            TraumaMechanism.BlastExplosion, new DateTime(2023, 8, 1),
            "Afghanistan", "PRC-Tampa",
            "Combat Medic Referral",
            "TEAM-001", "Polytrauma Rehab Team",
            "CM-001", "Case Manager Jones",
            "OEF veteran with blast injuries");

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("Polytrauma Patient"));
        Assert.That(state.TraumaMechanism, Is.EqualTo(TraumaMechanism.BlastExplosion));
        Assert.That(state.PolytraumaNetworkSite, Is.EqualTo("PRC-Tampa"));
        Assert.That(state.CaseManagerName, Is.EqualTo("Case Manager Jones"));
        Assert.That(state.Status, Is.EqualTo(PolytraumaStatus.Active));
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PolytraumaRecord_AddInjury_AppendsToList()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPolytraumaRecordGrain grain = GetRecordGrain(patientId);
        await grain.RegisterPatientAsync(
            patientId, "Injury Patient", null,
            TraumaMechanism.MVA, DateTime.UtcNow, "Highway 95",
            "PRC-Richmond", "ER Referral",
            "TEAM-002", "Rehab Team", "CM-002", "CM Smith", null);

        PolytraumaInjury injury = new PolytraumaInjury
        {
            InjuryId = Guid.NewGuid().ToString("N"),
            BodyRegion = BodyRegion.Head,
            InjuryDescription = "Closed head injury with LOC",
            AisScore = 3,
            SeverityScore = InjurySeverityScore.Serious,
            Notes = "CT showed small subdural hematoma"
        };
        await grain.AddInjuryAsync(injury);

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Injuries, Has.Count.EqualTo(1));
        Assert.That(state.Injuries[0].BodyRegion, Is.EqualTo(BodyRegion.Head));
        Assert.That(state.Injuries[0].AisScore, Is.EqualTo(3));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PolytraumaRecord_MultipleInjuries()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPolytraumaRecordGrain grain = GetRecordGrain(patientId);
        await grain.RegisterPatientAsync(
            patientId, "Multi-Injury Patient", null,
            TraumaMechanism.BlastExplosion, DateTime.UtcNow, "Iraq",
            "PRC-Tampa", "Medevac",
            "TEAM-001", "Team A", "CM-001", "CM A", null);

        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            InjuryId = Guid.NewGuid().ToString("N"), BodyRegion = BodyRegion.Head,
            InjuryDescription = "TBI from blast", AisScore = 4, SeverityScore = InjurySeverityScore.Severe
        });
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            InjuryId = Guid.NewGuid().ToString("N"), BodyRegion = BodyRegion.LowerExtremity,
            InjuryDescription = "Bilateral below-knee amputations", AisScore = 3, SeverityScore = InjurySeverityScore.Serious
        });
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            InjuryId = Guid.NewGuid().ToString("N"), BodyRegion = BodyRegion.Face,
            InjuryDescription = "Facial lacerations and burns", AisScore = 2, SeverityScore = InjurySeverityScore.Moderate
        });

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Injuries, Has.Count.EqualTo(3));
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PolytraumaRecord_UpdateStatus_ToInactive()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPolytraumaRecordGrain grain = GetRecordGrain(patientId);
        await grain.RegisterPatientAsync(
            patientId, "Status Patient", null,
            TraumaMechanism.Fall, DateTime.UtcNow, "Home",
            "PSC-Denver", "VA Referral",
            "TEAM-003", "Team C", "CM-003", "CM C", null);

        await grain.UpdateStatusAsync(PolytraumaStatus.Inactive, DateTime.UtcNow);

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Status, Is.EqualTo(PolytraumaStatus.Inactive));
        Assert.That(state.DeactivationDate, Is.Not.Null);
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PolytraumaRecord_UpdateTBIStatus_SetsFields()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPolytraumaRecordGrain grain = GetRecordGrain(patientId);
        await grain.RegisterPatientAsync(
            patientId, "TBI Status Patient", null,
            TraumaMechanism.BlastExplosion, DateTime.UtcNow, "Combat Zone",
            "PRC-Tampa", "Referral",
            "TEAM-001", "Team A", "CM-001", "CM A", null);

        await grain.UpdateTBIStatusAsync(true, TBISeverity.ModerateSevere);

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.HasTBI, Is.True);
        Assert.That(state.TBISeverity, Is.EqualTo(TBISeverity.ModerateSevere));
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task RegistryIndex_UpsertAndGetAll()
    {
        IPolytraumaRegistryIndexGrain index = GetRegistryIndex();

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await index.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
        {
            PatientId = patientId,
            PatientName = "Registry Patient",
            Status = PolytraumaStatus.Active,
            RegistrationDate = DateTime.UtcNow,
            PrimaryCareTeam = "Polytrauma Team",
            TBISeverity = TBISeverity.Mild,
            InjuryCount = 2,
            IssTotalScore = 18,
            LastModifiedDate = DateTime.UtcNow
        });

        List<PolytraumaRegistrySummaryEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all.Any(p => p.PatientId == patientId), Is.True);
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task RegistryIndex_GetActive_FiltersCorrectly()
    {
        IPolytraumaRegistryIndexGrain index = GetRegistryIndex();

        string activeId = $"PAT-{Guid.NewGuid():N}";
        string inactiveId = $"PAT-{Guid.NewGuid():N}";

        await index.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
        {
            PatientId = activeId, PatientName = "Active PT",
            Status = PolytraumaStatus.Active, RegistrationDate = DateTime.UtcNow,
            PrimaryCareTeam = "Team A", InjuryCount = 3, IssTotalScore = 25,
            LastModifiedDate = DateTime.UtcNow
        });
        await index.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
        {
            PatientId = inactiveId, PatientName = "Inactive PT",
            Status = PolytraumaStatus.Inactive, RegistrationDate = DateTime.UtcNow,
            PrimaryCareTeam = "Team B", InjuryCount = 1, IssTotalScore = 9,
            LastModifiedDate = DateTime.UtcNow
        });

        List<PolytraumaRegistrySummaryEntry> active = await index.GetActivePatientAsync();
        Assert.That(active.Any(p => p.PatientId == activeId), Is.True);
        Assert.That(active.All(p => p.Status == PolytraumaStatus.Active), Is.True);
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_ScreeningAndRegistration()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        string screeningId = Guid.NewGuid().ToString("N");

        // Step 1: TBI screening
        ITBIScreeningGrain screening = GetScreeningGrain(screeningId);
        await screening.CreateScreeningAsync(
            patientId, "E2E Veteran",
            DateTime.UtcNow, "VA Primary Care",
            "PRV-001", "Dr. Screener", "Post-Deployment",
            CreateDVBICAnswers(true, true, true, true), null);
        await screening.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        // Step 2: Full evaluation
        await screening.RecordFullEvaluationAsync(
            DateTime.UtcNow, "PRV-NEURO", "Dr. Neurologist", TBISeverity.ModerateSevere);

        // Step 3: Index the screening
        ITBIScreeningIndexGrain screenIndex = GetScreeningIndex(patientId);
        TBIScreeningState screenState = await screening.GetScreeningAsync();
        await screenIndex.UpsertScreeningAsync(new TBIScreeningSummaryEntry
        {
            ScreeningId = screeningId, PatientId = patientId, PatientName = "E2E Veteran",
            ScreeningDate = screenState.ScreeningDate, Result = screenState.Result,
            ScreenedById = screenState.ScreenedById, ScreenedByName = screenState.ScreenedByName,
            TriggeredFullEvaluation = true
        });

        // Step 4: Register in polytrauma
        IPolytraumaRecordGrain record = GetRecordGrain(patientId);
        await record.RegisterPatientAsync(
            patientId, "E2E Veteran", new DateTime(1990, 6, 15),
            TraumaMechanism.BlastExplosion, new DateTime(2023, 3, 1),
            "Afghanistan", "PRC-Tampa", "TBI Screening Referral",
            "TEAM-001", "Polytrauma Rehab", "CM-001", "Case Manager", null);
        await record.UpdateTBIStatusAsync(true, TBISeverity.ModerateSevere);
        await record.AddInjuryAsync(new PolytraumaInjury
        {
            InjuryId = Guid.NewGuid().ToString("N"), BodyRegion = BodyRegion.Head,
            InjuryDescription = "Moderate-severe TBI from blast",
            AisScore = 4, SeverityScore = InjurySeverityScore.Severe
        });

        // Step 5: Index in registry
        IPolytraumaRegistryIndexGrain regIndex = GetRegistryIndex();
        PolytraumaRecordState ptState = await record.GetRecordAsync();
        await regIndex.UpsertPatientAsync(new PolytraumaRegistrySummaryEntry
        {
            PatientId = patientId, PatientName = "E2E Veteran",
            Status = PolytraumaStatus.Active, RegistrationDate = ptState.RegistrationDate,
            PrimaryCareTeam = ptState.PrimaryPolytraumaTeamName,
            TBISeverity = ptState.TBISeverity, InjuryCount = ptState.Injuries.Count,
            IssTotalScore = ptState.IssTotalScore, LastModifiedDate = DateTime.UtcNow
        });

        // Verify full chain
        List<TBIScreeningSummaryEntry> positiveScreenings = await screenIndex.GetPositiveScreeningsAsync();
        Assert.That(positiveScreenings, Has.Count.EqualTo(1));
        Assert.That(positiveScreenings[0].TriggeredFullEvaluation, Is.True);

        Assert.That(ptState.HasTBI, Is.True);
        Assert.That(ptState.TBISeverity, Is.EqualTo(TBISeverity.ModerateSevere));
        Assert.That(ptState.Injuries, Has.Count.EqualTo(1));

        List<PolytraumaRegistrySummaryEntry> regAll = await regIndex.GetAllPatientsAsync();
        Assert.That(regAll.Any(p => p.PatientId == patientId), Is.True);
    }
}
