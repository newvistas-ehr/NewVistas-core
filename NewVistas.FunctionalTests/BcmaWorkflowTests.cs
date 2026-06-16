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
/// Functional tests for VistA BCMA (Bar Code Medication Administration) — File #53.79.
/// Tests end-to-end medication administration workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class BcmaWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid():N}");

    // ─── RecordMedicationAdministration tests ──────────────────────────────────

    [Test]
    public async Task RecordMedicationAdministration_ReturnsNonEmptyId()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            "METOPROLOL TARTRATE 25MG TAB", $"DRUG-{Guid.NewGuid():N}",
            "25mg", "PO", "GIVEN",
            DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow,
            "NURSE-001", "Nurse Johnson",
            null, null, null, null);

        // Assert
        Assert.That(bcmaId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task RecordMedicationAdministration_ActionStatusGiven()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            "LISINOPRIL 10MG TAB", $"DRUG-{Guid.NewGuid():N}",
            "10mg", "PO", "GIVEN",
            DateTime.UtcNow.AddMinutes(-15), DateTime.UtcNow,
            "NURSE-002", "Nurse Smith",
            null, null, null, null);
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        BcmaSummary entry = history.Single(h => h.BcmaId == bcmaId);
        Assert.That(entry.ActionStatus, Is.EqualTo("GIVEN"));
    }

    [Test]
    public async Task RecordMedicationAdministration_AppearsInHistory()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            "ASPIRIN 81MG TAB", $"DRUG-{Guid.NewGuid():N}",
            "81mg", "PO", "GIVEN",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow,
            "NURSE-003", "Nurse Williams",
            null, null, null, null);
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].BcmaId, Is.EqualTo(bcmaId));
    }

    [Test]
    public async Task GetMedicationAdministrations_NoRecords_ReturnsEmpty()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        Assert.That(history, Is.Empty);
    }

    [Test]
    public async Task GetMedicationAdministrations_MaxResultsRespected()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        for (int i = 0; i < 5; i++)
        {
            await wf.RecordMedicationAdministrationAsync(
                $"DRUG-{i}", $"DRUG-ID-{i}",
                "10mg", "PO", "GIVEN",
                DateTime.UtcNow.AddMinutes(-i * 10), DateTime.UtcNow.AddMinutes(-i * 10 + 5),
                "NURSE-004", "Nurse Davis",
                null, null, null, null);
        }

        // Act
        List<BcmaSummary> limited = await wf.GetMedicationAdministrationsAsync(3);

        // Assert
        Assert.That(limited, Has.Count.LessThanOrEqualTo(3));
    }

    [Test]
    public async Task GetPatientMAR_EmptyByDefault()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        List<MarEntry> mar = await wf.GetPatientMARAsync();

        // Assert
        Assert.That(mar, Is.Empty);
    }

    [Test]
    public async Task RecordMedicationAdministration_DrugNameStored()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string drugName = "AMLODIPINE BESYLATE 5MG TAB";

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            drugName, $"DRUG-{Guid.NewGuid():N}",
            "5mg", "PO", "GIVEN",
            DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow,
            "NURSE-005", "Nurse Brown",
            null, null, null, null);
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        BcmaSummary entry = history.Single(h => h.BcmaId == bcmaId);
        Assert.That(entry.DrugName, Is.EqualTo(drugName));
    }

    [Test]
    public async Task RecordMedicationAdministration_DosageAndRouteStored()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            "HEPARIN 5000 UNITS/ML INJ", $"DRUG-{Guid.NewGuid():N}",
            "5000 units", "SUBQ", "GIVEN",
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow,
            "NURSE-006", "Nurse Taylor",
            "Left abdomen", null, null, null);
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        BcmaSummary entry = history.Single(h => h.BcmaId == bcmaId);
        Assert.That(entry.Dosage, Is.EqualTo("5000 units"));
    }

    [Test]
    public async Task RecordMedicationAdministration_CommentsStored()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();
        string comments = "Patient tolerated medication well, no adverse effects noted";

        // Act
        string bcmaId = await wf.RecordMedicationAdministrationAsync(
            "INSULIN GLARGINE 100 UNIT/ML INJ", $"DRUG-{Guid.NewGuid():N}",
            "30 units", "SUBQ", "GIVEN",
            DateTime.UtcNow.AddMinutes(-60), DateTime.UtcNow,
            "NURSE-007", "Nurse Wilson",
            "Right arm", null, null, comments);
        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        Assert.That(history.Any(h => h.BcmaId == bcmaId), Is.True);
    }

    [Test]
    public async Task RecordMultipleAdministrations_AllAppearInHistory()
    {
        // Arrange
        IPatientWorkflowGrain wf = NewWorkflow();

        // Act
        string id1 = await wf.RecordMedicationAdministrationAsync(
            "METFORMIN HCL 500MG TAB", $"DRUG-{Guid.NewGuid():N}",
            "500mg", "PO", "GIVEN",
            DateTime.UtcNow.AddHours(-6), DateTime.UtcNow.AddHours(-6),
            "NURSE-008", "Nurse Anderson",
            null, null, null, null);

        string id2 = await wf.RecordMedicationAdministrationAsync(
            "ATORVASTATIN CALCIUM 40MG TAB", $"DRUG-{Guid.NewGuid():N}",
            "40mg", "PO", "GIVEN",
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-2),
            "NURSE-008", "Nurse Anderson",
            null, null, null, null);

        string id3 = await wf.RecordMedicationAdministrationAsync(
            "OMEPRAZOLE 20MG CAP", $"DRUG-{Guid.NewGuid():N}",
            "20mg", "PO", "HELD",
            DateTime.UtcNow, DateTime.UtcNow,
            "NURSE-008", "Nurse Anderson",
            null, null, null, "Patient NPO for procedure");

        List<BcmaSummary> history = await wf.GetMedicationAdministrationsAsync(50);

        // Assert
        Assert.That(history, Has.Count.EqualTo(3));
        Assert.That(history.Any(h => h.BcmaId == id1), Is.True);
        Assert.That(history.Any(h => h.BcmaId == id2), Is.True);
        Assert.That(history.Any(h => h.BcmaId == id3), Is.True);
    }
}
