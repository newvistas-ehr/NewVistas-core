// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for patient selection workflows — care-team-based ("My Patients")
/// and ward-based ("Floor Nurse View") patient selection, plus NewPerson default ward fields.
///
/// Tests provider patient index grains, ward census grains, NewPerson ward assignment,
/// and care team integration with the provider patient index.
/// </summary>
[TestFixture]
public class PatientSelectionWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        RequestContext.Remove(RequestContextKeys.UserId);
        RequestContext.Remove(RequestContextKeys.UserName);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private IProviderPatientIndexGrain GetProviderIndex(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}");

    private IWardCensusGrain GetWardCensus(string wardId) =>
        _cluster.GrainFactory.GetGrain<IWardCensusGrain>($"WARD-CENSUS:{wardId}");

    private INewPersonGrain GetNewPerson(string userId) =>
        _cluster.GrainFactory.GetGrain<INewPersonGrain>(userId);

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// Creates a patient with demographics so the workflow grain can read name/DOB/SSN.
    /// </summary>
    private async Task SetupPatient(string patientId, string name = "TESTPATIENT,SELECT",
        string ssn = "999887766")
    {
        IPatientGrain patientGrain = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patientGrain.UpdateDemographicsAsync(name, "M", new DateTime(1970, 6, 15), ssn);
    }

    /// <summary>
    /// Provisions a user with XUPROG key and sets RequestContext for security bypass.
    /// </summary>
    private async Task<string> SetupSecurityContext()
    {
        string userId = $"USER-PSEL-{Guid.NewGuid()}";
        IAccessControlGrain acl = _cluster.GrainFactory.GetGrain<IAccessControlGrain>($"ACL:{userId}");
        await acl.GrantKeyAsync(SecurityKeys.XUPROG, "ADMIN", "ADMIN,SYS");
        await acl.StartSessionAsync(null, null, null, null);
        RequestContext.Set(RequestContextKeys.UserId, userId);
        RequestContext.Set(RequestContextKeys.UserName, "TEST,PSELECT");
        return userId;
    }

    // ════════════════════════════════════════════════════════════════════════
    // Provider Patient Index — "My Patients" (5 tests)
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task ProviderIndex_AddPatient_AppearsInActiveList()
    {
        // Arrange
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        IProviderPatientIndexGrain index = GetProviderIndex(providerId);

        ProviderPatientEntry entry = new()
        {
            PatientId = patientId,
            PatientName = "JONES,ACTIVE A",
            DateOfBirth = new DateTime(1955, 1, 10),
            SsnLast4 = "1234",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        };

        // Act
        await index.AddOrUpdatePatientAsync(entry);

        // Assert
        List<ProviderPatientEntry> active = await index.GetActivePatientsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].PatientId, Is.EqualTo(patientId));
        Assert.That(active[0].PatientName, Is.EqualTo("JONES,ACTIVE A"));
        Assert.That(active[0].Relationship, Is.EqualTo("PCP"));
    }

    [Test]
    public async Task ProviderIndex_DeactivatePatient_ExcludedFromActiveList()
    {
        // Arrange
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        IProviderPatientIndexGrain index = GetProviderIndex(providerId);

        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = patientId,
            PatientName = "SMITH,DEACTIVATE",
            Relationship = "SPECIALIST",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Act
        await index.DeactivatePatientAsync(patientId);

        // Assert — not in active list
        List<ProviderPatientEntry> active = await index.GetActivePatientsAsync();
        Assert.That(active, Has.Count.EqualTo(0));

        // Assert — still in all list
        List<ProviderPatientEntry> all = await index.GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IsActive, Is.False);
    }

    [Test]
    public async Task ProviderIndex_SearchByName_FiltersCorrectly()
    {
        // Arrange
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        IProviderPatientIndexGrain index = GetProviderIndex(providerId);

        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "ANDERSON,JAMES",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });
        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "ANDERSON,MARY",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });
        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "BAKER,THOMAS",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Act
        List<ProviderPatientEntry> results = await index.SearchPatientsAsync("ANDERSON");

        // Assert
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(p => p.PatientName.StartsWith("ANDERSON")), Is.True);
    }

    [Test]
    public async Task ProviderIndex_GetByRole_FiltersByRelationship()
    {
        // Arrange
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        IProviderPatientIndexGrain index = GetProviderIndex(providerId);

        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "PCP,PATIENT A",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });
        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "NURSE,PATIENT B",
            Relationship = "NURSE",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });
        await index.AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
            PatientName = "PCP,PATIENT C",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Act
        List<ProviderPatientEntry> pcpPatients = await index.GetPatientsByRoleAsync("PCP");

        // Assert
        Assert.That(pcpPatients, Has.Count.EqualTo(2));
        Assert.That(pcpPatients.All(p => p.Relationship == "PCP"), Is.True);
    }

    [Test]
    public async Task ProviderIndex_MultipleProviders_IndependentLists()
    {
        // Arrange
        string providerId1 = $"PROV-PSEL-{Guid.NewGuid()}";
        string providerId2 = $"PROV-PSEL-{Guid.NewGuid()}";
        string patientA = $"PAT-PSEL-{Guid.NewGuid()}";
        string patientB = $"PAT-PSEL-{Guid.NewGuid()}";

        await GetProviderIndex(providerId1).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = patientA,
            PatientName = "ALPHA,PATIENT",
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        await GetProviderIndex(providerId2).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = patientB,
            PatientName = "BRAVO,PATIENT",
            Relationship = "SPECIALIST",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Act
        List<ProviderPatientEntry> prov1Patients = await GetProviderIndex(providerId1).GetActivePatientsAsync();
        List<ProviderPatientEntry> prov2Patients = await GetProviderIndex(providerId2).GetActivePatientsAsync();

        // Assert
        Assert.That(prov1Patients, Has.Count.EqualTo(1));
        Assert.That(prov1Patients[0].PatientId, Is.EqualTo(patientA));

        Assert.That(prov2Patients, Has.Count.EqualTo(1));
        Assert.That(prov2Patients[0].PatientId, Is.EqualTo(patientB));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Ward Census — "Floor Nurse View" (4 tests)
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WardCensus_AddPatient_AppearsInCensus()
    {
        // Arrange
        string wardId = $"WARD-PSEL-{Guid.NewGuid()}";
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        IWardCensusGrain census = GetWardCensus(wardId);

        WardCensusEntry entry = new()
        {
            PatientId = patientId,
            PatientName = "CENSUS,PATIENT A",
            AdmissionId = $"ADM-{Guid.NewGuid()}",
            AdmitDate = DateTime.UtcNow.AddDays(-2),
            RoomBed = "301-A",
            TreatingSpecialty = "INTERNAL MEDICINE",
            AttendingPhysicianName = "DOCTOR,ATTENDING MD",
            LastMovementDate = DateTime.UtcNow
        };

        // Act
        await census.AddOrUpdatePatientAsync(entry);

        // Assert
        List<WardCensusEntry> patients = await census.GetCensusAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(patients[0].PatientName, Is.EqualTo("CENSUS,PATIENT A"));
        Assert.That(patients[0].RoomBed, Is.EqualTo("301-A"));
    }

    [Test]
    public async Task WardCensus_RemovePatient_DisappearsFromCensus()
    {
        // Arrange
        string wardId = $"WARD-PSEL-{Guid.NewGuid()}";
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        IWardCensusGrain census = GetWardCensus(wardId);

        await census.AddOrUpdatePatientAsync(new WardCensusEntry
        {
            PatientId = patientId,
            PatientName = "DISCHARGE,PATIENT",
            AdmissionId = $"ADM-{Guid.NewGuid()}",
            AdmitDate = DateTime.UtcNow.AddDays(-5),
            RoomBed = "402-B",
            LastMovementDate = DateTime.UtcNow
        });

        // Act
        await census.RemovePatientAsync(patientId);

        // Assert
        List<WardCensusEntry> patients = await census.GetCensusAsync();
        Assert.That(patients, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task WardCensus_PatientCount_MatchesCensus()
    {
        // Arrange
        string wardId = $"WARD-PSEL-{Guid.NewGuid()}";
        IWardCensusGrain census = GetWardCensus(wardId);

        for (int i = 0; i < 3; i++)
        {
            await census.AddOrUpdatePatientAsync(new WardCensusEntry
            {
                PatientId = $"PAT-PSEL-{Guid.NewGuid()}",
                PatientName = $"COUNT,PATIENT {i}",
                AdmissionId = $"ADM-{Guid.NewGuid()}",
                AdmitDate = DateTime.UtcNow.AddDays(-i),
                RoomBed = $"50{i}-A",
                LastMovementDate = DateTime.UtcNow
            });
        }

        // Act
        int count = await census.GetPatientCountAsync();

        // Assert
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task WardCensus_MultipleWards_IndependentCensus()
    {
        // Arrange
        string wardId1 = $"WARD-PSEL-{Guid.NewGuid()}";
        string wardId2 = $"WARD-PSEL-{Guid.NewGuid()}";
        string patientA = $"PAT-PSEL-{Guid.NewGuid()}";
        string patientB = $"PAT-PSEL-{Guid.NewGuid()}";

        await GetWardCensus(wardId1).AddOrUpdatePatientAsync(new WardCensusEntry
        {
            PatientId = patientA,
            PatientName = "WARD1,ONLY",
            AdmissionId = $"ADM-{Guid.NewGuid()}",
            AdmitDate = DateTime.UtcNow.AddDays(-1),
            RoomBed = "101-A",
            LastMovementDate = DateTime.UtcNow
        });

        await GetWardCensus(wardId2).AddOrUpdatePatientAsync(new WardCensusEntry
        {
            PatientId = patientB,
            PatientName = "WARD2,ONLY",
            AdmissionId = $"ADM-{Guid.NewGuid()}",
            AdmitDate = DateTime.UtcNow.AddDays(-1),
            RoomBed = "201-B",
            LastMovementDate = DateTime.UtcNow
        });

        // Act
        List<WardCensusEntry> ward1 = await GetWardCensus(wardId1).GetCensusAsync();
        List<WardCensusEntry> ward2 = await GetWardCensus(wardId2).GetCensusAsync();

        // Assert
        Assert.That(ward1, Has.Count.EqualTo(1));
        Assert.That(ward1[0].PatientId, Is.EqualTo(patientA));

        Assert.That(ward2, Has.Count.EqualTo(1));
        Assert.That(ward2[0].PatientId, Is.EqualTo(patientB));
    }

    // ════════════════════════════════════════════════════════════════════════
    // NewPerson Ward Assignment (3 tests)
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task NewPerson_SetDefaultWard_PersistsWardId()
    {
        // Arrange
        string userId = $"USER-PSEL-{Guid.NewGuid()}";
        INewPersonGrain person = GetNewPerson(userId);

        // Act
        await person.SetDefaultWardAsync("WARD-ICU-1", "ICU 1");

        // Assert
        NewPersonState state = await person.GetPersonAsync();
        Assert.That(state.DefaultWardId, Is.EqualTo("WARD-ICU-1"));
    }

    [Test]
    public async Task NewPerson_SetDefaultWard_PersistsWardName()
    {
        // Arrange
        string userId = $"USER-PSEL-{Guid.NewGuid()}";
        INewPersonGrain person = GetNewPerson(userId);

        // Act
        await person.SetDefaultWardAsync("WARD-MED-3A", "Medical 3A");

        // Assert
        NewPersonState state = await person.GetPersonAsync();
        Assert.That(state.DefaultWardName, Is.EqualTo("Medical 3A"));
    }

    [Test]
    public async Task NewPerson_DefaultWard_InitiallyEmpty()
    {
        // Arrange
        string userId = $"USER-PSEL-{Guid.NewGuid()}";
        INewPersonGrain person = GetNewPerson(userId);

        // Act
        NewPersonState state = await person.GetPersonAsync();

        // Assert
        Assert.That(state.DefaultWardId, Is.EqualTo(string.Empty));
        Assert.That(state.DefaultWardName, Is.EqualTo(string.Empty));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Care Team Integration with Patient Selection (4 tests)
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task CareTeam_AddMember_SyncsToProviderIndex()
    {
        // Arrange
        await SetupSecurityContext();
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        await SetupPatient(patientId, "SYNC,PATIENT A");

        // Act — add care team member via workflow
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.AddCareTeamMemberAsync(providerId, "SYNC,PROVIDER MD",
            "SPECIALIST", "CARDIOLOGY", "MANUAL", null);

        // Assert — provider index should contain the patient
        List<ProviderPatientEntry> patients = await GetProviderIndex(providerId).GetActivePatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(patients[0].PatientName, Is.EqualTo("SYNC,PATIENT A"));
        Assert.That(patients[0].Relationship, Is.EqualTo("SPECIALIST"));
    }

    [Test]
    public async Task CareTeam_DeactivateMember_MarksInactiveInProviderIndex()
    {
        // Arrange
        await SetupSecurityContext();
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        await SetupPatient(patientId, "DEACT,PATIENT B");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.AddCareTeamMemberAsync(providerId, "DEACT,PROVIDER MD",
            "CONSULTING", "NEUROLOGY", "CONSULT", null);

        // Act — remove from care team via workflow
        await workflow.RemoveCareTeamMemberAsync(providerId);

        // Assert — provider index entry should be deactivated, not deleted
        List<ProviderPatientEntry> active = await GetProviderIndex(providerId).GetActivePatientsAsync();
        Assert.That(active, Has.Count.EqualTo(0));

        List<ProviderPatientEntry> all = await GetProviderIndex(providerId).GetAllPatientsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IsActive, Is.False);
    }

    [Test]
    public async Task CareTeam_SetPcp_ReflectsInProviderIndex()
    {
        // Arrange
        await SetupSecurityContext();
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        string providerId = $"PROV-PSEL-{Guid.NewGuid()}";
        await SetupPatient(patientId, "PCP,PATIENT C", "999112233");

        // Act — set PCP via workflow
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        await workflow.SetPcpAsync(providerId, "PCP,DOCTOR MD", "FAMILY MEDICINE");

        // Assert — provider index should have this patient with role "PCP"
        List<ProviderPatientEntry> patients = await GetProviderIndex(providerId).GetActivePatientsAsync();
        Assert.That(patients, Has.Count.EqualTo(1));
        Assert.That(patients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(patients[0].Relationship, Is.EqualTo("PCP"));
        Assert.That(patients[0].PatientName, Is.EqualTo("PCP,PATIENT C"));
    }

    [Test]
    public async Task CareTeam_MultipleRoles_AllAppearInProviderIndex()
    {
        // Arrange
        await SetupSecurityContext();
        string patientId = $"PAT-PSEL-{Guid.NewGuid()}";
        string pcpProviderId = $"PROV-PSEL-PCP-{Guid.NewGuid()}";
        string nurseProviderId = $"PROV-PSEL-RN-{Guid.NewGuid()}";
        await SetupPatient(patientId, "MULTI,PATIENT D", "999445566");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Act — add PCP and nurse
        await workflow.SetPcpAsync(pcpProviderId, "PCP,MULTI MD", "INTERNAL MEDICINE");
        await workflow.AddCareTeamMemberAsync(nurseProviderId, "NURSE,MULTI RN",
            "NURSE", null, "MANUAL", null);

        // Assert — PCP provider sees patient
        List<ProviderPatientEntry> pcpPatients = await GetProviderIndex(pcpProviderId).GetActivePatientsAsync();
        Assert.That(pcpPatients, Has.Count.EqualTo(1));
        Assert.That(pcpPatients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(pcpPatients[0].Relationship, Is.EqualTo("PCP"));

        // Assert — nurse provider sees same patient
        List<ProviderPatientEntry> nursePatients = await GetProviderIndex(nurseProviderId).GetActivePatientsAsync();
        Assert.That(nursePatients, Has.Count.EqualTo(1));
        Assert.That(nursePatients[0].PatientId, Is.EqualTo(patientId));
        Assert.That(nursePatients[0].Relationship, Is.EqualTo("NURSE"));
    }
}
