// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ── TBIScreeningGrain Tests ──────────────────────────────────────────────────

[TestFixture]
public class TBIScreeningGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static List<TBIScreeningAnswer> MakeAnswers(bool q1, bool q2, bool q3, bool q4) => new()
    {
        new() { QuestionNumber = 1, QuestionText = "Head injury?", Answer = q1 },
        new() { QuestionNumber = 2, QuestionText = "LOC/confusion?", Answer = q2 },
        new() { QuestionNumber = 3, QuestionText = "Persistent symptoms?", Answer = q3 },
        new() { QuestionNumber = 4, QuestionText = "Current symptoms?", Answer = q4 },
    };

    [Test]
    public async Task TBIScreeningGrain_CanCreateScreening()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-001", "John Doe", DateTime.UtcNow, "Primary Care",
            "PROV-1", "Dr. Smith", "Post-Deployment", MakeAnswers(true, true, false, false), null);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Doe"));
    }

    [Test]
    public async Task TBIScreeningGrain_ScreeningIdMatchesGrainKey()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-002", "Jane Doe", DateTime.UtcNow, "Telehealth",
            "PROV-2", "Dr. Jones", "Primary Care", MakeAnswers(false, false, false, false), null);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.ScreeningId, Is.EqualTo(key));
    }

    [Test]
    public async Task TBIScreeningGrain_PositiveAnswerCountCalculated()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-003", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-3", "Dr. Test", "Specialty", MakeAnswers(true, false, true, true), null);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.PositiveAnswerCount, Is.EqualTo(3));
    }

    [Test]
    public async Task TBIScreeningGrain_CanFinalizeAsNegative()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-004", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-4", "Dr. Test", "Primary Care", MakeAnswers(false, false, false, false), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.Negative, false);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.Result, Is.EqualTo(TBIScreeningResult.Negative));
    }

    [Test]
    public async Task TBIScreeningGrain_CanFinalizeAsPositive()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-005", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-5", "Dr. Test", "Post-Deployment", MakeAnswers(true, true, true, true), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.Result, Is.EqualTo(TBIScreeningResult.PositiveRequiresEvaluation));
    }

    [Test]
    public async Task TBIScreeningGrain_PositiveScreeningTriggersFullEvaluation()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-006", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-6", "Dr. Test", "Specialty", MakeAnswers(true, true, false, true), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.TriggeredFullEvaluation, Is.True);
    }

    [Test]
    public async Task TBIScreeningGrain_NegativeScreeningDoesNotTriggerFullEvaluation()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-007", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-7", "Dr. Test", "Primary Care", MakeAnswers(false, false, false, false), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.Negative, false);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.TriggeredFullEvaluation, Is.False);
    }

    [Test]
    public async Task TBIScreeningGrain_CanRecordFullEvaluation()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-008", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-8", "Dr. Test", "Post-Deployment", MakeAnswers(true, true, true, false), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);

        DateTime evalDate = DateTime.UtcNow.AddDays(7);
        await grain.RecordFullEvaluationAsync(evalDate, "NEUROLOGIST-1", "Dr. Neuro", TBISeverity.Mild);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.FullEvaluationProviderId, Is.EqualTo("NEUROLOGIST-1"));
        Assert.That(state.FullEvaluationProviderName, Is.EqualTo("Dr. Neuro"));
    }

    [Test]
    public async Task TBIScreeningGrain_FullEvaluationSetsSeverity()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        await grain.CreateScreeningAsync("PAT-009", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-9", "Dr. Test", "Specialty", MakeAnswers(true, true, true, true), null);
        await grain.FinalizeScreeningAsync(TBIScreeningResult.PositiveRequiresEvaluation, true);
        await grain.RecordFullEvaluationAsync(DateTime.UtcNow.AddDays(5), "PROV-9", "Dr. Test", TBISeverity.ModerateSevere);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.ConfirmedTBISeverity, Is.EqualTo(TBISeverity.ModerateSevere));
    }

    [Test]
    public async Task TBIScreeningGrain_CreatedDateSet()
    {
        string key = $"TBI-SCREEN:{Guid.NewGuid()}";
        ITBIScreeningGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningGrain>(key);
        DateTime before = DateTime.UtcNow.AddSeconds(-1);
        await grain.CreateScreeningAsync("PAT-010", "Test Patient", DateTime.UtcNow, "Clinic",
            "PROV-10", "Dr. Test", "Primary Care", MakeAnswers(false, false, false, false), null);

        TBIScreeningState state = await grain.GetScreeningAsync();
        Assert.That(state.CreatedDate, Is.GreaterThan(before));
    }
}

