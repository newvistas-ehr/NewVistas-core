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
/// Functional tests for Registration enhancements — Bed Availability, Advance Directives,
/// Identity Verification, Insurance at Registration (VistA DG).
/// </summary>
[TestFixture]
public class RegistrationEnhancedWorkflowTests
{
    private TestCluster _cluster = null!;
    [OneTimeSetUp] public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IAdvanceDirectiveGrain NewAdvDir(string pid)
        => _cluster.GrainFactory.GetGrain<IAdvanceDirectiveGrain>($"ADV-DIR:{pid}");
    private IIdentityVerificationGrain NewIdentity(string pid)
        => _cluster.GrainFactory.GetGrain<IIdentityVerificationGrain>($"IDENTITY:{pid}");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    private IInpatientUnitGrain Unit(string institutionId, string unitId)
        => _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");

    private IBedCapacityGrain Capacity(string institutionId)
        => _cluster.GrainFactory.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{institutionId}");

    /// <summary>Fresh, isolated unit in its own institution (no rooms).</summary>
    private async Task<(string Inst, string UnitId, IInpatientUnitGrain Grain)> NewUnitAsync(
        string name = "Med-Surg")
    {
        string inst = $"INST-{Guid.NewGuid():N}";
        string unitId = $"U-{Guid.NewGuid():N}";
        IInpatientUnitGrain unit = Unit(inst, unitId);
        await unit.ConfigureUnitAsync(name, "MedSurg", null);
        return (inst, unitId, unit);
    }

    private static UnitAdmissionRequest Admission(string patientId, string patientName, string bedId) => new()
    {
        PatientId = patientId,
        PatientName = patientName,
        MovementId = $"ADT-{Guid.NewGuid()}",
        BedId = bedId,
        AdmitDate = DateTime.UtcNow
    };

    // ─── Advance Directives ──────────────────────────────────────────────────

    [Test]
    public async Task AdvDir_UpdateCodeStatus_SetsDNR()
    {
        IAdvanceDirectiveGrain ad = NewAdvDir($"PAT-AD-{Guid.NewGuid()}");
        await ad.UpdateCodeStatusAsync(CodeStatus.DNR, "USR-1");
        AdvanceDirectiveState state = await ad.GetAsync();
        Assert.That(state.CodeStatus, Is.EqualTo(CodeStatus.DNR));
        Assert.That(state.CodeStatusDate, Is.Not.Null);
    }

    [Test]
    public async Task AdvDir_SetProxy_StoresProxyInfo()
    {
        IAdvanceDirectiveGrain ad = NewAdvDir($"PAT-AD2-{Guid.NewGuid()}");
        await ad.SetHealthcareProxyAsync("Jane Doe", "555-1234", "Spouse");
        AdvanceDirectiveState state = await ad.GetAsync();
        Assert.That(state.HealthcareProxyName, Is.EqualTo("Jane Doe"));
        Assert.That(state.HealthcareProxyRelationship, Is.EqualTo("Spouse"));
    }

    [Test]
    public async Task AdvDir_AddDocument_AppearsInList()
    {
        IAdvanceDirectiveGrain ad = NewAdvDir($"PAT-AD3-{Guid.NewGuid()}");
        await ad.AddDocumentAsync(AdvanceDirectiveType.LivingWill, DateTime.UtcNow, "Patient provided", null, null);
        await ad.AddDocumentAsync(AdvanceDirectiveType.HealthcarePowerOfAttorney, DateTime.UtcNow, "Attorney", null, null);
        AdvanceDirectiveState state = await ad.GetAsync();
        Assert.That(state.Documents, Has.Count.EqualTo(2));
        Assert.That(state.HasAdvanceDirectives, Is.True);
    }

    [Test]
    public async Task AdvDir_RemoveDocument_UpdatesFlag()
    {
        IAdvanceDirectiveGrain ad = NewAdvDir($"PAT-AD4-{Guid.NewGuid()}");
        await ad.AddDocumentAsync(AdvanceDirectiveType.DoNotResuscitate, DateTime.UtcNow, "Physician", null, null);
        AdvanceDirectiveState state = await ad.GetAsync();
        string docId = state.Documents[0].DocumentId;
        await ad.RemoveDocumentAsync(docId);
        state = await ad.GetAsync();
        Assert.That(state.Documents, Has.Count.EqualTo(0));
        Assert.That(state.HasAdvanceDirectives, Is.False);
    }

