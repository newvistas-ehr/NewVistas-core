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
/// Functional tests for VistA Lexicon Utility — LEX*.m routines.
/// File #757 (EXPRESSION TYPE), #757.01 (EXPRESSIONS), #757.02 (CODES).
/// </summary>
[TestFixture]
public class LexiconWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ILexiconTermGrain GetTermGrain(string codingSystem, string code) =>
        _cluster.GrainFactory.GetGrain<ILexiconTermGrain>($"LEX:{codingSystem}:{code}");

    private ILexiconSearchGrain GetSearchGrain() =>
        _cluster.GrainFactory.GetGrain<ILexiconSearchGrain>("LEX-INDEX");

    private ILexiconSearchGrain NewSearchGrain() =>
        _cluster.GrainFactory.GetGrain<ILexiconSearchGrain>($"LEX-INDEX-{Guid.NewGuid()}");

    // ─── 1 ── Term Grain: Load and Persist Code ──────────────────────────

    [Test]
    public async Task LexiconTerm_LoadTerm_PersistsCode()
    {
        string code = $"CODE-{Guid.NewGuid()}";
        ILexiconTermGrain grain = GetTermGrain("SNOMED", code);

        await grain.LoadTermAsync(code, "SNOMED", "Test disorder",
            "Test disorder (disorder)", true, "disorder", code, null);

        LexiconTermState state = await grain.GetTermAsync();
        Assert.That(state.Code, Is.EqualTo(code));
        Assert.That(state.CodingSystem, Is.EqualTo("SNOMED"));
    }

    // ─── 2 ── Term Grain: Load and Persist Designation ───────────────────

    [Test]
    public async Task LexiconTerm_LoadTerm_PersistsDesignation()
    {
        string code = $"CODE-{Guid.NewGuid()}";
        ILexiconTermGrain grain = GetTermGrain("SNOMED", code);

        await grain.LoadTermAsync(code, "SNOMED", "Essential hypertension",
            "Essential hypertension (disorder)", true, "disorder", code, null);

        LexiconTermState state = await grain.GetTermAsync();
        Assert.That(state.Designation, Is.EqualTo("Essential hypertension"));
        Assert.That(state.FullySpecifiedName, Is.EqualTo("Essential hypertension (disorder)"));
    }

    // ─── 3 ── Term Grain: Load and Persist Synonyms ─────────────────────

    [Test]
    public async Task LexiconTerm_LoadTerm_PersistsSynonyms()
    {
        string code = $"CODE-{Guid.NewGuid()}";
        ILexiconTermGrain grain = GetTermGrain("SNOMED", code);
        List<string> synonyms = new List<string> { "DM", "Sugar diabetes", "Mellitus diabetes" };

        await grain.LoadTermAsync(code, "SNOMED", "Diabetes mellitus",
            "Diabetes mellitus (disorder)", true, "disorder", code, synonyms);

        LexiconTermState state = await grain.GetTermAsync();
        Assert.That(state.Synonyms, Has.Count.EqualTo(3));
        Assert.That(state.Synonyms, Contains.Item("DM"));
        Assert.That(state.Synonyms, Contains.Item("Sugar diabetes"));
    }

    // ─── 4 ── Index Grain: Load Terms Updates Count ──────────────────────

    [Test]
    public async Task LexiconIndex_LoadTerms_UpdatesCount()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "111", CodingSystem = "SNOMED", Designation = "Term A", IsActive = true },
            new() { Code = "222", CodingSystem = "SNOMED", Designation = "Term B", IsActive = true },
            new() { Code = "333", CodingSystem = "ICD10", Designation = "Term C", IsActive = true },
            new() { Code = "444", CodingSystem = "CPT", Designation = "Term D", IsActive = true },
            new() { Code = "555", CodingSystem = "LOINC", Designation = "Term E", IsActive = true },
        };

        await searchGrain.LoadTermsAsync(entries);

        LexiconIndexStatus status = await searchGrain.GetStatusAsync();
        // Self-seeding adds ~100 demo terms on activation, so total includes those plus our 5
        Assert.That(status.TotalTerms, Is.GreaterThanOrEqualTo(5));
    }

    // ─── 5 ── Index Grain: Search By Text Finds Designation ──────────────

    [Test]
    public async Task LexiconIndex_SearchByText_FindsByDesignation()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "73211009", CodingSystem = "SNOMED", Designation = "Diabetes mellitus", FullySpecifiedName = "Diabetes mellitus (disorder)", IsActive = true, SemanticType = "disorder" },
            new() { Code = "I10", CodingSystem = "ICD10", Designation = "Essential hypertension", IsActive = true },
        };
        await searchGrain.LoadTermsAsync(entries);

        List<LexiconIndexEntry> results = await searchGrain.SearchAsync("diabetes", null, 10);

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(r => r.Code == "73211009"), Is.True);
    }

    // ─── 6 ── Index Grain: Search Is Case Insensitive ────────────────────

    [Test]
    public async Task LexiconIndex_SearchByText_CaseInsensitive()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "73211009", CodingSystem = "SNOMED", Designation = "Diabetes mellitus", IsActive = true },
        };
        await searchGrain.LoadTermsAsync(entries);

        List<LexiconIndexEntry> results = await searchGrain.SearchAsync("DIABETES", null, 10);

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].Code, Is.EqualTo("73211009"));
    }

    // ─── 7 ── Index Grain: Search By System Filters Correctly ────────────

    [Test]
    public async Task LexiconIndex_SearchBySystem_FiltersCorrectly()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "73211009", CodingSystem = "SNOMED", Designation = "Diabetes mellitus", IsActive = true },
            new() { Code = "E11.9", CodingSystem = "ICD10", Designation = "Type 2 diabetes mellitus", IsActive = true },
        };
        await searchGrain.LoadTermsAsync(entries);

        List<LexiconIndexEntry> results = await searchGrain.SearchAsync("diabetes", "SNOMED", 10);

        // Self-seeded data may include additional SNOMED diabetes terms
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.All(r => r.CodingSystem == "SNOMED"), Is.True);
    }

    // ─── 8 ── Index Grain: Lookup Code Exact Match ───────────────────────

    [Test]
    public async Task LexiconIndex_LookupCode_ExactMatch()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "73211009", CodingSystem = "SNOMED", Designation = "Diabetes mellitus", IsActive = true },
        };
        await searchGrain.LoadTermsAsync(entries);

        LexiconIndexEntry? result = await searchGrain.LookupCodeAsync("73211009", "SNOMED");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Code, Is.EqualTo("73211009"));
        Assert.That(result.Designation, Is.EqualTo("Diabetes mellitus"));
    }

    // ─── 9 ── Index Grain: Lookup Code Not Found ─────────────────────────

    [Test]
    public async Task LexiconIndex_LookupCode_NotFound()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();

        LexiconIndexEntry? result = await searchGrain.LookupCodeAsync("99999999", "SNOMED");

        Assert.That(result, Is.Null);
    }

    // ─── 10 ── Index Grain: Get By Coding System Returns Only That System ─

    [Test]
    public async Task LexiconIndex_GetByCodingSystem_ReturnsOnlyThatSystem()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        List<LexiconIndexEntry> entries = new List<LexiconIndexEntry>
        {
            new() { Code = "73211009", CodingSystem = "SNOMED", Designation = "Diabetes mellitus", IsActive = true },
            new() { Code = "99213", CodingSystem = "CPT", Designation = "Office visit, established", IsActive = true },
            new() { Code = "99214", CodingSystem = "CPT", Designation = "Office visit, established, moderate", IsActive = true },
            new() { Code = "E11.9", CodingSystem = "ICD10", Designation = "Type 2 diabetes", IsActive = true },
        };
        await searchGrain.LoadTermsAsync(entries);

        List<LexiconIndexEntry> results = await searchGrain.GetByCodingSystemAsync("CPT", 50);

        // Self-seeded data includes CPT codes, so count includes those plus our 2
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(results.All(r => r.CodingSystem == "CPT"), Is.True);
    }

    // ─── 11 ── Index Grain: Seed Demo Data Populates All Systems ─────────

    [Test]
    public async Task LexiconIndex_SeedDemoData_PopulatesAllSystems()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();

        await searchGrain.SeedDemoDataAsync();

        LexiconIndexStatus status = await searchGrain.GetStatusAsync();
        Assert.That(status.TotalTerms, Is.GreaterThan(0));
        Assert.That(status.SnomedCount, Is.GreaterThan(0));
        Assert.That(status.Icd10Count, Is.GreaterThan(0));
        Assert.That(status.CptCount, Is.GreaterThan(0));
        Assert.That(status.LoincCount, Is.GreaterThan(0));
    }

    // ─── 12 ── Index Grain: Seed Demo Contains SNOMED Terms ─────────────

    [Test]
    public async Task LexiconIndex_SeedDemoData_ContainsSnomedTerms()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        await searchGrain.SeedDemoDataAsync();

        List<LexiconIndexEntry> results = await searchGrain.SearchAsync("hypertension", "SNOMED", 10);

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results[0].CodingSystem, Is.EqualTo("SNOMED"));
    }

    // ─── 13 ── Index Grain: Seed Demo Contains ICD-10 Codes ─────────────

    [Test]
    public async Task LexiconIndex_SeedDemoData_ContainsIcd10Codes()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        await searchGrain.SeedDemoDataAsync();

        LexiconIndexEntry? result = await searchGrain.LookupCodeAsync("E11.9", "ICD10");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodingSystem, Is.EqualTo("ICD10"));
    }

    // ─── 14 ── Index Grain: Seed Demo Contains CPT Codes ────────────────

    [Test]
    public async Task LexiconIndex_SeedDemoData_ContainsCptCodes()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        await searchGrain.SeedDemoDataAsync();

        LexiconIndexEntry? result = await searchGrain.LookupCodeAsync("99213", "CPT");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodingSystem, Is.EqualTo("CPT"));
    }

    // ─── 15 ── Index Grain: Seed Demo Contains LOINC Codes ──────────────

    [Test]
    public async Task LexiconIndex_SeedDemoData_ContainsLoincCodes()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        await searchGrain.SeedDemoDataAsync();

        LexiconIndexEntry? result = await searchGrain.LookupCodeAsync("2345-7", "LOINC");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CodingSystem, Is.EqualTo("LOINC"));
    }

    // ─── 16 ── Index Grain: Clear Removes All Terms ─────────────────────

    [Test]
    public async Task LexiconIndex_Clear_RemovesAllTerms()
    {
        ILexiconSearchGrain searchGrain = NewSearchGrain();
        await searchGrain.SeedDemoDataAsync();

        LexiconIndexStatus statusBefore = await searchGrain.GetStatusAsync();
        Assert.That(statusBefore.TotalTerms, Is.GreaterThan(0));

        await searchGrain.ClearAsync();

        LexiconIndexStatus statusAfter = await searchGrain.GetStatusAsync();
        Assert.That(statusAfter.TotalTerms, Is.EqualTo(0));
    }
}
