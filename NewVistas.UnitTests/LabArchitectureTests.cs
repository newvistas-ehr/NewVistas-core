// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

[TestFixture]
public class PatientLabSummaryTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task NewResult_UpdatesSummary()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");

        var evt = new LabResultEvent
        {
            PatientIcn = patientIcn,
            ResultId = "R1",
            LoincCode = "2160-0",
            TestName = "Creatinine",
            Value = "1.1",
            Units = "mg/dL",
            ReferenceRange = "0.7-1.3",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = DateTimeOffset.UtcNow,
            FacilityCode = "688"
        };

        await summary.RecordNewResult(evt);

        var current = await summary.GetCurrentResult("2160-0");
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Value, Is.EqualTo("1.1"));
        Assert.That(current.TestName, Is.EqualTo("Creatinine"));
        Assert.That(current.RecentTrend, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OutOfOrderResult_DoesNotClobberNewer()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");

        var now = DateTimeOffset.UtcNow;

        // Ingest newer result first
        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R2", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.4", Units = "mg/dL",
            ReferenceRange = "0.7-1.3", AbnormalFlag = LabAbnormalFlag.High,
            ResultDate = now, FacilityCode = "688"
        });

        // Now ingest older result — should NOT overwrite
        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R1", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.0", Units = "mg/dL",
            ReferenceRange = "0.7-1.3", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now.AddDays(-7), FacilityCode = "688"
        });

        var current = await summary.GetCurrentResult("2160-0");
        Assert.That(current!.Value, Is.EqualTo("1.4"));
        Assert.That(current.AbnormalFlag, Is.EqualTo(LabAbnormalFlag.High));
    }

    [Test]
    public async Task TrendCalculation_KeepsLast3()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");

        var baseDate = DateTimeOffset.UtcNow.AddDays(-30);

        // Ingest 5 results in chronological order
        for (int i = 0; i < 5; i++)
        {
            await summary.RecordNewResult(new LabResultEvent
            {
                PatientIcn = patientIcn, ResultId = $"R{i}",
                LoincCode = "2857-1", TestName = "PSA",
                Value = $"{i + 1}.0", Units = "ng/mL",
                ReferenceRange = "0.0-4.0",
                AbnormalFlag = i >= 4 ? LabAbnormalFlag.High : LabAbnormalFlag.Normal,
                ResultDate = baseDate.AddDays(i * 7), FacilityCode = "688"
            });
        }

        var current = await summary.GetCurrentResult("2857-1");
        Assert.That(current!.RecentTrend, Has.Count.EqualTo(3));
        Assert.That(current.RecentTrend[0].Value, Is.EqualTo("5.0")); // Most recent
        Assert.That(current.RecentTrend[1].Value, Is.EqualTo("4.0"));
        Assert.That(current.RecentTrend[2].Value, Is.EqualTo("3.0"));
    }

    [Test]
    public async Task AbnormalResults_FiltersCorrectly()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");

        var now = DateTimeOffset.UtcNow;

        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R1", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.0", Units = "mg/dL",
            AbnormalFlag = LabAbnormalFlag.Normal, ResultDate = now, FacilityCode = "688"
        });

        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R2", LoincCode = "2823-3",
            TestName = "Potassium", Value = "6.1", Units = "mmol/L",
            AbnormalFlag = LabAbnormalFlag.CriticalHigh, ResultDate = now, FacilityCode = "688"
        });

        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R3", LoincCode = "718-7",
            TestName = "Hemoglobin", Value = "7.2", Units = "g/dL",
            AbnormalFlag = LabAbnormalFlag.Low, ResultDate = now, FacilityCode = "688"
        });

        var abnormal = await summary.GetAbnormalResults();
        Assert.That(abnormal, Has.Count.EqualTo(2));
        Assert.That(abnormal.Select(a => a.LoincCode),
            Is.EquivalentTo(new[] { "2823-3", "718-7" }));
    }

    [Test]
    public async Task CrossFacility_MostRecentWins()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");

        var now = DateTimeOffset.UtcNow;

        // Result from facility 688 (older)
        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R1", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.0", Units = "mg/dL",
            AbnormalFlag = LabAbnormalFlag.Normal, ResultDate = now.AddDays(-1),
            FacilityCode = "688"
        });

        // Result from facility 523 (newer)
        await summary.RecordNewResult(new LabResultEvent
        {
            PatientIcn = patientIcn, ResultId = "R2", LoincCode = "2160-0",
            TestName = "Creatinine", Value = "1.5", Units = "mg/dL",
            AbnormalFlag = LabAbnormalFlag.High, ResultDate = now,
            FacilityCode = "523"
        });

        var current = await summary.GetCurrentResult("2160-0");
        Assert.That(current!.Value, Is.EqualTo("1.5"));
        Assert.That(current.FacilityCode, Is.EqualTo("523"));
    }
}

