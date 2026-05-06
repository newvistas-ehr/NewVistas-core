// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Cover sheet ViewModel — drives the 8-panel CPRS cover sheet (fCover.pas)
/// plus an optional 9th "Diabetes Registry" panel (snapshot + pre-visit plan)
/// that appears only when the patient is enrolled in the diabetes registry.
///
/// CPRS panels mirror lst_1..lst_8 in fCover.dfm:
///   1=Active Problems  2=Allergies/ADR  3=Patient Record Flags + Postings
///   4=Pending Orders   5=Upcoming Appts 6=Vital Signs (Latest)
///   7=Recent Labs      8=Clinical Reminders
///
/// Diabetes panel (HasDiabetesRegistry) — IHS / tribal deployments:
///   HbA1c control + last value, foot/eye/ACR exam status, kidney function,
///   plus pre-visit plan items due/overdue at today's date.
/// </summary>
public sealed partial class CoverSheetViewModel : ChartTabViewModelBase
{
    // Panel 1 — Active Problems
    public ObservableCollection<ProblemDto>     Problems    { get; } = new();

    // Panel 2 — Allergies/ADR
    public ObservableCollection<AllergyDto>     Allergies   { get; } = new();

    // Panel 3 — Patient Record Flags (maroon text, lstFlag)
    public ObservableCollection<string>         Flags       { get; } = new();

    // Panel 4 — Pending Orders
    public ObservableCollection<OrderDto>       Orders      { get; } = new();

    // Panel 5 — Upcoming Appointments
    public ObservableCollection<AppointmentDto> Appointments{ get; } = new();

    // Panel 6 — Latest Vital Signs
    public ObservableCollection<VitalSummaryDto> Vitals     { get; } = new();

    // Panel 7 — Recent Lab Results
    public ObservableCollection<LabResultDto>   Labs        { get; } = new();

    // Panel 8 — Clinical Reminders
    public ObservableCollection<ReminderDto>    Reminders   { get; } = new();

