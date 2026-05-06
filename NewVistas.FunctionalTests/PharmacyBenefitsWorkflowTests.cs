// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class PharmacyBenefitsWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientBenefitPlanGrain GetPlanGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{patientId}");

    private IFormularyIndexGrain GetFormularyGrain(string planId) =>
        _cluster.GrainFactory.GetGrain<IFormularyIndexGrain>($"PBM-FORMULARY:{planId}");

    private IPriorAuthorizationGrain GetPaGrain(string paId) =>
        _cluster.GrainFactory.GetGrain<IPriorAuthorizationGrain>($"PA:{paId}");

    private IPriorAuthIndexGrain GetPaIndexGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPriorAuthIndexGrain>($"PA-INDEX:{patientId}");

    // ── 1 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientBenefitGrain_SetPlan_PersistsAllFields()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientBenefitPlanGrain grain = GetPlanGrain(patientId);

        await grain.SetPlanAsync("PLAN-001", "TRICARE Standard", "Defense Health Agency",
            "GRP-001", "MBR-12345",
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            0m, 11m, 22m, 90, 150m);

        PatientBenefitPlanState plan = await grain.GetPlanAsync();

        Assert.That(plan.PlanId, Is.EqualTo("PLAN-001"));
        Assert.That(plan.PlanName, Is.EqualTo("TRICARE Standard"));
        Assert.That(plan.InsuranceName, Is.EqualTo("Defense Health Agency"));
        Assert.That(plan.MemberId, Is.EqualTo("MBR-12345"));
        Assert.That(plan.CopayTier1, Is.EqualTo(0m));
        Assert.That(plan.CopayTier2, Is.EqualTo(11m));
        Assert.That(plan.CopayTier3, Is.EqualTo(22m));
        Assert.That(plan.DaySupplyLimit, Is.EqualTo(90));
        Assert.That(plan.AnnualDeductible, Is.EqualTo(150m));
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientBenefitGrain_SetPlan_IsActive()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientBenefitPlanGrain grain = GetPlanGrain(patientId);

        await grain.SetPlanAsync("PLAN-002", "Medicare Part D", "CMS",
            null, null, null, null, 1m, 5m, 10m, 90, 0m);

        PatientBenefitPlanState plan = await grain.GetPlanAsync();
        Assert.That(plan.IsActive, Is.True);
    }

    // ── 3 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientBenefitGrain_Deactivate_SetsInactive()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientBenefitPlanGrain grain = GetPlanGrain(patientId);
        await grain.SetPlanAsync("PLAN-003", "Test Plan", "Insurer", null, null, null, null, 0, 5, 10, 90, 0);

        await grain.DeactivateAsync();

        PatientBenefitPlanState plan = await grain.GetPlanAsync();
        Assert.That(plan.IsActive, Is.False);
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task PatientBenefitGrain_MarkDeductibleMet_SetsFlag()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientBenefitPlanGrain grain = GetPlanGrain(patientId);
        await grain.SetPlanAsync("PLAN-004", "Test Plan", "Insurer", null, null, null, null, 0, 5, 10, 90, 500m);

        await grain.MarkDeductibleMetAsync();

        PatientBenefitPlanState plan = await grain.GetPlanAsync();
        Assert.That(plan.DeductibleMet, Is.True);
    }

    // ── 5 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task FormularyIndex_AddEntry_AppearsInGetAll()
    {
        string planId = $"PLAN-{Guid.NewGuid()}";
        IFormularyIndexGrain grain = GetFormularyGrain(planId);

        await grain.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = "50-METFORMIN", DrugName = "Metformin 500mg",
            Tier = 1, CoverageStatus = "COVERED", RequiresPriorAuth = false
        });

        List<FormularyEntry> entries = await grain.GetAllAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].DrugId, Is.EqualTo("50-METFORMIN"));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task FormularyIndex_GetByTier_FiltersCorrectly()
    {
        string planId = $"PLAN-{Guid.NewGuid()}";
        IFormularyIndexGrain grain = GetFormularyGrain(planId);

        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-T1", DrugName = "Generic Drug", Tier = 1, CoverageStatus = "COVERED" });
        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-T2", DrugName = "Brand Drug", Tier = 2, CoverageStatus = "COVERED" });
        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-T3", DrugName = "Specialty Drug", Tier = 3, CoverageStatus = "COVERED" });

        List<FormularyEntry> tier1 = await grain.GetByTierAsync(1);
        Assert.That(tier1, Has.Count.EqualTo(1));
        Assert.That(tier1[0].DrugId, Is.EqualTo("D-T1"));
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task FormularyIndex_GetRequiringPA_FiltersCorrectly()
    {
        string planId = $"PLAN-{Guid.NewGuid()}";
        IFormularyIndexGrain grain = GetFormularyGrain(planId);

        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-PA", DrugName = "Bio Drug", Tier = 3, CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true });
        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-NOPA", DrugName = "Normal Drug", Tier = 1, CoverageStatus = "COVERED", RequiresPriorAuth = false });

        List<FormularyEntry> paRequired = await grain.GetRequiringPriorAuthAsync();
        Assert.That(paRequired, Has.Count.EqualTo(1));
        Assert.That(paRequired[0].DrugId, Is.EqualTo("D-PA"));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task FormularyIndex_Search_MatchesDrugName()
    {
        string planId = $"PLAN-{Guid.NewGuid()}";
        IFormularyIndexGrain grain = GetFormularyGrain(planId);

        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-WARF", DrugName = "Warfarin 5mg", Tier = 2, CoverageStatus = "COVERED" });
        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-META", DrugName = "Metformin 500mg", Tier = 1, CoverageStatus = "COVERED" });

        List<FormularyEntry> results = await grain.SearchAsync("warf");
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DrugId, Is.EqualTo("D-WARF"));
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task FormularyIndex_AddOrUpdate_ReplacesExistingEntry()
    {
        string planId = $"PLAN-{Guid.NewGuid()}";
        IFormularyIndexGrain grain = GetFormularyGrain(planId);

        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-UPD", DrugName = "Test Drug", Tier = 2, CoverageStatus = "COVERED" });
        await grain.AddOrUpdateEntryAsync(new FormularyEntry { DrugId = "D-UPD", DrugName = "Test Drug Updated", Tier = 1, CoverageStatus = "COVERED", RequiresPriorAuth = true });

        List<FormularyEntry> entries = await grain.GetAllAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Tier, Is.EqualTo(1));
        Assert.That(entries[0].RequiresPriorAuth, Is.True);
    }

    // ── 10 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuth_Submit_SetsPendingStatus()
    {
        string paId = Guid.NewGuid().ToString();
        IPriorAuthorizationGrain grain = GetPaGrain(paId);

        await grain.SubmitRequestAsync("P-001", "D-001", "Adalimumab", "PLAN-001",
            "PROV-001", "Dr. Smith",
            new List<string> { "M05.79" }, "Failed methotrexate");

        PriorAuthorizationState pa = await grain.GetAsync();
        Assert.That(pa.Status, Is.EqualTo("PENDING"));
        Assert.That(pa.PatientId, Is.EqualTo("P-001"));
        Assert.That(pa.DrugName, Is.EqualTo("Adalimumab"));
        Assert.That(pa.DiagnosisCodes, Contains.Item("M05.79"));
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuth_Approve_SetsApprovedWithExpiration()
    {
        string paId = Guid.NewGuid().ToString();
        IPriorAuthorizationGrain grain = GetPaGrain(paId);
        await grain.SubmitRequestAsync("P-002", "D-002", "Eculizumab", "PLAN-001",
            null, null, new List<string>(), null);

        DateTime expDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await grain.ApproveAsync("REV-001", "Dr. Reviewer", "Approved per protocol", expDate);

        PriorAuthorizationState pa = await grain.GetAsync();
        Assert.That(pa.Status, Is.EqualTo("APPROVED"));
        Assert.That(pa.DecisionByName, Is.EqualTo("Dr. Reviewer"));
        Assert.That(pa.ExpirationDate, Is.EqualTo(expDate));
    }

    // ── 12 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuth_Deny_SetsDeniedWithReason()
    {
        string paId = Guid.NewGuid().ToString();
        IPriorAuthorizationGrain grain = GetPaGrain(paId);
        await grain.SubmitRequestAsync("P-003", "D-003", "Biologic X", "PLAN-001",
            null, null, new List<string>(), null);

        await grain.DenyAsync("REV-002", "Dr. Denier", "Step therapy not completed");

        PriorAuthorizationState pa = await grain.GetAsync();
        Assert.That(pa.Status, Is.EqualTo("DENIED"));
        Assert.That(pa.DenialReason, Is.EqualTo("Step therapy not completed"));
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuth_Expire_SetsExpiredStatus()
    {
        string paId = Guid.NewGuid().ToString();
        IPriorAuthorizationGrain grain = GetPaGrain(paId);
        await grain.SubmitRequestAsync("P-004", null, "Drug Y", null, null, null, new List<string>(), null);
        await grain.ApproveAsync("REV-001", "Reviewer", null, DateTime.UtcNow.AddDays(-1));

        await grain.ExpireAsync();

        PriorAuthorizationState pa = await grain.GetAsync();
        Assert.That(pa.Status, Is.EqualTo("EXPIRED"));
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuthIndex_GetPending_FiltersCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPriorAuthIndexGrain indexGrain = GetPaIndexGrain(patientId);

        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = "PA-P1", DrugName = "Drug A", Status = "PENDING", RequestDate = DateTime.UtcNow
        });
        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = "PA-A1", DrugName = "Drug B", Status = "APPROVED", RequestDate = DateTime.UtcNow
        });

        List<PriorAuthIndexEntry> pending = await indexGrain.GetPendingAsync();
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].PaId, Is.EqualTo("PA-P1"));
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task PriorAuthIndex_GetApproved_FiltersCorrectly()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPriorAuthIndexGrain indexGrain = GetPaIndexGrain(patientId);

        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = "PA-P2", DrugName = "Drug C", Status = "PENDING", RequestDate = DateTime.UtcNow
        });
        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = "PA-A2", DrugName = "Drug D", Status = "APPROVED", RequestDate = DateTime.UtcNow
        });
        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = "PA-A3", DrugName = "Drug E", Status = "APPROVED", RequestDate = DateTime.UtcNow
        });

        List<PriorAuthIndexEntry> approved = await indexGrain.GetApprovedAsync();
        Assert.That(approved, Has.Count.EqualTo(2));
    }

    // ── 16 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task Workflow_SubmitCheckCoverageApprovePA_EndToEnd()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string planId = $"PLAN-{Guid.NewGuid()}";

        // 1. Set up patient benefit plan
        IPatientBenefitPlanGrain planGrain = GetPlanGrain(patientId);
        await planGrain.SetPlanAsync(planId, "TRICARE Standard", "Defense Health Agency",
            null, null, null, null, 0m, 11m, 22m, 90, 150m);

        // 2. Add formulary entries
        IFormularyIndexGrain formularyGrain = GetFormularyGrain(planId);
        await formularyGrain.AddOrUpdateEntryAsync(new FormularyEntry
        {
            DrugId = "50-ADALIMUMAB", DrugName = "Adalimumab (Humira)",
            Tier = 3, CoverageStatus = "REQUIRES_PA", RequiresPriorAuth = true
        });

        // 3. Check coverage — should show PA required
        FormularyEntry? entry = await formularyGrain.GetDrugAsync("50-ADALIMUMAB");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.RequiresPriorAuth, Is.True);

        // 4. Submit PA
        string paId = Guid.NewGuid().ToString();
        IPriorAuthorizationGrain paGrain = GetPaGrain(paId);
        await paGrain.SubmitRequestAsync(patientId, "50-ADALIMUMAB", "Adalimumab (Humira)",
            planId, "PROV-007", "Dr. Sarah Lee",
            new List<string> { "M05.79" }, "Failed DMARDs");

        PriorAuthorizationState submitted = await paGrain.GetAsync();
        Assert.That(submitted.Status, Is.EqualTo("PENDING"));

        // 5. Add to index
        IPriorAuthIndexGrain indexGrain = GetPaIndexGrain(patientId);
        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = paId, DrugName = submitted.DrugName, Status = submitted.Status,
            RequestDate = submitted.RequestDate, ProviderName = submitted.ProviderName
        });

        List<PriorAuthIndexEntry> pending = await indexGrain.GetPendingAsync();
        Assert.That(pending, Has.Count.EqualTo(1));

        // 6. Approve PA
        DateTime expiry = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        await paGrain.ApproveAsync("PHARM-MGR", "Chief Pharmacist", "Medically necessary", expiry);

        // 7. Sync index
        PriorAuthorizationState approved = await paGrain.GetAsync();
        await indexGrain.AddOrUpdateAsync(new PriorAuthIndexEntry
        {
            PaId = paId, DrugName = approved.DrugName, Status = approved.Status,
            RequestDate = approved.RequestDate, ExpirationDate = approved.ExpirationDate,
            ProviderName = approved.ProviderName
        });

        List<PriorAuthIndexEntry> approvedList = await indexGrain.GetApprovedAsync();
        Assert.That(approvedList, Has.Count.EqualTo(1));
        Assert.That(approvedList[0].ExpirationDate, Is.EqualTo(expiry));
    }
}