[TestFixture]
public class PatientLabBatchTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task BatchPartitioning_ResultsInCorrectBatch()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";

        // Jan result → H1
        var janResult = MakeResult("R1", "2160-0", "Creatinine", "1.0",
            new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero));
        var h1Key = LabBatchKeyHelper.GetBatchKey(patientIcn, janResult.ResultDate);
        var h1Batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(h1Key);
        await h1Batch.AddResult(janResult);

        // Sep result → H2
        var sepResult = MakeResult("R2", "2160-0", "Creatinine", "1.2",
            new DateTimeOffset(2024, 9, 10, 0, 0, 0, TimeSpan.Zero));
        var h2Key = LabBatchKeyHelper.GetBatchKey(patientIcn, sepResult.ResultDate);
        var h2Batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(h2Key);
        await h2Batch.AddResult(sepResult);

        // Verify partitions
        Assert.That(h1Key, Does.Contain("2024-H1"));
        Assert.That(h2Key, Does.Contain("2024-H2"));

        var h1Results = await h1Batch.GetAllResults();
        var h2Results = await h2Batch.GetAllResults();
        Assert.That(h1Results, Has.Count.EqualTo(1));
        Assert.That(h2Results, Has.Count.EqualTo(1));
        Assert.That(h1Results[0].ResultId, Is.EqualTo("R1"));
        Assert.That(h2Results[0].ResultId, Is.EqualTo("R2"));
    }

    [Test]
    public async Task GetResultsByType_FiltersCorrectly()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var batchKey = $"PatientLabBatch/{patientIcn}/2024-H1";
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);

        await batch.AddResult(MakeResult("R1", "2160-0", "Creatinine", "1.0",
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero)));
        await batch.AddResult(MakeResult("R2", "718-7", "Hemoglobin", "14.0",
            new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero)));
        await batch.AddResult(MakeResult("R3", "2160-0", "Creatinine", "1.1",
            new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero)));

        var creatinines = await batch.GetResultsByType("2160-0");
        Assert.That(creatinines, Has.Count.EqualTo(2));
        Assert.That(creatinines.All(r => r.LoincCode == "2160-0"), Is.True);
    }

    [Test]
    public async Task GetResultsByPanel_FiltersCorrectly()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var batchKey = $"PatientLabBatch/{patientIcn}/2024-H1";
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);

        var cbcResult = MakeResult("R1", "6690-2", "WBC", "7.5",
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        cbcResult.PanelName = "CBC";
        await batch.AddResult(cbcResult);

        var cmpResult = MakeResult("R2", "2160-0", "Creatinine", "1.0",
            new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        cmpResult.PanelName = "CMP";
        await batch.AddResult(cmpResult);

        var cbcResults = await batch.GetResultsByPanel("CBC");
        Assert.That(cbcResults, Has.Count.EqualTo(1));
        Assert.That(cbcResults[0].TestName, Is.EqualTo("WBC"));
    }

    private static LabResultDetail MakeResult(string id, string loinc, string name,
        string value, DateTimeOffset date)
    {
        return new LabResultDetail
        {
            ResultId = id,
            LoincCode = loinc,
            TestName = name,
            Value = value,
            Units = "mg/dL",
            ReferenceRange = "0.7-1.3",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = date,
            FacilityCode = "688",
            FacilityName = "Washington DC VAMC",
            Specimen = "Blood"
        };
    }
}

