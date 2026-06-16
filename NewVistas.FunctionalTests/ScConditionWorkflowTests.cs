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
/// Functional tests for Service Connected Conditions — VistA File #2.04.
/// SC conditions are now embedded on the patient grain as ScConditionEntry.
/// Tests exercise the workflow grain methods for rated disabilities, combined rating,
/// appeals, exams, P&amp;T status, SMC, and notes.
/// </summary>
[TestFixture]
public class ScConditionWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private async Task<string> RecordStandardAsync(IPatientWorkflowGrain w,
        string condition = "PTSD",
        string? diagnosisCode = "F43.10",
        int? disabilityPercentage = 50,
        bool isServiceConnected = true)
    {
        return await w.RecordServiceConnectedConditionAsync(
            condition, diagnosisCode,
            disabilityPercentage, isServiceConnected,
            new DateTime(2020, 6, 1), null, "Initial rating");
    }

    // ─── 1. Record condition ──────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanRecordCondition()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await RecordStandardAsync(w);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ConditionId, Is.Not.Null.And.Not.Empty);
        Assert.That(entry.Condition, Is.EqualTo("PTSD"));
        Assert.That(entry.Status, Is.EqualTo("ACTIVE"));
    }

    // ─── 2. Get condition ─────────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanGetCondition()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await RecordStandardAsync(w, condition: "Tinnitus", diagnosisCode: "H93.19");

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Condition, Is.EqualTo("Tinnitus"));
        Assert.That(entry.DiagnosisCode, Is.EqualTo("H93.19"));
        Assert.That(entry.IsServiceConnected, Is.True);
    }

    // ─── 3. Set percentage ────────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanSetPercentage()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetServiceConnectedPercentageAsync(id, 70);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.ServiceConnectedPercentage, Is.EqualTo(70));
    }

    // ─── 4. Add rated disability ──────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanAddRatedDisability()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);

        List<RatedDisability> disabilities = await w.GetScRatedDisabilitiesAsync(id);
        Assert.That(disabilities, Has.Count.EqualTo(1));
        Assert.That(disabilities[0].ConditionName, Is.EqualTo("PTSD"));
        Assert.That(disabilities[0].RatingPercentage, Is.EqualTo(50));
        Assert.That(disabilities[0].DiagnosticCode, Is.EqualTo("9411"));
    }

    // ─── 5. Add multiple disabilities ─────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanAddMultipleDisabilities()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);
        await w.AddRatedDisabilityAsync(id, "Tinnitus", 10, new DateTime(2020, 6, 1), "6260", true);
        await w.AddRatedDisabilityAsync(id, "Lumbar Strain", 20, new DateTime(2021, 1, 15), "5237", false);

        List<RatedDisability> disabilities = await w.GetScRatedDisabilitiesAsync(id);
        Assert.That(disabilities, Has.Count.EqualTo(3));
    }

    // ─── 6. Remove rated disability ───────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanRemoveRatedDisability()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);
        await w.AddRatedDisabilityAsync(id, "Tinnitus", 10, new DateTime(2020, 6, 1), "6260", true);

        await w.RemoveScRatedDisabilityAsync(id, "Tinnitus");

        List<RatedDisability> disabilities = await w.GetScRatedDisabilitiesAsync(id);
        Assert.That(disabilities, Has.Count.EqualTo(1));
        Assert.That(disabilities[0].ConditionName, Is.EqualTo("PTSD"));
    }

    // ─── 7. Calculate combined rating ─────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanCalculateCombinedRating()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);
        await w.AddRatedDisabilityAsync(id, "Lumbar Strain", 30, new DateTime(2021, 1, 15), "5237", false);

        // VA combined: 50 + (100-50)*30/100 = 50 + 15 = 65 -> round to nearest 10 = 60
        await w.CalculateScCombinedRatingAsync(id);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.CombinedRating, Is.EqualTo(60));
    }

    // ─── 8. Set appeal status ─────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanSetAppealStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        DateTime appealDate = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        await w.SetScAppealStatusAsync(id, "FILED", appealDate);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.AppealStatus, Is.EqualTo("FILED"));
        Assert.That(entry.AppealFiledDate, Is.EqualTo(appealDate));
    }

    // ─── 9. Record exam ──────────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanRecordExam()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        DateTime examDate = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime nextExam = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);

        await w.RecordScExamAsync(id, examDate, "VA Portland HCS", nextExam);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.LastExamDate, Is.EqualTo(examDate));
        Assert.That(entry.ExaminingFacility, Is.EqualTo("VA Portland HCS"));
        Assert.That(entry.NextExamDueDate, Is.EqualTo(nextExam));
    }

    // ─── 10. Set permanent and total ──────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanSetPermanentAndTotal()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetScPermanentAndTotalAsync(id, true);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.IsPermanentAndTotal, Is.True);
    }

    // ─── 11. Set special monthly compensation ─────────────────────────────────

    [Test]
    public async Task ScCondition_CanSetSpecialMonthlyCompensation()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetScSpecialMonthlyCompensationAsync(id, "SMC-K");

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.SpecialMonthlyCompensation, Is.EqualTo("SMC-K"));
    }

    // ─── 12. Add note ─────────────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanAddNote()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddScConditionNoteAsync(id, "Dr. Johnson", "Patient reports worsening symptoms");

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Notes, Has.Count.EqualTo(1));
        Assert.That(entry.Notes[0].AuthorName, Is.EqualTo("Dr. Johnson"));
        Assert.That(entry.Notes[0].NoteText, Is.EqualTo("Patient reports worsening symptoms"));
        Assert.That(entry.Notes[0].NoteDate, Is.Not.EqualTo(default(DateTime)));
    }

    // ─── 13. Get rated disabilities ───────────────────────────────────────────

    [Test]
    public async Task ScCondition_CanGetRatedDisabilities()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);
        await w.AddRatedDisabilityAsync(id, "Tinnitus", 10, new DateTime(2020, 6, 1), "6260", true);

        List<RatedDisability> disabilities = await w.GetScRatedDisabilitiesAsync(id);

        Assert.That(disabilities, Has.Count.EqualTo(2));
        Assert.That(disabilities[0].ConditionName, Is.EqualTo("PTSD"));
        Assert.That(disabilities[1].ConditionName, Is.EqualTo("Tinnitus"));
        Assert.That(disabilities[1].IsStatic, Is.True);
    }

    // ─── 14. Combined rating single disability ────────────────────────────────

    [Test]
    public async Task ScCondition_CombinedRating_SingleDisability()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        await w.AddRatedDisabilityAsync(id, "PTSD", 70, new DateTime(2020, 6, 1), "9411", false);

        // single 70% -> combined 70 -> round to 70
        await w.CalculateScCombinedRatingAsync(id);

        ScConditionEntry? entry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.CombinedRating, Is.EqualTo(70));
    }

    // ─── 15. List SC Conditions ───────────────────────────────────────────────

    [Test]
    public async Task ScCondition_ListReturnsAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await RecordStandardAsync(w, condition: "PTSD");
        await RecordStandardAsync(w, condition: "Tinnitus", diagnosisCode: "H93.19", disabilityPercentage: 10);

        List<ServiceConnectedSummary> list = await w.GetServiceConnectedConditionsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── 16. Full workflow ────────────────────────────────────────────────────

    [Test]
    public async Task ScCondition_FullWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Record condition
        string id = await RecordStandardAsync(w, condition: "Multiple SC Conditions", diagnosisCode: null);
        ScConditionEntry? afterRecord = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(afterRecord!.Status, Is.EqualTo("ACTIVE"));

        // Add rated disabilities
        await w.AddRatedDisabilityAsync(id, "PTSD", 50, new DateTime(2020, 6, 1), "9411", false);
        await w.AddRatedDisabilityAsync(id, "Tinnitus", 10, new DateTime(2020, 6, 1), "6260", true);
        await w.AddRatedDisabilityAsync(id, "Lumbar Strain", 20, new DateTime(2021, 1, 15), "5237", false);

        // Calculate combined rating
        // 50 + (100-50)*20/100 = 60; 60 + (100-60)*10/100 = 64 -> round to 60
        await w.CalculateScCombinedRatingAsync(id);
        ScConditionEntry? afterCombined = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(afterCombined!.CombinedRating, Is.EqualTo(60));

        // Record exam
        DateTime examDate = new DateTime(2024, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        await w.RecordScExamAsync(id, examDate, "VA Seattle HCS", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc));

        // Set appeal
        await w.SetScAppealStatusAsync(id, "FILED", new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        // Set P&T
        await w.SetScPermanentAndTotalAsync(id, true);

        // Set SMC
        await w.SetScSpecialMonthlyCompensationAsync(id, "SMC-S");

        // Add notes
        await w.AddScConditionNoteAsync(id, "Dr. Adams", "Increased rating request submitted");
        await w.AddScConditionNoteAsync(id, "Dr. Rivera", "C&P exam completed");

        // Assert final state
        ScConditionEntry? finalEntry = await GetPatient(patientId).GetScConditionAsync(id);
        Assert.That(finalEntry, Is.Not.Null);
        Assert.That(finalEntry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(finalEntry.CombinedRating, Is.EqualTo(60));
        Assert.That(finalEntry.LastExamDate, Is.EqualTo(examDate));
        Assert.That(finalEntry.ExaminingFacility, Is.EqualTo("VA Seattle HCS"));
        Assert.That(finalEntry.AppealStatus, Is.EqualTo("FILED"));
        Assert.That(finalEntry.IsPermanentAndTotal, Is.True);
        Assert.That(finalEntry.SpecialMonthlyCompensation, Is.EqualTo("SMC-S"));
        Assert.That(finalEntry.Notes, Has.Count.EqualTo(2));
        List<RatedDisability> disabilities = await w.GetScRatedDisabilitiesAsync(id);
        Assert.That(disabilities, Has.Count.EqualTo(3));
    }
}
