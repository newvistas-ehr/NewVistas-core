// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Appointment Wait List grains — IHS RPMS SD Wait List (File #409.3)
/// auto-rebooking functionality. Tests the IAppointmentWaitListGrain and
/// IAppointmentWaitListIndexGrain directly, verifying wait list lifecycle,
/// slot offers, accept/decline, cancellation, expiry, and index queries.
/// </summary>
[TestFixture]
public class AppointmentWaitListGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IAppointmentWaitListGrain GetWaitListGrain(string entryId) =>
        _cluster.GrainFactory.GetGrain<IAppointmentWaitListGrain>(entryId);

    private IAppointmentWaitListIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IAppointmentWaitListIndexGrain>("SD-WL-IDX");

    private async Task<AppointmentWaitListState> CreateTestEntryAsync(
        string entryId,
        string patientId = "PATIENT-1",
        string patientName = "DOE,JOHN",
        string clinicId = "CLINIC-PRIMARY",
        string clinicName = "Primary Care",
        string priority = "ROUTINE")
    {
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);
        AppointmentWaitListState result = await grain.CreateEntryAsync(
            patientId, patientName, clinicId, clinicName,
            "FOLLOW-UP", null, null,
            priority, null, null, null,
            "PROV-1", "Dr. Jones");
        return result;
    }

    [Test]
    public async Task WaitListGrain_CreatesEntry()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";

        // Act
        AppointmentWaitListState result = await CreateTestEntryAsync(entryId);

        // Assert
        Assert.That(result.EntryId, Is.EqualTo(entryId));
        Assert.That(result.PatientId, Is.EqualTo("PATIENT-1"));
        Assert.That(result.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(result.ClinicId, Is.EqualTo("CLINIC-PRIMARY"));
        Assert.That(result.ClinicName, Is.EqualTo("Primary Care"));
        Assert.That(result.DesiredAppointmentType, Is.EqualTo("FOLLOW-UP"));
        Assert.That(result.Priority, Is.EqualTo("ROUTINE"));
        Assert.That(result.Status, Is.EqualTo("WAITING"));
        Assert.That(result.OfferCount, Is.EqualTo(0));
        Assert.That(result.AuditTrail, Has.Count.EqualTo(1));
        Assert.That(result.AuditTrail[0].Action, Is.EqualTo("CREATED"));
    }

    [Test]
    public async Task WaitListGrain_UpdatesPriority()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);

        // Act
        await grain.UpdatePriorityAsync("URGENT");

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Priority, Is.EqualTo("URGENT"));
        Assert.That(state.AuditTrail, Has.Count.EqualTo(2));
        Assert.That(state.AuditTrail[1].Action, Is.EqualTo("PRIORITY_CHANGED"));
    }

    [Test]
    public async Task WaitListGrain_OffersSlot()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);
        DateTime offeredTime = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        await grain.OfferSlotAsync("APPT-123", offeredTime, "Scheduler Smith");

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("OFFERED"));
        Assert.That(state.OfferedAppointmentId, Is.EqualTo("APPT-123"));
        Assert.That(state.OfferedDateTime, Is.EqualTo(offeredTime));
        Assert.That(state.OfferCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WaitListGrain_AcceptsOffer()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);
        DateTime offeredTime = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);
        await grain.OfferSlotAsync("APPT-456", offeredTime, "Scheduler Smith");

        // Act
        await grain.AcceptOfferAsync("Patient DOE");

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("BOOKED"));
        Assert.That(state.BookedAppointmentId, Is.EqualTo("APPT-456"));
        Assert.That(state.BookedDateTime, Is.EqualTo(offeredTime));
        Assert.That(state.BookedByName, Is.EqualTo("Patient DOE"));
    }

    [Test]
    public async Task WaitListGrain_DeclinesOffer()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);
        await grain.OfferSlotAsync("APPT-789", new DateTime(2026, 5, 20, 14, 0, 0, DateTimeKind.Utc), "Scheduler");

        // Act
        await grain.DeclineOfferAsync("Time conflict", "Patient DOE");

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("WAITING"));
        Assert.That(state.DeclineReason, Is.EqualTo("Time conflict"));
        Assert.That(state.OfferedAppointmentId, Is.Null);
        Assert.That(state.OfferCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WaitListGrain_AcceptFailsWithoutOffer()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await grain.AcceptOfferAsync("Patient");
        });
    }

    [Test]
    public async Task WaitListGrain_CancelsEntry()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);

        // Act
        await grain.CancelEntryAsync("Patient no longer needs appointment", "Admin User");

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient no longer needs appointment"));
    }

    [Test]
    public async Task WaitListGrain_ExpiresEntry()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IAppointmentWaitListGrain grain = GetWaitListGrain(entryId);

        // Act
        await grain.ExpireEntryAsync();

        // Assert
        AppointmentWaitListState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("EXPIRED"));
    }

    [Test]
    public async Task WaitListIndex_UpdatedOnCreate()
    {
        // Arrange
        string entryId = $"SD-WL:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        // Act
        await CreateTestEntryAsync(entryId, patientId: patientId);

        // Assert
        IAppointmentWaitListIndexGrain index = GetIndex();
        List<AppointmentWaitListIndexEntry> entries = await index.GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].EntryId, Is.EqualTo(entryId));
        Assert.That(entries[0].PatientId, Is.EqualTo(patientId));
        Assert.That(entries[0].Status, Is.EqualTo("WAITING"));
    }

    [Test]
    public async Task WaitListIndex_PendingByClinicSortedByPriority()
    {
        // Arrange — create entries with different priorities for the same clinic
        string clinicId = $"CLINIC-{Guid.NewGuid()}";
        string routineId = $"SD-WL:{Guid.NewGuid()}";
        string urgentId = $"SD-WL:{Guid.NewGuid()}";
        string statId = $"SD-WL:{Guid.NewGuid()}";

        await CreateTestEntryAsync(routineId, clinicId: clinicId, clinicName: "Test Clinic", priority: "ROUTINE");
        await CreateTestEntryAsync(urgentId, clinicId: clinicId, clinicName: "Test Clinic", priority: "URGENT");
        await CreateTestEntryAsync(statId, clinicId: clinicId, clinicName: "Test Clinic", priority: "STAT");

        // Act
        IAppointmentWaitListIndexGrain index = GetIndex();
        List<AppointmentWaitListIndexEntry> pending = await index.GetPendingByClinicAsync(clinicId);

        // Assert — STAT first, then URGENT, then ROUTINE
        Assert.That(pending, Has.Count.EqualTo(3));
        Assert.That(pending[0].Priority, Is.EqualTo("STAT"));
        Assert.That(pending[1].Priority, Is.EqualTo("URGENT"));
        Assert.That(pending[2].Priority, Is.EqualTo("ROUTINE"));
    }
}
