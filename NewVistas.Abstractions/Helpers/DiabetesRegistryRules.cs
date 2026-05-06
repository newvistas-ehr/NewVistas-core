// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Pure functions that compute diabetes-registry status enums and pre-visit
/// plans from raw <see cref="DiabetesRegistryState"/>. Centralised so the
/// snapshot view and the pre-visit plan use the same rules; testable in
/// isolation without spinning up a TestCluster.
///
/// All thresholds documented inline. Tweak per the IHS standard of care
/// version a deployment is targeting; tests pin the current values.
/// </summary>
public static class DiabetesRegistryRules
{
    // HbA1c thresholds (per ADA/IHS Diabetes Standards of Care 2024-25;
    // also align with the "Diabetes: Poor HbA1c Control (>9.0)" GPRA measure).
    public const decimal HbA1cGoodThreshold = 7.0m;
    public const decimal HbA1cPoorThreshold = 9.0m;

    // Annual-exam thresholds. Standard interval is 12 months; 12-15 months
    // is "due", > 15 months (or never) is "overdue".
    public const int AnnualExamDueAfterMonths = 12;
    public const int AnnualExamOverdueAfterMonths = 15;

    // Kidney function (eGFR mL/min/1.73m²); CKD G3+ at 60.
    public const decimal EgfrNormalThreshold = 60m;
    public const decimal EgfrSevereThreshold = 30m;

    public static DiabetesRegistrySnapshot BuildSnapshot(DiabetesRegistryState state, DateTime asOf)
    {
        HbA1cReading? lastHbA1c = state.HbA1cHistory.Count > 0
            ? state.HbA1cHistory[^1]
            : null;

        return new DiabetesRegistrySnapshot
        {
            Icn = state.Icn,
            IsEnrolled = state.IsEnrolled,
            DiabetesType = state.DiabetesType,

            LastHbA1cValue = lastHbA1c?.Value,
            LastHbA1cDate = lastHbA1c?.DateOfTest,
            HbA1cControl = ClassifyHbA1c(lastHbA1c?.Value),

            FootExamStatus = ClassifyAnnualExam(state.LastFootExamDate, asOf),
            EyeExamStatus = ClassifyAnnualExam(state.LastEyeExamDate, asOf),
            AcrStatus = ClassifyAnnualExam(state.LastAcrDate, asOf),

            KidneyFunction = ClassifyKidneyFunction(state.LastEgfr),
            LastEgfrValue = state.LastEgfr,
            LastEgfrDate = state.LastEgfrDate,
            LastAcrValue = state.LastAcrMgPerGram,
            LastAcrDate = state.LastAcrDate,
            LastFootExamDate = state.LastFootExamDate,
            LastEyeExamDate = state.LastEyeExamDate,
        };
    }

    public static DiabetesPreVisitPlan BuildPreVisitPlan(DiabetesRegistryState state, DateTime visitDate)
    {
        var snapshot = BuildSnapshot(state, visitDate);
        var plan = new DiabetesPreVisitPlan
        {
            Icn = state.Icn,
            VisitDate = visitDate,
            Snapshot = snapshot,
        };

        // HbA1c: due if last test > 6 months ago; overdue if > 12 months or never.
        AddHbA1cItems(plan, state, visitDate);

        AddExamItem(plan, "Annual diabetic foot exam", snapshot.FootExamStatus, snapshot.LastFootExamDate, visitDate);
        AddExamItem(plan, "Annual dilated retinal eye exam", snapshot.EyeExamStatus, snapshot.LastEyeExamDate, visitDate);
        AddExamItem(plan, "Annual urine albumin/creatinine ratio (nephropathy screen)",
                    snapshot.AcrStatus, snapshot.LastAcrDate, visitDate);

        // Poor control gets surfaced regardless of recency.
        if (snapshot.HbA1cControl == HbA1cControlStatus.Poor)
            plan.ItemsOverdue.Add(
                $"HbA1c poor control: last value {snapshot.LastHbA1cValue:0.0}% (≥9.0). " +
                "Discuss intensification of therapy.");

        // Reduced/severe kidney function — surface as needing follow-up.
        if (snapshot.KidneyFunction == KidneyFunctionStatus.Severe)
            plan.ItemsOverdue.Add(
                $"Severe CKD: last eGFR {snapshot.LastEgfrValue:0} (< 30). Refer to nephrology if not already.");
        else if (snapshot.KidneyFunction == KidneyFunctionStatus.Reduced)
            plan.ItemsDue.Add(
                $"Reduced kidney function: last eGFR {snapshot.LastEgfrValue:0} (CKD G3). " +
                "Monitor and review medication renal dosing.");

        return plan;
    }