    // ─── Identity Verification ───────────────────────────────────────────────

    [Test]
    public async Task Identity_RecordVerification_SetsVerified()
    {
        IIdentityVerificationGrain id = NewIdentity($"PAT-ID-{Guid.NewGuid()}");
        await id.RecordVerificationAsync(IdentityDocumentType.VaIdCard, "VA-12345", "VA", null,
            IdentityVerificationResult.Verified, true, "/photos/pat123.jpg", null, "USR-1", "Clerk", null);
        IdentityVerificationState state = await id.GetAsync();
        Assert.That(state.IsVerified, Is.True);
        Assert.That(state.HasPhotoOnFile, Is.True);
        Assert.That(state.VerificationHistory, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Identity_VerifiedWithDiscrepancy_StillVerified()
    {
        IIdentityVerificationGrain id = NewIdentity($"PAT-ID2-{Guid.NewGuid()}");
        await id.RecordVerificationAsync(IdentityDocumentType.DriversLicense, "DL-999", "State", null,
            IdentityVerificationResult.VerifiedWithDiscrepancy, false, null, "Name spelling differs", "USR-1", "Clerk", null);
        IdentityVerificationState state = await id.GetAsync();
        Assert.That(state.IsVerified, Is.True);
        Assert.That(state.CurrentVerificationResult, Is.EqualTo(IdentityVerificationResult.VerifiedWithDiscrepancy));
    }

    [Test]
    public async Task Identity_Failed_SetsNotVerified()
    {
        IIdentityVerificationGrain id = NewIdentity($"PAT-ID3-{Guid.NewGuid()}");
        await id.RecordVerificationAsync(IdentityDocumentType.Other, null, null, null,
            IdentityVerificationResult.Failed, false, null, "No valid ID presented", "USR-1", "Clerk", null);
        IdentityVerificationState state = await id.GetAsync();
        Assert.That(state.IsVerified, Is.False);
    }

    [Test]
    public async Task Identity_UpdatePhoto_StoresReference()
    {
        IIdentityVerificationGrain id = NewIdentity($"PAT-ID4-{Guid.NewGuid()}");
        await id.UpdatePhotoAsync("/photos/patient-new.jpg");
        IdentityVerificationState state = await id.GetAsync();
        Assert.That(state.HasPhotoOnFile, Is.True);
        Assert.That(state.PhotoReference, Is.EqualTo("/photos/patient-new.jpg"));
    }

    // ─── Bed Availability ────────────────────────────────────────────────────

    [Test]
    public async Task BedAvailability_AddedBed_AppearsInAvailable()
    {
        var (inst, _, unit) = await NewUnitAsync();
        await unit.AddBedAsync("MED-101-A", null, BedType.Regular);

        string pid = $"PAT-REG-BA-{Guid.NewGuid()}";
        List<InpatientBed> available = await WF(pid).FindAvailableBedsAsync(inst, null, null);
        Assert.That(available.Any(b => b.BedId == "MED-101-A"), Is.True);
    }

    [Test]
    public async Task BedAvailability_OccupiedBed_NotInAvailable()
    {
        var (inst, _, unit) = await NewUnitAsync();
        await unit.AddBedAsync("MED-102-A", null, BedType.Regular);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "SMITH,JOHN", "MED-102-A"));

        string pid = $"PAT-REG-BA2-{Guid.NewGuid()}";
        List<InpatientBed> available = await WF(pid).FindAvailableBedsAsync(inst, null, null);
        Assert.That(available.Any(b => b.BedId == "MED-102-A"), Is.False);
    }