// ── TBIScreeningIndexGrain Tests ─────────────────────────────────────────────

[TestFixture]
public class TBIScreeningIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static TBIScreeningSummaryEntry MakeEntry(string screeningId, string patientId,
        TBIScreeningResult result, DateTime? date = null) => new()
    {
        ScreeningId = screeningId,
        PatientId = patientId,
        PatientName = "Test Patient",
        ScreeningDate = date ?? DateTime.UtcNow,
        Result = result,
        ScreenedById = "PROV-1",
        ScreenedByName = "Dr. Test",
        TriggeredFullEvaluation = result == TBIScreeningResult.PositiveRequiresEvaluation
    };

    [Test]
    public async Task TBIScreeningIndex_EmptyOnStart()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task TBIScreeningIndex_CanUpsertAndRetrieve()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        await grain.UpsertScreeningAsync(MakeEntry("SCR-1", "PAT-1", TBIScreeningResult.Negative));

        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ScreeningId, Is.EqualTo("SCR-1"));
    }

    [Test]
    public async Task TBIScreeningIndex_OrderedNewestFirst()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        DateTime older = DateTime.UtcNow.AddDays(-10);
        DateTime newer = DateTime.UtcNow;
        await grain.UpsertScreeningAsync(MakeEntry("SCR-OLD", "PAT-2", TBIScreeningResult.Negative, older));
        await grain.UpsertScreeningAsync(MakeEntry("SCR-NEW", "PAT-2", TBIScreeningResult.Negative, newer));

        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all[0].ScreeningId, Is.EqualTo("SCR-NEW"));
    }

    [Test]
    public async Task TBIScreeningIndex_GetPositiveFilters()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        await grain.UpsertScreeningAsync(MakeEntry("SCR-NEG", "PAT-3", TBIScreeningResult.Negative));
        await grain.UpsertScreeningAsync(MakeEntry("SCR-POS", "PAT-3", TBIScreeningResult.PositiveRequiresEvaluation));

        List<TBIScreeningSummaryEntry> positives = await grain.GetPositiveScreeningsAsync();
        Assert.That(positives, Has.Count.EqualTo(1));
        Assert.That(positives[0].ScreeningId, Is.EqualTo("SCR-POS"));
    }

    [Test]
    public async Task TBIScreeningIndex_UpsertUpdatesExisting()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        await grain.UpsertScreeningAsync(MakeEntry("SCR-UPD", "PAT-4", TBIScreeningResult.Inconclusive));

        TBIScreeningSummaryEntry updated = MakeEntry("SCR-UPD", "PAT-4", TBIScreeningResult.PositiveRequiresEvaluation);
        updated.TriggeredFullEvaluation = true;
        await grain.UpsertScreeningAsync(updated);

        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Result, Is.EqualTo(TBIScreeningResult.PositiveRequiresEvaluation));
    }

    [Test]
    public async Task TBIScreeningIndex_RemoveIsIdempotent()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        await grain.UpsertScreeningAsync(MakeEntry("SCR-DEL", "PAT-5", TBIScreeningResult.Negative));
        await grain.RemoveScreeningAsync("SCR-DEL");
        await grain.RemoveScreeningAsync("SCR-DEL"); // idempotent

        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task TBIScreeningIndex_MultipleScreeningsForSamePatient()
    {
        string key = $"TBI-SCREEN-IDX:{Guid.NewGuid()}";
        ITBIScreeningIndexGrain grain = _cluster.GrainFactory.GetGrain<ITBIScreeningIndexGrain>(key);
        await grain.UpsertScreeningAsync(MakeEntry("SCR-A", "PAT-6", TBIScreeningResult.Negative, DateTime.UtcNow.AddDays(-5)));
        await grain.UpsertScreeningAsync(MakeEntry("SCR-B", "PAT-6", TBIScreeningResult.PositiveRequiresEvaluation, DateTime.UtcNow.AddDays(-2)));
        await grain.UpsertScreeningAsync(MakeEntry("SCR-C", "PAT-6", TBIScreeningResult.Negative, DateTime.UtcNow));

        List<TBIScreeningSummaryEntry> all = await grain.GetAllScreeningsAsync();
        Assert.That(all, Has.Count.EqualTo(3));
    }
}

