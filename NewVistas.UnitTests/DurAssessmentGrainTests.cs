// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for DUR Assessment and Index grains.
/// Tests grain-level behavior directly without the workflow grain.
/// </summary>
[TestFixture]
public class DurAssessmentGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Assessment Grain Tests ─────────────────────────────────────────────

    [Test]
    public async Task AssessmentGrain_Create_AllPassChecks_StatusPassed()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "No duplicate." },
            new DurCheckResult { CheckType = DurCheckType.DrugAllergyContraindication, Outcome = DurOutcome.Pass, Severity = "None", Message = "No allergy." },
            new DurCheckResult { CheckType = DurCheckType.DaysSupplyExceeded, Outcome = DurOutcome.Pass, Severity = "None", Message = "Within limit." },
        };

        await grain.CreateAsync("RX-001", "PATIENT-001", "METFORMIN 500MG", "12345", "HS502",
            "500mg", "ORAL", "BID", 30, 60, "PHARM-001", checks);

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.AssessmentId, Is.EqualTo(id));
        Assert.That(state.PrescriptionId, Is.EqualTo("RX-001"));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.DrugName, Is.EqualTo("METFORMIN 500MG"));
        Assert.That(state.DrugId, Is.EqualTo("12345"));
        Assert.That(state.DrugClass, Is.EqualTo("HS502"));
        Assert.That(state.Dosage, Is.EqualTo("500mg"));
        Assert.That(state.Route, Is.EqualTo("ORAL"));
        Assert.That(state.Schedule, Is.EqualTo("BID"));
        Assert.That(state.DaysSupply, Is.EqualTo(30));
        Assert.That(state.Quantity, Is.EqualTo(60));
        Assert.That(state.PerformedBy, Is.EqualTo("PHARM-001"));
        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Passed));
        Assert.That(state.OverallOutcome, Is.EqualTo(DurOutcome.Pass));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
        Assert.That(state.WarningCheckCount, Is.EqualTo(0));
        Assert.That(state.Checks, Has.Count.EqualTo(3));
        Assert.That(state.PerformedDate, Is.Not.Null);
    }

    [Test]
    public async Task AssessmentGrain_Create_WithFailedChecks_StatusFailed()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Fail, Severity = "Significant", Message = "Duplicate found." },
            new DurCheckResult { CheckType = DurCheckType.DrugAllergyContraindication, Outcome = DurOutcome.Pass, Severity = "None", Message = "No allergy." },
            new DurCheckResult { CheckType = DurCheckType.DaysSupplyExceeded, Outcome = DurOutcome.Warning, Severity = "Moderate", Message = "Close to limit." },
        };

        await grain.CreateAsync("RX-002", "PATIENT-002", "LISINOPRIL 10MG", null, null,
            "10mg", "ORAL", "QD", 90, 90, "PHARM-002", checks);

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Failed));
        Assert.That(state.OverallOutcome, Is.EqualTo(DurOutcome.Fail));
        Assert.That(state.FailedCheckCount, Is.EqualTo(1));
        Assert.That(state.WarningCheckCount, Is.EqualTo(1));
    }

    [Test]
    public async Task AssessmentGrain_Create_WarningsOnly_StatusPending()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.AgeBasedDosing, Outcome = DurOutcome.Warning, Severity = "Minor", Message = "Geriatric patient." },
            new DurCheckResult { CheckType = DurCheckType.ControlledSubstance, Outcome = DurOutcome.Warning, Severity = "Significant", Message = "Schedule IV." },
        };

        await grain.CreateAsync("RX-003", "PATIENT-003", "LORAZEPAM 1MG", null, null,
            "1mg", "ORAL", "PRN", 30, 30, "PHARM-003", checks);

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Pending));
        Assert.That(state.OverallOutcome, Is.EqualTo(DurOutcome.Warning));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
        Assert.That(state.WarningCheckCount, Is.EqualTo(2));
    }

    [Test]
    public async Task AssessmentGrain_OverrideCheck_ChangesOutcomeToOverride()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Fail, Severity = "Significant", Message = "Duplicate." },
            new DurCheckResult { CheckType = DurCheckType.DrugAllergyContraindication, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK." },
        };

        await grain.CreateAsync("RX-004", "PATIENT-004", "ATORVASTATIN 40MG", null, null,
            "40mg", "ORAL", "QD", 90, 90, "PHARM-001", checks);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(DurAssessmentStatus.Failed));

        await grain.OverrideCheckAsync(DurCheckType.DuplicateDrug, "PHARM-SENIOR", "Different formulation needed for this patient.");

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.OverriddenByPharmacist));
        Assert.That(state.OverallOutcome, Is.EqualTo(DurOutcome.Override));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
        Assert.That(state.OverriddenCheckCount, Is.EqualTo(1));

        DurCheckResult overriddenCheck = state.Checks.First(c => c.CheckType == DurCheckType.DuplicateDrug);
        Assert.That(overriddenCheck.Outcome, Is.EqualTo(DurOutcome.Override));
        Assert.That(overriddenCheck.OverriddenBy, Is.EqualTo("PHARM-SENIOR"));
        Assert.That(overriddenCheck.OverrideReason, Does.Contain("Different formulation"));
        Assert.That(overriddenCheck.OverrideDate, Is.Not.Null);
    }

    [Test]
    public async Task AssessmentGrain_OverrideNonFailedCheck_DoesNothing()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK." },
        };

        await grain.CreateAsync("RX-005", "PATIENT-005", "ASPIRIN 81MG", null, null,
            "81mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        await grain.OverrideCheckAsync(DurCheckType.DuplicateDrug, "PHARM-SENIOR", "Trying to override pass.");

        DurAssessmentState state = await grain.GetAsync();
        Assert.That(state.Checks.First().Outcome, Is.EqualTo(DurOutcome.Pass));
        Assert.That(state.OverriddenCheckCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AssessmentGrain_OverrideMultipleFailedChecks_PartialOverride()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Fail, Severity = "Significant", Message = "Dup." },
            new DurCheckResult { CheckType = DurCheckType.DaysSupplyExceeded, Outcome = DurOutcome.Fail, Severity = "Moderate", Message = "Exceeded." },
        };

        await grain.CreateAsync("RX-006", "PATIENT-006", "GABAPENTIN 300MG", null, null,
            "300mg", "ORAL", "TID", 120, 360, "PHARM-001", checks);

        await grain.OverrideCheckAsync(DurCheckType.DuplicateDrug, "PHARM-001", "Justified.");

        DurAssessmentState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Failed));
        Assert.That(state.FailedCheckCount, Is.EqualTo(1));
        Assert.That(state.OverriddenCheckCount, Is.EqualTo(1));

        await grain.OverrideCheckAsync(DurCheckType.DaysSupplyExceeded, "PHARM-001", "90-day mail order.");

        state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.OverriddenByPharmacist));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
        Assert.That(state.OverriddenCheckCount, Is.EqualTo(2));
    }

    [Test]
    public async Task AssessmentGrain_Acknowledge_TransitionsToAcknowledged()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK." },
        };

        await grain.CreateAsync("RX-007", "PATIENT-007", "OMEPRAZOLE 20MG", null, null,
            "20mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        await grain.AcknowledgeAsync("PHARM-002", "Reviewed and approved.");

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Acknowledged));
        Assert.That(state.AcknowledgedBy, Is.EqualTo("PHARM-002"));
        Assert.That(state.AcknowledgedDate, Is.Not.Null);
        Assert.That(state.Notes, Does.Contain("Reviewed and approved"));
    }

    [Test]
    public async Task AssessmentGrain_AddNotes_AppendsToExisting()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK." },
        };

        await grain.CreateAsync("RX-008", "PATIENT-008", "AMLODIPINE 5MG", null, null,
            "5mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        await grain.AddNotesAsync("First note.");
        await grain.AddNotesAsync("Second note.");

        DurAssessmentState state = await grain.GetAsync();
        Assert.That(state.Notes, Does.Contain("First note."));
        Assert.That(state.Notes, Does.Contain("Second note."));
    }

    // ─── Index Grain Tests ──────────────────────────────────────────────────

    [Test]
    public async Task IndexGrain_AddAndGetAll_ReturnsMostRecentFirst()
    {
        string indexKey = $"DUR-IDX:PATIENT-{Guid.NewGuid()}";
        IDurAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDurAssessmentIndexGrain>(indexKey);

        string id1 = $"DUR:{Guid.NewGuid()}";
        string id2 = $"DUR:{Guid.NewGuid()}";

        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = id1, DrugName = "Drug A", Status = DurAssessmentStatus.Passed,
            OverallOutcome = DurOutcome.Pass, PerformedDate = DateTime.UtcNow.AddMinutes(-10)
        });
        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = id2, DrugName = "Drug B", Status = DurAssessmentStatus.Failed,
            OverallOutcome = DurOutcome.Fail, FailedCheckCount = 2, PerformedDate = DateTime.UtcNow
        });

        List<DurAssessmentIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(2));
        Assert.That(all[0].AssessmentId, Is.EqualTo(id2));
        Assert.That(all[1].AssessmentId, Is.EqualTo(id1));
    }

    [Test]
    public async Task IndexGrain_GetPendingReview_FiltersCorrectly()
    {
        string indexKey = $"DUR-IDX:PATIENT-{Guid.NewGuid()}";
        IDurAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDurAssessmentIndexGrain>(indexKey);

        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Passed Drug",
            Status = DurAssessmentStatus.Passed, OverallOutcome = DurOutcome.Pass
        });
        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Failed Drug",
            Status = DurAssessmentStatus.Failed, OverallOutcome = DurOutcome.Fail, FailedCheckCount = 1
        });
        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Pending Drug",
            Status = DurAssessmentStatus.Pending, OverallOutcome = DurOutcome.Warning, WarningCheckCount = 1
        });

        List<DurAssessmentIndexEntry> pending = await index.GetPendingReviewAsync();

        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(pending.Select(e => e.DrugName), Does.Contain("Failed Drug"));
        Assert.That(pending.Select(e => e.DrugName), Does.Contain("Pending Drug"));
    }

    [Test]
    public async Task IndexGrain_GetByStatus_FiltersCorrectly()
    {
        string indexKey = $"DUR-IDX:PATIENT-{Guid.NewGuid()}";
        IDurAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDurAssessmentIndexGrain>(indexKey);

        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Passed",
            Status = DurAssessmentStatus.Passed, OverallOutcome = DurOutcome.Pass
        });
        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Also Passed",
            Status = DurAssessmentStatus.Passed, OverallOutcome = DurOutcome.Pass
        });
        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = $"DUR:{Guid.NewGuid()}", DrugName = "Failed",
            Status = DurAssessmentStatus.Failed, OverallOutcome = DurOutcome.Fail
        });

        List<DurAssessmentIndexEntry> passed = await index.GetByStatusAsync(DurAssessmentStatus.Passed);
        List<DurAssessmentIndexEntry> failed = await index.GetByStatusAsync(DurAssessmentStatus.Failed);

        Assert.That(passed, Has.Count.EqualTo(2));
        Assert.That(failed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IndexGrain_GetByPrescription_ReturnsMatch()
    {
        string indexKey = $"DUR-IDX:PATIENT-{Guid.NewGuid()}";
        IDurAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDurAssessmentIndexGrain>(indexKey);

        string rxId = $"RX-{Guid.NewGuid()}";
        string assessmentId = $"DUR:{Guid.NewGuid()}";

        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = assessmentId, PrescriptionId = rxId,
            DrugName = "METFORMIN 500MG", Status = DurAssessmentStatus.Passed
        });

        DurAssessmentIndexEntry? found = await index.GetByPrescriptionAsync(rxId);
        Assert.That(found, Is.Not.Null);
        Assert.That(found!.AssessmentId, Is.EqualTo(assessmentId));

        DurAssessmentIndexEntry? notFound = await index.GetByPrescriptionAsync("RX-NONEXISTENT");
        Assert.That(notFound, Is.Null);
    }

    [Test]
    public async Task IndexGrain_UpdateEntryStatus_UpdatesCorrectEntry()
    {
        string indexKey = $"DUR-IDX:PATIENT-{Guid.NewGuid()}";
        IDurAssessmentIndexGrain index = _cluster.GrainFactory.GetGrain<IDurAssessmentIndexGrain>(indexKey);

        string assessmentId = $"DUR:{Guid.NewGuid()}";

        await index.AddEntryAsync(new DurAssessmentIndexEntry
        {
            AssessmentId = assessmentId, DrugName = "LISINOPRIL 10MG",
            Status = DurAssessmentStatus.Failed, OverallOutcome = DurOutcome.Fail,
            FailedCheckCount = 2, WarningCheckCount = 0
        });

        await index.UpdateEntryStatusAsync(assessmentId,
            DurAssessmentStatus.OverriddenByPharmacist, DurOutcome.Override, 0, 0);

        List<DurAssessmentIndexEntry> all = await index.GetAllAsync();
        DurAssessmentIndexEntry entry = all.First(e => e.AssessmentId == assessmentId);

        Assert.That(entry.Status, Is.EqualTo(DurAssessmentStatus.OverriddenByPharmacist));
        Assert.That(entry.OverallOutcome, Is.EqualTo(DurOutcome.Override));
        Assert.That(entry.FailedCheckCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AssessmentGrain_AllCheckTypes_PersistCorrectly()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.DuplicateTherapy, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.DrugInteraction, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.DrugAllergyContraindication, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.MaxDoseExceeded, Outcome = DurOutcome.NotApplicable, Severity = "None", Message = "N/A" },
            new DurCheckResult { CheckType = DurCheckType.DaysSupplyExceeded, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.RefillTooSoon, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK" },
            new DurCheckResult { CheckType = DurCheckType.AgeBasedDosing, Outcome = DurOutcome.Warning, Severity = "Minor", Message = "Geriatric" },
            new DurCheckResult { CheckType = DurCheckType.RenalAdjustment, Outcome = DurOutcome.NotApplicable, Severity = "None", Message = "N/A" },
            new DurCheckResult { CheckType = DurCheckType.HepaticAdjustment, Outcome = DurOutcome.NotApplicable, Severity = "None", Message = "N/A" },
            new DurCheckResult { CheckType = DurCheckType.ControlledSubstance, Outcome = DurOutcome.Pass, Severity = "None", Message = "Not CS" },
        };

        await grain.CreateAsync("RX-ALL", "PATIENT-ALL", "TEST DRUG", null, null,
            "10mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Checks, Has.Count.EqualTo(11));
        Assert.That(state.WarningCheckCount, Is.EqualTo(1));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Pending));
    }

    // ─── Unavailable Outcome (fail-closed reference data) ───────────────────

    [Test]
    public async Task AssessmentGrain_UnavailableCheck_StatusFailed()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        // A DrugInteraction check could not run because the interaction dataset
        // is not loaded — the assessment must block (not silently pass).
        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Pass, Severity = "None", Message = "OK." },
            new DurCheckResult { CheckType = DurCheckType.DrugInteraction, Outcome = DurOutcome.Unavailable, Severity = "Significant", Message = "Dataset not loaded." },
        };

        await grain.CreateAsync("RX-UNAVAIL", "PATIENT-UNAVAIL", "WARFARIN 5MG", null, null,
            "5mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        DurAssessmentState state = await grain.GetAsync();

        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Failed));
        // With no Fail checks present, the overall outcome is Unavailable
        // (distinct from Fail) so the UI can explain the cause.
        Assert.That(state.OverallOutcome, Is.EqualTo(DurOutcome.Unavailable));
        Assert.That(state.FailedCheckCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AssessmentGrain_UnavailableCheck_IsNotOverridable()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult { CheckType = DurCheckType.DrugInteraction, Outcome = DurOutcome.Unavailable, Severity = "Significant", Message = "Dataset not loaded." },
            new DurCheckResult { CheckType = DurCheckType.DuplicateDrug, Outcome = DurOutcome.Fail, Severity = "Significant", Message = "Duplicate." },
        };

        await grain.CreateAsync("RX-UNAVAIL2", "PATIENT-UNAVAIL2", "WARFARIN 5MG", null, null,
            "5mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(DurAssessmentStatus.Failed));

        // Overriding the (overridable) Fail must NOT clear the block while an
        // Unavailable check remains — only an administrator loading the dataset
        // resolves it.
        await grain.OverrideCheckAsync(DurCheckType.DuplicateDrug, "PHARM-SENIOR", "Justified.");
        Assert.That((await grain.GetAsync()).Status, Is.EqualTo(DurAssessmentStatus.Failed),
            "An Unavailable check keeps the assessment Failed even after the Fail is overridden.");

        // Attempting to override the Unavailable check itself is a no-op
        // (OverrideCheckAsync only acts on Fail outcomes).
        await grain.OverrideCheckAsync(DurCheckType.DrugInteraction, "PHARM-SENIOR", "Trying to override unavailable.");
        DurAssessmentState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(DurAssessmentStatus.Failed));
        Assert.That(state.Checks.First(c => c.CheckType == DurCheckType.DrugInteraction).Outcome,
            Is.EqualTo(DurOutcome.Unavailable));
    }

    [Test]
    public async Task AssessmentGrain_ConflictingEntityFields_PersistCorrectly()
    {
        string id = $"DUR:{Guid.NewGuid()}";
        IDurAssessmentGrain grain = _cluster.GrainFactory.GetGrain<IDurAssessmentGrain>(id);

        List<DurCheckResult> checks = new()
        {
            new DurCheckResult
            {
                CheckType = DurCheckType.DuplicateDrug,
                Outcome = DurOutcome.Fail,
                Severity = "Significant",
                Message = "Duplicate found.",
                ConflictingEntityId = "RX-EXISTING-001",
                ConflictingEntityName = "LISINOPRIL 10MG",
                Details = "Active prescription found with same drug."
            },
        };

        await grain.CreateAsync("RX-NEW", "PATIENT-CONF", "LISINOPRIL 10MG", null, null,
            "10mg", "ORAL", "QD", 30, 30, "PHARM-001", checks);

        DurAssessmentState state = await grain.GetAsync();
        DurCheckResult dupCheck = state.Checks.First(c => c.CheckType == DurCheckType.DuplicateDrug);

        Assert.That(dupCheck.ConflictingEntityId, Is.EqualTo("RX-EXISTING-001"));
        Assert.That(dupCheck.ConflictingEntityName, Is.EqualTo("LISINOPRIL 10MG"));
        Assert.That(dupCheck.Details, Does.Contain("Active prescription"));
    }
}
