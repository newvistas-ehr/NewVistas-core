// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the Care Team grain layer.
/// VistA PCMM File #404.43 (Patient-Team Assignment).
/// Tests CareTeamGrain, ProviderPatientIndexGrain, and ProviderScheduleIndexGrain.
/// </summary>
[TestFixture]
public class CareTeamGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── CareTeamGrain — Add Member ──────────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_AddMember_PersistsAllFields()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        DateTime expiration = DateTime.UtcNow.AddDays(90);
        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN MD", "SPECIALIST",
            "CARDIOLOGY", "APPOINTMENT", expiration);

        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
        Assert.That(members[0].ProviderId, Is.EqualTo("PROV-001"));
        Assert.That(members[0].ProviderName, Is.EqualTo("SMITH,JOHN MD"));
        Assert.That(members[0].Role, Is.EqualTo("SPECIALIST"));
        Assert.That(members[0].Specialty, Is.EqualTo("CARDIOLOGY"));
        Assert.That(members[0].AssignmentSource, Is.EqualTo("APPOINTMENT"));
        Assert.That(members[0].IsActive, Is.True);
        Assert.That(members[0].ExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task CareTeamGrain_AddMember_Idempotent_DoesNotDuplicate()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST",
            null, "APPOINTMENT", DateTime.UtcNow.AddDays(30));
        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN MD", "ATTENDING",
            "CARDIOLOGY", "MANUAL", DateTime.UtcNow.AddDays(90));

        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
        Assert.That(members[0].Role, Is.EqualTo("ATTENDING"));
        Assert.That(members[0].Specialty, Is.EqualTo("CARDIOLOGY"));
    }

    [Test]
    public async Task CareTeamGrain_AddMember_ReactivatesInactiveMember()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST",
            null, "APPOINTMENT", null);
        await grain.RemoveMemberAsync("PROV-001");

        bool activeBeforeReactivation = await grain.HasActiveMemberAsync("PROV-001");
        Assert.That(activeBeforeReactivation, Is.False);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST",
            null, "APPOINTMENT", null);

        bool activeAfterReactivation = await grain.HasActiveMemberAsync("PROV-001");
        Assert.That(activeAfterReactivation, Is.True);
        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
    }

    // ── CareTeamGrain — Remove Member ───────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_RemoveMember_SetsInactiveAndDeactivationDate()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST",
            null, "MANUAL", null);
        await grain.RemoveMemberAsync("PROV-001");

        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(1));
        Assert.That(members[0].IsActive, Is.False);
        Assert.That(members[0].DeactivationDate, Is.Not.Null);
    }

    [Test]
    public async Task CareTeamGrain_RemoveMember_NonExistent_NoOp()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.RemoveMemberAsync("NON-EXISTENT");
        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members, Has.Count.EqualTo(0));
    }

    // ── CareTeamGrain — Active Members ──────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_GetActiveMembers_ExcludesExpired()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        // Add one expired member and one active
        await grain.AddMemberAsync("PROV-EXPIRED", "EXPIRED,DOC", "SPECIALIST",
            null, "APPOINTMENT", DateTime.UtcNow.AddDays(-1));
        await grain.AddMemberAsync("PROV-ACTIVE", "ACTIVE,DOC", "SPECIALIST",
            null, "APPOINTMENT", DateTime.UtcNow.AddDays(90));

        List<CareTeamMember> active = await grain.GetActiveMembersAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ProviderId, Is.EqualTo("PROV-ACTIVE"));
    }

    [Test]
    public async Task CareTeamGrain_GetActiveMembers_ExcludesInactive()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST", null, "MANUAL", null);
        await grain.AddMemberAsync("PROV-002", "JONES,MARY", "NURSE", null, "MANUAL", null);
        await grain.RemoveMemberAsync("PROV-001");

        List<CareTeamMember> active = await grain.GetActiveMembersAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].ProviderId, Is.EqualTo("PROV-002"));
    }

    // ── CareTeamGrain — PCP ─────────────────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_SetPcp_AssignsRole()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.SetPcpAsync("PROV-PCP", "PRIMARY,DOC MD", "INTERNAL MEDICINE");

        CareTeamMember? pcp = await grain.GetPcpAsync();
        Assert.That(pcp, Is.Not.Null);
        Assert.That(pcp!.ProviderId, Is.EqualTo("PROV-PCP"));
        Assert.That(pcp.Role, Is.EqualTo("PCP"));
        Assert.That(pcp.Specialty, Is.EqualTo("INTERNAL MEDICINE"));
        Assert.That(pcp.ExpirationDate, Is.Null);
    }

    [Test]
    public async Task CareTeamGrain_SetPcp_ReplacesExistingPcp()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.SetPcpAsync("PROV-OLD", "OLD,PCP", "FAMILY MEDICINE");
        await grain.SetPcpAsync("PROV-NEW", "NEW,PCP", "INTERNAL MEDICINE");

        CareTeamMember? pcp = await grain.GetPcpAsync();
        Assert.That(pcp, Is.Not.Null);
        Assert.That(pcp!.ProviderId, Is.EqualTo("PROV-NEW"));

        // Old PCP should be deactivated
        List<CareTeamMember> all = await grain.GetMembersAsync();
        CareTeamMember old = all.First(m => m.ProviderId == "PROV-OLD");
        Assert.That(old.IsActive, Is.False);
    }

    // ── CareTeamGrain — HasActiveMember ─────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_HasActiveMember_ReturnsTrueForActive()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST", null, "MANUAL", null);
        Assert.That(await grain.HasActiveMemberAsync("PROV-001"), Is.True);
    }

    [Test]
    public async Task CareTeamGrain_HasActiveMember_ReturnsFalseForExpired()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST",
            null, "APPOINTMENT", DateTime.UtcNow.AddDays(-1));
        Assert.That(await grain.HasActiveMemberAsync("PROV-001"), Is.False);
    }

    [Test]
    public async Task CareTeamGrain_HasActiveMember_ReturnsFalseForInactive()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST", null, "MANUAL", null);
        await grain.RemoveMemberAsync("PROV-001");
        Assert.That(await grain.HasActiveMemberAsync("PROV-001"), Is.False);
    }

    // ── CareTeamGrain — GetMembersByRole ────────────────────────────────────

    [Test]
    public async Task CareTeamGrain_GetMembersByRole_FiltersCorrectly()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST", null, "MANUAL", null);
        await grain.AddMemberAsync("PROV-002", "JONES,MARY", "NURSE", null, "MANUAL", null);
        await grain.AddMemberAsync("PROV-003", "DOE,JANE", "SPECIALIST", null, "MANUAL", null);

        List<CareTeamMember> specialists = await grain.GetMembersByRoleAsync("SPECIALIST");
        Assert.That(specialists, Has.Count.EqualTo(2));
    }

    // ── CareTeamGrain — UpdateMemberLastSeen ────────────────────────────────

    [Test]
    public async Task CareTeamGrain_UpdateMemberLastSeen_UpdatesDate()
    {
        string key = $"CARE-TEAM:{Guid.NewGuid()}";
        ICareTeamGrain grain = _cluster.GrainFactory.GetGrain<ICareTeamGrain>(key);

        await grain.AddMemberAsync("PROV-001", "SMITH,JOHN", "SPECIALIST", null, "MANUAL", null);
        DateTime lastSeen = new DateTime(2026, 3, 20, 14, 30, 0, DateTimeKind.Utc);
        await grain.UpdateMemberLastSeenAsync("PROV-001", lastSeen);

        List<CareTeamMember> members = await grain.GetMembersAsync();
        Assert.That(members[0].LastSeenDate, Is.EqualTo(lastSeen));
    }

    // ── ProviderPatientIndexGrain ───────────────────────────────────────────

    [Test]
    public async Task ProviderPatientIndexGrain_AddOrUpdate_PersistsEntry()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001",
            PatientName = "PATIENT,TEST A",
            DateOfBirth = new DateTime(1960, 5, 15),
            SsnLast4 = "1234",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        List<ProviderPatientEntry> patients = await grain.GetAllPatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientName, Is.EqualTo("PATIENT,TEST A"));
        Assert.That(patients[0].SsnLast4, Is.EqualTo("1234"));
    }

    [Test]
    public async Task ProviderPatientIndexGrain_AddOrUpdate_UpdatesExisting()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "PATIENT,OLD NAME",
            Relationship = "SPECIALIST", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "PATIENT,NEW NAME",
            Relationship = "PCP", IsActive = true, AssignmentDate = DateTime.UtcNow
        });

        List<ProviderPatientEntry> patients = await grain.GetAllPatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientName, Is.EqualTo("PATIENT,NEW NAME"));
        Assert.That(patients[0].Relationship, Is.EqualTo("PCP"));
    }

    [Test]
    public async Task ProviderPatientIndexGrain_SearchPatients_ByNamePrefix()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "SMITH,JOHN",
            Relationship = "PCP", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-002", PatientName = "JONES,MARY",
            Relationship = "SPECIALIST", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-003", PatientName = "SMITH,JANE",
            Relationship = "SPECIALIST", IsActive = true, AssignmentDate = DateTime.UtcNow
        });

        List<ProviderPatientEntry> results = await grain.SearchPatientsAsync("SMITH");
        Assert.That(results, Has.Count.EqualTo(2));

        List<ProviderPatientEntry> resultsLower = await grain.SearchPatientsAsync("smith");
        Assert.That(resultsLower, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ProviderPatientIndexGrain_GetActive_ExcludesInactive()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "ACTIVE,PATIENT",
            Relationship = "PCP", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-002", PatientName = "INACTIVE,PATIENT",
            Relationship = "SPECIALIST", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.DeactivatePatientAsync("PAT-002");

        List<ProviderPatientEntry> active = await grain.GetActivePatientsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].PatientId, Is.EqualTo("PAT-001"));
    }

    [Test]
    public async Task ProviderPatientIndexGrain_GetByRole_FiltersCorrectly()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "A,PATIENT",
            Relationship = "PCP", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-002", PatientName = "B,PATIENT",
            Relationship = "SPECIALIST", IsActive = true, AssignmentDate = DateTime.UtcNow
        });

        List<ProviderPatientEntry> pcps = await grain.GetPatientsByRoleAsync("PCP");
        Assert.That(pcps, Has.Count.EqualTo(1));
        Assert.That(pcps[0].PatientId, Is.EqualTo("PAT-001"));
    }

    [Test]
    public async Task ProviderPatientIndexGrain_Deactivate_SetsInactive()
    {
        string key = $"PROV-PAT-IDX:{Guid.NewGuid()}";
        IProviderPatientIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>(key);

        await grain.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = "PAT-001", PatientName = "TEST,PATIENT",
            Relationship = "PCP", IsActive = true, AssignmentDate = DateTime.UtcNow
        });
        await grain.DeactivatePatientAsync("PAT-001");

        List<ProviderPatientEntry> all = await grain.GetAllPatientsAsync();
        Assert.That(all[0].IsActive, Is.False);
    }

    // ── ProviderScheduleIndexGrain ──────────────────────────────────────────

    [Test]
    public async Task ProviderScheduleIndexGrain_AddOrUpdate_PersistsEntry()
    {
        string key = $"PROV-SCHED:{Guid.NewGuid()}";
        IProviderScheduleIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>(key);

        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-001",
            PatientId = "PAT-001",
            PatientName = "TEST,PATIENT",
            AppointmentDateTime = DateTime.UtcNow.Date.AddHours(10),
            ClinicId = "CL-001",
            ClinicName = "PRIMARY CARE",
            DurationMinutes = 30,
            Status = "Scheduled",
            Purpose = "Follow-up"
        });

        List<ProviderScheduleEntry> all = await grain.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].PatientName, Is.EqualTo("TEST,PATIENT"));
    }

    [Test]
    public async Task ProviderScheduleIndexGrain_GetToday_FiltersCorrectly()
    {
        string key = $"PROV-SCHED:{Guid.NewGuid()}";
        IProviderScheduleIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>(key);

        DateTime today = DateTime.UtcNow.Date;
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-TODAY", PatientId = "PAT-001", PatientName = "TODAY,PATIENT",
            AppointmentDateTime = today.AddHours(9), ClinicId = "CL-001", ClinicName = "PRIMARY CARE",
            DurationMinutes = 30, Status = "Scheduled"
        });
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-TOMORROW", PatientId = "PAT-002", PatientName = "TOMORROW,PATIENT",
            AppointmentDateTime = today.AddDays(1).AddHours(10), ClinicId = "CL-001", ClinicName = "PRIMARY CARE",
            DurationMinutes = 30, Status = "Scheduled"
        });

        List<ProviderScheduleEntry> todayEntries = await grain.GetTodayAsync();
        Assert.That(todayEntries, Has.Count.EqualTo(1));
        Assert.That(todayEntries[0].AppointmentId, Is.EqualTo("APPT-TODAY"));
    }

    [Test]
    public async Task ProviderScheduleIndexGrain_GetByDate_FiltersCorrectly()
    {
        string key = $"PROV-SCHED:{Guid.NewGuid()}";
        IProviderScheduleIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>(key);

        DateTime targetDate = DateTime.UtcNow.Date.AddDays(3);
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-TARGET", PatientId = "PAT-001", PatientName = "TARGET,PATIENT",
            AppointmentDateTime = targetDate.AddHours(14), ClinicId = "CL-001", ClinicName = "CARDIOLOGY",
            DurationMinutes = 45, Status = "Scheduled"
        });
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-OTHER", PatientId = "PAT-002", PatientName = "OTHER,PATIENT",
            AppointmentDateTime = targetDate.AddDays(1).AddHours(10), ClinicId = "CL-002", ClinicName = "DERMATOLOGY",
            DurationMinutes = 20, Status = "Scheduled"
        });

        List<ProviderScheduleEntry> results = await grain.GetByDateAsync(targetDate);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].AppointmentId, Is.EqualTo("APPT-TARGET"));
    }

    [Test]
    public async Task ProviderScheduleIndexGrain_GetUpcoming_FiltersCorrectly()
    {
        string key = $"PROV-SCHED:{Guid.NewGuid()}";
        IProviderScheduleIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>(key);

        DateTime now = DateTime.UtcNow;
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-SOON", PatientId = "PAT-001", PatientName = "SOON,PATIENT",
            AppointmentDateTime = now.AddDays(2), ClinicId = "CL-001", ClinicName = "PRIMARY CARE",
            DurationMinutes = 30, Status = "Scheduled"
        });
        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-FAR", PatientId = "PAT-002", PatientName = "FAR,PATIENT",
            AppointmentDateTime = now.AddDays(30), ClinicId = "CL-001", ClinicName = "PRIMARY CARE",
            DurationMinutes = 30, Status = "Scheduled"
        });

        List<ProviderScheduleEntry> upcoming = await grain.GetUpcomingAsync(7);
        Assert.That(upcoming, Has.Count.EqualTo(1));
        Assert.That(upcoming[0].AppointmentId, Is.EqualTo("APPT-SOON"));
    }

    [Test]
    public async Task ProviderScheduleIndexGrain_UpdateStatus_ChangesStatus()
    {
        string key = $"PROV-SCHED:{Guid.NewGuid()}";
        IProviderScheduleIndexGrain grain = _cluster.GrainFactory.GetGrain<IProviderScheduleIndexGrain>(key);

        await grain.AddOrUpdateAsync(new ProviderScheduleEntry
        {
            AppointmentId = "APPT-001", PatientId = "PAT-001", PatientName = "TEST,PATIENT",
            AppointmentDateTime = DateTime.UtcNow.AddHours(2), ClinicId = "CL-001", ClinicName = "PRIMARY CARE",
            DurationMinutes = 30, Status = "Scheduled"
        });

        await grain.UpdateStatusAsync("APPT-001", "Checked In");

        List<ProviderScheduleEntry> all = await grain.GetAllAsync();
        Assert.That(all[0].Status, Is.EqualTo("Checked In"));
    }
}