// ── PolytraumaRecordGrain Tests ──────────────────────────────────────────────

[TestFixture]
public class PolytraumaRecordGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IPolytraumaRecordGrain> RegisteredGrain(string patientId)
    {
        string key = $"PT-RECORD:{patientId}";
        IPolytraumaRecordGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRecordGrain>(key);
        await grain.RegisterPatientAsync(
            patientId, "Test Patient", new DateTime(1985, 6, 15),
            TraumaMechanism.BlastExplosion, new DateTime(2022, 3, 1), "OEF/OIF",
            "PRC", "VA Referral",
            "TEAM-1", "Polytrauma Team Alpha",
            "CM-1", "Jane Case Manager",
            "Initial registration notes");
        return grain;
    }

    [Test]
    public async Task PolytraumaRecordGrain_CanRegisterPatient()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.PatientName, Is.EqualTo("Test Patient"));
        Assert.That(state.TraumaMechanism, Is.EqualTo(TraumaMechanism.BlastExplosion));
    }

    [Test]
    public async Task PolytraumaRecordGrain_PatientIdMatchesGrainKey()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.PatientId, Is.EqualTo(pid));
    }

    [Test]
    public async Task PolytraumaRecordGrain_DefaultStatusIsActive()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Status, Is.EqualTo(PolytraumaStatus.Active));
    }

    [Test]
    public async Task PolytraumaRecordGrain_CanAddSingleInjury()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            BodyRegion = BodyRegion.Head,
            InjuryDescription = "TBI — blast exposure",
            AisScore = 3,
            SeverityScore = InjurySeverityScore.Serious
        });

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Injuries, Has.Count.EqualTo(1));
        Assert.That(state.Injuries[0].BodyRegion, Is.EqualTo(BodyRegion.Head));
    }

    [Test]
    public async Task PolytraumaRecordGrain_CanAddMultipleInjuries()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            BodyRegion = BodyRegion.Head,
            InjuryDescription = "TBI",
            AisScore = 3,
            SeverityScore = InjurySeverityScore.Serious
        });
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            BodyRegion = BodyRegion.LowerExtremity,
            InjuryDescription = "Femur fracture",
            AisScore = 2,
            SeverityScore = InjurySeverityScore.Moderate
        });

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Injuries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PolytraumaRecordGrain_InjuryIdAutoAssigned()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        await grain.AddInjuryAsync(new PolytraumaInjury
        {
            BodyRegion = BodyRegion.Thorax,
            InjuryDescription = "Rib fractures",
            AisScore = 2,
            SeverityScore = InjurySeverityScore.Moderate
        });

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Injuries[0].InjuryId, Is.Not.Empty);
        Assert.That(Guid.TryParse(state.Injuries[0].InjuryId, out _), Is.True);
    }

    [Test]
    public async Task PolytraumaRecordGrain_CanUpdateStatus()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        await grain.UpdateStatusAsync(PolytraumaStatus.Transferred, DateTime.UtcNow);

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.Status, Is.EqualTo(PolytraumaStatus.Transferred));
        Assert.That(state.DeactivationDate, Is.Not.Null);
    }

    [Test]
    public async Task PolytraumaRecordGrain_CanUpdateTBIStatus()
    {
        string pid = $"PAT-{Guid.NewGuid()}";
        IPolytraumaRecordGrain grain = await RegisteredGrain(pid);
        await grain.UpdateTBIStatusAsync(true, TBISeverity.Mild);

        PolytraumaRecordState state = await grain.GetRecordAsync();
        Assert.That(state.HasTBI, Is.True);
        Assert.That(state.TBISeverity, Is.EqualTo(TBISeverity.Mild));
    }
}

