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
public class EncounterFormWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() { _cluster = SharedCluster.Instance; }

    private IPatientWorkflowGrain GetWorkflow(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
    private ISiteParametersGrain GetSiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
    private IPatientIndexGrain GetPatientIndex() => _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        var grain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await grain.UpdateDemographicsAsync(name, sex, dob, ssn);
        await GetPatientIndex().AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId, Name = name, DateOfBirth = dob, Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty, IsActive = true
        });
        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowEncounterForm_FailsWhenFeatureDisabled()
    {
        await GetSiteParams().DisableFeatureAsync("ENCOUNTER_FORM_TEMPLATES");
        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetWorkflow(patientId).CreateEncounterFormInstanceAsync("EF-TPL:1", "DM Visit", null, "PROV-1", "Dr. Jones"));
    }

    [Test, Order(2)]
    public async Task WorkflowEncounterForm_CreatesInstanceWhenEnabled()
    {
        await GetSiteParams().EnableFeatureAsync("ENCOUNTER_FORM_TEMPLATES");
        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");

        var instance = await GetWorkflow(patientId).CreateEncounterFormInstanceAsync("EF-TPL:1", "DM Visit", null, "PROV-1", "Dr. Jones");

        Assert.That(instance, Is.Not.Null);
        Assert.That(instance.PatientId, Is.EqualTo(patientId));
        Assert.That(instance.Status, Is.EqualTo("DRAFT"));
        Assert.That(instance.TemplateName, Is.EqualTo("DM Visit"));
    }

    [Test, Order(3)]
    public async Task WorkflowEncounterForm_ListsInstances()
    {
        await GetSiteParams().EnableFeatureAsync("ENCOUNTER_FORM_TEMPLATES");
        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        var workflow = GetWorkflow(patientId);

        await workflow.CreateEncounterFormInstanceAsync("EF-TPL:1", "DM Visit", null, "PROV-1", "Dr. Jones");
        await workflow.CreateEncounterFormInstanceAsync("EF-TPL:2", "HTN Visit", null, "PROV-1", "Dr. Jones");

        var instances = await workflow.GetEncounterFormInstancesAsync();
        Assert.That(instances, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowEncounterForm_FillAndSubmit()
    {
        await GetSiteParams().EnableFeatureAsync("ENCOUNTER_FORM_TEMPLATES");
        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        var workflow = GetWorkflow(patientId);

        var instance = await workflow.CreateEncounterFormInstanceAsync("EF-TPL:1", "DM Visit", null, "PROV-1", "Dr. Jones");

        await workflow.SetEncounterFormFieldValuesAsync(instance.InstanceId,
            new Dictionary<string, string?> { ["chief_complaint"] = "Follow-up DM", ["bp_systolic"] = "130", ["bp_diastolic"] = "85" });

        await workflow.SubmitEncounterFormAsync(instance.InstanceId, "Dr. Jones");

        var submitted = await workflow.GetEncounterFormInstanceAsync(instance.InstanceId);
        Assert.That(submitted.Status, Is.EqualTo("SUBMITTED"));
        Assert.That(submitted.FieldValues["chief_complaint"], Is.EqualTo("Follow-up DM"));
        Assert.That(submitted.FieldValues["bp_systolic"], Is.EqualTo("130"));
    }
}
