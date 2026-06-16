// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class ReferenceDataImportTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICptCodeGrain GetCptGrain(string code) =>
        _cluster.GrainFactory.GetGrain<ICptCodeGrain>($"CPT:{code}");

    private ICptCodeIndexGrain GetCptIndex() =>
        _cluster.GrainFactory.GetGrain<ICptCodeIndexGrain>("CPT-INDEX");

    private ILoincCodeGrain GetLoincGrain(string code) =>
        _cluster.GrainFactory.GetGrain<ILoincCodeGrain>($"LOINC:{code}");

    private ILoincCodeIndexGrain GetLoincIndex() =>
        _cluster.GrainFactory.GetGrain<ILoincCodeIndexGrain>("LOINC-INDEX");

    // ── 1 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_CreateAndGet()
    {
        // Arrange
        string code = $"T{Guid.NewGuid().ToString()[..4]}";
        ICptCodeGrain grain = GetCptGrain(code);

        // Act
        await grain.LoadCodeAsync(code, "Test Procedure", "Test procedure long description",
            "Surgery", 1.5m, 2.5m, "ACTIVE", DateTime.UtcNow);

        // Assert
        CptCodeState state = await grain.GetCodeAsync();
        Assert.That(state.Code, Is.EqualTo(code));
        Assert.That(state.ShortName, Is.EqualTo("Test Procedure"));
        Assert.That(state.LongDescription, Is.EqualTo("Test procedure long description"));
        Assert.That(state.Category, Is.EqualTo("Surgery"));
        Assert.That(state.WorkRvu, Is.EqualTo(1.5m));
        Assert.That(state.TotalRvu, Is.EqualTo(2.5m));
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // ── 2 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_SearchByTerm()
    {
        // Arrange
        ICptCodeIndexGrain index = GetCptIndex();
        string uniqueTerm = $"Xyzzy{Guid.NewGuid().ToString()[..4]}";
        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = "99999",
            ShortName = $"{uniqueTerm} procedure",
            LongDescription = "A unique test procedure",
            Category = "E&M",
            Status = "ACTIVE"
        });

        // Act
        List<CptCodeIndexEntry> results = await index.SearchAsync(uniqueTerm, 50);

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].ShortName, Does.Contain(uniqueTerm));
    }

    // ── 3 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCodeIndex_AddAndSearch()
    {
        // Arrange
        ICptCodeIndexGrain index = GetCptIndex();
        string code1 = $"C{Guid.NewGuid().ToString()[..4]}";
        string code2 = $"C{Guid.NewGuid().ToString()[..4]}";

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = code1,
            ShortName = "Office visit level 3",
            LongDescription = "Office visit evaluation management level 3",
            Category = "E&M",
            Status = "ACTIVE"
        });

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = code2,
            ShortName = "Office visit level 4",
            LongDescription = "Office visit evaluation management level 4",
            Category = "E&M",
            Status = "ACTIVE"
        });

        // Act
        CptCodeIndexEntry? found = await index.GetCodeAsync(code1);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Code, Is.EqualTo(code1));
        Assert.That(found.ShortName, Is.EqualTo("Office visit level 3"));
    }

    // ── 4 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_CreateAndGet()
    {
        // Arrange
        string code = $"9{Guid.NewGuid().ToString()[..3]}-0";
        ILoincCodeGrain grain = GetLoincGrain(code);

        // Act
        await grain.LoadCodeAsync(code, "Glucose", "MCnc", "Pt", "Ser/Plas",
            "Qn", "", "Glucose SerPl-mCnc",
            "Glucose [Mass/volume] in Serum or Plasma", "ACTIVE", "1");

        // Assert
        LoincCodeState state = await grain.GetCodeAsync();
        Assert.That(state.Code, Is.EqualTo(code));
        Assert.That(state.Component, Is.EqualTo("Glucose"));
        Assert.That(state.Property, Is.EqualTo("MCnc"));
        Assert.That(state.System, Is.EqualTo("Ser/Plas"));
        Assert.That(state.Scale, Is.EqualTo("Qn"));
        Assert.That(state.LongCommonName, Is.EqualTo("Glucose [Mass/volume] in Serum or Plasma"));
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // ── 5 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_SearchByTerm()
    {
        // Arrange
        ILoincCodeIndexGrain index = GetLoincIndex();
        string uniqueComponent = $"Quux{Guid.NewGuid().ToString()[..4]}";
        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = "88888-8",
            Component = uniqueComponent,
            Property = "MCnc",
            TimeAspect = "Pt",
            System = "Ser/Plas",
            Scale = "Qn",
            Method = "",
            ShortName = $"{uniqueComponent} SerPl",
            LongCommonName = $"{uniqueComponent} in Serum",
            Status = "ACTIVE",
            ClassType = "1"
        });

        // Act
        List<LoincCodeIndexEntry> results = await index.SearchAsync(uniqueComponent, 50);

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].Component, Is.EqualTo(uniqueComponent));
    }

    // ── 6 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCodeIndex_AddAndSearch()
    {
        // Arrange
        ILoincCodeIndexGrain index = GetLoincIndex();
        string code1 = $"7{Guid.NewGuid().ToString()[..3]}-1";
        string code2 = $"7{Guid.NewGuid().ToString()[..3]}-2";

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = code1,
            Component = "Hemoglobin",
            ShortName = "Hgb Bld",
            LongCommonName = "Hemoglobin in Blood",
            Status = "ACTIVE",
            ClassType = "1"
        });

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = code2,
            Component = "Hematocrit",
            ShortName = "Hct Bld",
            LongCommonName = "Hematocrit of Blood",
            Status = "ACTIVE",
            ClassType = "1"
        });

        // Act
        LoincCodeIndexEntry? found = await index.GetCodeAsync(code1);

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Code, Is.EqualTo(code1));
        Assert.That(found.Component, Is.EqualTo("Hemoglobin"));
    }

    // ── 7 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_IdempotentCreate()
    {
        // Arrange
        string code = $"I{Guid.NewGuid().ToString()[..4]}";
        ICptCodeGrain grain = GetCptGrain(code);

        // Act — load twice
        await grain.LoadCodeAsync(code, "First Name", "First description",
            "E&M", 1.0m, 2.0m, "ACTIVE", null);
        await grain.LoadCodeAsync(code, "Second Name", "Second description",
            "Surgery", 3.0m, 4.0m, "ACTIVE", null);

        // Assert — second load overwrites (grain-level idempotency check is in the import service)
        CptCodeState state = await grain.GetCodeAsync();
        Assert.That(state.ShortName, Is.EqualTo("Second Name"));
        Assert.That(state.Category, Is.EqualTo("Surgery"));
    }

    // ── 8 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_IdempotentCreate()
    {
        // Arrange
        string code = $"8{Guid.NewGuid().ToString()[..3]}-9";
        ILoincCodeGrain grain = GetLoincGrain(code);

        // Act — load twice
        await grain.LoadCodeAsync(code, "First", "MCnc", "Pt", "Bld",
            "Qn", "", "First Short", "First Long", "ACTIVE", "1");
        await grain.LoadCodeAsync(code, "Second", "NCnc", "24H", "Urine",
            "Ord", "Strip", "Second Short", "Second Long", "ACTIVE", "2");

        // Assert — second load overwrites
        LoincCodeState state = await grain.GetCodeAsync();
        Assert.That(state.Component, Is.EqualTo("Second"));
        Assert.That(state.System, Is.EqualTo("Urine"));
    }

    // ── 9 ────────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_MultipleCategories()
    {
        // Arrange
        ICptCodeIndexGrain index = GetCptIndex();
        string suffix = Guid.NewGuid().ToString()[..4];

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = $"EM{suffix}",
            ShortName = "E&M visit",
            LongDescription = "E&M visit",
            Category = "E&M",
            Status = "ACTIVE"
        });

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = $"SG{suffix}",
            ShortName = "Surgery procedure",
            LongDescription = "Surgery procedure",
            Category = "Surgery",
            Status = "ACTIVE"
        });

        // Act
        List<CptCodeIndexEntry> emResults = await index.GetByCategoryAsync("E&M", 100);
        List<CptCodeIndexEntry> surgeryResults = await index.GetByCategoryAsync("Surgery", 100);

        // Assert
        Assert.That(emResults.Any(r => r.Code == $"EM{suffix}"), Is.True);
        Assert.That(surgeryResults.Any(r => r.Code == $"SG{suffix}"), Is.True);
    }

    // ── 10 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_MultipleSystems()
    {
        // Arrange
        ILoincCodeIndexGrain index = GetLoincIndex();
        string suffix = Guid.NewGuid().ToString()[..4];

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = $"S1{suffix}-0",
            Component = "Sodium",
            System = "Ser/Plas",
            ShortName = "Na SerPl",
            LongCommonName = "Sodium in Serum",
            Status = "ACTIVE",
            ClassType = "1"
        });

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = $"S2{suffix}-0",
            Component = "Sodium",
            System = "Urine",
            ShortName = "Na Ur",
            LongCommonName = "Sodium in Urine",
            Status = "ACTIVE",
            ClassType = "1"
        });

        // Act
        List<LoincCodeIndexEntry> serumResults = await index.GetBySystemAsync("Ser/Plas", 100);
        List<LoincCodeIndexEntry> urineResults = await index.GetBySystemAsync("Urine", 100);

        // Assert
        Assert.That(serumResults.Any(r => r.Code == $"S1{suffix}-0"), Is.True);
        Assert.That(urineResults.Any(r => r.Code == $"S2{suffix}-0"), Is.True);
    }

    // ── 11 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_StatusFilter()
    {
        // Arrange
        string code = $"SF{Guid.NewGuid().ToString()[..4]}";
        ICptCodeGrain grain = GetCptGrain(code);

        await grain.LoadCodeAsync(code, "Test Status", "Description",
            "Medicine", 1.0m, 2.0m, "ACTIVE", null);

        // Act
        bool isActive = await grain.IsActiveAsync();
        await grain.DeactivateAsync();
        bool isInactive = await grain.IsActiveAsync();

        // Assert
        Assert.That(isActive, Is.True);
        Assert.That(isInactive, Is.False);
    }

    // ── 12 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_StatusFilter()
    {
        // Arrange
        string code = $"6{Guid.NewGuid().ToString()[..3]}-5";
        ILoincCodeGrain grain = GetLoincGrain(code);

        await grain.LoadCodeAsync(code, "Test", "MCnc", "Pt", "Bld",
            "Qn", "", "Test Short", "Test Long", "ACTIVE", "1");

        // Act
        bool isActive = await grain.IsActiveAsync();
        await grain.DeactivateAsync();
        bool isInactive = await grain.IsActiveAsync();

        // Assert
        Assert.That(isActive, Is.True);
        Assert.That(isInactive, Is.False);
    }

    // ── 13 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCodeIndex_EmptySearch_ReturnsAll()
    {
        // Arrange
        ICptCodeIndexGrain index = GetCptIndex();
        string suffix = Guid.NewGuid().ToString()[..4];

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = $"AL{suffix}",
            ShortName = "All Test",
            LongDescription = "All test code",
            Category = "Medicine",
            Status = "ACTIVE"
        });

        // Act — empty search term should return codes
        List<CptCodeIndexEntry> results = await index.SearchAsync("", 200);

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }

    // ── 14 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCodeIndex_EmptySearch_ReturnsAll()
    {
        // Arrange
        ILoincCodeIndexGrain index = GetLoincIndex();
        string suffix = Guid.NewGuid().ToString()[..4];

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = $"AL{suffix}-0",
            Component = "AllTest",
            ShortName = "All Test Short",
            LongCommonName = "All Test Long",
            Status = "ACTIVE",
            ClassType = "1"
        });

        // Act — empty search term should return codes
        List<LoincCodeIndexEntry> results = await index.SearchAsync("", 200);

        // Assert
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
    }

    // ── 15 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task CptCode_GetByExactCode()
    {
        // Arrange
        ICptCodeIndexGrain index = GetCptIndex();
        string code = $"EX{Guid.NewGuid().ToString()[..4]}";

        await index.AddOrUpdateEntryAsync(new CptCodeIndexEntry
        {
            Code = code,
            ShortName = "Exact Match Test",
            LongDescription = "Exact match test description",
            Category = "Pathology",
            Status = "ACTIVE"
        });

        // Act
        CptCodeIndexEntry? found = await index.GetCodeAsync(code);
        CptCodeIndexEntry? notFound = await index.GetCodeAsync($"NOTEXIST{Guid.NewGuid().ToString()[..4]}");

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Code, Is.EqualTo(code));
        Assert.That(found.ShortName, Is.EqualTo("Exact Match Test"));
        Assert.That(notFound, Is.Null);
    }

    // ── 16 ───────────────────────────────────────────────────────────────────
    [Test]
    public async Task LoincCode_GetByExactCode()
    {
        // Arrange
        ILoincCodeIndexGrain index = GetLoincIndex();
        string code = $"5{Guid.NewGuid().ToString()[..3]}-7";

        await index.AddOrUpdateEntryAsync(new LoincCodeIndexEntry
        {
            Code = code,
            Component = "Creatinine",
            Property = "MCnc",
            TimeAspect = "Pt",
            System = "Ser/Plas",
            Scale = "Qn",
            Method = "",
            ShortName = "Creat SerPl",
            LongCommonName = "Creatinine [Mass/volume] in Serum or Plasma",
            Status = "ACTIVE",
            ClassType = "1"
        });

        // Act
        LoincCodeIndexEntry? found = await index.GetCodeAsync(code);
        LoincCodeIndexEntry? notFound = await index.GetCodeAsync($"NOTEXIST{Guid.NewGuid().ToString()[..4]}-0");

        // Assert
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Code, Is.EqualTo(code));
        Assert.That(found.Component, Is.EqualTo("Creatinine"));
        Assert.That(found.LongCommonName, Is.EqualTo("Creatinine [Mass/volume] in Serum or Plasma"));
        Assert.That(notFound, Is.Null);
    }
}