// ── PolytraumaRegistryIndexGrain Tests ───────────────────────────────────────

[TestFixture]
public class PolytraumaRegistryIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static PolytraumaRegistrySummaryEntry MakeEntry(string patientId,
        PolytraumaStatus status = PolytraumaStatus.Active,
        DateTime? regDate = null) => new()
    {
        PatientId = patientId,
        PatientName = "Registry Patient",
        Status = status,
        RegistrationDate = regDate ?? DateTime.UtcNow,
        PrimaryCareTeam = "Polytrauma Team",
        TBISeverity = null,
        InjuryCount = 2,
        IssTotalScore = 9,
        LastModifiedDate = DateTime.UtcNow,
    };

    [Test]
    public async Task PolytraumaRegistryIndex_EmptyOnStart()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        List<PolytraumaRegistrySummaryEntry> all = await grain.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task PolytraumaRegistryIndex_CanUpsertAndRetrieve()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        await grain.UpsertPatientAsync(MakeEntry("PAT-A"));

        List<PolytraumaRegistrySummaryEntry> all = await grain.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientId, Is.EqualTo("PAT-A"));
    }

    [Test]
    public async Task PolytraumaRegistryIndex_GetActiveFilters()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        await grain.UpsertPatientAsync(MakeEntry("PAT-ACTIVE", PolytraumaStatus.Active));
        await grain.UpsertPatientAsync(MakeEntry("PAT-INACTIVE", PolytraumaStatus.Inactive));
        await grain.UpsertPatientAsync(MakeEntry("PAT-TRANSFERRED", PolytraumaStatus.Transferred));

        List<PolytraumaRegistrySummaryEntry> active = await grain.GetActivePatientAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].PatientId, Is.EqualTo("PAT-ACTIVE"));
    }

    [Test]
    public async Task PolytraumaRegistryIndex_GetByStatusFilters()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        await grain.UpsertPatientAsync(MakeEntry("PAT-T1", PolytraumaStatus.Transferred));
        await grain.UpsertPatientAsync(MakeEntry("PAT-T2", PolytraumaStatus.Transferred));
        await grain.UpsertPatientAsync(MakeEntry("PAT-A1", PolytraumaStatus.Active));

        List<PolytraumaRegistrySummaryEntry> transferred = await grain.GetPatientsByStatusAsync(PolytraumaStatus.Transferred);
        Assert.That(transferred, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PolytraumaRegistryIndex_UpsertUpdatesExisting()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        await grain.UpsertPatientAsync(MakeEntry("PAT-UPD", PolytraumaStatus.Active));

        PolytraumaRegistrySummaryEntry updated = MakeEntry("PAT-UPD", PolytraumaStatus.Inactive);
        updated.InjuryCount = 5;
        await grain.UpsertPatientAsync(updated);

        List<PolytraumaRegistrySummaryEntry> all = await grain.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(PolytraumaStatus.Inactive));
        Assert.That(all[0].InjuryCount, Is.EqualTo(5));
    }

    [Test]
    public async Task PolytraumaRegistryIndex_RemoveIsIdempotent()
    {
        string key = $"PT-REGISTRY-IDX-{Guid.NewGuid()}";
        IPolytraumaRegistryIndexGrain grain = _cluster.GrainFactory.GetGrain<IPolytraumaRegistryIndexGrain>(key);
        await grain.UpsertPatientAsync(MakeEntry("PAT-DEL", PolytraumaStatus.Active));
        await grain.RemovePatientAsync("PAT-DEL");
        await grain.RemovePatientAsync("PAT-DEL"); // idempotent

        List<PolytraumaRegistrySummaryEntry> all = await grain.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }
}
