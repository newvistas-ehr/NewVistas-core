// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.UnitTests;

/// <summary>
/// Guards the batched reference-data import against the data-destroying bug
/// where per-batch flushes called LoadCodesAsync — which clears the whole
/// index — so an import ended up with only the final ≤100 codes.
///
/// Contract under test:
///   LoadCodesAsync / LoadProductsAsync = FULL REPLACE (single-shot loads)
///   AddCodesAsync / AddProductsAsync   = batch-safe additive upsert
///   Import sequence = Clear once, then Add per batch → everything retained.
///
/// Uses unique grain keys per test (not the "ICD10-INDEX" singleton) so the
/// fixture stays safe under ParallelScope.Fixtures.
/// </summary>
[TestFixture]
public class ReferenceDataIndexBatchTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static Icd10IndexEntry Icd10(string code, string desc, bool billable = true, int order = 0) =>
        new()
        {
            Code = code,
            ShortDescription = desc,
            LongDescription = desc,
            IsBillable = billable,
            OrderNumber = order,
            Chapter = "T00-T99 Test chapter",
            IsActive = true
        };

    private IIcd10IndexGrain NewIcd10Index() =>
        _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>($"ICD10-INDEX-{Guid.NewGuid()}");

    [Test]
    public async Task Icd10IndexGrain_AddCodes_TwoBatches_RetainsBothBatches()
    {
        var index = NewIcd10Index();

        await index.AddCodesAsync(
        [
            Icd10("A00", "Cholera header", billable: false, order: 1),
            Icd10("A00.0", "Cholera classic", order: 2)
        ]);

        await index.AddCodesAsync(
        [
            Icd10("E11.9", "Type 2 diabetes without complications", order: 3),
            Icd10("I10", "Essential hypertension", order: 4)
        ]);

        // Both batches present — the second batch must NOT wipe the first.
        Assert.That(await index.GetCodeAsync("A00"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("A00.0"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("E11.9"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("I10"), Is.Not.Null);

        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(4));
        Assert.That(status.BillableCodes, Is.EqualTo(3)); // A00 is a header code
    }

    [Test]
    public async Task Icd10IndexGrain_ImportSequence_ClearThenAddBatches_RetainsEverything()
    {
        var index = NewIcd10Index();

        // Stale catalog from a previous load.
        await index.LoadCodesAsync([Icd10("Z99.89", "Stale code from prior load", order: 1)]);

        // Import-shaped sequence: clear once, then additive batches.
        await index.ClearAsync();
        await index.AddCodesAsync(
        [
            Icd10("A00.0", "Cholera classic", order: 1),
            Icd10("A00.1", "Cholera eltor", order: 2)
        ]);
        await index.AddCodesAsync(
        [
            Icd10("M54.5", "Low back pain", order: 3)
        ]);

        // Stale code replaced, everything from both batches retained.
        Assert.That(await index.GetCodeAsync("Z99.89"), Is.Null);
        Assert.That(await index.GetCodeAsync("A00.0"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("A00.1"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("M54.5"), Is.Not.Null);

        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(3));
        Assert.That(status.BillableCodes, Is.EqualTo(3));
    }

    [Test]
    public async Task Icd10IndexGrain_AddCodes_UpsertSameCode_DoesNotDoubleCount()
    {
        var index = NewIcd10Index();

        await index.AddCodesAsync([Icd10("I10", "Essential hypertension", order: 1)]);
        await index.AddCodesAsync([Icd10("I10", "Essential (primary) hypertension", order: 1)]);

        var status = await index.GetStatusAsync();
        Assert.That(status.TotalCodes, Is.EqualTo(1));
        Assert.That(status.BillableCodes, Is.EqualTo(1));

        var entry = await index.GetCodeAsync("I10");
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ShortDescription, Is.EqualTo("Essential (primary) hypertension"));
    }

    [Test]
    public async Task Icd10IndexGrain_LoadCodes_StillFullyReplaces()
    {
        var index = NewIcd10Index();

        await index.AddCodesAsync(
        [
            Icd10("A00.0", "Cholera classic", order: 1),
            Icd10("E11.9", "Type 2 diabetes without complications", order: 2)
        ]);

        // Single-shot full load replaces everything previously added.
        await index.LoadCodesAsync([Icd10("I10", "Essential hypertension", order: 1)]);

        Assert.That(await index.GetCodeAsync("A00.0"), Is.Null);
        Assert.That(await index.GetCodeAsync("E11.9"), Is.Null);
        Assert.That(await index.GetCodeAsync("I10"), Is.Not.Null);

        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(1));
        Assert.That(status.BillableCodes, Is.EqualTo(1));
    }

    [Test]
    public async Task CptCodeIndexGrain_AddCodes_TwoBatches_RetainsBothBatches()
    {
        var index = _cluster.GrainFactory.GetGrain<ICptCodeIndexGrain>($"CPT-INDEX-{Guid.NewGuid()}");

        await index.AddCodesAsync(
        [
            new CptCodeIndexEntry { Code = "99213", ShortName = "Office visit est", LongDescription = "Office outpatient visit, established", Category = "E/M", Status = "ACTIVE" }
        ]);
        await index.AddCodesAsync(
        [
            new CptCodeIndexEntry { Code = "27447", ShortName = "Total knee arthroplasty", LongDescription = "Arthroplasty, knee, condyle and plateau", Category = "Surgery", Status = "ACTIVE" },
            new CptCodeIndexEntry { Code = "00000", ShortName = "Retired code", LongDescription = "Retired code", Category = "Misc", Status = "INACTIVE" }
        ]);

        Assert.That(await index.GetCodeAsync("99213"), Is.Not.Null);
        Assert.That(await index.GetCodeAsync("27447"), Is.Not.Null);

        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(3));
        Assert.That(status.ActiveCodes, Is.EqualTo(2));

        // LoadCodesAsync remains a full replace.
        await index.LoadCodesAsync(
        [
            new CptCodeIndexEntry { Code = "10021", ShortName = "FNA without imaging", LongDescription = "Fine needle aspiration without imaging guidance", Category = "Surgery", Status = "ACTIVE" }
        ]);
        Assert.That(await index.GetCodeAsync("99213"), Is.Null);
        Assert.That((await index.GetStatusAsync()).TotalCodes, Is.EqualTo(1));
    }
}

