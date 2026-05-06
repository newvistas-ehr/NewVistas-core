// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Cover sheet display — mirrors VistA CPRS cover sheet (ORWCV.m POLL sections).
/// Shows a unified view of active problems, allergies, meds, vitals, orders,
/// notes, consults, and upcoming visits.
/// </summary>
public class CoverSheetMenu : IMenu
{
    public string Title => "Cover Sheet";

    public async Task RunAsync(MenuContext ctx)
    {
        if (!ctx.Patient.HasPatient)
        {
            TerminalIO.WriteLine("No patient selected.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.Clear();
        TerminalIO.WritePatientBanner(ctx.Session, ctx.Patient);

        CoverSheetState cs = await ctx.GetWorkflow().GetCoverSheetAsync();
        ctx.Patient.SetFromCoverSheet(cs);

        // Active Problems
        TerminalIO.WriteHeader("Active Problems");
        if (cs.ActiveProblems.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
            TerminalIO.WriteTable(
                ["", "ICD", "Problem", "Status", "Onset"],
                [4, 8, 36, 8, 12],
                cs.ActiveProblems.Select((p, i) => new[]
                {
                    (i + 1).ToString(),
                    p.DiagnosisCode ?? "",
                    p.Diagnosis,
                    TerminalColor.Status(p.Status),
                    p.DateOfOnset?.ToString("MM/dd/yyyy") ?? ""
                }));

        // Allergies
        TerminalIO.WriteHeader("Allergies");
        if (cs.Allergies.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Green("  No Known Allergies"));
        else
            TerminalIO.WriteTable(
                ["", "Allergen", "Severity", "Reactions"],
                [4, 22, 12, 36],
                cs.Allergies.Select((a, i) => new[]
                {
                    (i + 1).ToString(),
                    a.Allergen,
                    TerminalColor.Severity(a.Severity),
                    string.Join(", ", a.Reactions)
                }));

        // Active Medications
        TerminalIO.WriteHeader("Active Medications");
        if (cs.ActiveMedications.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
            TerminalIO.WriteTable(
                ["", "Drug", "Sig", "Status"],
                [4, 28, 30, 10],
                cs.ActiveMedications.Select((m, i) => new[]
                {
                    (i + 1).ToString(),
                    m.DrugName,
                    m.Sig ?? "",
                    TerminalColor.Status(m.Status)
                }));

        // Recent Vitals
        TerminalIO.WriteHeader("Recent Vitals");
        if (cs.RecentVitals.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
        {
            foreach (VitalSummary v in cs.RecentVitals)
            {
                string flag = TerminalColor.AbnormalFlag(v.AbnormalFlag);
                TerminalIO.WriteLine($"  {v.VitalType,-16} {v.Value} {v.Units ?? ""}{(flag.Length > 0 ? " " : "")}{flag}   ({v.DateTimeTaken:MM/dd/yyyy HH:mm})");
            }
        }

        // Active Orders
        TerminalIO.WriteHeader("Active Orders");
        if (cs.ActiveOrders.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
            TerminalIO.WriteTable(
                ["", "Order", "Type", "Status", "Date"],
                [4, 32, 10, 10, 12],
                cs.ActiveOrders.Select((o, i) => new[]
                {
                    (i + 1).ToString(),
                    o.OrderText,
                    o.OrderType,
                    TerminalColor.Status(o.Status),
                    o.StartDate?.ToString("MM/dd/yyyy") ?? ""
                }));

        // Recent Notes
        TerminalIO.WriteHeader("Recent Notes");
        if (cs.RecentNotes.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
            TerminalIO.WriteTable(
                ["", "Type", "Author", "Status", "Date"],
                [4, 22, 18, 12, 12],
                cs.RecentNotes.Select((n, i) => new[]
                {
                    (i + 1).ToString(),
                    n.DocumentType,
                    n.AuthorName ?? "",
                    TerminalColor.Status(n.Status),
                    n.ReferenceDate.ToString("MM/dd/yyyy")
                }));

        // Active Consults
        TerminalIO.WriteHeader("Active Consults");
        if (cs.ActiveConsults.Count == 0)
            TerminalIO.WriteLine(TerminalColor.Dim("  (none)"));
        else
            TerminalIO.WriteTable(
                ["", "To Service", "Status", "Urgency", "Date"],
                [4, 24, 12, 10, 12],
                cs.ActiveConsults.Select((c, i) => new[]
                {
                    (i + 1).ToString(),
                    c.ToService,
                    TerminalColor.Status(c.Status),
                    TerminalColor.Urgency(c.Urgency),
                    c.RequestDateTime.ToString("MM/dd/yyyy")
                }));

        // Upcoming Visits
        TerminalIO.WriteHeader("Upcoming Appointments");
        if (cs.RecentVisits.Count == 0)
            TerminalIO.WriteLine("  (none)");
        else
            TerminalIO.WriteTable(
                ["", "Date/Time", "Clinic", "Provider", "Status"],
                [4, 18, 22, 18, 10],
                cs.RecentVisits.Select((v, i) => new[]
                {
                    (i + 1).ToString(),
                    v.AppointmentDateTime.ToString("MM/dd/yyyy HH:mm"),
                    v.ClinicName ?? "",
                    v.ProviderName ?? "",
                    v.Status
                }));

        TerminalIO.Pause();
    }
}
