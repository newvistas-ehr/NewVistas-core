// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ─────────────────────────────────────────────────────────────────────────────
// Safety Plan Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class SafetyPlanGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISafetyPlanGrain GetGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ISafetyPlanGrain>($"SP-PLAN:{id}");

    private async Task<ISafetyPlanGrain> CreateTestPlan(string id)
    {
        ISafetyPlanGrain grain = GetGrain(id);
        await grain.CreatePlanAsync(id, "PAT-001", "John Smith", "PROV-001", "Dr. Jones");
        return grain;
    }

    [Test]
    public async Task CanCreatePlan()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        SafetyPlanState state = await grain.GetPlanAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Smith"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Jones"));
    }

    [Test]
    public async Task PlanIdSetOnCreate()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.PlanId, Is.EqualTo(id));
    }

    [Test]
    public async Task DefaultStatusIsDraft()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.Status, Is.EqualTo(SafetyPlanStatus.Draft));
    }

    [Test]
    public async Task CanUpdateWarningSigns()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        List<string> signs = new() { "Increasing isolation", "Giving away possessions", "Hopelessness" };
        await grain.UpdateWarningSigns(signs);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.WarningSigns, Has.Count.EqualTo(3));
        Assert.That(state.WarningSigns, Contains.Item("Hopelessness"));
    }

    [Test]
    public async Task CanUpdateCopingStrategies()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        List<string> strategies = new() { "Deep breathing", "Walk around the block", "Listen to music" };
        await grain.UpdateCopingStrategies(strategies);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.InternalCopingStrategies, Has.Count.EqualTo(3));
        Assert.That(state.InternalCopingStrategies, Contains.Item("Deep breathing"));
    }

    [Test]
    public async Task CanUpdateContacts()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        List<SupportContact> supportContacts = new()
        {
            new SupportContact { Name = "Jane Smith", PhoneNumber = "555-1234", Relationship = "Spouse" },
        };
        List<ProfessionalContact> profContacts = new()
        {
            new ProfessionalContact { Name = "Dr. Jones", PhoneNumber = "555-5678", Agency = "VA", Role = "Psychiatrist" },
        };
        await grain.UpdateContacts(new() { "Coffee shop" }, supportContacts, profContacts, new() { "988" });

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.SupportContacts, Has.Count.EqualTo(1));
        Assert.That(state.SupportContacts[0].Name, Is.EqualTo("Jane Smith"));
        Assert.That(state.ProfessionalContacts, Has.Count.EqualTo(1));
        Assert.That(state.CrisisLineNumbers, Contains.Item("988"));
    }

    [Test]
    public async Task CanUpdateMeansRestriction()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        await grain.UpdateMeansRestriction(new() { "Firearms removed to neighbor", "Medications locked" }, "Spouse holds key");

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.MeansRemoved, Has.Count.EqualTo(2));
        Assert.That(state.EnvironmentSafetyNotes, Is.EqualTo("Spouse holds key"));
    }

    [Test]
    public async Task CanUpdateReasonsForLiving()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        await grain.UpdateReasonsForLiving(new() { "My children", "My dog", "Service to others" });

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.ReasonsForLiving, Has.Count.EqualTo(3));
        Assert.That(state.ReasonsForLiving, Contains.Item("My children"));
    }

    [Test]
    public async Task ReviewPlanUpdatesDate()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        DateTime reviewDate = new DateTime(2025, 7, 1);
        await grain.ReviewPlanAsync(reviewDate);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.LastReviewedDate, Is.EqualTo(reviewDate));
        // Draft → Active on first review
        Assert.That(state.Status, Is.EqualTo(SafetyPlanStatus.Active));
    }

    [Test]
    public async Task CanArchivePlan()
    {
        string id = $"PLAN-{Guid.NewGuid()}";
        ISafetyPlanGrain grain = await CreateTestPlan(id);
        await grain.ReviewPlanAsync(DateTime.UtcNow); // Draft → Active
        await grain.ArchivePlanAsync();

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.Status, Is.EqualTo(SafetyPlanStatus.Archived));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Safety Plan Index Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class SafetyPlanIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISafetyPlanIndexGrain GetIndex(string patientId) =>
        _cluster.GrainFactory.GetGrain<ISafetyPlanIndexGrain>($"SP-PLAN-IDX:{patientId}-{Guid.NewGuid()}");

    private static SafetyPlanSummary MakeSummary(string planId, SafetyPlanStatus status, DateTime? created = null) =>
        new()
        {
            PlanId = planId,
            PatientId = "PAT-001",
            PatientName = "John Smith",
            Status = status,
            CreatedDate = created ?? DateTime.UtcNow,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        string id = $"PLAN-{Guid.NewGuid()}";
        await index.UpsertPlanAsync(MakeSummary(id, SafetyPlanStatus.Active));

        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PlanId, Is.EqualTo(id));
    }

    [Test]
    public async Task GetActivePlanReturnsDraftOrActive()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        await index.UpsertPlanAsync(MakeSummary($"PLAN-{Guid.NewGuid()}", SafetyPlanStatus.Archived));
        string activeId = $"PLAN-{Guid.NewGuid()}";
        await index.UpsertPlanAsync(MakeSummary(activeId, SafetyPlanStatus.Active));

        SafetyPlanSummary? active = await index.GetActivePlanAsync();
        Assert.That(active, Is.Not.Null);
        Assert.That(active!.PlanId, Is.EqualTo(activeId));
    }

    [Test]
    public async Task GetActivePlanReturnsNullWhenNone()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        await index.UpsertPlanAsync(MakeSummary($"PLAN-{Guid.NewGuid()}", SafetyPlanStatus.Archived));

        SafetyPlanSummary? active = await index.GetActivePlanAsync();
        Assert.That(active, Is.Null);
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        string id = $"PLAN-{Guid.NewGuid()}";
        await index.UpsertPlanAsync(MakeSummary(id, SafetyPlanStatus.Draft));
        await index.UpsertPlanAsync(MakeSummary(id, SafetyPlanStatus.Active));

        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(SafetyPlanStatus.Active));
    }

    [Test]
    public async Task RemovePlanIsIdempotent()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        string id = $"PLAN-{Guid.NewGuid()}";
        await index.UpsertPlanAsync(MakeSummary(id, SafetyPlanStatus.Active));
        await index.RemovePlanAsync(id);
        await index.RemovePlanAsync(id); // second remove should not throw

        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task OrderedNewestFirst()
    {
        ISafetyPlanIndexGrain index = GetIndex("PAT-001");
        await index.UpsertPlanAsync(MakeSummary($"PLAN-{Guid.NewGuid()}", SafetyPlanStatus.Archived, new DateTime(2024, 1, 1)));
        await index.UpsertPlanAsync(MakeSummary($"PLAN-{Guid.NewGuid()}", SafetyPlanStatus.Active, new DateTime(2025, 6, 1)));
        await index.UpsertPlanAsync(MakeSummary($"PLAN-{Guid.NewGuid()}", SafetyPlanStatus.Draft, new DateTime(2025, 3, 1)));

        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all[0].CreatedDate, Is.EqualTo(new DateTime(2025, 6, 1)));
        Assert.That(all[2].CreatedDate, Is.EqualTo(new DateTime(2024, 1, 1)));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Patient Risk Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class PatientRiskGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientRiskGrain GetGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientRiskGrain>($"SP-RISK:{patientId}");

    [Test]
    public async Task DefaultRiskIsNotAssessed()
    {
        IPatientRiskGrain grain = GetGrain($"PAT-{Guid.NewGuid()}");
        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.CurrentRiskLevel, Is.EqualTo(RiskLevel.NotAssessed));
    }

    [Test]
    public async Task CanSetRiskLevel()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.SetRiskLevelAsync(RiskLevel.High, patientId, "John Smith", "PROV-001", "Dr. Jones");

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.CurrentRiskLevel, Is.EqualTo(RiskLevel.High));
        Assert.That(state.PatientName, Is.EqualTo("John Smith"));
    }

    [Test]
    public async Task RiskLevelAppendedToHistory()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.SetRiskLevelAsync(RiskLevel.Moderate, patientId, "Jane Doe", "PROV-001", "Dr. Smith");

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.DesignationHistory, Has.Count.EqualTo(1));
        Assert.That(state.DesignationHistory[0].RiskLevel, Is.EqualTo(RiskLevel.Moderate));
        Assert.That(state.DesignationHistory[0].ProviderName, Is.EqualTo("Dr. Smith"));
    }

    [Test]
    public async Task MultipleRiskUpdatesAppend()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.SetRiskLevelAsync(RiskLevel.Low, patientId, "Jane Doe", "PROV-001", "Dr. A");
        await grain.SetRiskLevelAsync(RiskLevel.High, patientId, "Jane Doe", "PROV-001", "Dr. B");
        await grain.SetRiskLevelAsync(RiskLevel.Moderate, patientId, "Jane Doe", "PROV-001", "Dr. C");

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.CurrentRiskLevel, Is.EqualTo(RiskLevel.Moderate));
        Assert.That(state.DesignationHistory, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task CanSetHighRiskFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.SetHighRiskFlagAsync(true);

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.IsHighRiskFlagged, Is.True);
    }

    [Test]
    public async Task CanClearHighRiskFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.SetHighRiskFlagAsync(true);
        await grain.SetHighRiskFlagAsync(false);

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.IsHighRiskFlagged, Is.False);
    }

    [Test]
    public async Task CanAddFollowUpContact()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.AddFollowUpContactAsync(new FollowUpContact
        {
            ContactDate = new DateTime(2025, 7, 1),
            ContactType = FollowUpContactType.Phone,
            Outcome = FollowUpContactOutcome.Contacted,
            ProviderName = "Dr. Smith",
            Notes = "Patient stable, plan reviewed.",
        });

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.FollowUpContacts, Has.Count.EqualTo(1));
        Assert.That(state.FollowUpContacts[0].Outcome, Is.EqualTo(FollowUpContactOutcome.Contacted));
        Assert.That(state.FollowUpContacts[0].ProviderName, Is.EqualTo("Dr. Smith"));
    }

    [Test]
    public async Task FollowUpContactIdAutoAssigned()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientRiskGrain grain = GetGrain(patientId);
        await grain.AddFollowUpContactAsync(new FollowUpContact
        {
            ContactDate = DateTime.UtcNow,
            ContactType = FollowUpContactType.InPerson,
            Outcome = FollowUpContactOutcome.Contacted,
            ProviderName = "Dr. Jones",
        });

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.FollowUpContacts[0].ContactId, Is.Not.Null.And.Not.Empty);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Suicide Prevention Index Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class SuicidePreventionIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ISuicidePreventionIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<ISuicidePreventionIndexGrain>($"SP-INDEX-{Guid.NewGuid()}");

    private static PatientHighRiskSummary MakeSummary(string patientId, bool highRisk, RiskLevel level = RiskLevel.High) =>
        new()
        {
            PatientId = patientId,
            PatientName = "Test Patient",
            CurrentRiskLevel = level,
            IsHighRiskFlagged = highRisk,
            ActivePlanCount = 1,
            LastModifiedDate = DateTime.UtcNow,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        List<PatientHighRiskSummary> all = await index.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        string id = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeSummary(id, true));

        List<PatientHighRiskSummary> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientId, Is.EqualTo(id));
    }

    [Test]
    public async Task GetHighRiskFilters()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", true, RiskLevel.High));
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", false, RiskLevel.Moderate));
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", true, RiskLevel.Imminent));

        List<PatientHighRiskSummary> highRisk = await index.GetHighRiskPatientsAsync();
        Assert.That(highRisk, Has.Count.EqualTo(2));
        Assert.That(highRisk.All(p => p.IsHighRiskFlagged), Is.True);
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        string id = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeSummary(id, false, RiskLevel.Low));
        PatientHighRiskSummary updated = MakeSummary(id, true, RiskLevel.High);
        await index.UpsertPatientAsync(updated);

        List<PatientHighRiskSummary> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IsHighRiskFlagged, Is.True);
        Assert.That(all[0].CurrentRiskLevel, Is.EqualTo(RiskLevel.High));
    }

    [Test]
    public async Task RemovePatientIsIdempotent()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        string id = $"PAT-{Guid.NewGuid()}";
        await index.UpsertPatientAsync(MakeSummary(id, true));
        await index.RemovePatientAsync(id);
        await index.RemovePatientAsync(id); // should not throw

        List<PatientHighRiskSummary> all = await index.GetAllPatientsAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task MultiplePatients()
    {
        ISuicidePreventionIndexGrain index = GetIndex();
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", true, RiskLevel.High));
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", true, RiskLevel.Imminent));
        await index.UpsertPatientAsync(MakeSummary($"PAT-{Guid.NewGuid()}", false, RiskLevel.Moderate));

        List<PatientHighRiskSummary> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(3));
    }
}
