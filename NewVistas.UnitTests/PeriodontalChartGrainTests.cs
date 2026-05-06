// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

[TestFixture]
public class PeriodontalChartGrainTests
{
    private TestCluster _cluster = default!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IPeriodontalChartGrain GetChart(string id) => _cluster.GrainFactory.GetGrain<IPeriodontalChartGrain>(id);
    private IPeriodontalChartIndexGrain GetIndex() => _cluster.GrainFactory.GetGrain<IPeriodontalChartIndexGrain>("PERIO-IDX");

    [Test]
    public async Task PerioChart_Creates()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        var result = await GetChart(id).CreateChartAsync("P-1", "DOE,JOHN", "PROV-1", "Dr. Smith", "Initial perio exam");

        Assert.That(result.ChartId, Is.EqualTo(id));
        Assert.That(result.PatientId, Is.EqualTo("P-1"));
        Assert.That(result.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(result.Status, Is.EqualTo("DRAFT"));
        Assert.That(result.TeethCharted, Is.EqualTo(0));
    }

    [Test]
    public async Task PerioChart_RecordsToothData()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);

        await GetChart(id).RecordToothDataAsync(3, new PeriodontalToothData
        {
            ProbingDepths = [3, 2, 3, 3, 2, 3],
            Recession = [0, 0, 0, 0, 0, 0],
            BleedingOnProbing = [false, false, false, false, false, false],
            Furcation = "NONE", Mobility = 0
        });

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.TeethCharted, Is.EqualTo(1));
        Assert.That(state.TeethData.ContainsKey(3), Is.True);
        Assert.That(state.DeepPocketCount, Is.EqualTo(0));
    }

    [Test]
    public async Task PerioChart_CountsDeepPockets()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);

        await GetChart(id).RecordToothDataAsync(14, new PeriodontalToothData
        {
            ProbingDepths = [5, 4, 6, 3, 4, 5],
            Recession = [1, 0, 2, 0, 0, 1],
            BleedingOnProbing = [true, false, true, false, true, true],
            Furcation = "CLASS_II", Mobility = 1
        });

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.DeepPocketCount, Is.EqualTo(5)); // 5,4,6,4,5 are >=4
        Assert.That(state.BleedingSiteCount, Is.EqualTo(4)); // 4 true values
    }

    [Test]
    public async Task PerioChart_RecordsMultipleTeeth()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);

        await GetChart(id).RecordMultipleTeethAsync(new List<PeriodontalToothEntry>
        {
            new() { ToothNumber = 1, Data = new() { ProbingDepths = [3,3,3,3,3,3] } },
            new() { ToothNumber = 2, Data = new() { ProbingDepths = [2,2,2,2,2,2] } },
            new() { ToothNumber = 3, Data = new() { ProbingDepths = [4,3,3,3,3,4] } }
        });

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.TeethCharted, Is.EqualTo(3));
        Assert.That(state.DeepPocketCount, Is.EqualTo(2)); // tooth 3 has two 4mm sites
    }

    [Test]
    public async Task PerioChart_MarksToothMissing()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);
        await GetChart(id).RecordToothDataAsync(18, new() { ProbingDepths = [3,3,3,3,3,3] });

        await GetChart(id).MarkToothMissingAsync(18, "Extracted due to caries");

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.MissingTeeth.ContainsKey(18), Is.True);
        Assert.That(state.MissingTeeth[18], Is.EqualTo("Extracted due to caries"));
        Assert.That(state.TeethData.ContainsKey(18), Is.False);
    }

    [Test]
    public async Task PerioChart_SetsClassification()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);

        await GetChart(id).SetOverallAssessmentAsync("STAGE_II", "SRP all quadrants", "Dr. Smith");

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.Classification, Is.EqualTo("STAGE_II"));
        Assert.That(state.TreatmentPlan, Is.EqualTo("SRP all quadrants"));
    }

    [Test]
    public async Task PerioChart_Finalizes()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);

        await GetChart(id).FinalizeChartAsync("Dr. Smith");

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.Status, Is.EqualTo("FINALIZED"));
    }

    [Test]
    public async Task PerioChart_CannotModifyFinalized()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);
        await GetChart(id).FinalizeChartAsync("Dr.");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetChart(id).RecordToothDataAsync(1, new() { ProbingDepths = [3,3,3,3,3,3] }));
    }

    [Test]
    public async Task PerioChart_AddendsFinalized()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync("P-1", "DOE", "PROV-1", "Dr.", null);
        await GetChart(id).FinalizeChartAsync("Dr.");

        await GetChart(id).AddendChartAsync("Corrected tooth 14 mobility to grade 2", "Dr. Smith");

        var state = await GetChart(id).GetChartAsync();
        Assert.That(state.Status, Is.EqualTo("ADDENDED"));
        Assert.That(state.AddendumNotes, Does.Contain("Corrected tooth 14"));
    }

    [Test]
    public async Task PerioIndex_UpdatedOnCreate()
    {
        string id = $"PERIO:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await GetChart(id).CreateChartAsync(patientId, "DOE", "PROV-1", "Dr.", null);

        var entries = await GetIndex().GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ChartId, Is.EqualTo(id));
        Assert.That(entries[0].Status, Is.EqualTo("DRAFT"));
    }
}
