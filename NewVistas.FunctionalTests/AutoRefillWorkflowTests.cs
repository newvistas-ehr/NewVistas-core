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
public class AutoRefillWorkflowTests
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
    public async Task WorkflowAutoRefill_FailsWhenDisabled()
    {
        await GetSiteParams().DisableFeatureAsync("AUTO_REFILL");
        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await GetWorkflow(patientId).EnrollAutoRefillAsync(
                "RX-001", "Metoprolol 25mg", "CV100",
                30, 3, new DateTime(2026, 3, 1),
                "PHARM-1", "Main Pharmacy", "PROV-1", "Dr. Jones"));
    }

    [Test, Order(2)]
    public async Task WorkflowAutoRefill_EnrollsWhenEnabled()
    {
        await GetSiteParams().EnableFeatureAsync("AUTO_REFILL");
        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");

        var enrollment = await GetWorkflow(patientId).EnrollAutoRefillAsync(
            "RX-001", "Lisinopril 10mg", "CV800",
            90, 5, new DateTime(2026, 3, 1),
            "PHARM-1", "Main Pharmacy", "PROV-1", "Dr. Jones");

        Assert.That(enrollment, Is.Not.Null);
        Assert.That(enrollment.PatientId, Is.EqualTo(patientId));
        Assert.That(enrollment.Status, Is.EqualTo("ACTIVE"));
        Assert.That(enrollment.DrugName, Is.EqualTo("Lisinopril 10mg"));
        Assert.That(enrollment.DaysSupply, Is.EqualTo(90));
    }

    [Test, Order(3)]
    public async Task WorkflowAutoRefill_ListsEnrollments()
    {
        await GetSiteParams().EnableFeatureAsync("AUTO_REFILL");
        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        var workflow = GetWorkflow(patientId);

        await workflow.EnrollAutoRefillAsync("RX-A", "Metformin 500mg", "HS502", 30, 6,
            new DateTime(2026, 3, 1), "PHARM-1", "Main", "PROV-1", "Dr. Jones");
        await workflow.EnrollAutoRefillAsync("RX-B", "Atorvastatin 20mg", "CV350", 90, 3,
            new DateTime(2026, 2, 15), "PHARM-1", "Main", "PROV-1", "Dr. Jones");

        var enrollments = await workflow.GetAutoRefillEnrollmentsAsync();
        Assert.That(enrollments, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowAutoRefill_SuspendsAndResumes()
    {
        await GetSiteParams().EnableFeatureAsync("AUTO_REFILL");
        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        var workflow = GetWorkflow(patientId);

        var enrollment = await workflow.EnrollAutoRefillAsync("RX-C", "Amlodipine 5mg", "CV200", 30, 4,
            new DateTime(2026, 3, 10), "PHARM-2", "Satellite", "PROV-2", "Dr. Wilson");

        await workflow.SuspendAutoRefillAsync(enrollment.EnrollmentId, "Patient hospitalized", "Pharmacist");
        var suspended = await workflow.GetAutoRefillEnrollmentAsync(enrollment.EnrollmentId);
        Assert.That(suspended.Status, Is.EqualTo("SUSPENDED"));

        await workflow.ResumeAutoRefillAsync(enrollment.EnrollmentId, "Pharmacist");
        var resumed = await workflow.GetAutoRefillEnrollmentAsync(enrollment.EnrollmentId);
        Assert.That(resumed.Status, Is.EqualTo("ACTIVE"));
    }
}
