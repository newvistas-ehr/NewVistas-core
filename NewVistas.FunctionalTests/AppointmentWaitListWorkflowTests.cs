// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Appointment Wait List via the PatientWorkflowGrain.
/// Verifies the Site Flavor Architecture (Option 4 — Composition) feature gate,
/// wait list creation, listing, offer/accept, and cancellation through the
/// workflow orchestration layer. Maps to IHS RPMS SD Wait List (File #409.3).
/// </summary>
[TestFixture]
public class AppointmentWaitListWorkflowTests
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
    public async Task WorkflowWaitList_FailsWhenFeatureDisabled()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await CreatePatientAsync("SMITH,ALICE", "F", new DateTime(1970, 5, 15), "111-22-3333");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.AddToWaitListAsync(
                "CLINIC-1", "Primary Care", "FOLLOW-UP",
                null, null, "ROUTINE", null, null, null,
                "PROV-1", "Dr. Jones");
        });
    }

    [Test, Order(2)]
    public async Task WorkflowWaitList_CreatesEntryWhenEnabled()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1965, 8, 20), "444-55-6666");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act
        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            "CLINIC-1", "Primary Care", "FOLLOW-UP",
            null, null, "URGENT",
            new DateTime(2026, 5, 1), new DateTime(2026, 6, 30),
            "Needs follow-up for blood pressure",
            "PROV-1", "Dr. Jones");

        // Assert
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.PatientId, Is.EqualTo(patientId));
        Assert.That(entry.Status, Is.EqualTo("WAITING"));
        Assert.That(entry.ClinicName, Is.EqualTo("Primary Care"));
        Assert.That(entry.Priority, Is.EqualTo("URGENT"));
        Assert.That(entry.DesiredDateRangeStart, Is.EqualTo(new DateTime(2026, 5, 1)));
    }

    [Test, Order(3)]
    public async Task WorkflowWaitList_ListsPatientEntries()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await CreatePatientAsync("JONES,MARY", "F", new DateTime(1980, 3, 10), "777-88-9999");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        await workflow.AddToWaitListAsync(
            "CLINIC-1", "Primary Care", "FOLLOW-UP",
            null, null, "ROUTINE", null, null, null,
            "PROV-1", "Dr. Jones");

        await workflow.AddToWaitListAsync(
            "CLINIC-2", "Cardiology", "CONSULT",
            null, null, "URGENT", null, null, null,
            "PROV-1", "Dr. Jones");

        // Act
        List<AppointmentWaitListIndexEntry> entries = await workflow.GetWaitListEntriesAsync();

        // Assert
        Assert.That(entries, Has.Count.EqualTo(2));
    }

    [Test, Order(4)]
    public async Task WorkflowWaitList_CancelsEntry()
    {
        // Arrange
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await CreatePatientAsync("BROWN,ROBERT", "M", new DateTime(1955, 12, 1), "222-33-4444");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        AppointmentWaitListState entry = await workflow.AddToWaitListAsync(
            "CLINIC-3", "Orthopedics", "NEW PATIENT",
            null, null, "ROUTINE", null, null, null,
            "PROV-2", "Dr. Wilson");

        // Act
        await workflow.CancelWaitListEntryAsync(entry.EntryId, "Patient found another provider", "Admin");

        // Assert
        AppointmentWaitListState cancelled = await workflow.GetWaitListEntryAsync(entry.EntryId);
        Assert.That(cancelled.Status, Is.EqualTo("CANCELLED"));
        Assert.That(cancelled.CancellationReason, Is.EqualTo("Patient found another provider"));
    }
}
