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
/// Functional tests for VistA Infection Control module.
/// System-level grains; no workflow grain involvement.
/// Tests end-to-end HAI case + outbreak workflows via direct grain factory access.
/// </summary>
[TestFixture]
public class InfectionControlWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IHAICaseGrain GetCaseGrain(string id) =>
        _cluster.GrainFactory.GetGrain<IHAICaseGrain>($"HAI-CASE:{id}");

    private IHAICaseIndexGrain GetCaseIndex() =>
        _cluster.GrainFactory.GetGrain<IHAICaseIndexGrain>("HAI-CASE-IDX");

    private IOutbreakGrain GetOutbreakGrain(string id) =>
        _cluster.GrainFactory.GetGrain<IOutbreakGrain>($"HAI-OUTBREAK:{id}");

    private IOutbreakIndexGrain GetOutbreakIndex() =>
        _cluster.GrainFactory.GetGrain<IOutbreakIndexGrain>("HAI-OUTBREAK-IDX");

    private static async Task CreateDefaultCase(IHAICaseGrain grain, string caseId)
    {
        await grain.CreateCaseAsync(
            caseId, "PAT-001", "John Patient",
            new DateTime(1955, 6, 15), "ICU-1A", "Medical ICU",
            HAIType.CLABSI, DateTime.UtcNow.AddDays(-2),
            "Staphylococcus aureus",
            "RN-001", "Nurse Smith", "Suspected CLABSI in central line");
    }

    // ── 1 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_Create_PersistsAllFields()
    {
        string caseId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);

        await CreateDefaultCase(grain, caseId);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.CaseId, Is.EqualTo(caseId));
        Assert.That(state.PatientName, Is.EqualTo("John Patient"));
        Assert.That(state.LocationName, Is.EqualTo("Medical ICU"));
        Assert.That(state.HAIType, Is.EqualTo(HAIType.CLABSI));
        Assert.That(state.Pathogen, Is.EqualTo("Staphylococcus aureus"));
        Assert.That(state.Status, Is.EqualTo(HAICaseStatus.Suspected));
    }

    // ── 2 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_UpdateStatus_ToConfirmed()
    {
        string caseId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);
        await CreateDefaultCase(grain, caseId);

        DateTime confirmedDate = DateTime.UtcNow;
        await grain.UpdateStatusAsync(HAICaseStatus.Confirmed, confirmedDate);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.Status, Is.EqualTo(HAICaseStatus.Confirmed));
        Assert.That(state.ConfirmedDate, Is.Not.Null);
    }

    // ── 3 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_UpdateClinicalData_PersistsLabAndDeviceInfo()
    {
        string caseId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);
        await CreateDefaultCase(grain, caseId);

        await grain.UpdateClinicalDataAsync(
            "Blood", DateTime.UtcNow.AddDays(-1),
            "Gram-positive cocci in clusters",
            "MRSA confirmed",
            "Central Venous Catheter", 14,
            null, string.Empty);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.CultureSource, Is.EqualTo("Blood"));
        Assert.That(state.GramStain, Is.EqualTo("Gram-positive cocci in clusters"));
        Assert.That(state.CultureResult, Is.EqualTo("MRSA confirmed"));
        Assert.That(state.DeviceType, Is.EqualTo("Central Venous Catheter"));
        Assert.That(state.DeviceInDays, Is.EqualTo(14));
    }

    // ── 4 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_AddSusceptibilityResult_AppendsToList()
    {
        string caseId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);
        await CreateDefaultCase(grain, caseId);

        AntibioticSusceptibilityResult result = new AntibioticSusceptibilityResult
        {
            AntibioticName = "Vancomycin",
            Susceptibility = AntibioticSusceptibility.Susceptible,
            MIC = "1.0 mcg/mL"
        };
        await grain.AddSusceptibilityResultAsync(result);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.SusceptibilityResults, Has.Count.EqualTo(1));
        Assert.That(state.SusceptibilityResults[0].AntibioticName, Is.EqualTo("Vancomycin"));
        Assert.That(state.SusceptibilityResults[0].Susceptibility, Is.EqualTo(AntibioticSusceptibility.Susceptible));
    }

    // ── 5 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_MultipleSusceptibilityResults()
    {
        string caseId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);
        await CreateDefaultCase(grain, caseId);

        await grain.AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
        {
            AntibioticName = "Vancomycin", Susceptibility = AntibioticSusceptibility.Susceptible, MIC = "1.0 mcg/mL"
        });
        await grain.AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
        {
            AntibioticName = "Oxacillin", Susceptibility = AntibioticSusceptibility.Resistant, MIC = ">4 mcg/mL"
        });

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.SusceptibilityResults, Has.Count.EqualTo(2));
    }

    // ── 6 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICaseIndex_UpsertAndGetAll()
    {
        IHAICaseIndexGrain index = GetCaseIndex();

        string caseId = Guid.NewGuid().ToString("N");
        await index.UpsertCaseAsync(new HAICaseSummary
        {
            CaseId = caseId,
            PatientId = "PAT-100",
            PatientName = "Index Patient",
            HAIType = HAIType.CAUTI,
            Status = HAICaseStatus.Suspected,
            InfectionDate = DateTime.UtcNow,
            LocationId = "UNIT-2B",
            LocationName = "Step-Down Unit",
            Pathogen = "E. coli"
        });

        List<HAICaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all.Any(c => c.CaseId == caseId), Is.True);
    }

    // ── 7 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICaseIndex_GetByType_FiltersCorrectly()
    {
        IHAICaseIndexGrain index = GetCaseIndex();

        string cautiId = Guid.NewGuid().ToString("N");
        string clabsiId = Guid.NewGuid().ToString("N");
        await index.UpsertCaseAsync(new HAICaseSummary
        {
            CaseId = cautiId, PatientId = "PAT-A", PatientName = "A",
            HAIType = HAIType.CAUTI, Status = HAICaseStatus.Confirmed,
            LocationId = "U-1", LocationName = "Unit 1", Pathogen = "E. coli"
        });
        await index.UpsertCaseAsync(new HAICaseSummary
        {
            CaseId = clabsiId, PatientId = "PAT-B", PatientName = "B",
            HAIType = HAIType.CLABSI, Status = HAICaseStatus.Confirmed,
            LocationId = "U-2", LocationName = "Unit 2", Pathogen = "S. aureus"
        });

        List<HAICaseSummary> cauti = await index.GetByTypeAsync(HAIType.CAUTI);
        Assert.That(cauti.Any(c => c.CaseId == cautiId), Is.True);
        Assert.That(cauti.All(c => c.HAIType == HAIType.CAUTI), Is.True);
    }

    // ── 8 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Outbreak_Create_PersistsAllFields()
    {
        string outbreakId = Guid.NewGuid().ToString("N");
        IOutbreakGrain grain = GetOutbreakGrain(outbreakId);

        await grain.CreateOutbreakAsync(
            outbreakId, "ICU MRSA Cluster",
            "Three confirmed MRSA cases in ICU within two weeks",
            HAIType.MRSA, DateTime.UtcNow.AddDays(-14),
            "ICU-1A", "Medical ICU", "MRSA");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.OutbreakId, Is.EqualTo(outbreakId));
        Assert.That(state.Name, Is.EqualTo("ICU MRSA Cluster"));
        Assert.That(state.HAIType, Is.EqualTo(HAIType.MRSA));
        Assert.That(state.Status, Is.EqualTo(OutbreakStatus.Active));
        Assert.That(state.Pathogen, Is.EqualTo("MRSA"));
    }

    // ── 9 ─────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Outbreak_AddCase_TracksCaseIds()
    {
        string outbreakId = Guid.NewGuid().ToString("N");
        IOutbreakGrain grain = GetOutbreakGrain(outbreakId);
        await grain.CreateOutbreakAsync(
            outbreakId, "CDiff Outbreak",
            "Cluster of CDiff cases",
            HAIType.CDiff, DateTime.UtcNow,
            "WARD-3", "Ward 3", "C. difficile");

        await grain.AddCaseAsync("CASE-001");
        await grain.AddCaseAsync("CASE-002");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.LinkedCaseIds, Has.Count.EqualTo(2));
        Assert.That(state.LinkedCaseIds, Contains.Item("CASE-001"));
        Assert.That(state.LinkedCaseIds, Contains.Item("CASE-002"));
    }

    // ── 10 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Outbreak_UpdateStatus_ToControlled()
    {
        string outbreakId = Guid.NewGuid().ToString("N");
        IOutbreakGrain grain = GetOutbreakGrain(outbreakId);
        await grain.CreateOutbreakAsync(
            outbreakId, "VRE Outbreak",
            "VRE cluster",
            HAIType.VRE, DateTime.UtcNow.AddDays(-7),
            "WARD-1", "Ward 1", "VRE");

        await grain.UpdateStatusAsync(OutbreakStatus.Controlled, DateTime.UtcNow, null);

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.Status, Is.EqualTo(OutbreakStatus.Controlled));
        Assert.That(state.ControlDate, Is.Not.Null);
    }

    // ── 11 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task Outbreak_NotifyPublicHealth_SetsFlag()
    {
        string outbreakId = Guid.NewGuid().ToString("N");
        IOutbreakGrain grain = GetOutbreakGrain(outbreakId);
        await grain.CreateOutbreakAsync(
            outbreakId, "CRE Outbreak",
            "CRE cluster requiring notification",
            HAIType.CRE, DateTime.UtcNow,
            "ICU-2", "Surgical ICU", "CRE");

        await grain.NotifyPublicHealthAsync(DateTime.UtcNow);

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.NotifiedPublicHealth, Is.True);
        Assert.That(state.NotificationDate, Is.Not.Null);
    }

    // ── 12 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task OutbreakIndex_UpsertAndGetActive()
    {
        IOutbreakIndexGrain index = GetOutbreakIndex();

        string activeId = Guid.NewGuid().ToString("N");
        string closedId = Guid.NewGuid().ToString("N");
        await index.UpsertOutbreakAsync(new OutbreakSummary
        {
            OutbreakId = activeId, Name = "Active Outbreak",
            HAIType = HAIType.MRSA, Status = OutbreakStatus.Active,
            StartDate = DateTime.UtcNow, LocationId = "ICU-1", LocationName = "ICU", CaseCount = 3
        });
        await index.UpsertOutbreakAsync(new OutbreakSummary
        {
            OutbreakId = closedId, Name = "Closed Outbreak",
            HAIType = HAIType.CDiff, Status = OutbreakStatus.Closed,
            StartDate = DateTime.UtcNow.AddDays(-30), LocationId = "WARD-5", LocationName = "Ward 5", CaseCount = 5
        });

        List<OutbreakSummary> active = await index.GetActiveAsync();
        Assert.That(active.Any(o => o.OutbreakId == activeId), Is.True);
        Assert.That(active.All(o => o.Status == OutbreakStatus.Active), Is.True);
    }

    // ── 13 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task HAICase_LinkToOutbreak_SetsOutbreakId()
    {
        string caseId = Guid.NewGuid().ToString("N");
        string outbreakId = Guid.NewGuid().ToString("N");
        IHAICaseGrain grain = GetCaseGrain(caseId);
        await CreateDefaultCase(grain, caseId);

        await grain.LinkToOutbreakAsync(outbreakId);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.OutbreakId, Is.EqualTo(outbreakId));
    }

    // ── 14 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task EndToEnd_CaseCreatedLinkedToOutbreakAndIndexed()
    {
        string caseId = Guid.NewGuid().ToString("N");
        string outbreakId = Guid.NewGuid().ToString("N");

        // Create outbreak
        IOutbreakGrain outbreak = GetOutbreakGrain(outbreakId);
        await outbreak.CreateOutbreakAsync(
            outbreakId, "E2E Outbreak",
            "End-to-end test outbreak",
            HAIType.MRSA, DateTime.UtcNow,
            "ICU-1A", "Medical ICU", "MRSA");

        // Create case and link to outbreak
        IHAICaseGrain caseGrain = GetCaseGrain(caseId);
        await caseGrain.CreateCaseAsync(
            caseId, "PAT-E2E", "E2E Patient",
            new DateTime(1970, 1, 1), "ICU-1A", "Medical ICU",
            HAIType.MRSA, DateTime.UtcNow, "MRSA",
            "RN-E2E", "Nurse E2E", null);

        await caseGrain.LinkToOutbreakAsync(outbreakId);
        await outbreak.AddCaseAsync(caseId);

        // Confirm updates
        HAICaseState caseState = await caseGrain.GetCaseAsync();
        Assert.That(caseState.OutbreakId, Is.EqualTo(outbreakId));

        OutbreakState outbreakState = await outbreak.GetOutbreakAsync();
        Assert.That(outbreakState.LinkedCaseIds, Contains.Item(caseId));

        // Index the case
        IHAICaseIndexGrain caseIndex = GetCaseIndex();
        await caseIndex.UpsertCaseAsync(new HAICaseSummary
        {
            CaseId = caseId, PatientId = "PAT-E2E", PatientName = "E2E Patient",
            HAIType = HAIType.MRSA, Status = HAICaseStatus.Suspected,
            LocationId = "ICU-1A", LocationName = "Medical ICU",
            Pathogen = "MRSA", OutbreakId = outbreakId
        });

        List<HAICaseSummary> all = await caseIndex.GetAllCasesAsync();
        Assert.That(all.Any(c => c.CaseId == caseId && c.OutbreakId == outbreakId), Is.True);
    }
}
