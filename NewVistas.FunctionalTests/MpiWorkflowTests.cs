// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Master Patient Index (MPI) — Patient Correlation (#985),
/// MPI Search/Index, and probabilistic patient matching.
/// </summary>
[TestFixture]
public class MpiWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IMpiCorrelationGrain GetCorrelation(string icn)
        => _cluster.GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");

    private IMpiSearchGrain GetSearch()
        => _cluster.GrainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");

    private IMpiMatchGrain GetMatcher()
        => _cluster.GrainFactory.GetGrain<IMpiMatchGrain>("MPI-MATCHER");

    // --- Correlation Tests ---

    [Test]
    public async Task MpiCorrelation_SetCorrelation_PersistsIcn()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);

        await grain.SetCorrelationAsync(icn, "SMITH,JOHN A", "123456789", new DateTime(1960, 3, 15), "M");

        MpiCorrelationState state = await grain.GetCorrelationAsync();
        Assert.That(state.Icn, Is.EqualTo(icn));
    }

    [Test]
    public async Task MpiCorrelation_SetCorrelation_PersistsName()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);

        await grain.SetCorrelationAsync(icn, "DOE,JANE B", "987654321", new DateTime(1975, 7, 20), "F");

        MpiCorrelationState state = await grain.GetCorrelationAsync();
        Assert.That(state.PatientName, Is.EqualTo("DOE,JANE B"));
    }

    [Test]
    public async Task MpiCorrelation_SetCorrelation_PersistsSsnDob()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        DateTime dob = new DateTime(1985, 11, 30);

        await grain.SetCorrelationAsync(icn, "JONES,ROBERT C", "111223333", dob, "M");

        MpiCorrelationState state = await grain.GetCorrelationAsync();
        Assert.That(state.Ssn, Is.EqualTo("111223333"));
        Assert.That(state.DateOfBirth, Is.EqualTo(dob));
    }

    [Test]
    public async Task MpiCorrelation_AddFacility_PersistsCorrelation()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        await grain.SetCorrelationAsync(icn, "BROWN,ALICE D", "222334444", new DateTime(1970, 1, 5), "F");

        await grain.AddLocalCorrelationAsync("500", "VA Palo Alto", "12345", DateTime.UtcNow);

        List<MpiLocalCorrelation> facilities = await grain.GetTreatingFacilitiesAsync();
        Assert.That(facilities, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MpiCorrelation_AddMultipleFacilities_ReturnsList()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        await grain.SetCorrelationAsync(icn, "WILSON,MARY E", "333445555", new DateTime(1968, 4, 12), "F");

        await grain.AddLocalCorrelationAsync("500", "VA Palo Alto", "10001", DateTime.UtcNow);
        await grain.AddLocalCorrelationAsync("612", "VA Portland", "20002", DateTime.UtcNow);
        await grain.AddLocalCorrelationAsync("648", "VA Portland HCS", "30003", DateTime.UtcNow);

        List<MpiLocalCorrelation> facilities = await grain.GetTreatingFacilitiesAsync();
        Assert.That(facilities, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task MpiCorrelation_RemoveFacility_RemovesFromList()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        await grain.SetCorrelationAsync(icn, "TAYLOR,JAMES F", "444556666", new DateTime(1955, 9, 22), "M");

        await grain.AddLocalCorrelationAsync("500", "VA Palo Alto", "40001", DateTime.UtcNow);
        await grain.AddLocalCorrelationAsync("612", "VA Portland", "50002", DateTime.UtcNow);

        await grain.RemoveLocalCorrelationAsync("500");

        List<MpiLocalCorrelation> facilities = await grain.GetTreatingFacilitiesAsync();
        Assert.That(facilities, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MpiCorrelation_UpdateLastSeen_UpdatesDate()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        await grain.SetCorrelationAsync(icn, "GARCIA,MARIA G", "555667777", new DateTime(1980, 6, 18), "F");

        DateTime correlationDate = DateTime.UtcNow.AddDays(-30);
        await grain.AddLocalCorrelationAsync("500", "VA Palo Alto", "60001", correlationDate);

        DateTime newLastSeen = DateTime.UtcNow;
        await grain.UpdateLastSeenAsync("500", newLastSeen);

        List<MpiLocalCorrelation> facilities = await grain.GetTreatingFacilitiesAsync();
        MpiLocalCorrelation? facility = facilities.FirstOrDefault(f => f.FacilityId == "500");
        Assert.That(facility, Is.Not.Null);
        Assert.That(facility!.LastSeenDate, Is.EqualTo(newLastSeen));
    }

    [Test]
    public async Task MpiCorrelation_MarkDeceased_SetsFields()
    {
        string icn = $"ICN-{Guid.NewGuid()}";
        IMpiCorrelationGrain grain = GetCorrelation(icn);
        await grain.SetCorrelationAsync(icn, "MARTINEZ,CARLOS H", "666778888", new DateTime(1940, 2, 28), "M");

        DateTime deathDate = new DateTime(2025, 12, 15);
        await grain.MarkDeceasedAsync(deathDate);

        MpiCorrelationState state = await grain.GetCorrelationAsync();
        Assert.That(state.IsDeceased, Is.True);
        Assert.That(state.DateOfDeath, Is.EqualTo(deathDate));
    }

    // --- Search Tests ---

    [Test]
    public async Task MpiSearch_AddAndSearch_FindsByNamePrefix()
    {
        IMpiSearchGrain search = GetSearch();
        string icn = $"ICN-{Guid.NewGuid()}";

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = "SMITH,JOHN A",
            Ssn = "123456789",
            DateOfBirth = new DateTime(1960, 3, 15),
            Sex = "M",
            FacilityCount = 1
        });

        List<MpiSearchResult> results = await search.SearchAsync("SMITH", 10);
        Assert.That(results.Any(r => r.Icn == icn), Is.True);
    }

    [Test]
    public async Task MpiSearch_AddAndSearch_FindsBySsnLast4()
    {
        IMpiSearchGrain search = GetSearch();
        string icn = $"ICN-{Guid.NewGuid()}";

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = "THOMPSON,LINDA I",
            Ssn = "123456789",
            DateOfBirth = new DateTime(1972, 8, 3),
            Sex = "F",
            FacilityCount = 2
        });

        List<MpiSearchResult> results = await search.SearchAsync("6789", 10);
        Assert.That(results.Any(r => r.Icn == icn), Is.True);
    }

    [Test]
    public async Task MpiSearch_LookupByIcn_ExactMatch()
    {
        IMpiSearchGrain search = GetSearch();
        string icn = $"ICN-{Guid.NewGuid()}";

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = "ANDERSON,DAVID J",
            Ssn = "234567890",
            DateOfBirth = new DateTime(1988, 12, 1),
            Sex = "M",
            FacilityCount = 1
        });

        MpiSearchResult? result = await search.LookupByIcnAsync(icn);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PatientName, Is.EqualTo("ANDERSON,DAVID J"));
    }

    [Test]
    public async Task MpiSearch_LookupByIcn_NotFound()
    {
        IMpiSearchGrain search = GetSearch();

        MpiSearchResult? result = await search.LookupByIcnAsync($"NONEXISTENT-{Guid.NewGuid()}");
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task MpiSearch_LookupBySsn_FindsPatient()
    {
        IMpiSearchGrain search = GetSearch();
        string icn = $"ICN-{Guid.NewGuid()}";
        string ssn = $"999{Guid.NewGuid().ToString("N")[..6]}";

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = "CLARK,SUSAN K",
            Ssn = ssn,
            DateOfBirth = new DateTime(1965, 5, 25),
            Sex = "F",
            FacilityCount = 1
        });

        List<MpiSearchResult> results = await search.LookupBySsnAsync(ssn);
        Assert.That(results.Any(r => r.Icn == icn), Is.True);
    }

    [Test]
    public async Task MpiSearch_SeedDemoData_PopulatesIndex()
    {
        IMpiSearchGrain search = GetSearch();

        await search.SeedDemoDataAsync();

        MpiIndexStatus status = await search.GetStatusAsync();
        Assert.That(status.TotalPatients, Is.GreaterThanOrEqualTo(10));
    }

    // --- Match Tests ---

    [Test]
    public async Task MpiMatch_FindMatch_ExactSsnMatch()
    {
        IMpiSearchGrain search = GetSearch();
        await search.SeedDemoDataAsync();

        // Use a known seeded patient: SMITH,JOHN A / ICN 1000000001V123456 / SSN 666000001
        MpiSearchResult? target = await search.LookupByIcnAsync("1000000001V123456");
        Assert.That(target, Is.Not.Null, "Seeded patient should exist");

        IMpiMatchGrain matcher = GetMatcher();
        MpiMatchResult result = await matcher.FindMatchAsync(
            target!.PatientName, target.Ssn, target.DateOfBirth, target.Sex);

        Assert.That(result.MatchFound, Is.True);
        Assert.That(result.Confidence, Is.GreaterThan(0.7));
    }

    [Test]
    public async Task MpiMatch_FindMatch_NoMatch()
    {
        IMpiSearchGrain search = GetSearch();
        await search.SeedDemoDataAsync();

        IMpiMatchGrain matcher = GetMatcher();
        MpiMatchResult result = await matcher.FindMatchAsync(
            "ZZZZZZZ,QQQQQQQ X", "000000000", new DateTime(2099, 1, 1), "M");

        Assert.That(result.MatchFound, Is.False);
    }
}
