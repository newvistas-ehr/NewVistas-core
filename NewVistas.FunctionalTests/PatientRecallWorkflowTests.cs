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
/// Functional tests for Patient Recall via the PatientWorkflowGrain.
/// Verifies the Site Flavor Architecture (Option 4 — Composition) feature gate,
/// recall creation, listing, letter generation, and completion through the
/// workflow orchestration layer. Maps to IHS RPMS SC Recall (File #403.5).
/// </summary>
[TestFixture]
public class PatientRecallWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private IPatientIndexGrain GetPatientIndex() =>
        _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    private async Task<string> CreatePatientAsync(string name, string sex, DateTime dob, string ssn)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain grain = GetPatient(patientId);
        await grain.UpdateDemographicsAsync(name, sex, dob, ssn);

        IPatientIndexGrain index = GetPatientIndex();
        await index.AddOrUpdateAsync(new PatientIndexEntry
        {
            PatientId = patientId,
            Name = name,
            DateOfBirth = dob,
            Sex = sex,
            SsnLast4 = ssn.Length >= 4 ? ssn[^4..] : string.Empty,
            IsActive = true
        });

        return patientId;
    }

    [Test, Order(1)]
    public async Task WorkflowRecall_FailsWhenFeatureDisabled()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PATIENT_RECALL");

        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.CreateRecallEntryAsync(
                "CLINIC-1", "Primary Care",
                "FOLLOW-UP", new DateTime(2026, 6, 15),
                null, null, null, null,
                "PROV-1", "Dr. Jones");
        });
    }

    [Test, Order(2)]
    public async Task WorkflowRecall_CreatesEntryWhenEnabled()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_RECALL");

        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        PatientRecallState entry = await workflow.CreateRecallEntryAsync(
            "CLINIC-1", "Primary Care",
            "ANNUAL_EXAM", new DateTime(2026, 9, 1),
            "PROV-1", "Dr. Jones",
            "Diabetes management", "Fasting labs needed",
            "PROV-1", "Dr. Jones");

        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.PatientId, Is.EqualTo(patientId));
        Assert.That(entry.Status, Is.EqualTo("PENDING"));
        Assert.That(entry.RecallType, Is.EqualTo("ANNUAL_EXAM"));
        Assert.That(entry.RecallDate, Is.EqualTo(new DateTime(2026, 9, 1)));
        Assert.That(entry.Diagnosis, Is.EqualTo("Diabetes management"));
    }

    [Test, Order(3)]
    public async Task WorkflowRecall_ListsPatientEntries()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_RECALL");

        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.CreateRecallEntryAsync(
            "CLINIC-1", "Primary Care", "FOLLOW-UP", new DateTime(2026, 7, 1),
            null, null, null, null, "PROV-1", "Dr. Jones");

        await workflow.CreateRecallEntryAsync(
            "CLINIC-2", "Endocrinology", "LAB_RECHECK", new DateTime(2026, 8, 15),
            null, null, null, null, "PROV-1", "Dr. Jones");

        List<PatientRecallIndexEntry> entries = await workflow.GetRecallEntriesAsync();

        Assert.That(entries, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowRecall_GeneratesLetterAndCompletes()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_RECALL");

        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        PatientRecallState entry = await workflow.CreateRecallEntryAsync(
            "CLINIC-3", "Cardiology", "CHRONIC_CARE", new DateTime(2026, 7, 15),
            null, null, "CHF", "Echocardiogram follow-up",
            "PROV-2", "Dr. Wilson");

        // Generate letter
        await workflow.GenerateRecallLetterAsync(entry.EntryId, "FIRST_NOTICE", "Clerk Smith");

        PatientRecallState afterLetter = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(afterLetter.Status, Is.EqualTo("LETTER_SENT"));
        Assert.That(afterLetter.LetterCount, Is.EqualTo(1));

        // Complete
        await workflow.CompleteRecallEntryAsync(entry.EntryId, "Dr. Wilson", "Patient seen");

        PatientRecallState completed = await workflow.GetRecallEntryAsync(entry.EntryId);
        Assert.That(completed.Status, Is.EqualTo("COMPLETED"));
    }
}