/// <summary>
/// End-to-end regression for the batched CSV import: more than one full batch
/// (BatchSize = 100) must survive into the index grain. Before the fix, every
/// per-batch flush called LoadCodesAsync (full replace), so the index kept
/// only the final ≤100 rows.
///
/// NonParallelizable — drives the shared "ICD10-INDEX" singleton that other
/// fixtures also load.
/// </summary>
[TestFixture, NonParallelizable]
public class ReferenceDataImportServiceBatchTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ImportIcd10Codes_MultipleBatches_AllCodesSurviveInIndex()
    {
        const int rowCount = 250; // 2 full batches of 100 + a final partial flush

        var csv = new StringBuilder();
        csv.AppendLine("Code,ShortDescription,LongDescription,Category");
        for (int i = 1; i <= rowCount; i++)
        {
            csv.AppendLine($"XT{i:D3}.0,Import test code {i},Import test code {i} long,T00-T99 Test chapter");
        }

        var service = new ReferenceDataImportService(NullLogger<ReferenceDataImportService>.Instance);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));

        ImportResult result = await service.ImportIcd10CodesAsync(
            _cluster.GrainFactory, stream, idempotent: false);

        Assert.That(result.TotalRecords, Is.EqualTo(rowCount));
        Assert.That(result.ImportedRecords, Is.EqualTo(rowCount));
        Assert.That(result.ErrorRecords, Is.EqualTo(0));

        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        var status = await index.GetStatusAsync();
        Assert.That(status.IsLoaded, Is.True);
        Assert.That(status.TotalCodes, Is.EqualTo(rowCount),
            "Every imported batch must be retained — a per-batch full reload keeps only the last ≤100 codes");

        // Spot-check codes from the first, middle, and last batches.
        Assert.That(await index.GetCodeAsync("XT001.0"), Is.Not.Null, "first-batch code lost");
        Assert.That(await index.GetCodeAsync("XT150.0"), Is.Not.Null, "middle-batch code lost");
        Assert.That(await index.GetCodeAsync($"XT{rowCount:D3}.0"), Is.Not.Null, "final-batch code lost");
    }
}
