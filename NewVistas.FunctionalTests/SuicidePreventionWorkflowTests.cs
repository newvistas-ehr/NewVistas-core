// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Suicide Prevention / Safety Planning module.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end safety plan lifecycle via direct grain factory access.
/// </summary>
[TestFixture]
public class SuicidePreventionWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ISafetyPlanGrain GetPlanGrain(string id) =>
        _cluster.GrainFactory.GetGrain<ISafetyPlanGrain>($"SP-PLAN:{id}");

    private ISafetyPlanIndexGrain GetPlanIndex(string patientId) =>
        _cluster.GrainFactory.GetGrain<ISafetyPlanIndexGrain>($"SP-PLAN-IDX:{patientId}");

    private IPatientRiskGrain GetRiskGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientRiskGrain>($"SP-RISK:{patientId}");

    private ISuicidePreventionIndexGrain GetSiteIndex() =>
        _cluster.GrainFactory.GetGrain<ISuicidePreventionIndexGrain>("SP-INDEX");

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_Create_PersistsAllFields()
    {
        string planId = Guid.NewGuid().ToString("N");
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ISafetyPlanGrain grain = GetPlanGrain(planId);

        await grain.CreatePlanAsync(planId, patientId, "John Veteran", "PRV-001", "Dr. Smith");

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.PlanId, Is.EqualTo(planId));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("John Veteran"));
        Assert.That(state.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.Status, Is.EqualTo(SafetyPlanStatus.Draft));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_UpdateWarningSigns_PersistsList()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-001", "Test Patient", "PRV-001", "Dr. A");

        List<string> signs = new List<string>
        {
            "Feeling hopeless",
            "Increasing alcohol use",
            "Withdrawing from friends"
        };
        await grain.UpdateWarningSigns(signs);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.WarningSigns, Has.Count.EqualTo(3));
        Assert.That(state.WarningSigns, Contains.Item("Feeling hopeless"));
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_UpdateCopingStrategies_PersistsList()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-002", "Test Patient 2", "PRV-001", "Dr. A");

        List<string> strategies = new List<string>
        {
            "Go for a walk",
            "Listen to music",
            "Deep breathing exercises"
        };
        await grain.UpdateCopingStrategies(strategies);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.InternalCopingStrategies, Has.Count.EqualTo(3));
        Assert.That(state.InternalCopingStrategies, Contains.Item("Go for a walk"));
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_UpdateContacts_PersistsAllContactTypes()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-003", "Test Patient 3", "PRV-001", "Dr. A");

        List<string> distractionContacts = new List<string> { "Coffee shop", "Local gym" };
        List<SupportContact> supportContacts = new List<SupportContact>
        {
            new SupportContact { Name = "Jane Doe", PhoneNumber = "555-0101", Relationship = "Spouse" }
        };
        List<ProfessionalContact> professionalContacts = new List<ProfessionalContact>
        {
            new ProfessionalContact { Name = "Dr. Williams", PhoneNumber = "555-0200", Agency = "VA Medical Center", Role = "Psychiatrist" }
        };
        List<string> crisisLines = new List<string> { "988 (press 1)" };

        await grain.UpdateContacts(distractionContacts, supportContacts, professionalContacts, crisisLines);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.DistractionContacts, Has.Count.EqualTo(2));
        Assert.That(state.SupportContacts, Has.Count.EqualTo(1));
        Assert.That(state.SupportContacts[0].Name, Is.EqualTo("Jane Doe"));
        Assert.That(state.ProfessionalContacts, Has.Count.EqualTo(1));
        Assert.That(state.CrisisLineNumbers, Has.Count.EqualTo(1));
        Assert.That(state.CrisisLineNumbers, Contains.Item("988 (press 1)"));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_UpdateMeansRestriction_PersistsData()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-004", "Test Patient 4", "PRV-001", "Dr. A");

        List<string> means = new List<string> { "Firearms secured at friend's house", "Medications locked up" };
        await grain.UpdateMeansRestriction(means, "Veteran agreed to secure all firearms off-site");

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.MeansRemoved, Has.Count.EqualTo(2));
        Assert.That(state.EnvironmentSafetyNotes, Is.EqualTo("Veteran agreed to secure all firearms off-site"));
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_UpdateReasonsForLiving_PersistsList()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-005", "Test Patient 5", "PRV-001", "Dr. A");

        List<string> reasons = new List<string> { "My children", "My dog", "Recovery goals" };
        await grain.UpdateReasonsForLiving(reasons);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.ReasonsForLiving, Has.Count.EqualTo(3));
        Assert.That(state.ReasonsForLiving, Contains.Item("My children"));
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_Review_SetsReviewDate()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-006", "Test Patient 6", "PRV-001", "Dr. A");

        DateTime reviewDate = DateTime.UtcNow;
        await grain.ReviewPlanAsync(reviewDate);

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.LastReviewedDate, Is.Not.Null);
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlan_Archive_SetsStatusToArchived()
    {
        string planId = Guid.NewGuid().ToString("N");
        ISafetyPlanGrain grain = GetPlanGrain(planId);
        await grain.CreatePlanAsync(planId, "PAT-007", "Test Patient 7", "PRV-001", "Dr. A");

        await grain.ArchivePlanAsync();

        SafetyPlanState state = await grain.GetPlanAsync();
        Assert.That(state.Status, Is.EqualTo(SafetyPlanStatus.Archived));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlanIndex_UpsertAndGetAll()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ISafetyPlanIndexGrain index = GetPlanIndex(patientId);

        SafetyPlanSummary summary = new SafetyPlanSummary
        {
            PlanId = Guid.NewGuid().ToString("N"),
            PatientId = patientId,
            PatientName = "Index Test Patient",
            Status = SafetyPlanStatus.Active,
            CreatedDate = DateTime.UtcNow
        };
        await index.UpsertPlanAsync(summary);

        List<SafetyPlanSummary> all = await index.GetAllPlansAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientName, Is.EqualTo("Index Test Patient"));
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SafetyPlanIndex_GetActivePlan_ReturnsActiveOnly()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        ISafetyPlanIndexGrain index = GetPlanIndex(patientId);

        await index.UpsertPlanAsync(new SafetyPlanSummary
        {
            PlanId = Guid.NewGuid().ToString("N"), PatientId = patientId,
            PatientName = "Patient X", Status = SafetyPlanStatus.Archived, CreatedDate = DateTime.UtcNow
        });
        await index.UpsertPlanAsync(new SafetyPlanSummary
        {
            PlanId = Guid.NewGuid().ToString("N"), PatientId = patientId,
            PatientName = "Patient X", Status = SafetyPlanStatus.Active, CreatedDate = DateTime.UtcNow
        });

        SafetyPlanSummary? active = await index.GetActivePlanAsync();
        Assert.That(active, Is.Not.Null);
        Assert.That(active!.Status, Is.EqualTo(SafetyPlanStatus.Active));
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientRisk_SetRiskLevel_PersistsLevelAndHistory()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPatientRiskGrain grain = GetRiskGrain(patientId);

        await grain.SetRiskLevelAsync(RiskLevel.High, patientId, "Risk Patient", "PRV-001", "Dr. Risk");

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.CurrentRiskLevel, Is.EqualTo(RiskLevel.High));
        Assert.That(state.PatientName, Is.EqualTo("Risk Patient"));
        Assert.That(state.DesignationHistory, Has.Count.GreaterThanOrEqualTo(1));
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientRisk_SetHighRiskFlag_PersistsFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPatientRiskGrain grain = GetRiskGrain(patientId);
        await grain.SetRiskLevelAsync(RiskLevel.High, patientId, "Flag Patient", "PRV-001", "Dr. Flag");

        await grain.SetHighRiskFlagAsync(true);

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.IsHighRiskFlagged, Is.True);
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientRisk_AddFollowUpContact_AppendsToList()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IPatientRiskGrain grain = GetRiskGrain(patientId);
        await grain.SetRiskLevelAsync(RiskLevel.High, patientId, "FollowUp Patient", "PRV-001", "Dr. A");

        FollowUpContact contact = new FollowUpContact
        {
            ContactId = Guid.NewGuid().ToString("N"),
            ContactDate = DateTime.UtcNow,
            ContactType = FollowUpContactType.Phone,
            Outcome = FollowUpContactOutcome.Contacted,
            ProviderName = "SPC Coordinator",
            Notes = "Patient reports improved coping"
        };
        await grain.AddFollowUpContactAsync(contact);

        PatientRiskState state = await grain.GetRiskStateAsync();
        Assert.That(state.FollowUpContacts, Has.Count.EqualTo(1));
        Assert.That(state.FollowUpContacts[0].Outcome, Is.EqualTo(FollowUpContactOutcome.Contacted));
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task SiteIndex_UpsertAndGetHighRisk()
    {
        ISuicidePreventionIndexGrain siteIndex = GetSiteIndex();

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await siteIndex.UpsertPatientAsync(new PatientHighRiskSummary
        {
            PatientId = patientId,
            PatientName = "High Risk Vet",
            CurrentRiskLevel = RiskLevel.High,
            IsHighRiskFlagged = true,
            ActivePlanCount = 1,
            LastModifiedDate = DateTime.UtcNow
        });

        List<PatientHighRiskSummary> highRisk = await siteIndex.GetHighRiskPatientsAsync();
        Assert.That(highRisk.Any(p => p.PatientId == patientId), Is.True);
        Assert.That(highRisk.First(p => p.PatientId == patientId).IsHighRiskFlagged, Is.True);
    }
}
