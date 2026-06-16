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
public class PeriodontalChartWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private ISiteParametersGrain GetSiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).UpdateDemographicsAsync(name, sex, dob, ssn);
        await _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX").AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId, Name = name, DateOfBirth = dob, Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty, IsActive = true
        });
        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowPerio_FailsWhenDisabled()
    {
        await GetSiteParams().DisableFeatureAsync("PERIODONTAL_CHARTING");
        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetWorkflow(patientId).CreatePeriodontalChartAsync("PROV-1", "Dr. Smith", null));
    }

    [Test, Order(2)]
    public async Task WorkflowPerio_CreatesChartWhenEnabled()
    {
        await GetSiteParams().EnableFeatureAsync("PERIODONTAL_CHARTING");
        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");

        var chart = await GetWorkflow(patientId).CreatePeriodontalChartAsync("PROV-1", "Dr. Smith", "Full perio exam");

        Assert.That(chart, Is.Not.Null);
        Assert.That(chart.PatientId, Is.EqualTo(patientId));
        Assert.That(chart.Status, Is.EqualTo("DRAFT"));
        Assert.That(chart.ProviderName, Is.EqualTo("Dr. Smith"));
    }

    [Test, Order(3)]
    public async Task WorkflowPerio_ListsCharts()
    {
        await GetSiteParams().EnableFeatureAsync("PERIODONTAL_CHARTING");
        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        var workflow = GetWorkflow(patientId);

        await workflow.CreatePeriodontalChartAsync("PROV-1", "Dr. Smith", null);
        await workflow.CreatePeriodontalChartAsync("PROV-2", "Dr. Lee", null);

        var charts = await workflow.GetPeriodontalChartsAsync();
        Assert.That(charts, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowPerio_RecordDataAndFinalize()
    {
        await GetSiteParams().EnableFeatureAsync("PERIODONTAL_CHARTING");
        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        var workflow = GetWorkflow(patientId);

        var chart = await workflow.CreatePeriodontalChartAsync("PROV-1", "Dr. Smith", null);

        // Record tooth data
        await workflow.RecordPeriodontalToothDataAsync(chart.ChartId, 8, new PeriodontalToothData
        {
            ProbingDepths = [4, 3, 5, 3, 3, 4],
            Recession = [1, 0, 2, 0, 0, 1],
            BleedingOnProbing = [true, false, true, false, false, true],
            Furcation = "NONE", Mobility = 0
        });

        // Finalize
        await workflow.FinalizePeriodontalChartAsync(chart.ChartId, "Dr. Smith");

        var finalized = await workflow.GetPeriodontalChartAsync(chart.ChartId);
        Assert.That(finalized.Status, Is.EqualTo("FINALIZED"));
        Assert.That(finalized.TeethCharted, Is.EqualTo(1));
        Assert.That(finalized.DeepPocketCount, Is.EqualTo(3)); // 4, 5, 4
        Assert.That(finalized.BleedingSiteCount, Is.EqualTo(3));
    }
}