[TestFixture]
public class PatientLabIndexTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task IndexRebuildsFromBatches()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";

        // Put results into batch grains
        var now = DateTimeOffset.UtcNow;
        var batchKey = LabBatchKeyHelper.GetBatchKey(patientIcn, now);
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);
        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R1", LoincCode = "2160-0", TestName = "Creatinine",
            Value = "1.0", Units = "mg/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now, FacilityCode = "688", Specimen = "Blood"
        });
        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R2", LoincCode = "718-7", TestName = "Hemoglobin",
            Value = "14.0", Units = "g/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now.AddHours(-1), FacilityCode = "688", Specimen = "Blood"
        });

        // Activate the index — it should rebuild from the batch grains
        var index = _cluster.GrainFactory.GetGrain<IPatientLabIndex>(
            $"PatientLabIndex/{patientIcn}");
        await index.RebuildIndex();

        var testTypes = await index.GetDistinctTestTypes();
        Assert.That(testTypes, Has.Count.EqualTo(2));
        Assert.That(testTypes, Contains.Item("2160-0"));
        Assert.That(testTypes, Contains.Item("718-7"));
    }

    [Test]
    public async Task LastNByType_ReturnsCorrectResults()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        // Place 5 PSA results across the current batch
        var batchKey = LabBatchKeyHelper.GetBatchKey(patientIcn, now);
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);
        for (int i = 0; i < 5; i++)
        {
            await batch.AddResult(new LabResultDetail
            {
                ResultId = $"PSA-{i}", LoincCode = "2857-1", TestName = "PSA",
                Value = $"{i + 1}.0", Units = "ng/mL",
                AbnormalFlag = LabAbnormalFlag.Normal,
                ResultDate = now.AddDays(-i * 30), FacilityCode = "688", Specimen = "Blood"
            });
        }

        var index = _cluster.GrainFactory.GetGrain<IPatientLabIndex>(
            $"PatientLabIndex/{patientIcn}");
        await index.RebuildIndex();

        var last3 = await index.GetLastNByType("2857-1", 3);
        Assert.That(last3, Has.Count.EqualTo(3));
        // Should be most recent first (date descending)
        Assert.That(last3[0].ResultId, Is.EqualTo("PSA-0"));
        Assert.That(last3[1].ResultId, Is.EqualTo("PSA-1"));
        Assert.That(last3[2].ResultId, Is.EqualTo("PSA-2"));
    }

    [Test]
    public async Task AbnormalByDateRange_FiltersCorrectly()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        var batchKey = LabBatchKeyHelper.GetBatchKey(patientIcn, now);
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);

        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R1", LoincCode = "2160-0", TestName = "Creatinine",
            Value = "1.0", Units = "mg/dL", AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = now, FacilityCode = "688", Specimen = "Blood"
        });
        await batch.AddResult(new LabResultDetail
        {
            ResultId = "R2", LoincCode = "2823-3", TestName = "Potassium",
            Value = "6.1", Units = "mmol/L", AbnormalFlag = LabAbnormalFlag.CriticalHigh,
            ResultDate = now, FacilityCode = "688", Specimen = "Blood"
        });

        var index = _cluster.GrainFactory.GetGrain<IPatientLabIndex>(
            $"PatientLabIndex/{patientIcn}");
        await index.RebuildIndex();

        var abnormal = await index.GetAbnormalByDateRange(now.AddDays(-1), now.AddDays(1));
        Assert.That(abnormal, Has.Count.EqualTo(1));
        Assert.That(abnormal[0].LoincCode, Is.EqualTo("2823-3"));
    }
}

