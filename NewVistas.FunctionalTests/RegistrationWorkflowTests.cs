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
/// Functional tests for Registration Expansion — VistA Files #27.11, #26.11, #29.11, #408.12, #408.13, #391.91.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class RegistrationWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Enrollment Tests ─────────────────────────────────────────────────────

    [Test]
    public async Task SetEnrollmentStatus_UpdatesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", "Initial enrollment verified");

        PatientEnrollmentState state = await wf.GetEnrollmentAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
        Assert.That(state.LastStatusChangedByUserId, Is.EqualTo("CLERK-001"));
        Assert.That(state.Notes, Does.Contain("verified"));
    }

    [Test]
    public async Task SetEnrollmentPriorityGroup_SetsPriorityAndCopay()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentPriorityGroupAsync(
            "1", "a", meansTestRequired: false, copayExempt: true, copayExemptionReason: "SC 50%+");

        PatientEnrollmentState state = await wf.GetEnrollmentAsync();
        Assert.That(state.PriorityGroup, Is.EqualTo("1"));
        Assert.That(state.PrioritySubgroup, Is.EqualTo("a"));
        Assert.That(state.CopayExempt, Is.True);
        Assert.That(state.CopayExemptionReason, Is.EqualTo("SC 50%+"));
        Assert.That(state.MeansTestRequired, Is.False);
    }

    [Test]
    public async Task SetEnrollmentStatus_ThenPriority_BothPersist()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-002", null);
        await wf.SetEnrollmentPriorityGroupAsync("5", null, true, false, null);

        PatientEnrollmentState state = await wf.GetEnrollmentAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
        Assert.That(state.PriorityGroup, Is.EqualTo("5"));
        Assert.That(state.MeansTestRequired, Is.True);
    }

    // ── PRF Flag Tests ───────────────────────────────────────────────────────

    [Test]
    public async Task AssignPrfFlag_CreatesActiveFlag()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AssignPrfFlagAsync(
            "FLAG-001", "BEHAVIORAL", "NATIONAL", true,
            "ADMIN-001", "Dr. Safety",
            "Patient has history of violent behavior");

        PrfAssignmentState state = await wf.GetPrfFlagsAsync();
        Assert.That(state.Assignments, Has.Count.EqualTo(1));
        Assert.That(state.Assignments[0].FlagName, Is.EqualTo("BEHAVIORAL"));
        Assert.That(state.Assignments[0].IsNational, Is.True);
        Assert.That(state.Assignments[0].IsActive, Is.True);
        Assert.That(state.Assignments[0].Narrative, Does.Contain("violent behavior"));
    }

    [Test]
    public async Task DeactivatePrfFlag_SetsInactive()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AssignPrfFlagAsync(
            "FLAG-002", "HIGH RISK FOR SUICIDE", "LOCAL", false,
            "ADMIN-002", "Dr. Mental Health",
            "Patient screened positive on C-SSRS");

        await wf.DeactivatePrfFlagAsync("FLAG-002", "Risk reassessed to low", "ADMIN-003");

        PrfAssignmentState state = await wf.GetPrfFlagsAsync();
        PrfFlagAssignment flag = state.Assignments.First(a => a.FlagId == "FLAG-002");
        Assert.That(flag.IsActive, Is.False);
        Assert.That(flag.DeactivatedReason, Does.Contain("reassessed"));
        Assert.That(flag.DeactivatedDate, Is.Not.Null);
    }

    [Test]
    public async Task AssignMultipleFlags_AllAppear()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AssignPrfFlagAsync("FLAG-A", "BEHAVIORAL", "NATIONAL", true, "ADM-1", "Dr. A", null);
        await wf.AssignPrfFlagAsync("FLAG-B", "MISSING PATIENT", "LOCAL", false, "ADM-2", "Dr. B", null);

        PrfAssignmentState state = await wf.GetPrfFlagsAsync();
        Assert.That(state.Assignments, Has.Count.EqualTo(2));
    }

    // ── MST Screening Tests ──────────────────────────────────────────────────

    [Test]
    public async Task RecordMstScreening_CreatesScreeningEntry()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordMstScreeningAsync(
            DateTime.UtcNow, MstStatus.Verified,
            "SCREEN-001", "Dr. Screener",
            "Primary Care Clinic", "Patient disclosed MST during routine visit");

        MstHistoryState state = await wf.GetMstHistoryAsync();
        Assert.That(state.Screenings, Has.Count.EqualTo(1));
        Assert.That(state.CurrentStatus, Is.EqualTo(MstStatus.Verified));
        Assert.That(state.MstPositive, Is.True);
        Assert.That(state.Screenings[0].ScreenedByUserName, Is.EqualTo("Dr. Screener"));
    }

    [Test]
    public async Task RecordMultipleMstScreenings_TracksHistory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.RecordMstScreeningAsync(
            DateTime.UtcNow.AddYears(-1), MstStatus.Denied,
            "SCREEN-001", "Dr. A", "Clinic A", null);

        await wf.RecordMstScreeningAsync(
            DateTime.UtcNow, MstStatus.Verified,
            "SCREEN-002", "Dr. B", "Clinic B", "Patient disclosed after trust built");

        MstHistoryState state = await wf.GetMstHistoryAsync();
        Assert.That(state.Screenings, Has.Count.EqualTo(2));
        Assert.That(state.CurrentStatus, Is.EqualTo(MstStatus.Verified));
        Assert.That(state.MstPositive, Is.True);
    }

    // ── Patient Relations Tests ──────────────────────────────────────────────

    [Test]
    public async Task AddPatientRelation_CreatesRelationRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string relationId = await wf.AddOrUpdatePatientRelationAsync(new PatientRelation
        {
            RelationshipType = RelationshipType.Spouse,
            Name = "Mary Doe",
            Phone = "555-1234",
            Address = "123 Main St",
            IsPrimaryNextOfKin = true,
            IsEmergencyContact = true
        });

        Assert.That(relationId, Is.Not.Empty);

        PatientRelationState state = await wf.GetPatientRelationsAsync();
        Assert.That(state.Relations, Has.Count.EqualTo(1));
        Assert.That(state.Relations[0].Name, Is.EqualTo("Mary Doe"));
        Assert.That(state.Relations[0].RelationshipType, Is.EqualTo(RelationshipType.Spouse));
        Assert.That(state.Relations[0].IsPrimaryNextOfKin, Is.True);
    }

    [Test]
    public async Task RemovePatientRelation_DeletesFromList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string relationId = await wf.AddOrUpdatePatientRelationAsync(new PatientRelation
        {
            RelationshipType = RelationshipType.Child,
            Name = "Tom Doe Jr",
            Phone = "555-5678",
            IsEmergencyContact = true
        });

        await wf.RemovePatientRelationAsync(relationId);

        PatientRelationState state = await wf.GetPatientRelationsAsync();
        Assert.That(state.Relations, Has.Count.EqualTo(0));
    }

    // ── Income / Household Tests ─────────────────────────────────────────────

    [Test]
    public async Task AddIncomeHouseholdMember_CreatesRecord()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string personId = await wf.AddOrUpdateIncomePersonAsync(new IncomePerson
        {
            RelationshipType = "SELF",
            Name = "John Doe",
            GrossAnnualIncome = 45000m,
            NetWorth = 120000m,
            IncomeYear = 2024,
            IsVeteranSelf = true
        });

        Assert.That(personId, Is.Not.Empty);

        IncomeHouseholdState state = await wf.GetIncomeHouseholdAsync();
        Assert.That(state.HouseholdMembers, Has.Count.EqualTo(1));
        Assert.That(state.HouseholdMembers[0].GrossAnnualIncome, Is.EqualTo(45000m));
        Assert.That(state.HouseholdMembers[0].IsVeteranSelf, Is.True);
    }

    [Test]
    public async Task RecordMeansTestDecision_SetsDecisionFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddOrUpdateIncomePersonAsync(new IncomePerson
        {
            RelationshipType = "SELF", Name = "Jane Smith",
            GrossAnnualIncome = 35000m, IncomeYear = 2024, IsVeteranSelf = true
        });

        await wf.RecordMeansTestDecisionAsync("COPAY REQUIRED", DateTime.UtcNow, 44000m);

        IncomeHouseholdState state = await wf.GetIncomeHouseholdAsync();
        Assert.That(state.MeansTestDecision, Is.EqualTo("COPAY REQUIRED"));
        Assert.That(state.ThresholdApplied, Is.EqualTo(44000m));
    }

    // ── Treating Facilities Tests ────────────────────────────────────────────

    [Test]
    public async Task AddTreatingFacility_CreatesEntry()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-001",
            FacilityName = "VA Medical Center Richmond",
            FacilityType = "VAMC",
            LastActivityDate = DateTime.UtcNow,
            IsActive = true,
            RelationshipType = "OUTPATIENT"
        });

        TreatingFacilityListState state = await wf.GetTreatingFacilitiesAsync();
        Assert.That(state.Facilities, Has.Count.EqualTo(1));
        Assert.That(state.Facilities[0].FacilityName, Is.EqualTo("VA Medical Center Richmond"));
    }

    [Test]
    public async Task SetPrimaryTreatingFacility_UpdatesPrimary()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-100", FacilityName = "VA DC", IsActive = true
        });
        await wf.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-200", FacilityName = "VA Richmond", IsActive = true
        });

        await wf.SetPrimaryTreatingFacilityAsync("FAC-200", "VA Richmond");

        TreatingFacilityListState state = await wf.GetTreatingFacilitiesAsync();
        Assert.That(state.PrimaryFacilityId, Is.EqualTo("FAC-200"));
        Assert.That(state.PrimaryFacilityName, Is.EqualTo("VA Richmond"));
    }

    [Test]
    public async Task MultipleFacilities_IndependentPatients()
    {
        string p1 = $"PATIENT-{Guid.NewGuid():N}";
        string p2 = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-A", FacilityName = "VA Boston", IsActive = true
        });
        await wf1.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-B", FacilityName = "VA Providence", IsActive = true
        });

        await wf2.AddOrUpdateTreatingFacilityAsync(new TreatingFacilityEntry
        {
            FacilityId = "FAC-C", FacilityName = "VA New York", IsActive = true
        });

        TreatingFacilityListState state1 = await wf1.GetTreatingFacilitiesAsync();
        TreatingFacilityListState state2 = await wf2.GetTreatingFacilitiesAsync();

        Assert.That(state1.Facilities, Has.Count.EqualTo(2));
        Assert.That(state2.Facilities, Has.Count.EqualTo(1));
    }

    // ── Demographics Tests ──────────────────────────────────────────────────

    [Test]
    public async Task UpdateContactInfo_PersistsPhoneAndEmail()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDemographicsAsync("DOE,JOHN", "M", new DateTime(1970, 5, 15), "000-00-0001");
        await wf.UpdateContactInfoAsync("555-0100", "555-0200", "test@va.gov");

        PatientState state = await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(state.PhoneNumberResidence, Is.EqualTo("555-0100"));
        Assert.That(state.PhoneNumberWork, Is.EqualTo("555-0200"));
        Assert.That(state.Email, Is.EqualTo("test@va.gov"));
    }

    [Test]
    public async Task UpdateEmergencyContact_PersistsNameAndPhone()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDemographicsAsync("DOE,JANE", "F", new DateTime(1985, 3, 20), "000-00-0002");
        await wf.UpdateEmergencyContactAsync("Jane Doe", "Spouse", "555-9999");

        PatientState state = await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(state.EmergencyContactName, Is.EqualTo("Jane Doe"));
        Assert.That(state.EmergencyContactRelationship, Is.EqualTo("Spouse"));
        Assert.That(state.EmergencyContactPhone, Is.EqualTo("555-9999"));
    }

    [Test]
    public async Task UpdateMaritalStatus_PersistsStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDemographicsAsync("SMITH,BOB", "M", new DateTime(1960, 11, 1), "000-00-0003");
        await wf.UpdateMaritalStatusAsync("MARRIED");

        PatientState state = await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(state.MaritalStatus, Is.EqualTo("MARRIED"));
    }

    [Test]
    public async Task UpdateMilitaryService_PersistsServiceDates()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDemographicsAsync("JONES,WILLIAM", "M", new DateTime(1968, 7, 4), "000-00-0004");
        await wf.UpdateMilitaryServiceAsync(
            new DateTime(1990, 6, 1), new DateTime(2010, 9, 30), "ARMY", "HONORABLE", null);

        PatientState state = await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(state.ServiceEntryDate, Is.EqualTo(new DateTime(1990, 6, 1)));
        Assert.That(state.ServiceSeparationDate, Is.EqualTo(new DateTime(2010, 9, 30)));
        Assert.That(state.ServiceBranch, Is.EqualTo("ARMY"));
        Assert.That(state.ServiceDischargeType, Is.EqualTo("HONORABLE"));
    }

    [Test]
    public async Task SetIcn_PersistsIntegrationControlNumber()
    {
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.UpdateDemographicsAsync("BROWN,MARY", "F", new DateTime(1975, 12, 25), "000-00-0005");
        await wf.SetIcnAsync("1234567890V123456");

        PatientState state = await _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId).GetPatientAsync();
        Assert.That(state.Icn, Is.EqualTo("1234567890V123456"));
    }
}