    // Panel 9 (optional) — Diabetes Registry summary + pre-visit plan
    public ObservableCollection<string> DiabetesItemsOverdue  { get; } = new();
    public ObservableCollection<string> DiabetesItemsDue      { get; } = new();
    public ObservableCollection<string> DiabetesItemsUpToDate { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiabetesRegistry))]
    [NotifyPropertyChangedFor(nameof(DiabetesHeaderLine))]
    [NotifyPropertyChangedFor(nameof(HbA1cText))]
    [NotifyPropertyChangedFor(nameof(HbA1cBrush))]
    [NotifyPropertyChangedFor(nameof(KidneyFunctionText))]
    [NotifyPropertyChangedFor(nameof(KidneyFunctionBrush))]
    [NotifyPropertyChangedFor(nameof(FootExamText))]
    [NotifyPropertyChangedFor(nameof(EyeExamText))]
    [NotifyPropertyChangedFor(nameof(AcrExamText))]
    private DiabetesRegistrySnapshotDto? _diabetesSnapshot;

    /// <summary>True only when the patient is enrolled — drives panel visibility.</summary>
    public bool HasDiabetesRegistry => DiabetesSnapshot?.IsEnrolled == true;

    public string DiabetesHeaderLine => DiabetesSnapshot?.IsEnrolled == true
        ? $"Diabetes Registry — {DiabetesSnapshot.DiabetesType ?? "Unspecified"}"
        : "Diabetes Registry";

    public string HbA1cText => DiabetesSnapshot switch
    {
        null => "—",
        { LastHbA1cValue: null } => "No HbA1c on file",
        var s => $"{s.LastHbA1cValue:0.0}%  {HbA1cLabel(s.HbA1cControl)}  ({s.LastHbA1cDate:yyyy-MM-dd})",
    };

    /// <summary>Maroon for Poor, navy for Good/AtTarget, grey otherwise.</summary>
    public string HbA1cBrush => DiabetesSnapshot?.HbA1cControl switch
    {
        3 => "#CC0000",          // Poor
        1 or 2 => "#003366",     // Good or AtTarget
        _ => "#777777",
    };

    public string KidneyFunctionText => DiabetesSnapshot switch
    {
        null => "—",
        { LastEgfrValue: null } => "No eGFR on file",
        var s => $"eGFR {s.LastEgfrValue:0}  {KidneyLabel(s.KidneyFunction)}  ({s.LastEgfrDate:yyyy-MM-dd})",
    };

    public string KidneyFunctionBrush => DiabetesSnapshot?.KidneyFunction switch
    {
        3 => "#CC0000",          // Severe
        2 => "#B85C00",          // Reduced
        1 => "#003366",          // Normal
        _ => "#777777",
    };

    public string FootExamText => ExamLine("Foot exam",
        DiabetesSnapshot?.LastFootExamDate, DiabetesSnapshot?.FootExamStatus);

    public string EyeExamText => ExamLine("Eye exam",
        DiabetesSnapshot?.LastEyeExamDate, DiabetesSnapshot?.EyeExamStatus);

    public string AcrExamText => ExamLine("ACR (nephropathy)",
        DiabetesSnapshot?.LastAcrDate, DiabetesSnapshot?.AcrStatus);

    public CoverSheetViewModel(ApiClient api, PatientContext context)
        : base(api, context) { }

    public void Refresh() => _ = ReloadAsync();

    protected override async Task LoadAsync()
    {
        var problems   = Api.GetProblemsAsync(PatientId);
        var allergies  = Api.GetAllergiesAsync(PatientId);
        var orders     = Api.GetOrdersAsync(PatientId);
        var appts      = Api.GetAppointmentsAsync(PatientId);
        var vitals     = Api.GetVitalsAsync(PatientId);
        var labs       = Api.GetAbnormalLabsAsync(PatientId);
        var reminders  = Api.GetRemindersAsync(PatientId);
        var dmSnapshot = SafeGetDiabetesSnapshotAsync();
        var dmPlan     = SafeGetDiabetesPreVisitPlanAsync();

        await Task.WhenAll(problems, allergies, orders, appts, vitals, labs, reminders,
                           dmSnapshot, dmPlan);

        Populate(Problems,     await problems);
        Populate(Allergies,    await allergies);
        Populate(Orders,       await orders);
        Populate(Appointments, await appts);
        Populate(Vitals,       await vitals);
        Populate(Labs,         await labs);
        Populate(Reminders,    await reminders);

        DiabetesSnapshot = await dmSnapshot;
        DiabetesPreVisitPlanDto? plan = await dmPlan;
        Populate(DiabetesItemsOverdue,  plan?.ItemsOverdue  ?? []);
        Populate(DiabetesItemsDue,      plan?.ItemsDue      ?? []);
        Populate(DiabetesItemsUpToDate, plan?.ItemsUpToDate ?? []);
    }

    /// <summary>
    /// Diabetes endpoints throw on 404/500; the registry is feature-flagged
    /// per site and not every patient is enrolled. Swallow errors so the
    /// panel just stays hidden rather than failing the whole cover sheet load.
    /// </summary>
    private async Task<DiabetesRegistrySnapshotDto?> SafeGetDiabetesSnapshotAsync()
    {
        try { return await Api.GetDiabetesSnapshotAsync(PatientId); }
        catch { return null; }
    }

    private async Task<DiabetesPreVisitPlanDto?> SafeGetDiabetesPreVisitPlanAsync()
    {
        try { return await Api.GetDiabetesPreVisitPlanAsync(PatientId); }
        catch { return null; }
    }

    protected override void ClearData()
    {
        Problems.Clear();
        Allergies.Clear();
        Flags.Clear();
        Orders.Clear();
        Appointments.Clear();
        Vitals.Clear();
        Labs.Clear();
        Reminders.Clear();
        DiabetesSnapshot = null;
        DiabetesItemsOverdue.Clear();
        DiabetesItemsDue.Clear();
        DiabetesItemsUpToDate.Clear();
    }

    private static void Populate<T>(ObservableCollection<T> col, IEnumerable<T> items)
    {
        col.Clear();
        foreach (T item in items) col.Add(item);
    }

    private static string HbA1cLabel(int code) => code switch
    {
        1 => "(Good)", 2 => "(At target)", 3 => "(Poor — discuss intensification)", _ => string.Empty,
    };

    private static string KidneyLabel(int code) => code switch
    {
        1 => "(Normal)", 2 => "(Reduced — CKD G3)", 3 => "(Severe — refer nephrology)", _ => string.Empty,
    };

    private static string ExamLine(string label, DateTime? lastDate, int? status) => status switch
    {
        null or 0 => $"{label}: never recorded",
        1 => $"{label}: up to date ({lastDate:yyyy-MM-dd})",
        2 => $"{label}: due ({lastDate:yyyy-MM-dd})",
        3 => $"{label}: overdue ({(lastDate.HasValue ? lastDate.Value.ToString("yyyy-MM-dd") : "never")})",
        _ => label,
    };
}
