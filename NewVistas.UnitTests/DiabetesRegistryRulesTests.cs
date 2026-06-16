// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for <see cref="DiabetesRegistryRules"/>. Pure functions over
/// <see cref="DiabetesRegistryState"/>, so tested without a TestCluster.
/// Pins the thresholds that drive both the snapshot and the pre-visit plan
/// so any future change to ADA/IHS standard-of-care intervals surfaces as
/// a deliberate test update.
/// </summary>
[TestFixture]
public class DiabetesRegistryRulesTests
{
    // ── HbA1c classification ────────────────────────────────────────────

    [TestCase(null, ExpectedResult = HbA1cControlStatus.NoData)]
    [TestCase("6.9", ExpectedResult = HbA1cControlStatus.Good)]
    [TestCase("7.0", ExpectedResult = HbA1cControlStatus.AtTarget)]
    [TestCase("8.9", ExpectedResult = HbA1cControlStatus.AtTarget)]
    [TestCase("9.0", ExpectedResult = HbA1cControlStatus.Poor)]
    [TestCase("12.5", ExpectedResult = HbA1cControlStatus.Poor)]
    public HbA1cControlStatus ClassifyHbA1c_PinsThresholds(string? raw)
    {
        decimal? value = raw is null ? null : decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        return DiabetesRegistryRules.ClassifyHbA1c(value);
    }

    // ── Annual exam classification ──────────────────────────────────────

    [Test]
    public void ClassifyAnnualExam_NoData_ReturnsNoData()
    {
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(null, new DateTime(2026, 1, 1));
        Assert.That(s, Is.EqualTo(DueStatus.NoData));
    }

    [Test]
    public void ClassifyAnnualExam_RecentExam_IsUpToDate()
    {
        DateTime asOf = new(2026, 1, 1);
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(asOf.AddMonths(-6), asOf);
        Assert.That(s, Is.EqualTo(DueStatus.UpToDate));
    }

    [Test]
    public void ClassifyAnnualExam_ExactlyTwelveMonths_StillUpToDate()
    {
        DateTime asOf = new(2026, 6, 1);
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(asOf.AddMonths(-12), asOf);
        Assert.That(s, Is.EqualTo(DueStatus.UpToDate));
    }

    [Test]
    public void ClassifyAnnualExam_ThirteenMonths_IsDue()
    {
        DateTime asOf = new(2026, 6, 1);
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(asOf.AddMonths(-13), asOf);
        Assert.That(s, Is.EqualTo(DueStatus.Due));
    }

    [Test]
    public void ClassifyAnnualExam_FifteenMonths_IsDue()
    {
        DateTime asOf = new(2026, 6, 1);
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(asOf.AddMonths(-15), asOf);
        Assert.That(s, Is.EqualTo(DueStatus.Due));
    }

    [Test]
    public void ClassifyAnnualExam_SixteenMonths_IsOverdue()
    {
        DateTime asOf = new(2026, 6, 1);
        DueStatus s = DiabetesRegistryRules.ClassifyAnnualExam(asOf.AddMonths(-16), asOf);
        Assert.That(s, Is.EqualTo(DueStatus.Overdue));
    }

    // ── Kidney function classification ──────────────────────────────────

    [TestCase(null, ExpectedResult = KidneyFunctionStatus.NoData)]
    [TestCase("90", ExpectedResult = KidneyFunctionStatus.Normal)]
    [TestCase("60", ExpectedResult = KidneyFunctionStatus.Normal)]
    [TestCase("59", ExpectedResult = KidneyFunctionStatus.Reduced)]
    [TestCase("30", ExpectedResult = KidneyFunctionStatus.Reduced)]
    [TestCase("29", ExpectedResult = KidneyFunctionStatus.Severe)]
    [TestCase("8", ExpectedResult = KidneyFunctionStatus.Severe)]
    public KidneyFunctionStatus ClassifyKidneyFunction_PinsThresholds(string? raw)
    {
        decimal? value = raw is null ? null : decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        return DiabetesRegistryRules.ClassifyKidneyFunction(value);
    }

    // ── Snapshot ────────────────────────────────────────────────────────

    [Test]
    public void BuildSnapshot_EmptyState_ReturnsNoDataEverywhere()
    {
        var state = new DiabetesRegistryState { Icn = "099-TEST" };
        DiabetesRegistrySnapshot s = DiabetesRegistryRules.BuildSnapshot(state, new DateTime(2026, 6, 1));

        Assert.That(s.Icn, Is.EqualTo("099-TEST"));
        Assert.That(s.HbA1cControl, Is.EqualTo(HbA1cControlStatus.NoData));
        Assert.That(s.FootExamStatus, Is.EqualTo(DueStatus.NoData));
        Assert.That(s.EyeExamStatus, Is.EqualTo(DueStatus.NoData));
        Assert.That(s.AcrStatus, Is.EqualTo(DueStatus.NoData));
        Assert.That(s.KidneyFunction, Is.EqualTo(KidneyFunctionStatus.NoData));
        Assert.That(s.LastHbA1cValue, Is.Null);
    }