    // ── Classifiers ──────────────────────────────────────────────────────

    public static HbA1cControlStatus ClassifyHbA1c(decimal? lastValue)
    {
        if (lastValue is null) return HbA1cControlStatus.NoData;
        if (lastValue < HbA1cGoodThreshold) return HbA1cControlStatus.Good;
        if (lastValue >= HbA1cPoorThreshold) return HbA1cControlStatus.Poor;
        return HbA1cControlStatus.AtTarget;
    }

    public static DueStatus ClassifyAnnualExam(DateTime? lastExamDate, DateTime asOf)
    {
        if (lastExamDate is null) return DueStatus.NoData;
        int months = MonthsBetween(lastExamDate.Value, asOf);
        if (months <= AnnualExamDueAfterMonths) return DueStatus.UpToDate;
        if (months <= AnnualExamOverdueAfterMonths) return DueStatus.Due;
        return DueStatus.Overdue;
    }

    public static KidneyFunctionStatus ClassifyKidneyFunction(decimal? lastEgfr)
    {
        if (lastEgfr is null) return KidneyFunctionStatus.NoData;
        if (lastEgfr >= EgfrNormalThreshold) return KidneyFunctionStatus.Normal;
        if (lastEgfr >= EgfrSevereThreshold) return KidneyFunctionStatus.Reduced;
        return KidneyFunctionStatus.Severe;
    }

    // ── Pre-visit plan helpers ───────────────────────────────────────────

    private static void AddHbA1cItems(DiabetesPreVisitPlan plan, DiabetesRegistryState state, DateTime visitDate)
    {
        if (state.HbA1cHistory.Count == 0)
        {
            plan.ItemsOverdue.Add("HbA1c never recorded; order baseline test.");
            return;
        }
        DateTime last = state.HbA1cHistory[^1].DateOfTest;
        int months = MonthsBetween(last, visitDate);
        if (months <= 6) plan.ItemsUpToDate.Add($"HbA1c up to date (last test {months} mo ago).");
        else if (months <= 12) plan.ItemsDue.Add($"HbA1c due (last test {months} mo ago; standard interval 3–6 mo).");
        else plan.ItemsOverdue.Add($"HbA1c overdue (last test {months} mo ago).");
    }

    private static void AddExamItem(
        DiabetesPreVisitPlan plan, string label, DueStatus status,
        DateTime? lastDate, DateTime visitDate)
    {
        switch (status)
        {
            case DueStatus.NoData:
                plan.ItemsOverdue.Add($"{label} never recorded; schedule.");
                break;
            case DueStatus.Due:
                plan.ItemsDue.Add(
                    $"{label} due (last performed {MonthsBetween(lastDate!.Value, visitDate)} mo ago).");
                break;
            case DueStatus.Overdue:
                plan.ItemsOverdue.Add(
                    $"{label} overdue (last performed {MonthsBetween(lastDate!.Value, visitDate)} mo ago).");
                break;
            case DueStatus.UpToDate:
                plan.ItemsUpToDate.Add(
                    $"{label} up to date (last performed {MonthsBetween(lastDate!.Value, visitDate)} mo ago).");
                break;
        }
    }

    /// <summary>Whole-month difference between two dates (asOf - earlier), clamped at 0.</summary>
    private static int MonthsBetween(DateTime earlier, DateTime asOf)
    {
        if (asOf < earlier) return 0;
        int months = ((asOf.Year - earlier.Year) * 12) + (asOf.Month - earlier.Month);
        if (asOf.Day < earlier.Day) months--;     // not yet a full month
        return Math.Max(0, months);
    }
}
