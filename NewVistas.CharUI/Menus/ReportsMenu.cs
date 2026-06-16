// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Reports menu — mirrors VistA ORWRP.m and CPRS Reports tab (fReports.pas).
/// Provides read-only access to patient data across multiple clinical domains.
/// </summary>
public class ReportsMenu : IMenu
{
    public string Title => "Reports";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Reports", [
            ("1",  "Health Summary (Cover Sheet)", HealthSummaryAsync),
            ("2",  "Problem List", ProblemReportAsync),
            ("3",  "Allergies Report", AllergyReportAsync),
            ("4",  "Active Medications", MedReportAsync),
            ("5",  "Lab Summary", LabSummaryReportAsync),
            ("6",  "Lab Results (Abnormal)", AbnormalLabsAsync),
            ("7",  "Vital Signs", VitalsReportAsync),
            ("8",  "Active Orders", OrderReportAsync),
            ("9",  "Consults", ConsultReportAsync),
            ("10", "Clinical Notes", NotesReportAsync),
            ("11", "Appointments", AppointmentReportAsync),
            ("12", "ADT Movements", AdtReportAsync),
            ("13", "Radiology", RadiologyReportAsync),
            ("14", "Surgery", SurgeryReportAsync),
        ], ctx);
    }

    private static async Task HealthSummaryAsync(MenuContext ctx)
    {
        CoverSheetState cs = await ctx.GetWorkflow().GetCoverSheetAsync();

        TerminalIO.Clear();
        TerminalIO.WritePatientBanner(ctx.Session, ctx.Patient);
        TerminalIO.WriteHeader("HEALTH SUMMARY REPORT");

        TerminalIO.WriteLine($"  Active Problems:     {cs.ActiveProblems.Count}");
        TerminalIO.WriteLine($"  Allergies:           {(cs.Allergies.Count == 0 ? "NKA" : cs.Allergies.Count.ToString())}");
        TerminalIO.WriteLine($"  Active Medications:  {cs.ActiveMedications.Count}");
        TerminalIO.WriteLine($"  Recent Vitals:       {cs.RecentVitals.Count}");
        TerminalIO.WriteLine($"  Active Orders:       {cs.ActiveOrders.Count}");
        TerminalIO.WriteLine($"  Recent Notes:        {cs.RecentNotes.Count}");
        TerminalIO.WriteLine($"  Active Consults:     {cs.ActiveConsults.Count}");
        TerminalIO.WriteLine($"  Upcoming Appts:      {cs.RecentVisits.Count}");
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  CWAD: {(string.IsNullOrEmpty(ctx.Patient.CwadFlags) ? "(none)" : ctx.Patient.CwadFlags)}");
        TerminalIO.WriteLine($"  SC:   {(ctx.Patient.IsServiceConnected ? $"Yes ({ctx.Patient.ServiceConnectedPercent}%)" : "No")}");

        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task ProblemReportAsync(MenuContext ctx)
    {
        List<ProblemSummary> problems = await ctx.GetWorkflow().GetAllProblemsAsync();
        TerminalIO.WriteHeader("Problem List Report");
        if (problems.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "ICD", "Problem", "Status", "Onset", "SC"],
                [4, 8, 28, 10, 12, 4],
                problems.Select((p, i) => new[]
                {
                    (i + 1).ToString(), p.DiagnosisCode ?? "", p.Diagnosis,
                    p.Status, p.DateOfOnset?.ToString("MM/dd/yyyy") ?? "",
                    p.IsServiceConnected ? "Y" : ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task AllergyReportAsync(MenuContext ctx)
    {
        List<AllergySummary> allergies = await ctx.GetWorkflow().GetAllergiesAsync();
        TerminalIO.WriteHeader("Allergies/ADR Report");
        if (allergies.Count == 0) { TerminalIO.WriteLine("  No Known Allergies (NKA)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Allergen", "Type", "Severity", "Reactions"],
                [4, 20, 8, 10, 30],
                allergies.Select((a, i) => new[]
                {
                    (i + 1).ToString(), a.Allergen, a.AllergenType ?? "",
                    a.Severity ?? "", string.Join(", ", a.Reactions)
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task MedReportAsync(MenuContext ctx)
    {
        List<MedicationSummary> meds = await ctx.GetWorkflow().GetActiveMedicationsAsync();
        TerminalIO.WriteHeader("Active Medications Report");
        if (meds.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Drug", "Sig", "Status", "Fill Date"],
                [4, 26, 22, 10, 12],
                meds.Select((m, i) => new[]
                {
                    (i + 1).ToString(), m.DrugName, m.Sig ?? "", m.Status,
                    m.FillDate?.ToString("MM/dd/yyyy") ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task LabSummaryReportAsync(MenuContext ctx)
    {
        List<LabTestSummaryEntry> labs = await ctx.GetWorkflow().GetLabSummaryAsync();
        TerminalIO.WriteHeader("Lab Summary Report");
        if (labs.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Test", "Result", "Units", "Flag", "Date"],
                [4, 18, 10, 8, 8, 12],
                labs.Select((l, i) => new[]
                {
                    (i + 1).ToString(), l.TestName, l.Value ?? "",
                    l.Units ?? "", l.AbnormalFlag.ToString(),
                    l.ResultDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task AbnormalLabsAsync(MenuContext ctx)
    {
        List<LabTestSummaryEntry> labs = await ctx.GetWorkflow().GetAbnormalLabResultsAsync();
        TerminalIO.WriteHeader("Abnormal Lab Results");
        if (labs.Count == 0) { TerminalIO.WriteLine("  No abnormal results."); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Test", "Result", "Units", "Flag", "Date"],
                [4, 18, 10, 8, 8, 12],
                labs.Select((l, i) => new[]
                {
                    (i + 1).ToString(), l.TestName, l.Value ?? "",
                    l.Units ?? "", l.AbnormalFlag.ToString(),
                    l.ResultDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task VitalsReportAsync(MenuContext ctx)
    {
        List<VitalSummary> vitals = await ctx.GetWorkflow().GetLatestVitalsAsync();
        TerminalIO.WriteHeader("Vital Signs Report");
        if (vitals.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            foreach (VitalSummary v in vitals)
            {
                string flag = !string.IsNullOrEmpty(v.AbnormalFlag) ? $" *{v.AbnormalFlag}*" : "";
                TerminalIO.WriteLine($"  {v.VitalType,-18} {v.Value,8} {v.Units ?? "",-6}{flag}   {v.DateTimeTaken:MM/dd/yyyy HH:mm}");
            }
        }
        TerminalIO.Pause();
    }

    private static async Task OrderReportAsync(MenuContext ctx)
    {
        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(2);
        TerminalIO.WriteHeader("Active Orders Report");
        if (orders.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Order", "Type", "Status", "Date"],
                [4, 28, 10, 10, 12],
                orders.Select((o, i) => new[]
                {
                    (i + 1).ToString(), o.OrderText, o.OrderType, o.Status,
                    o.StartDate?.ToString("MM/dd/yyyy") ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ConsultReportAsync(MenuContext ctx)
    {
        List<ConsultSummary> consults = await ctx.GetWorkflow().GetConsultsAsync(null, 50);
        TerminalIO.WriteHeader("Consults Report");
        if (consults.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "To Service", "Status", "Urgency", "Date"],
                [4, 22, 12, 10, 12],
                consults.Select((c, i) => new[]
                {
                    (i + 1).ToString(), c.ToService, c.Status, c.Urgency,
                    c.RequestDateTime.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task NotesReportAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 100);
        TerminalIO.WriteHeader("Clinical Notes Report");
        if (notes.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WritePagedList(notes,
                n => $"{n.ReferenceDate:MM/dd/yyyy}  {n.DocumentType,-16} {n.AuthorName ?? "",-16} {n.Status}");
        }
        TerminalIO.Pause();
    }

    private static async Task AppointmentReportAsync(MenuContext ctx)
    {
        List<AppointmentEntry> appts = await ctx.GetWorkflow().GetAllAppointmentsAsync();
        TerminalIO.WriteHeader("Appointments Report");
        if (appts.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Date/Time", "Clinic", "Status"],
                [4, 18, 24, 12],
                appts.Select((a, i) => new[]
                {
                    (i + 1).ToString(),
                    a.AppointmentDateTime.ToString("MM/dd/yyyy HH:mm"),
                    a.ClinicName ?? "", a.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task AdtReportAsync(MenuContext ctx)
    {
        List<AdtSummary> movements = await ctx.GetWorkflow().GetAdtMovementsAsync();
        TerminalIO.WriteHeader("ADT Movements Report");
        if (movements.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Type", "Date", "Ward", "Status"],
                [4, 12, 18, 16, 12],
                movements.Select((m, i) => new[]
                {
                    (i + 1).ToString(), m.MovementType,
                    m.MovementDateTime.ToString("MM/dd/yyyy HH:mm"),
                    m.WardLocationName ?? "", m.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task RadiologyReportAsync(MenuContext ctx)
    {
        List<RadiologySummary> studies = await ctx.GetWorkflow().GetRadiologyStudiesAsync(50);
        TerminalIO.WriteHeader("Radiology Report");
        if (studies.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Procedure", "Status", "Date"],
                [4, 30, 14, 12],
                studies.Select((r, i) => new[]
                {
                    (i + 1).ToString(), r.ProcedureName, r.Status,
                    r.ExamDateTime?.ToString("MM/dd/yyyy") ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task SurgeryReportAsync(MenuContext ctx)
    {
        List<SurgerySummary> surgeries = await ctx.GetWorkflow().GetSurgeriesAsync(50);
        TerminalIO.WriteHeader("Surgery Report");
        if (surgeries.Count == 0) { TerminalIO.WriteLine("  (none)"); }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Procedure", "Surgeon", "Status", "Date"],
                [4, 24, 16, 12, 12],
                surgeries.Select((s, i) => new[]
                {
                    (i + 1).ToString(), s.PrincipalProcedure, s.SurgeonName ?? "",
                    s.Status, s.DateOfOperation.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }
}