    [Test]
    public void BuildSnapshot_TakesMostRecentHbA1c()
    {
        var state = new DiabetesRegistryState { Icn = "T1" };
        state.HbA1cHistory.Add(new HbA1cReading { Value = 8.0m, DateOfTest = new DateTime(2025, 6, 1) });
        state.HbA1cHistory.Add(new HbA1cReading { Value = 6.9m, DateOfTest = new DateTime(2025, 12, 1) });

        DiabetesRegistrySnapshot s = DiabetesRegistryRules.BuildSnapshot(state, new DateTime(2026, 1, 1));
        Assert.That(s.LastHbA1cValue, Is.EqualTo(6.9m));
        Assert.That(s.HbA1cControl, Is.EqualTo(HbA1cControlStatus.Good));
    }

    // ── Pre-visit plan ──────────────────────────────────────────────────

    [Test]
    public void PreVisitPlan_NoHistory_PutsHbA1cAndAllExamsInOverdue()
    {
        var state = new DiabetesRegistryState { Icn = "T2" };
        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, new DateTime(2026, 6, 1));

        Assert.That(plan.ItemsOverdue, Has.Count.EqualTo(4),
            "HbA1c never recorded + 3 annual exams never recorded.");
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("HbA1c never")), Is.True);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("foot exam")), Is.True);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("eye exam")), Is.True);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("nephropathy")), Is.True);
    }

    [Test]
    public void PreVisitPlan_RecentHbA1cWithinSixMonths_IsUpToDate()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState { Icn = "T3" };
        state.HbA1cHistory.Add(new HbA1cReading { Value = 7.5m, DateOfTest = visit.AddMonths(-3) });

        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsUpToDate.Any(i => i.Contains("HbA1c up to date")), Is.True);
    }

    [Test]
    public void PreVisitPlan_HbA1cAt9months_IsDue()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState { Icn = "T4" };
        state.HbA1cHistory.Add(new HbA1cReading { Value = 7.5m, DateOfTest = visit.AddMonths(-9) });

        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsDue.Any(i => i.Contains("HbA1c due")), Is.True);
    }

    [Test]
    public void PreVisitPlan_PoorControl_GoesToOverdue_RegardlessOfRecency()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState { Icn = "T5" };
        // Recent test (so HbA1c-by-date is up-to-date) but value is 9.5 (poor control).
        state.HbA1cHistory.Add(new HbA1cReading { Value = 9.5m, DateOfTest = visit.AddMonths(-2) });

        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("poor control")), Is.True);
    }

    [Test]
    public void PreVisitPlan_SevereCKD_GoesToOverdue()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState
        {
            Icn = "T6",
            LastEgfr = 25m,
            LastEgfrDate = visit.AddMonths(-2),
        };
        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsOverdue.Any(i => i.Contains("Severe CKD")), Is.True);
    }

    [Test]
    public void PreVisitPlan_ReducedKidneyFunction_GoesToDue()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState
        {
            Icn = "T7",
            LastEgfr = 45m,
            LastEgfrDate = visit.AddMonths(-2),
        };
        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsDue.Any(i => i.Contains("Reduced kidney function")), Is.True);
    }

    [Test]
    public void PreVisitPlan_AllUpToDate_NoOverdueOrDueItems()
    {
        DateTime visit = new(2026, 6, 1);
        var state = new DiabetesRegistryState
        {
            Icn = "T8",
            LastFootExamDate = visit.AddMonths(-3),
            LastEyeExamDate = visit.AddMonths(-2),
            LastAcrDate = visit.AddMonths(-4),
            LastEgfr = 90m,
            LastEgfrDate = visit.AddMonths(-1),
        };
        state.HbA1cHistory.Add(new HbA1cReading { Value = 6.5m, DateOfTest = visit.AddMonths(-2) });

        DiabetesPreVisitPlan plan = DiabetesRegistryRules.BuildPreVisitPlan(state, visit);
        Assert.That(plan.ItemsOverdue, Is.Empty);
        Assert.That(plan.ItemsDue, Is.Empty);
        Assert.That(plan.ItemsUpToDate, Has.Count.EqualTo(4),
            "Should have 4 up-to-date items: HbA1c + 3 annual exams.");
    }
}
