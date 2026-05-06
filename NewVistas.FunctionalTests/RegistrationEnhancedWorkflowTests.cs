// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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
    private IBedGrain NewBed(string facilityId, string bedId)
        => _cluster.GrainFactory.GetGrain<IBedGrain>($"BED:{facilityId}:{bedId}");
    private IBedBoardGrain BedBoard(string facilityId)
        => _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:{facilityId}");
    private IPatientWorkflowGrain WF(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

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
    public async Task BedBoard_AddAvailableBed_AppearsInAvailable()
    {
        string facility = $"FAC-{Guid.NewGuid():N}";
        IBedBoardGrain board = BedBoard(facility);
        await board.AddOrUpdateBedAsync(new BedSummaryEntry
        {
            BedId = "MED-101-A", WardId = "WARD-MED", WardName = "Med-Surg",
            RoomNumber = "101", BedPosition = "A", Status = "AVAILABLE", BedType = "REGULAR",
        });
        List<BedSummaryEntry> available = await board.GetAvailableBedsAsync();
        Assert.That(available.Any(b => b.BedId == "MED-101-A"), Is.True);
    }

    [Test]
    public async Task BedBoard_OccupiedBed_NotInAvailable()
    {
        string facility = $"FAC-{Guid.NewGuid():N}";
        IBedBoardGrain board = BedBoard(facility);
        await board.AddOrUpdateBedAsync(new BedSummaryEntry
        {
            BedId = "MED-102-A", WardId = "WARD-MED", WardName = "Med-Surg",
            Status = "OCCUPIED", BedType = "REGULAR", PatientName = "SMITH,JOHN",
        });
        List<BedSummaryEntry> available = await board.GetAvailableBedsAsync();
        Assert.That(available.Any(b => b.BedId == "MED-102-A"), Is.False);
    }

    [Test]
    public async Task BedBoard_Counts_ReturnCorrectNumbers()
    {
        string facility = $"FAC-{Guid.NewGuid():N}";
        IBedBoardGrain board = BedBoard(facility);
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = $"B1-{facility[..8]}", Status = "AVAILABLE", BedType = "REGULAR" });
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = $"B2-{facility[..8]}", Status = "AVAILABLE", BedType = "ICU" });
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = $"B3-{facility[..8]}", Status = "OCCUPIED", BedType = "REGULAR" });
        int total = await board.GetTotalBedCountAsync();
        int avail = await board.GetAvailableBedCountAsync();
        int occ = await board.GetOccupiedBedCountAsync();
        Assert.That(total, Is.GreaterThanOrEqualTo(3));
        Assert.That(avail, Is.GreaterThanOrEqualTo(2));
        Assert.That(occ, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task BedBoard_FilterByType_ReturnsTyped()
    {
        string facility = $"FAC-{Guid.NewGuid():N}";
        IBedBoardGrain board = BedBoard(facility);
        string icuBedId = $"ICU-{facility[..8]}";
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = icuBedId, Status = "AVAILABLE", BedType = "ICU" });
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = $"REG-{facility[..8]}", Status = "AVAILABLE", BedType = "REGULAR" });
        List<BedSummaryEntry> icu = await board.GetAvailableBedsByTypeAsync("ICU");
        Assert.That(icu.Any(b => b.BedId == icuBedId), Is.True);
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
        string fac = $"FAC-WF-{Guid.NewGuid():N}";
        IBedBoardGrain board = _cluster.GrainFactory.GetGrain<IBedBoardGrain>($"BED-BOARD:{fac}");
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = "WF-BED-1", Status = "AVAILABLE", BedType = "REGULAR", WardId = "W1", WardName = "Ward 1" });
        await board.AddOrUpdateBedAsync(new BedSummaryEntry { BedId = "WF-BED-2", Status = "OCCUPIED", BedType = "REGULAR" });
        List<BedSummaryEntry> avail = await WF(pid).FindAvailableBedsAsync(fac, null, null);
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
