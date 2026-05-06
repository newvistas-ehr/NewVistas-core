// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Voluntary Service — VistA File #8810.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class VoluntaryServiceWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IVolunteerGrain GetVolunteer(string volunteerId)
        => _cluster.GrainFactory.GetGrain<IVolunteerGrain>($"VS-VOLUNTEER:{volunteerId}");

    private IVolunteerIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>("VS-INDEX");

    // ── Enrollment Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task EnrollVolunteer_SetsActiveStatus()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "John", "Doe", "M",
            new DateTime(1955, 3, 15), "555-1234", "john@email.com",
            "123 Main St, Anytown VA", "Jane Doe", "555-5678",
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            new List<string> { "Bilingual Spanish", "First Aid Certified" },
            new List<string> { "Patient Escort", "Gift Shop" },
            "Retired Army veteran");

        VolunteerState state = await grain.GetAsync();

        Assert.That(state.VolunteerId, Is.EqualTo(volunteerId));
        Assert.That(state.FirstName, Is.EqualTo("John"));
        Assert.That(state.LastName, Is.EqualTo("Doe"));
        Assert.That(state.Status, Is.EqualTo(VolunteerStatus.Active));
        Assert.That(state.BackgroundCheckStatus, Is.EqualTo(BackgroundCheckStatus.Cleared));
        Assert.That(state.Skills, Has.Count.EqualTo(2));
        Assert.That(state.Interests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpdateProfile_ChangesContactInfo()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Jane", "Smith", null,
            null, "555-0001", null, "456 Oak Ave",
            null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Pending, null, null, null);

        await grain.UpdateProfileAsync(
            "Jane", "Smith-Jones", null,
            "555-9999", "jane@newemail.com", "789 Pine Rd",
            "Bob Jones", "555-8888", "Changed last name after marriage");

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.LastName, Is.EqualTo("Smith-Jones"));
        Assert.That(state.PhoneNumber, Is.EqualTo("555-9999"));
        Assert.That(state.Email, Is.EqualTo("jane@newemail.com"));
        Assert.That(state.EmergencyContactName, Is.EqualTo("Bob Jones"));
    }

    [Test]
    public async Task UpdateStatus_TransitionsToInactive()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Bob", "Green", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        await grain.UpdateStatusAsync(VolunteerStatus.Inactive, "Volunteer relocating out of area");

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(VolunteerStatus.Inactive));
    }

    [Test]
    public async Task LogHours_UpdatesTotalHours()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Tom", "White", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        string hoursId1 = await grain.LogHoursAsync(
            DateTime.UtcNow.AddDays(-2), 4.0m,
            VolunteerServiceType.PatientEscort, null, "Morning shift");

        string hoursId2 = await grain.LogHoursAsync(
            DateTime.UtcNow.AddDays(-1), 3.5m,
            VolunteerServiceType.PatientEscort, null, "Afternoon shift");

        Assert.That(hoursId1, Is.Not.Empty);
        Assert.That(hoursId2, Is.Not.Empty);

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.TotalHours, Is.EqualTo(7.5m));
        Assert.That(state.HoursLog, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AddAssignment_CreatesActiveAssignment()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Sue", "Black", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        string assignmentId = await grain.AddAssignmentAsync(
            VolunteerServiceType.GiftShop, "Main Lobby Gift Shop",
            "Cashier Volunteer", DateTime.UtcNow,
            isPrimary: true,
            "SUPER-001", "Coordinator Adams",
            "Mondays and Wednesdays 9am-1pm");

        Assert.That(assignmentId, Is.Not.Empty);

        List<VolunteerAssignmentRecord> assignments = await grain.GetAssignmentsAsync();
        Assert.That(assignments, Has.Count.EqualTo(1));
        Assert.That(assignments[0].ServiceArea, Is.EqualTo("Main Lobby Gift Shop"));
        Assert.That(assignments[0].IsPrimary, Is.True);
        Assert.That(assignments[0].IsActive, Is.True);
    }

    [Test]
    public async Task EndAssignment_RecordsEndDate()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Dan", "Gray", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        string assignmentId = await grain.AddAssignmentAsync(
            VolunteerServiceType.Reading, "Library", "Reader",
            DateTime.UtcNow.AddMonths(-3), false,
            null, null, null);

        await grain.EndAssignmentAsync(assignmentId, DateTime.UtcNow, "Volunteer prefers different role");

        List<VolunteerAssignmentRecord> assignments = await grain.GetAssignmentsAsync();
        VolunteerAssignmentRecord ended = assignments.First(a => a.AssignmentId == assignmentId);
        Assert.That(ended.EndDate, Is.Not.Null);
        Assert.That(ended.IsActive, Is.False);
    }

    [Test]
    public async Task AddRecognition_RecordsAward()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Alice", "King", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        await grain.AddRecognitionAsync(
            VolunteerRecognitionType.FiveHundredHours,
            DateTime.UtcNow, "VA Director",
            "Outstanding 500 hours of dedicated service", "CERT-001");

        List<VolunteerRecognitionRecord> recognitions = await grain.GetRecognitionsAsync();
        Assert.That(recognitions, Has.Count.EqualTo(1));
        Assert.That(recognitions[0].RecognitionType, Is.EqualTo(VolunteerRecognitionType.FiveHundredHours));
        Assert.That(recognitions[0].CertificateNumber, Is.EqualTo("CERT-001"));
    }

    [Test]
    public async Task UpdateBackgroundCheck_SetsStatusAndDate()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Ray", "Brown", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Pending,
            null, null, null);

        DateTime checkDate = DateTime.UtcNow;
        await grain.UpdateBackgroundCheckAsync(BackgroundCheckStatus.Cleared, checkDate);

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.BackgroundCheckStatus, Is.EqualTo(BackgroundCheckStatus.Cleared));
        Assert.That(state.BackgroundCheckDate, Is.Not.Null);
    }

    [Test]
    public async Task GetHoursLog_ReturnsAllEntries()
    {
        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        IVolunteerGrain grain = GetVolunteer(volunteerId);

        await grain.EnrollAsync(
            volunteerId, "Pat", "Lee", null,
            null, null, null, null, null, null,
            DateTime.UtcNow, BackgroundCheckStatus.Cleared,
            null, null, null);

        await grain.LogHoursAsync(DateTime.UtcNow.AddDays(-3), 2.0m, VolunteerServiceType.ClericalSupport, null, null);
        await grain.LogHoursAsync(DateTime.UtcNow.AddDays(-2), 3.0m, VolunteerServiceType.ClericalSupport, null, null);
        await grain.LogHoursAsync(DateTime.UtcNow.AddDays(-1), 4.0m, VolunteerServiceType.FoodService, null, null);

        List<VolunteerHoursRecord> log = await grain.GetHoursLogAsync();
        Assert.That(log, Has.Count.EqualTo(3));
    }

    // ── Index Tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task VolunteerIndex_UpsertAndQueryByStatus()
    {
        IVolunteerIndexGrain index = GetIndex();

        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = volunteerId, FirstName = "Test", LastName = "Volunteer",
            Status = VolunteerStatus.Active, TotalHours = 100m,
            PrimaryServiceType = VolunteerServiceType.PatientEscort,
            EnrollmentDate = DateTime.UtcNow
        });

        List<VolunteerIndexEntry> active = await index.GetByStatusAsync(VolunteerStatus.Active);
        Assert.That(active.Any(v => v.VolunteerId == volunteerId), Is.True);
    }

    [Test]
    public async Task VolunteerIndex_SearchByName()
    {
        IVolunteerIndexGrain index = GetIndex();

        string volunteerId = $"VOL-{Guid.NewGuid():N}";
        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = volunteerId, FirstName = "Uniquename", LastName = "Searchtest",
            Status = VolunteerStatus.Active, TotalHours = 50m,
            EnrollmentDate = DateTime.UtcNow
        });

        List<VolunteerIndexEntry> results = await index.SearchAsync("Uniquename");
        Assert.That(results.Any(v => v.VolunteerId == volunteerId), Is.True);
    }
}
