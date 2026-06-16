// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class Icd10GrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task Icd10Grain_CanLoadAndRetrieveCode()
    {
        var grain = _cluster.GrainFactory.GetGrain<IIcd10Grain>("A00.0");

        await grain.LoadCodeAsync(
            "A00.0",
            "Cholera due to Vibrio cholerae 01, biovar cholerae",
            "Cholera due to Vibrio cholerae 01, biovar cholerae",
            isBillable: true,
            orderNumber: 2,
            chapter: "A00-B99 Certain infectious and parasitic diseases");

        var state = await grain.GetCodeAsync();
        Assert.That(state.Code, Is.EqualTo("A00.0"));
        Assert.That(state.ShortDescription, Does.Contain("Cholera"));
        Assert.That(state.IsBillable, Is.True);
        Assert.That(state.CodingSystem, Is.EqualTo("10D"));
        Assert.That(state.IsActive, Is.True);
    }

    [Test]
    public async Task Icd10Grain_CanDeactivateAndReactivate()
    {
        var grain = _cluster.GrainFactory.GetGrain<IIcd10Grain>("Z99.99");

        await grain.LoadCodeAsync("Z99.99", "Test code", "Test code long", true, 99999, null);

        await grain.DeactivateAsync();
        Assert.That(await grain.IsActiveAsync(), Is.False);

        await grain.ActivateAsync();
        Assert.That(await grain.IsActiveAsync(), Is.True);
    }
}

[TestFixture]
public class Icd10IndexGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task LoadTestCodes()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await index.LoadCodesAsync(
        [
            new Icd10IndexEntry
            {
                Code = "A00", ShortDescription = "Cholera", LongDescription = "Cholera",
                IsBillable = false, OrderNumber = 1, Chapter = "A00-B99 Certain infectious and parasitic diseases"
            },
            new Icd10IndexEntry
            {
                Code = "A00.0", ShortDescription = "Cholera due to Vibrio cholerae 01, biovar cholerae",
                LongDescription = "Cholera due to Vibrio cholerae 01, biovar cholerae",
                IsBillable = true, OrderNumber = 2, Chapter = "A00-B99 Certain infectious and parasitic diseases"
            },
            new Icd10IndexEntry
            {
                Code = "A00.1", ShortDescription = "Cholera due to Vibrio cholerae 01, biovar eltor",
                LongDescription = "Cholera due to Vibrio cholerae 01, biovar eltor",
                IsBillable = true, OrderNumber = 3, Chapter = "A00-B99 Certain infectious and parasitic diseases"
            },
            new Icd10IndexEntry
            {
                Code = "E11.9", ShortDescription = "Type 2 diabetes mellitus without complications",
                LongDescription = "Type 2 diabetes mellitus without complications",
                IsBillable = true, OrderNumber = 10000, Chapter = "E00-E89 Endocrine, nutritional and metabolic diseases"
            },
            new Icd10IndexEntry
            {
                Code = "E11.65", ShortDescription = "Type 2 diabetes mellitus with hyperglycemia",
                LongDescription = "Type 2 diabetes mellitus with hyperglycemia",
                IsBillable = true, OrderNumber = 10001, Chapter = "E00-E89 Endocrine, nutritional and metabolic diseases"
            },
            new Icd10IndexEntry
            {
                Code = "M54.5", ShortDescription = "Low back pain",
                LongDescription = "Low back pain",
                IsBillable = true, OrderNumber = 50000, Chapter = "M00-M99 Musculoskeletal and connective tissue diseases"
            },
            new Icd10IndexEntry
            {
                Code = "I10", ShortDescription = "Essential (primary) hypertension",
                LongDescription = "Essential (primary) hypertension",
                IsBillable = true, OrderNumber = 30000, Chapter = "I00-I99 Circulatory system diseases"
            }
        ]);
    }

    [Test]
    public async Task Index_LoadAndStatus()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(7));
        Assert.That(status.BillableCodes, Is.EqualTo(6)); // A00 is header
    }

    [Test]
    public async Task Index_GetCode_ExactMatch()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        var entry = await index.GetCodeAsync("M54.5");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ShortDescription, Is.EqualTo("Low back pain"));
        Assert.That(entry.IsBillable, Is.True);
    }

    [Test]
    public async Task Index_GetCode_NotFound()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        var entry = await index.GetCodeAsync("Z99.99");
        Assert.That(entry, Is.Null);
    }

    [Test]
    public async Task Index_SearchByCodePrefix()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        // Search "A00" should match A00, A00.0, A00.1
        var results = await index.SearchAsync("A00", billableOnly: false, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Index_SearchByCodePrefix_BillableOnly()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        // "A00" billable only should exclude the header code
        var results = await index.SearchAsync("A00", billableOnly: true, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.IsBillable), Is.True);
    }

    [Test]
    public async Task Index_SearchByDescription()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        // Text search "diabetes" should find E11.9 and E11.65
        var results = await index.SearchAsync("diabetes", billableOnly: false, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Code.StartsWith("E11")), Is.True);
    }

    [Test]
    public async Task Index_SearchByDescription_CaseInsensitive()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        var results = await index.SearchAsync("HYPERTENSION", billableOnly: false, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Code, Is.EqualTo("I10"));
    }

    [Test]
    public async Task Index_GetByPrefix()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        var results = await index.GetByPrefixAsync("E11", billableOnly: false, maxResults: 50);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Code.StartsWith("E11")), Is.True);
    }

    [Test]
    public async Task Index_Clear()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await LoadTestCodes();

        await index.ClearAsync();
        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.False);
        Assert.That(status.TotalCodes, Is.EqualTo(0));
    }
}