    [Test]
    public async Task BedAvailability_Counts_ReturnCorrectNumbers()
    {
        var (inst, _, unit) = await NewUnitAsync();
        await unit.AddBedAsync("B1", null, BedType.Regular);
        await unit.AddBedAsync("B2", null, BedType.Icu);
        await unit.AddBedAsync("B3", null, BedType.Regular);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "COUNT,PATIENT", "B3"));

        (int total, int avail, int occ) = await WF($"PAT-REG-BA3-{Guid.NewGuid()}").GetBedCountsAsync(inst);
        Assert.That(total, Is.EqualTo(3));
        Assert.That(avail, Is.EqualTo(2));
        Assert.That(occ, Is.EqualTo(1));
    }

    [Test]
    public async Task BedAvailability_FilterByType_ReturnsTyped()
    {
        var (inst, _, unit) = await NewUnitAsync();
        await unit.AddBedAsync("ICU-1", null, BedType.Icu);
        await unit.AddBedAsync("REG-1", null, BedType.Regular);

        List<InpatientBed> icu = await WF($"PAT-REG-BA4-{Guid.NewGuid()}")
            .FindAvailableBedsAsync(inst, null, BedType.Icu);
        Assert.That(icu.Any(b => b.BedId == "ICU-1"), Is.True);
        Assert.That(icu.Any(b => b.BedId == "REG-1"), Is.False);
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_GetAdvanceDirectives_ReturnsState()
    {
        string pid = $"PAT-REG-WF-{Guid.NewGuid()}";
        await WF(pid).UpdateCodeStatusAsync(CodeStatus.DNR_DNI, "USR-WF");
        AdvanceDirectiveState ad = await WF(pid).GetAdvanceDirectivesAsync();
        Assert.That(ad.CodeStatus, Is.EqualTo(CodeStatus.DNR_DNI));
    }

    [Test]
    public async Task Workflow_AddAdvanceDirectiveDocument_StoresOnFile()
    {
        string pid = $"PAT-REG-WF2-{Guid.NewGuid()}";
        await WF(pid).AddAdvanceDirectiveDocumentAsync(AdvanceDirectiveType.PolstMolst, DateTime.UtcNow, "ED physician", null, "POLST signed in ED");
        AdvanceDirectiveState ad = await WF(pid).GetAdvanceDirectivesAsync();
        Assert.That(ad.HasAdvanceDirectives, Is.True);
        Assert.That(ad.Documents[0].DirectiveType, Is.EqualTo(AdvanceDirectiveType.PolstMolst));
    }

    [Test]
    public async Task Workflow_RecordIdentityVerification_ReturnsId()
    {
        string pid = $"PAT-REG-WF3-{Guid.NewGuid()}";
        string vId = await WF(pid).RecordIdentityVerificationAsync(
            IdentityDocumentType.MilitaryId, "MIL-555", "DoD", DateTime.UtcNow.AddYears(5),
            IdentityVerificationResult.Verified, true, "/photos/mil.jpg", null, "USR-WF", "Clerk", null);
        Assert.That(vId, Is.Not.Null.And.Not.Empty);
        IdentityVerificationState id = await WF(pid).GetIdentityVerificationAsync();
        Assert.That(id.IsVerified, Is.True);
        Assert.That(id.HasPhotoOnFile, Is.True);
    }

    [Test]
    public async Task Workflow_GetInsuranceAtRegistration_ReturnsPolicies()
    {
        string pid = $"PAT-REG-WF4-{Guid.NewGuid()}";
        // Add a policy first
        await WF(pid).AddPersonalPolicyAsync(null, "Medicare Part A/B", "MBI-12345", null, "SELF", null, null, null, true, null, null, null);
        List<PersonalPolicyIndexEntry> policies = await WF(pid).GetInsuranceAtRegistrationAsync();
        Assert.That(policies, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task Workflow_FindAvailableBeds_ReturnsAvailable()
    {
        string pid = $"PAT-REG-WF5-{Guid.NewGuid()}";
        var (inst, _, unit) = await NewUnitAsync("Ward 1");
        await unit.AddBedAsync("WF-BED-1", null, BedType.Regular);
        await unit.AddBedAsync("WF-BED-2", null, BedType.Regular);
        await unit.AdmitPatientAsync(Admission($"PAT-{Guid.NewGuid()}", "OCCUPYING,PATIENT", "WF-BED-2"));

        List<InpatientBed> avail = await WF(pid).FindAvailableBedsAsync(inst, null, null);
        Assert.That(avail.Any(b => b.BedId == "WF-BED-1"), Is.True);
        Assert.That(avail.Any(b => b.BedId == "WF-BED-2"), Is.False);
    }

    [Test]
    public async Task Workflow_SetHealthcareProxy_Persists()
    {
        string pid = $"PAT-REG-WF6-{Guid.NewGuid()}";
        await WF(pid).SetHealthcareProxyAsync("Robert Smith", "555-9876", "Son");
        AdvanceDirectiveState ad = await WF(pid).GetAdvanceDirectivesAsync();
        Assert.That(ad.HealthcareProxyName, Is.EqualTo("Robert Smith"));
    }
}