[TestFixture]
public class LabResultIngestionTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task IngestResult_WritesBatchAndSummary()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";
        var ingestion = _cluster.GrainFactory.GetGrain<ILabResultIngestion>(patientIcn);

        var result = new LabResultDetail
        {
            ResultId = "R1",
            LoincCode = "2160-0",
            TestName = "Creatinine",
            Value = "1.1",
            Units = "mg/dL",
            ReferenceRange = "0.7-1.3",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = DateTimeOffset.UtcNow,
            FacilityCode = "688",
            FacilityName = "Washington DC VAMC",
            Specimen = "Blood"
        };

        await ingestion.IngestResult(result);

        // Verify summary was updated
        var summary = _cluster.GrainFactory.GetGrain<IPatientLabSummary>(
            $"PatientLabSummary/{patientIcn}");
        var current = await summary.GetCurrentResult("2160-0");
        Assert.That(current, Is.Not.Null);
        Assert.That(current!.Value, Is.EqualTo("1.1"));

        // Verify batch was updated
        var batchKey = LabBatchKeyHelper.GetBatchKey(patientIcn, result.ResultDate);
        var batch = _cluster.GrainFactory.GetGrain<IPatientLabBatch>(batchKey);
        var batchResults = await batch.GetAllResults();
        Assert.That(batchResults, Has.Count.EqualTo(1));
        Assert.That(batchResults[0].ResultId, Is.EqualTo("R1"));
    }

    [Test]
    public async Task StreamKeepsIndexCurrent()
    {
        var patientIcn = $"ICN-{Guid.NewGuid()}";

        // Activate the index first so it subscribes to the stream
        var index = _cluster.GrainFactory.GetGrain<IPatientLabIndex>(
            $"PatientLabIndex/{patientIcn}");
        await index.RebuildIndex(); // Activates and subscribes

        // Now ingest a result — stream should notify index
        var ingestion = _cluster.GrainFactory.GetGrain<ILabResultIngestion>(patientIcn);
        await ingestion.IngestResult(new LabResultDetail
        {
            ResultId = "R-STREAM-1",
            LoincCode = "2160-0",
            TestName = "Creatinine",
            Value = "1.2",
            Units = "mg/dL",
            AbnormalFlag = LabAbnormalFlag.Normal,
            ResultDate = DateTimeOffset.UtcNow,
            FacilityCode = "688",
            Specimen = "Blood"
        });

        // Give stream a moment to deliver
        await Task.Delay(500);

        var types = await index.GetDistinctTestTypes();
        Assert.That(types, Contains.Item("2160-0"));
    }
}

[TestFixture]
public class LabBatchKeyHelperTests
{
    [Test]
    public void GetBatchKey_H1()
    {
        var key = LabBatchKeyHelper.GetBatchKey("123V456",
            new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero));
        Assert.That(key, Is.EqualTo("PatientLabBatch/123V456/2024-H1"));
    }

    [Test]
    public void GetBatchKey_H2()
    {
        var key = LabBatchKeyHelper.GetBatchKey("123V456",
            new DateTimeOffset(2024, 9, 10, 0, 0, 0, TimeSpan.Zero));
        Assert.That(key, Is.EqualTo("PatientLabBatch/123V456/2024-H2"));
    }

    [Test]
    public void GetBatchKey_June_IsH1()
    {
        var key = LabBatchKeyHelper.GetBatchKey("123V456",
            new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero));
        Assert.That(key, Does.Contain("H1"));
    }

    [Test]
    public void GetBatchKey_July_IsH2()
    {
        var key = LabBatchKeyHelper.GetBatchKey("123V456",
            new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.That(key, Does.Contain("H2"));
    }

    [Test]
    public void GetBatchKeysForRange_SpansCorrectly()
    {
        var keys = LabBatchKeyHelper.GetBatchKeysForRange("123V456",
            new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 8, 1, 0, 0, 0, TimeSpan.Zero))
            .ToList();

        // Should span: 2023-H1, 2023-H2, 2024-H1, 2024-H2
        Assert.That(keys, Has.Count.EqualTo(4));
        Assert.That(keys[0], Does.Contain("2023-H1"));
        Assert.That(keys[1], Does.Contain("2023-H2"));
        Assert.That(keys[2], Does.Contain("2024-H1"));
        Assert.That(keys[3], Does.Contain("2024-H2"));
    }
}

/// <summary>
/// Shared silo configurator for all lab architecture tests.
/// </summary>
