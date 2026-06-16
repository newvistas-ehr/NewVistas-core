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
/// Functional tests for Means Test depth enhancements — VistA File #408.31.
/// Means tests are now embedded on the patient grain as MeansTestEntry.
/// Tests exercise the workflow grain methods for income/assets/expenses recording,
/// adjusted income calculation, GMT threshold, hardship determination, copay test,
/// dependents, and full workflow.
/// </summary>
[TestFixture]
public class MeansTestDepthWorkflowTests
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

    private async Task<string> RecordStandardAsync(IPatientWorkflowGrain w)
    {
        return await w.RecordMeansTestAsync(
            "MEANS TEST",
            new DateTime(2024, 1, 15),
            45000.00m,
            12000.00m,
            2,
            "VERIFIED",
            "5",
            "CLERK-001", "Mary Smith",
            "Annual means test");
    }

    // ─── 1. Record means test ─────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanRecordMeansTest()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await RecordStandardAsync(w);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.MeansTestId, Is.Not.Null.And.Not.Empty);
        Assert.That(entry.TestType, Is.EqualTo("MEANS TEST"));
    }

    // ─── 2. Get means test ────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanGetMeansTest()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        string id = await RecordStandardAsync(w);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.EligibilityStatus, Is.EqualTo("VERIFIED"));
        Assert.That(entry.PriorityGroup, Is.EqualTo("5"));
    }

    // ─── 3. Record income ─────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanRecordIncome()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.RecordMeansTestIncomeAsync(id, 65000.00m, 25000.00m, 5000.00m);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.VeteranGrossIncome, Is.EqualTo(65000.00m));
        Assert.That(entry.SpouseGrossIncome, Is.EqualTo(25000.00m));
        Assert.That(entry.DependentIncome, Is.EqualTo(5000.00m));
    }

    // ─── 4. Record assets ─────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanRecordAssets()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.RecordMeansTestAssetsAsync(id, 150000.00m, 120000.00m, 30000.00m);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.TotalNetWorth, Is.EqualTo(150000.00m));
        Assert.That(entry.PropertyValue, Is.EqualTo(120000.00m));
        Assert.That(entry.OtherAssets, Is.EqualTo(30000.00m));
    }

    // ─── 5. Record expenses ───────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanRecordExpenses()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.RecordMeansTestExpensesAsync(id, 8500.00m);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.DeductibleExpenses, Is.EqualTo(8500.00m));
    }

    // ─── 6. Calculate adjusted income ─────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanCalculateAdjustedIncome()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);
        await w.RecordMeansTestIncomeAsync(id, 65000.00m, 25000.00m, 5000.00m);
        await w.RecordMeansTestExpensesAsync(id, 10000.00m);

        // 65000 + 25000 + 5000 - 10000 = 85000
        await w.CalculateMeansTestAdjustedIncomeAsync(id);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.AdjustedIncome, Is.EqualTo(85000.00m));
    }

    // ─── 7. Set GMT threshold ─────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanSetGmtThreshold()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetMeansTestGmtThresholdAsync(id, 47000.00m);

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.GmtThreshold, Is.EqualTo(47000.00m));
    }

    // ─── 8. Determine hardship ───────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanDetermineHardship()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.DetermineMeansTestHardshipAsync(id, "HARDSHIP");

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.HardshipDetermination, Is.EqualTo("HARDSHIP"));
        Assert.That(entry.HardshipDecisionDate, Is.Not.Null);
    }

    // ─── 9. Set copay test result ────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanSetCopayTestResult()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.SetMeansTestCopayResultAsync(id, "EXEMPT");

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.CopayTestResult, Is.EqualTo("EXEMPT"));
    }

    // ─── 10. Add dependent ────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanAddDependent()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddMeansTestDependentAsync(id, "Jane Doe", "SPOUSE", 25000.00m, 10000.00m, new DateTime(1985, 7, 20));

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Dependents, Has.Count.EqualTo(1));
        Assert.That(entry.Dependents[0].Name, Is.EqualTo("Jane Doe"));
        Assert.That(entry.Dependents[0].Relationship, Is.EqualTo("SPOUSE"));
        Assert.That(entry.Dependents[0].Income, Is.EqualTo(25000.00m));
    }

    // ─── 11. Add multiple dependents ──────────────────────────────────────────

    [Test]
    public async Task MeansTest_CanAddMultipleDependents()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);
        string id = await RecordStandardAsync(w);

        await w.AddMeansTestDependentAsync(id, "Jane Doe", "SPOUSE", 25000.00m, 10000.00m, new DateTime(1985, 7, 20));
        await w.AddMeansTestDependentAsync(id, "John Doe Jr", "CHILD", 0m, 0m, new DateTime(2010, 3, 15));
        await w.AddMeansTestDependentAsync(id, "Mary Doe", "CHILD", 0m, 0m, new DateTime(2013, 11, 2));

        MeansTestEntry? entry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.Dependents, Has.Count.EqualTo(3));
    }

    // ─── 12. List Means Tests ────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_ListReturnsAll()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await RecordStandardAsync(w);
        await w.RecordMeansTestAsync(
            "COPAY EXEMPTION TEST",
            new DateTime(2024, 6, 1),
            22000.00m, 5000.00m, 0,
            "VERIFIED", "7",
            null, null, null);

        List<MeansTestSummary> list = await w.GetMeansTestsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    // ─── 13. Patient Linkage ──────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_LinksToPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await RecordStandardAsync(w);

        List<MeansTestEntry> entries = await GetPatient(patientId).GetMeansTestsAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].MeansTestId, Is.Not.Empty);
    }

    // ─── 14. Multiple Patients Independent ────────────────────────────────────

    [Test]
    public async Task MeansTest_MultiplePatients_Independent()
    {
        IPatientWorkflowGrain w1 = NewWorkflow();
        IPatientWorkflowGrain w2 = NewWorkflow();

        await RecordStandardAsync(w1);

        List<MeansTestSummary> list2 = await w2.GetMeansTestsAsync();
        Assert.That(list2, Is.Empty);
    }

    // ─── 15. Empty By Default ────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_EmptyByDefault()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<MeansTestSummary> list = await w.GetMeansTestsAsync();
        Assert.That(list, Is.Empty);
    }

    // ─── 16. Full workflow ────────────────────────────────────────────────────

    [Test]
    public async Task MeansTest_FullWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        // Record means test
        string id = await RecordStandardAsync(w);
        MeansTestEntry? afterRecord = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(afterRecord, Is.Not.Null);

        // Record income
        await w.RecordMeansTestIncomeAsync(id, 55000.00m, 20000.00m, 3000.00m);

        // Record assets
        await w.RecordMeansTestAssetsAsync(id, 80000.00m, 60000.00m, 20000.00m);

        // Record expenses
        await w.RecordMeansTestExpensesAsync(id, 12000.00m);

        // Calculate adjusted income: 55000 + 20000 + 3000 - 12000 = 66000
        await w.CalculateMeansTestAdjustedIncomeAsync(id);
        MeansTestEntry? afterCalc = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(afterCalc!.AdjustedIncome, Is.EqualTo(66000.00m));

        // Set GMT threshold
        await w.SetMeansTestGmtThresholdAsync(id, 47000.00m);

        // Add dependents
        await w.AddMeansTestDependentAsync(id, "Jane Doe", "SPOUSE", 20000.00m, 5000.00m, new DateTime(1985, 7, 20));
        await w.AddMeansTestDependentAsync(id, "John Doe Jr", "CHILD", 0m, 0m, new DateTime(2010, 3, 15));

        // Determine hardship
        await w.DetermineMeansTestHardshipAsync(id, "NOT_HARDSHIP");

        // Set copay test result
        await w.SetMeansTestCopayResultAsync(id, "COPAY_REQUIRED");

        // Assert final state
        MeansTestEntry? finalEntry = await GetPatient(patientId).GetMeansTestAsync(id);
        Assert.That(finalEntry, Is.Not.Null);
        Assert.That(finalEntry!.VeteranGrossIncome, Is.EqualTo(55000.00m));
        Assert.That(finalEntry.TotalNetWorth, Is.EqualTo(80000.00m));
        Assert.That(finalEntry.DeductibleExpenses, Is.EqualTo(12000.00m));
        Assert.That(finalEntry.AdjustedIncome, Is.EqualTo(66000.00m));
        Assert.That(finalEntry.GmtThreshold, Is.EqualTo(47000.00m));
        Assert.That(finalEntry.HardshipDetermination, Is.EqualTo("NOT_HARDSHIP"));
        Assert.That(finalEntry.CopayTestResult, Is.EqualTo("COPAY_REQUIRED"));
        Assert.That(finalEntry.Dependents, Has.Count.EqualTo(2));
    }
}
