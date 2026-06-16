// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Patient Merge grain — VistA DG MERGE utility (File #15.1).
/// Tests the IPatientMergeGrain directly, verifying merge logic for embedded
/// collections, ID lists, deduplication, validation, and audit state.
/// </summary>
[TestFixture]
public class PatientMergeGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private IPatientMergeGrain NewMergeGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{Guid.NewGuid()}");

    private IPatientIndexGrain GetPatientIndex() =>
        _cluster.GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    /// <summary>
    /// Helper: create a patient with demographics and register in the patient index.
    /// </summary>
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

    // ─── Test 1: Merges Embedded Allergies ──────────────────────────────

    [Test]
    public async Task PatientMergeGrain_MergesEmbeddedAllergies()
    {
        // Arrange
        string targetId = await CreatePatientAsync("DOE,JOHN", "M", new DateTime(1960, 1, 1), "123456789");
        string sourceId = await CreatePatientAsync("DOE,JOHNNY", "M", new DateTime(1960, 1, 1), "123456789");

        await GetPatient(targetId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-1",
            Allergen = "Penicillin",
            AllergenType = "Drug"
        });

        await GetPatient(sourceId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-2",
            Allergen = "Sulfa",
            AllergenType = "Drug"
        });

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        List<AllergyEntry> targetAllergies = await GetPatient(targetId).GetAllergiesAsync();
        Assert.That(targetAllergies, Has.Count.EqualTo(2));
        Assert.That(targetAllergies.Select(a => a.Allergen), Does.Contain("Penicillin"));
        Assert.That(targetAllergies.Select(a => a.Allergen), Does.Contain("Sulfa"));
    }

    // ─── Test 2: Merges Embedded Problems ───────────────────────────────

    [Test]
    public async Task PatientMergeGrain_MergesEmbeddedProblems()
    {
        // Arrange
        string targetId = await CreatePatientAsync("SMITH,JANE", "F", new DateTime(1970, 5, 15), "987654321");
        string sourceId = await CreatePatientAsync("SMITH,JANEY", "F", new DateTime(1970, 5, 15), "987654321");

        await GetPatient(targetId).AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-1",
            Diagnosis = "Hypertension",
            DiagnosisCode = "I10",
            Status = "ACTIVE"
        });

        await GetPatient(sourceId).AddProblemAsync(new ProblemEntry
        {
            ProblemId = "PROB-2",
            Diagnosis = "Diabetes Type 2",
            DiagnosisCode = "E11.9",
            Status = "ACTIVE"
        });

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        List<ProblemEntry> targetProblems = await GetPatient(targetId).GetProblemsAsync();
        Assert.That(targetProblems, Has.Count.EqualTo(2));
        Assert.That(targetProblems.Select(p => p.Diagnosis), Does.Contain("Hypertension"));
        Assert.That(targetProblems.Select(p => p.Diagnosis), Does.Contain("Diabetes Type 2"));
    }

    // ─── Test 3: Merges ID Lists ────────────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_MergesIdLists()
    {
        // Arrange
        string targetId = await CreatePatientAsync("JONES,MIKE", "M", new DateTime(1955, 8, 20), "111223333");
        string sourceId = await CreatePatientAsync("JONES,MICHAEL", "M", new DateTime(1955, 8, 20), "111223333");

        await GetPatient(targetId).AddLabTestIdAsync("LAB-001");
        await GetPatient(targetId).AddLabTestIdAsync("LAB-002");

        await GetPatient(sourceId).AddLabTestIdAsync("LAB-003");
        await GetPatient(sourceId).AddLabTestIdAsync("LAB-004");

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        List<string> targetLabIds = await GetPatient(targetId).GetLabTestIdsAsync();
        Assert.That(targetLabIds, Has.Count.EqualTo(4));
        Assert.That(targetLabIds, Does.Contain("LAB-001"));
        Assert.That(targetLabIds, Does.Contain("LAB-002"));
        Assert.That(targetLabIds, Does.Contain("LAB-003"));
        Assert.That(targetLabIds, Does.Contain("LAB-004"));
    }

    // ─── Test 4: Deduplicates on Merge ──────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_DeduplicatesOnMerge()
    {
        // Arrange — both patients have the same allergy ID
        string targetId = await CreatePatientAsync("BROWN,ALICE", "F", new DateTime(1980, 3, 10), "444556666");
        string sourceId = await CreatePatientAsync("BROWN,ALICIA", "F", new DateTime(1980, 3, 10), "444556666");

        AllergyEntry sharedAllergy = new AllergyEntry
        {
            AllergyId = "ALG-SHARED",
            Allergen = "Latex",
            AllergenType = "Other"
        };

        await GetPatient(targetId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-SHARED",
            Allergen = "Latex",
            AllergenType = "Other"
        });

        await GetPatient(sourceId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-SHARED",
            Allergen = "Latex",
            AllergenType = "Other"
        });

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert — no duplicate; still only 1 allergy on target
        Assert.That(result.Success, Is.True);
        List<AllergyEntry> targetAllergies = await GetPatient(targetId).GetAllergiesAsync();
        Assert.That(targetAllergies, Has.Count.EqualTo(1));
        Assert.That(targetAllergies[0].AllergyId, Is.EqualTo("ALG-SHARED"));
    }

    // ─── Test 5: Marks Source as Merged ─────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_MarksSourceAsMerged()
    {
        // Arrange
        string targetId = await CreatePatientAsync("WILSON,BOB", "M", new DateTime(1945, 12, 25), "777889999");
        string sourceId = await CreatePatientAsync("WILSON,ROBERT", "M", new DateTime(1945, 12, 25), "777889999");

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        PatientState sourceState = await GetPatient(sourceId).GetPatientAsync();
        Assert.That(sourceState.MergedIntoPatientId, Is.EqualTo(targetId));
        Assert.That(sourceState.IsActive, Is.False);
    }

    // ─── Test 6: Updates Patient Index ──────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_UpdatesPatientIndex()
    {
        // Arrange
        string targetId = await CreatePatientAsync("GARCIA,MARIA", "F", new DateTime(1975, 7, 4), "222334444");
        string sourceId = await CreatePatientAsync("GARCIA,MARIE", "F", new DateTime(1975, 7, 4), "222334444");

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);
        IPatientIndexGrain index = GetPatientIndex();
        PatientIndexEntry? sourceEntry = await index.GetByPatientIdAsync(sourceId);
        PatientIndexEntry? targetEntry = await index.GetByPatientIdAsync(targetId);
        Assert.That(sourceEntry, Is.Not.Null, "Source should still be in index but marked inactive");
        Assert.That(sourceEntry!.IsActive, Is.False, "Source should be marked inactive in patient index after merge");
        Assert.That(targetEntry, Is.Not.Null, "Target should still exist in patient index after merge");
        Assert.That(targetEntry!.IsActive, Is.True);
    }

    // ─── Test 7: Fails for Inactive Source ──────────────────────────────

    [Test]
    public async Task PatientMergeGrain_FailsForInactiveSource()
    {
        // Arrange
        string targetId = await CreatePatientAsync("LEE,CHRIS", "M", new DateTime(1990, 2, 14), "555667777");
        string sourceId = await CreatePatientAsync("LEE,CHRISTOPHER", "M", new DateTime(1990, 2, 14), "555667777");

        // Deactivate the source patient
        await GetPatient(sourceId).DeactivateAsync();

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("not active"));
    }

    // ─── Test 8: Fails for Same Patient ─────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_FailsForSamePatient()
    {
        // Arrange
        string patientId = await CreatePatientAsync("MARTINEZ,ANA", "F", new DateTime(1985, 11, 30), "888990000");

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            patientId, patientId, "Duplicate", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("themselves"));
    }

    // ─── Test 9: Fails for Already Merged ───────────────────────────────

    [Test]
    public async Task PatientMergeGrain_FailsForAlreadyMerged()
    {
        // Arrange — merge source once, then try to merge again
        string targetId = await CreatePatientAsync("PATEL,RAJ", "M", new DateTime(1965, 6, 18), "333445555");
        string sourceId = await CreatePatientAsync("PATEL,RAJESH", "M", new DateTime(1965, 6, 18), "333445555");

        IPatientMergeGrain mergeGrain1 = NewMergeGrain();
        PatientMergeResult firstResult = await mergeGrain1.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate", "USER1", "Admin");
        Assert.That(firstResult.Success, Is.True);

        // Act — try to merge the same source again
        string target2Id = await CreatePatientAsync("PATEL,RAVI", "M", new DateTime(1965, 6, 18), "333445556");
        IPatientMergeGrain mergeGrain2 = NewMergeGrain();
        PatientMergeResult secondResult = await mergeGrain2.ExecuteMergeAsync(
            target2Id, sourceId, "Duplicate", "USER2", "Admin2");

        // Assert
        Assert.That(secondResult.Success, Is.False);
        Assert.That(secondResult.ErrorMessage, Does.Contain("already merged"));
    }

    // ─── Test 10: Records Merge State ───────────────────────────────────

    [Test]
    public async Task PatientMergeGrain_RecordsMergeState()
    {
        // Arrange
        string targetId = await CreatePatientAsync("NGUYEN,TRAN", "M", new DateTime(1972, 9, 5), "666778888");
        string sourceId = await CreatePatientAsync("NGUYEN,TRANG", "M", new DateTime(1972, 9, 5), "666778888");

        await GetPatient(sourceId).AddAllergyAsync(new AllergyEntry
        {
            AllergyId = "ALG-STATE-1",
            Allergen = "Aspirin",
            AllergenType = "Drug"
        });

        await GetPatient(sourceId).AddLabTestIdAsync("LAB-STATE-1");

        // Act
        IPatientMergeGrain mergeGrain = NewMergeGrain();
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetId, sourceId, "Duplicate record", "USER1", "Admin");

        // Assert
        Assert.That(result.Success, Is.True);

        PatientMergeState mergeState = await mergeGrain.GetMergeStateAsync();
        Assert.That(mergeState.Status, Is.EqualTo("COMPLETED"));
        Assert.That(mergeState.TargetPatientId, Is.EqualTo(targetId));
        Assert.That(mergeState.SourcePatientId, Is.EqualTo(sourceId));
        Assert.That(mergeState.Reason, Is.EqualTo("Duplicate record"));
        Assert.That(mergeState.MergedByUserId, Is.EqualTo("USER1"));
        Assert.That(mergeState.MergedByUserName, Is.EqualTo("Admin"));
        Assert.That(mergeState.MergeDate, Is.Not.EqualTo(default(DateTime)));
        Assert.That(mergeState.ItemsMoved, Does.ContainKey("Allergies"));
        Assert.That(mergeState.ItemsMoved["Allergies"], Is.EqualTo(1));
        Assert.That(mergeState.ItemsMoved, Does.ContainKey("LabTests"));
        Assert.That(mergeState.ItemsMoved["LabTests"], Is.EqualTo(1));
    }
}
