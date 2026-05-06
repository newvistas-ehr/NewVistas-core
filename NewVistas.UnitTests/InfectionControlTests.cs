// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Grains;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ─────────────────────────────────────────────────────────────────────────────
// HAI Case Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class HAICaseGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IHAICaseGrain GetGrain(string id) =>
        _cluster.GrainFactory.GetGrain<IHAICaseGrain>($"HAI-CASE:{id}");

    private async Task<IHAICaseGrain> CreateTestCase(string id, HAIType type = HAIType.MRSA)
    {
        IHAICaseGrain grain = GetGrain(id);
        await grain.CreateCaseAsync(
            id, "PAT-001", "John Smith", new DateTime(1975, 3, 15),
            "LOC-ICU", "Medical ICU", type,
            new DateTime(2025, 6, 1), "MRSA",
            "INF-001", "Infection Control Nurse", "Suspect line infection");
        return grain;
    }

    [Test]
    public async Task CanCreateCase()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        HAICaseState state = await grain.GetCaseAsync();

        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Smith"));
        Assert.That(state.LocationName, Is.EqualTo("Medical ICU"));
        Assert.That(state.Pathogen, Is.EqualTo("MRSA"));
    }

    [Test]
    public async Task CaseIdSetOnCreate()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.CaseId, Is.EqualTo(id));
    }

    [Test]
    public async Task DefaultStatusIsSuspected()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.Status, Is.EqualTo(HAICaseStatus.Suspected));
    }

    [Test]
    public async Task CanUpdateStatusToConfirmed()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        DateTime confirmed = new DateTime(2025, 6, 3);
        await grain.UpdateStatusAsync(HAICaseStatus.Confirmed, confirmed);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.Status, Is.EqualTo(HAICaseStatus.Confirmed));
        Assert.That(state.ConfirmedDate, Is.EqualTo(confirmed));
    }

    [Test]
    public async Task SusceptibilityResultsEmptyOnCreate()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.SusceptibilityResults, Is.Empty);
    }

    [Test]
    public async Task CanAddSusceptibilityResult()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        await grain.AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
        {
            AntibioticName = "Vancomycin",
            Susceptibility = AntibioticSusceptibility.Susceptible,
            MIC = "1 mcg/mL",
        });

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.SusceptibilityResults, Has.Count.EqualTo(1));
        Assert.That(state.SusceptibilityResults[0].AntibioticName, Is.EqualTo("Vancomycin"));
        Assert.That(state.SusceptibilityResults[0].Susceptibility, Is.EqualTo(AntibioticSusceptibility.Susceptible));
    }

    [Test]
    public async Task DuplicateAntibioticReplacesPrevious()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);

        await grain.AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
        {
            AntibioticName = "Vancomycin",
            Susceptibility = AntibioticSusceptibility.Susceptible,
        });
        await grain.AddSusceptibilityResultAsync(new AntibioticSusceptibilityResult
        {
            AntibioticName = "Vancomycin",
            Susceptibility = AntibioticSusceptibility.Resistant,
        });

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.SusceptibilityResults, Has.Count.EqualTo(1));
        Assert.That(state.SusceptibilityResults[0].Susceptibility, Is.EqualTo(AntibioticSusceptibility.Resistant));
    }

    [Test]
    public async Task CanUpdateClinicalData()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        await grain.UpdateClinicalDataAsync(
            "Blood", new DateTime(2025, 6, 2),
            "Gram-positive cocci", "MRSA isolated",
            "Central Line", 7,
            null, string.Empty);

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.CultureSource, Is.EqualTo("Blood"));
        Assert.That(state.GramStain, Is.EqualTo("Gram-positive cocci"));
        Assert.That(state.DeviceType, Is.EqualTo("Central Line"));
        Assert.That(state.DeviceInDays, Is.EqualTo(7));
    }

    [Test]
    public async Task CanLinkToOutbreak()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        await grain.LinkToOutbreakAsync("OUTBREAK-001");

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.OutbreakId, Is.EqualTo("OUTBREAK-001"));
    }

    [Test]
    public async Task CanUnlinkFromOutbreak()
    {
        string id = $"CASE-{Guid.NewGuid()}";
        IHAICaseGrain grain = await CreateTestCase(id);
        await grain.LinkToOutbreakAsync("OUTBREAK-001");
        await grain.UnlinkFromOutbreakAsync();

        HAICaseState state = await grain.GetCaseAsync();
        Assert.That(state.OutbreakId, Is.Null);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HAI Case Index Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class HAICaseIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IHAICaseIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IHAICaseIndexGrain>($"HAI-CASE-IDX-{Guid.NewGuid()}");

    private static HAICaseSummary MakeSummary(string caseId, HAIType type, HAICaseStatus status,
        string locationId = "LOC-A", DateTime? infectionDate = null, string? outbreakId = null) =>
        new()
        {
            CaseId = caseId,
            PatientId = "PAT-001",
            PatientName = "Test Patient",
            HAIType = type,
            Status = status,
            InfectionDate = infectionDate ?? new DateTime(2025, 5, 1),
            LocationId = locationId,
            LocationName = "Unit A",
            Pathogen = "MRSA",
            OutbreakId = outbreakId,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        IHAICaseIndexGrain index = GetIndex();
        List<HAICaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        IHAICaseIndexGrain index = GetIndex();
        string id = $"CASE-{Guid.NewGuid()}";
        await index.UpsertCaseAsync(MakeSummary(id, HAIType.MRSA, HAICaseStatus.Confirmed));

        List<HAICaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].CaseId, Is.EqualTo(id));
    }

    [Test]
    public async Task GetActiveFiltersRuledOutAndClosed()
    {
        IHAICaseIndexGrain index = GetIndex();
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Suspected));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.RuledOut));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Closed));

        List<HAICaseSummary> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(c => c.Status == HAICaseStatus.Suspected || c.Status == HAICaseStatus.Confirmed), Is.True);
    }

    [Test]
    public async Task GetByTypeFilters()
    {
        IHAICaseIndexGrain index = GetIndex();
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.CAUTI, HAICaseStatus.Confirmed));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Suspected));

        List<HAICaseSummary> mrsa = await index.GetByTypeAsync(HAIType.MRSA);
        Assert.That(mrsa, Has.Count.EqualTo(2));
        Assert.That(mrsa.All(c => c.HAIType == HAIType.MRSA), Is.True);
    }

    [Test]
    public async Task GetByLocationFilters()
    {
        IHAICaseIndexGrain index = GetIndex();
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, "LOC-ICU"));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, "LOC-MED"));

        List<HAICaseSummary> icuCases = await index.GetByLocationAsync("LOC-ICU");
        Assert.That(icuCases, Has.Count.EqualTo(1));
        Assert.That(icuCases[0].LocationId, Is.EqualTo("LOC-ICU"));
    }

    [Test]
    public async Task GetByOutbreakFilters()
    {
        IHAICaseIndexGrain index = GetIndex();
        string outbreakId = $"OB-{Guid.NewGuid()}";
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, outbreakId: outbreakId));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, outbreakId: null));

        List<HAICaseSummary> linked = await index.GetByOutbreakAsync(outbreakId);
        Assert.That(linked, Has.Count.EqualTo(1));
        Assert.That(linked[0].OutbreakId, Is.EqualTo(outbreakId));
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        IHAICaseIndexGrain index = GetIndex();
        string id = $"CASE-{Guid.NewGuid()}";
        await index.UpsertCaseAsync(MakeSummary(id, HAIType.MRSA, HAICaseStatus.Suspected));
        HAICaseSummary updated = MakeSummary(id, HAIType.MRSA, HAICaseStatus.Confirmed);
        await index.UpsertCaseAsync(updated);

        List<HAICaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(HAICaseStatus.Confirmed));
    }

    [Test]
    public async Task OrderedNewestInfectionDateFirst()
    {
        IHAICaseIndexGrain index = GetIndex();
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, infectionDate: new DateTime(2025, 1, 1)));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, infectionDate: new DateTime(2025, 6, 1)));
        await index.UpsertCaseAsync(MakeSummary($"CASE-{Guid.NewGuid()}", HAIType.MRSA, HAICaseStatus.Confirmed, infectionDate: new DateTime(2025, 3, 1)));

        List<HAICaseSummary> all = await index.GetAllCasesAsync();
        Assert.That(all[0].InfectionDate, Is.EqualTo(new DateTime(2025, 6, 1)));
        Assert.That(all[2].InfectionDate, Is.EqualTo(new DateTime(2025, 1, 1)));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Outbreak Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class OutbreakGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOutbreakGrain GetGrain(string id) =>
        _cluster.GrainFactory.GetGrain<IOutbreakGrain>($"HAI-OUTBREAK:{id}");

    private async Task<IOutbreakGrain> CreateTestOutbreak(string id)
    {
        IOutbreakGrain grain = GetGrain(id);
        await grain.CreateOutbreakAsync(
            id, "ICU MRSA Cluster", "Cluster of MRSA BSI in the ICU",
            HAIType.MRSA, new DateTime(2025, 6, 1),
            "LOC-ICU", "Medical ICU", "MRSA");
        return grain;
    }

    [Test]
    public async Task CanCreateOutbreak()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        OutbreakState state = await grain.GetOutbreakAsync();

        Assert.That(state.Name, Is.EqualTo("ICU MRSA Cluster"));
        Assert.That(state.HAIType, Is.EqualTo(HAIType.MRSA));
        Assert.That(state.LocationName, Is.EqualTo("Medical ICU"));
        Assert.That(state.Pathogen, Is.EqualTo("MRSA"));
    }

    [Test]
    public async Task DefaultStatusIsActive()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.Status, Is.EqualTo(OutbreakStatus.Active));
    }

    [Test]
    public async Task CanAddCaseToOutbreak()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        await grain.AddCaseAsync("CASE-001");
        await grain.AddCaseAsync("CASE-002");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.LinkedCaseIds, Has.Count.EqualTo(2));
        Assert.That(state.LinkedCaseIds, Contains.Item("CASE-001"));
        Assert.That(state.LinkedCaseIds, Contains.Item("CASE-002"));
    }

    [Test]
    public async Task DuplicateCaseNotAdded()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        await grain.AddCaseAsync("CASE-001");
        await grain.AddCaseAsync("CASE-001");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.LinkedCaseIds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CanRemoveCaseFromOutbreak()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        await grain.AddCaseAsync("CASE-001");
        await grain.AddCaseAsync("CASE-002");
        await grain.RemoveCaseAsync("CASE-001");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.LinkedCaseIds, Has.Count.EqualTo(1));
        Assert.That(state.LinkedCaseIds, Does.Not.Contain("CASE-001"));
    }

    [Test]
    public async Task CanUpdateStatusToControlled()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        DateTime controlDate = new DateTime(2025, 6, 15);
        await grain.UpdateStatusAsync(OutbreakStatus.Controlled, controlDate, null);

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.Status, Is.EqualTo(OutbreakStatus.Controlled));
        Assert.That(state.ControlDate, Is.EqualTo(controlDate));
    }

    [Test]
    public async Task CanNotifyPublicHealth()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        DateTime notifyDate = new DateTime(2025, 6, 3);
        await grain.NotifyPublicHealthAsync(notifyDate);

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.NotifiedPublicHealth, Is.True);
        Assert.That(state.NotificationDate, Is.EqualTo(notifyDate));
    }

    [Test]
    public async Task LinkedCaseIdsTracked()
    {
        string id = $"OB-{Guid.NewGuid()}";
        IOutbreakGrain grain = await CreateTestOutbreak(id);
        await grain.AddCaseAsync("CASE-A");
        await grain.AddCaseAsync("CASE-B");
        await grain.AddCaseAsync("CASE-C");

        OutbreakState state = await grain.GetOutbreakAsync();
        Assert.That(state.LinkedCaseIds, Has.Count.EqualTo(3));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Outbreak Index Grain Tests
// ─────────────────────────────────────────────────────────────────────────────

[TestFixture]
public class OutbreakIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IOutbreakIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IOutbreakIndexGrain>($"HAI-OUTBREAK-IDX-{Guid.NewGuid()}");

    private static OutbreakSummary MakeSummary(string outbreakId, OutbreakStatus status, DateTime? start = null) =>
        new()
        {
            OutbreakId = outbreakId,
            Name = "Test Outbreak",
            HAIType = HAIType.MRSA,
            Status = status,
            StartDate = start ?? new DateTime(2025, 6, 1),
            LocationId = "LOC-ICU",
            LocationName = "ICU",
            CaseCount = 3,
        };

    [Test]
    public async Task EmptyOnStart()
    {
        IOutbreakIndexGrain index = GetIndex();
        List<OutbreakSummary> all = await index.GetAllOutbreaksAsync();
        Assert.That(all, Is.Empty);
    }

    [Test]
    public async Task CanUpsertAndRetrieve()
    {
        IOutbreakIndexGrain index = GetIndex();
        string id = $"OB-{Guid.NewGuid()}";
        await index.UpsertOutbreakAsync(MakeSummary(id, OutbreakStatus.Active));

        List<OutbreakSummary> all = await index.GetAllOutbreaksAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].OutbreakId, Is.EqualTo(id));
    }

    [Test]
    public async Task GetActiveFilters()
    {
        IOutbreakIndexGrain index = GetIndex();
        await index.UpsertOutbreakAsync(MakeSummary($"OB-{Guid.NewGuid()}", OutbreakStatus.Active));
        await index.UpsertOutbreakAsync(MakeSummary($"OB-{Guid.NewGuid()}", OutbreakStatus.Controlled));
        await index.UpsertOutbreakAsync(MakeSummary($"OB-{Guid.NewGuid()}", OutbreakStatus.Closed));
        await index.UpsertOutbreakAsync(MakeSummary($"OB-{Guid.NewGuid()}", OutbreakStatus.Active));

        List<OutbreakSummary> active = await index.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.All(o => o.Status == OutbreakStatus.Active), Is.True);
    }

    [Test]
    public async Task UpsertUpdatesExisting()
    {
        IOutbreakIndexGrain index = GetIndex();
        string id = $"OB-{Guid.NewGuid()}";
        await index.UpsertOutbreakAsync(MakeSummary(id, OutbreakStatus.Active));
        OutbreakSummary updated = MakeSummary(id, OutbreakStatus.Controlled);
        await index.UpsertOutbreakAsync(updated);

        List<OutbreakSummary> all = await index.GetAllOutbreaksAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(OutbreakStatus.Controlled));
    }

    [Test]
    public async Task RemoveIsIdempotent()
    {
        IOutbreakIndexGrain index = GetIndex();
        string id = $"OB-{Guid.NewGuid()}";
        await index.UpsertOutbreakAsync(MakeSummary(id, OutbreakStatus.Active));
        await index.RemoveOutbreakAsync(id);
        await index.RemoveOutbreakAsync(id); // second remove should not throw

        List<OutbreakSummary> all = await index.GetAllOutbreaksAsync();
        Assert.That(all, Is.Empty);
    }
}
