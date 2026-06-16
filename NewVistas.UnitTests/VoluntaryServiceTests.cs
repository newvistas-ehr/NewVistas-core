// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Voluntary Service grain layer.
/// VistA VOLUNTARY SERVICE file (#8810).
/// Tests VolunteerGrain and VolunteerIndexGrain.
/// </summary>
[TestFixture]
public class VoluntaryServiceTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── VolunteerGrain — Enrollment ────────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_Enroll_PersistsAllFields()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        DateTime dob = new DateTime(1975, 6, 15);
        DateTime enrolled = new DateTime(2024, 1, 10);

        await grain.EnrollAsync(
            volunteerId:             "VOL-001",
            firstName:               "Margaret",
            lastName:                "Johnson",
            middleName:              "Ann",
            dateOfBirth:             dob,
            phoneNumber:             "555-867-5309",
            email:                   "mjohnson@example.com",
            address:                 "123 Oak Street, Memphis TN 38103",
            emergencyContactName:    "Robert Johnson",
            emergencyContactPhone:   "555-111-2222",
            enrollmentDate:          enrolled,
            backgroundCheckStatus:   BackgroundCheckStatus.Cleared,
            skills:                  new List<string> { "Reading", "Transportation" },
            interests:               new List<string> { "Patient Escort", "Gift Shop" },
            notes:                   "Retired nurse, very dedicated.");

        VolunteerState state = await grain.GetAsync();

        Assert.That(state.VolunteerId,              Is.EqualTo("VOL-001"));
        Assert.That(state.FirstName,                Is.EqualTo("Margaret"));
        Assert.That(state.LastName,                 Is.EqualTo("Johnson"));
        Assert.That(state.MiddleName,               Is.EqualTo("Ann"));
        Assert.That(state.DateOfBirth,              Is.EqualTo(dob));
        Assert.That(state.PhoneNumber,              Is.EqualTo("555-867-5309"));
        Assert.That(state.Email,                    Is.EqualTo("mjohnson@example.com"));
        Assert.That(state.Address,                  Does.Contain("Oak Street"));
        Assert.That(state.EmergencyContactName,     Is.EqualTo("Robert Johnson"));
        Assert.That(state.EmergencyContactPhone,    Is.EqualTo("555-111-2222"));
        Assert.That(state.EnrollmentDate,           Is.EqualTo(enrolled));
        Assert.That(state.Status,                   Is.EqualTo(VolunteerStatus.Active));
        Assert.That(state.BackgroundCheckStatus,    Is.EqualTo(BackgroundCheckStatus.Cleared));
        Assert.That(state.Skills,                   Has.Count.EqualTo(2));
        Assert.That(state.Skills,                   Does.Contain("Transportation"));
        Assert.That(state.Interests,                Has.Count.EqualTo(2));
        Assert.That(state.Notes,                    Does.Contain("Retired nurse"));
        Assert.That(state.TotalHours,               Is.EqualTo(0));
    }

    [Test]
    public async Task VolunteerGrain_Enroll_NullableFields_DefaultsCorrectly()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            volunteerId:             "VOL-002",
            firstName:               "James",
            lastName:                "Smith",
            middleName:              null,
            dateOfBirth:             null,
            phoneNumber:             null,
            email:                   null,
            address:                 null,
            emergencyContactName:    null,
            emergencyContactPhone:   null,
            enrollmentDate:          DateTime.UtcNow,
            backgroundCheckStatus:   BackgroundCheckStatus.NotRequired,
            skills:                  null,
            interests:               null,
            notes:                   null);

        VolunteerState state = await grain.GetAsync();

        Assert.That(state.FirstName,            Is.EqualTo("James"));
        Assert.That(state.LastName,             Is.EqualTo("Smith"));
        Assert.That(state.MiddleName,           Is.Null);
        Assert.That(state.Status,               Is.EqualTo(VolunteerStatus.Active));
        Assert.That(state.Skills,               Is.Empty);
        Assert.That(state.Interests,            Is.Empty);
        Assert.That(state.TotalHours,           Is.EqualTo(0));
    }

    // ── VolunteerGrain — Profile Update ────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_UpdateProfile_ReflectsChanges()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-003", "Alice", "Brown", null, null, "555-100-0000", null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.NotRequired, null, null, null);

        await grain.UpdateProfileAsync(
            firstName:            "Alice",
            lastName:             "Brown-Davis",
            middleName:           "Marie",
            phoneNumber:          "555-999-8888",
            email:                "alice.bd@example.com",
            address:              "456 Elm Ave",
            emergencyContactName: "Carl Davis",
            emergencyContactPhone:"555-777-6666",
            notes:                "Name changed after marriage.");

        VolunteerState state = await grain.GetAsync();

        Assert.That(state.LastName,              Is.EqualTo("Brown-Davis"));
        Assert.That(state.MiddleName,            Is.EqualTo("Marie"));
        Assert.That(state.PhoneNumber,           Is.EqualTo("555-999-8888"));
        Assert.That(state.Email,                 Is.EqualTo("alice.bd@example.com"));
        Assert.That(state.EmergencyContactName,  Is.EqualTo("Carl Davis"));
        Assert.That(state.Notes,                 Does.Contain("marriage"));
    }

    // ── VolunteerGrain — Status Update ─────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_UpdateStatus_ChangesStatus()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-004", "Carol", "White", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.NotRequired, null, null, null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(VolunteerStatus.Active));

        await grain.UpdateStatusAsync(VolunteerStatus.Inactive, "Taking a break for health reasons.");

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(VolunteerStatus.Inactive));
        Assert.That(state.Notes,  Does.Contain("health reasons"));
    }

    [Test]
    public async Task VolunteerGrain_UpdateStatus_Withdrawn_SetsWithdrawnStatus()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-005", "David", "Green", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.NotRequired, null, null, null);

        await grain.UpdateStatusAsync(VolunteerStatus.Withdrawn, null);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(VolunteerStatus.Withdrawn));
    }

    // ── VolunteerGrain — Hours Logging ─────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_LogHours_AccumulatesTotalHours()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-006", "Eve", "Clark", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        string hoursId = await grain.LogHoursAsync(
            DateTime.Today, 4.5m, VolunteerServiceType.PatientEscort, null, "Morning shift");

        Assert.That(hoursId, Does.StartWith("VS-HOURS:"));

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.TotalHours,   Is.EqualTo(4.5m));
        Assert.That(state.HoursLog,     Has.Count.EqualTo(1));
        Assert.That(state.HoursLog[0].Hours,       Is.EqualTo(4.5m));
        Assert.That(state.HoursLog[0].ServiceType, Is.EqualTo(VolunteerServiceType.PatientEscort));
        Assert.That(state.HoursLog[0].Notes,       Is.EqualTo("Morning shift"));
    }

    [Test]
    public async Task VolunteerGrain_LogMultipleHours_TotalIsCorrect()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-007", "Frank", "Lee", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        await grain.LogHoursAsync(DateTime.Today.AddDays(-7), 3.0m, VolunteerServiceType.GiftShop, null, null);
        await grain.LogHoursAsync(DateTime.Today.AddDays(-3), 4.0m, VolunteerServiceType.Reading, null, null);
        await grain.LogHoursAsync(DateTime.Today, 2.5m, VolunteerServiceType.ClericalSupport, null, null);

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.TotalHours,   Is.EqualTo(9.5m));
        Assert.That(state.HoursLog,     Has.Count.EqualTo(3));
    }

    [Test]
    public async Task VolunteerGrain_LogHours_ReturnsUniqueIds()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-008", "Grace", "Hall", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.NotRequired, null, null, null);

        string id1 = await grain.LogHoursAsync(DateTime.Today, 2.0m, VolunteerServiceType.Other, null, null);
        string id2 = await grain.LogHoursAsync(DateTime.Today, 3.0m, VolunteerServiceType.Other, null, null);

        Assert.That(id1, Is.Not.EqualTo(id2));
        Assert.That(id1, Does.StartWith("VS-HOURS:"));
        Assert.That(id2, Does.StartWith("VS-HOURS:"));
    }

    // ── VolunteerGrain — Assignments ───────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_AddAssignment_PersistsRecord()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-009", "Henry", "Martin", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        DateTime startDate = new DateTime(2024, 2, 1);
        string assignmentId = await grain.AddAssignmentAsync(
            VolunteerServiceType.PatientEscort,
            "3 West",
            "Patient Escort Volunteer",
            startDate,
            isPrimary:         true,
            supervisorId:      "SUPV-001",
            supervisorName:    "Nurse Coordinator Jones",
            notes:             "Monday/Wednesday mornings.");

        Assert.That(assignmentId, Does.StartWith("VS-ASSIGN:"));

        List<VolunteerAssignmentRecord> assignments = await grain.GetAssignmentsAsync();
        Assert.That(assignments, Has.Count.EqualTo(1));

        VolunteerAssignmentRecord a = assignments[0];
        Assert.That(a.AssignmentId,   Is.EqualTo(assignmentId));
        Assert.That(a.ServiceType,    Is.EqualTo(VolunteerServiceType.PatientEscort));
        Assert.That(a.ServiceArea,    Is.EqualTo("3 West"));
        Assert.That(a.Role,           Is.EqualTo("Patient Escort Volunteer"));
        Assert.That(a.StartDate,      Is.EqualTo(startDate));
        Assert.That(a.IsPrimary,      Is.True);
        Assert.That(a.IsActive,       Is.True);
        Assert.That(a.SupervisorName, Is.EqualTo("Nurse Coordinator Jones"));
        Assert.That(a.Notes,          Does.Contain("Monday"));
    }

    [Test]
    public async Task VolunteerGrain_AddMultipleAssignments_NewPrimaryDemotesOld()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-010", "Irene", "Taylor", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        string id1 = await grain.AddAssignmentAsync(
            VolunteerServiceType.GiftShop, "Gift Shop", "Cashier Volunteer",
            DateTime.Today.AddMonths(-6), isPrimary: true, null, null, null);

        string id2 = await grain.AddAssignmentAsync(
            VolunteerServiceType.Reading, "Library", "Reading Volunteer",
            DateTime.Today.AddMonths(-1), isPrimary: true, null, null, null);

        List<VolunteerAssignmentRecord> assignments = await grain.GetAssignmentsAsync();
        Assert.That(assignments, Has.Count.EqualTo(2));

        VolunteerAssignmentRecord first  = assignments.First(a => a.AssignmentId == id1);
        VolunteerAssignmentRecord second = assignments.First(a => a.AssignmentId == id2);

        Assert.That(first.IsPrimary,  Is.False, "Original primary should be demoted");
        Assert.That(second.IsPrimary, Is.True,  "New primary should be set");
    }

    [Test]
    public async Task VolunteerGrain_EndAssignment_SetsEndDateAndInactive()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-011", "Jack", "Wilson", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        string assignmentId = await grain.AddAssignmentAsync(
            VolunteerServiceType.Transportation, "Motor Pool", "Driver Volunteer",
            DateTime.Today.AddMonths(-3), isPrimary: true, null, null, null);

        DateTime endDate = DateTime.Today;
        await grain.EndAssignmentAsync(assignmentId, endDate, "Volunteer relocated.");

        List<VolunteerAssignmentRecord> assignments = await grain.GetAssignmentsAsync();
        VolunteerAssignmentRecord a = assignments.First(x => x.AssignmentId == assignmentId);

        Assert.That(a.IsActive,  Is.False);
        Assert.That(a.EndDate,   Is.EqualTo(endDate));
        Assert.That(a.Notes,     Does.Contain("relocated"));
    }

    [Test]
    public async Task VolunteerGrain_EndAssignment_NonExistent_DoesNotThrow()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-012", "Kate", "Anderson", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.NotRequired, null, null, null);

        Assert.DoesNotThrowAsync(async () =>
            await grain.EndAssignmentAsync("VS-ASSIGN:nonexistent", DateTime.Today, null));
    }

    // ── VolunteerGrain — Recognition ───────────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_AddRecognition_PersistsRecord()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-013", "Laura", "Thomas", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        // Give the volunteer 100 hours first
        await grain.LogHoursAsync(DateTime.Today, 100m, VolunteerServiceType.PatientEscort, null, null);

        DateTime awardDate = new DateTime(2024, 12, 1);
        await grain.AddRecognitionAsync(
            VolunteerRecognitionType.OneHundredHours,
            awardDate,
            awardedBy:          "Voluntary Service Director",
            description:        "100 Hour Service Award",
            certificateNumber:  "CERT-2024-001");

        List<VolunteerRecognitionRecord> recognitions = await grain.GetRecognitionsAsync();
        Assert.That(recognitions, Has.Count.EqualTo(1));

        VolunteerRecognitionRecord r = recognitions[0];
        Assert.That(r.RecognitionId,      Does.StartWith("VS-RECOG:"));
        Assert.That(r.RecognitionType,    Is.EqualTo(VolunteerRecognitionType.OneHundredHours));
        Assert.That(r.AwardDate,          Is.EqualTo(awardDate));
        Assert.That(r.AwardedBy,          Is.EqualTo("Voluntary Service Director"));
        Assert.That(r.Description,        Is.EqualTo("100 Hour Service Award"));
        Assert.That(r.CertificateNumber,  Is.EqualTo("CERT-2024-001"));
    }

    [Test]
    public async Task VolunteerGrain_AddMultipleRecognitions_AllPersisted()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-014", "Mike", "Jackson", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Cleared, null, null, null);

        await grain.AddRecognitionAsync(VolunteerRecognitionType.OneHundredHours,
            new DateTime(2022, 12, 1), null, "100 Hour Award", null);

        await grain.AddRecognitionAsync(VolunteerRecognitionType.FiveHundredHours,
            new DateTime(2023, 12, 1), "VS Director", "500 Hour Award", "CERT-2023-012");

        await grain.AddRecognitionAsync(VolunteerRecognitionType.AnnualAward,
            new DateTime(2024, 5, 15), "Medical Center Director", "Volunteer of the Year 2024", null);

        List<VolunteerRecognitionRecord> recognitions = await grain.GetRecognitionsAsync();
        Assert.That(recognitions, Has.Count.EqualTo(3));
        Assert.That(recognitions.Select(r => r.RecognitionType),
            Does.Contain(VolunteerRecognitionType.FiveHundredHours));
    }

    // ── VolunteerGrain — Background Check ──────────────────────────────────────

    [Test]
    public async Task VolunteerGrain_UpdateBackgroundCheck_ChangesStatus()
    {
        string key = $"VS-VOLUNTEER:{Guid.NewGuid()}";
        IVolunteerGrain grain = _cluster.GrainFactory.GetGrain<IVolunteerGrain>(key);

        await grain.EnrollAsync(
            "VOL-015", "Nancy", "Roberts", null, null, null, null,
            null, null, null, DateTime.UtcNow,
            BackgroundCheckStatus.Pending, null, null, null);

        Assert.That((await grain.GetAsync()).BackgroundCheckStatus, Is.EqualTo(BackgroundCheckStatus.Pending));

        DateTime checkDate = new DateTime(2024, 3, 20);
        await grain.UpdateBackgroundCheckAsync(BackgroundCheckStatus.Cleared, checkDate);

        VolunteerState state = await grain.GetAsync();
        Assert.That(state.BackgroundCheckStatus, Is.EqualTo(BackgroundCheckStatus.Cleared));
        Assert.That(state.BackgroundCheckDate,   Is.EqualTo(checkDate));
    }

    // ── VolunteerIndexGrain ────────────────────────────────────────────────────

    [Test]
    public async Task VolunteerIndexGrain_UpsertEntry_IdempotentForSameVolunteer()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        VolunteerIndexEntry entry = new VolunteerIndexEntry
        {
            VolunteerId     = "IDX-VOL-001",
            FirstName       = "Oscar",
            LastName        = "Wright",
            Status          = VolunteerStatus.Active,
            TotalHours      = 50m,
            PrimaryServiceType = VolunteerServiceType.GiftShop,
            EnrollmentDate  = DateTime.UtcNow
        };

        await index.UpsertEntryAsync(entry);
        await index.UpsertEntryAsync(entry); // duplicate — should update, not duplicate

        List<VolunteerIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task VolunteerIndexGrain_UpsertEntry_UpdatesExistingEntry()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        VolunteerIndexEntry initial = new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-002",
            FirstName = "Paula",
            LastName = "Young",
            Status = VolunteerStatus.Active,
            TotalHours = 10m,
            EnrollmentDate = DateTime.UtcNow
        };

        await index.UpsertEntryAsync(initial);

        VolunteerIndexEntry updated = new VolunteerIndexEntry
        {
            VolunteerId = initial.VolunteerId,
            FirstName = initial.FirstName,
            LastName = initial.LastName,
            Status = VolunteerStatus.Active,
            TotalHours = 110m,
            EnrollmentDate = initial.EnrollmentDate
        };
        await index.UpsertEntryAsync(updated);

        List<VolunteerIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].TotalHours, Is.EqualTo(110m));
    }

    [Test]
    public async Task VolunteerIndexGrain_GetByStatus_FiltersCorrectly()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-010", FirstName = "Ray", LastName = "Adams",
            Status = VolunteerStatus.Active, TotalHours = 25m, EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-011", FirstName = "Sue", LastName = "Baker",
            Status = VolunteerStatus.Inactive, TotalHours = 120m, EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-012", FirstName = "Tom", LastName = "Carter",
            Status = VolunteerStatus.Withdrawn, TotalHours = 0m, EnrollmentDate = DateTime.UtcNow
        });

        List<VolunteerIndexEntry> active = await index.GetByStatusAsync(VolunteerStatus.Active);
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].VolunteerId, Is.EqualTo("IDX-VOL-010"));

        List<VolunteerIndexEntry> inactive = await index.GetByStatusAsync(VolunteerStatus.Inactive);
        Assert.That(inactive, Has.Count.EqualTo(1));
        Assert.That(inactive[0].VolunteerId, Is.EqualTo("IDX-VOL-011"));
    }

    [Test]
    public async Task VolunteerIndexGrain_GetByServiceType_FiltersCorrectly()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-020", FirstName = "Uma", LastName = "Davis",
            Status = VolunteerStatus.Active, TotalHours = 40m,
            PrimaryServiceType = VolunteerServiceType.PatientEscort,
            EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-021", FirstName = "Victor", LastName = "Evans",
            Status = VolunteerStatus.Active, TotalHours = 80m,
            PrimaryServiceType = VolunteerServiceType.GiftShop,
            EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-022", FirstName = "Wendy", LastName = "Foster",
            Status = VolunteerStatus.Active, TotalHours = 60m,
            PrimaryServiceType = VolunteerServiceType.PatientEscort,
            EnrollmentDate = DateTime.UtcNow
        });

        List<VolunteerIndexEntry> escorts = await index.GetByServiceTypeAsync(VolunteerServiceType.PatientEscort);
        Assert.That(escorts, Has.Count.EqualTo(2));
        Assert.That(escorts.Select(e => e.VolunteerId), Does.Contain("IDX-VOL-020"));
        Assert.That(escorts.Select(e => e.VolunteerId), Does.Contain("IDX-VOL-022"));

        List<VolunteerIndexEntry> giftShop = await index.GetByServiceTypeAsync(VolunteerServiceType.GiftShop);
        Assert.That(giftShop, Has.Count.EqualTo(1));
        Assert.That(giftShop[0].VolunteerId, Is.EqualTo("IDX-VOL-021"));
    }

    [Test]
    public async Task VolunteerIndexGrain_SearchByName_PartialMatchWorks()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-030", FirstName = "Xavier", LastName = "Gonzalez",
            Status = VolunteerStatus.Active, TotalHours = 30m, EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-031", FirstName = "Yolanda", LastName = "Green",
            Status = VolunteerStatus.Active, TotalHours = 55m, EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-032", FirstName = "Zachary", LastName = "Harris",
            Status = VolunteerStatus.Active, TotalHours = 90m, EnrollmentDate = DateTime.UtcNow
        });

        // Last name partial match
        List<VolunteerIndexEntry> greenMatches = await index.SearchAsync("gre");
        Assert.That(greenMatches.Select(e => e.VolunteerId), Does.Contain("IDX-VOL-031"));

        // First name partial match
        List<VolunteerIndexEntry> xavierMatches = await index.SearchAsync("Xav");
        Assert.That(xavierMatches, Has.Count.EqualTo(1));
        Assert.That(xavierMatches[0].VolunteerId, Is.EqualTo("IDX-VOL-030"));

        // Case-insensitive
        List<VolunteerIndexEntry> caseMatches = await index.SearchAsync("GONZALEZ");
        Assert.That(caseMatches, Has.Count.EqualTo(1));
        Assert.That(caseMatches[0].VolunteerId, Is.EqualTo("IDX-VOL-030"));
    }

    [Test]
    public async Task VolunteerIndexGrain_RemoveEntry_RemovesFromList()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-040", FirstName = "Anne", LastName = "Irving",
            Status = VolunteerStatus.Active, TotalHours = 10m, EnrollmentDate = DateTime.UtcNow
        });

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-041", FirstName = "Ben", LastName = "Johnson",
            Status = VolunteerStatus.Active, TotalHours = 20m, EnrollmentDate = DateTime.UtcNow
        });

        await index.RemoveEntryAsync("IDX-VOL-040");

        List<VolunteerIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Select(e => e.VolunteerId), Does.Not.Contain("IDX-VOL-040"));
        Assert.That(all.Select(e => e.VolunteerId), Does.Contain("IDX-VOL-041"));
    }

    [Test]
    public async Task VolunteerIndexGrain_SearchByName_NoMatch_ReturnsEmpty()
    {
        IVolunteerIndexGrain index = _cluster.GrainFactory.GetGrain<IVolunteerIndexGrain>($"VS-IDX-TEST:{Guid.NewGuid()}");

        await index.UpsertEntryAsync(new VolunteerIndexEntry
        {
            VolunteerId = "IDX-VOL-050", FirstName = "Clara", LastName = "Moore",
            Status = VolunteerStatus.Active, TotalHours = 15m, EnrollmentDate = DateTime.UtcNow
        });

        List<VolunteerIndexEntry> results = await index.SearchAsync("zzznomatch");
        Assert.That(results, Is.Empty);
    }
}
