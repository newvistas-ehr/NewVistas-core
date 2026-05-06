// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Radiology menu — mirrors VistA Radiology file (#75.1).
/// </summary>
public class RadiologyMenu : IMenu
{
    public string Title => "Radiology";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Radiology", [
            ("1", "List Radiology Studies", ListStudiesAsync),
            ("2", "View Study Detail", ViewDetailAsync),
            ("3", "Order Radiology Study", OrderStudyAsync),
        ], ctx);
    }

    private static async Task ListStudiesAsync(MenuContext ctx)
    {
        List<RadiologySummary> studies = await ctx.GetWorkflow().GetRadiologyStudiesAsync(50);
        TerminalIO.WriteHeader("Radiology Studies");
        if (studies.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Procedure", "Type", "Status", "Date", "Provider"],
                [4, 24, 8, 10, 12, 16],
                studies.Select((s, i) => new[]
                {
                    (i + 1).ToString(), s.ProcedureName, s.ImagingType ?? "",
                    s.Status, s.ExamDateTime?.ToString("MM/dd/yyyy") ?? "",
                    s.RequestingProviderName ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewDetailAsync(MenuContext ctx)
    {
        List<RadiologySummary> studies = await ctx.GetWorkflow().GetRadiologyStudiesAsync(50);
        if (studies.Count == 0)
        {
            TerminalIO.WriteLine("  No radiology studies on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("View Radiology Study");
        TerminalIO.WriteNumberedList(studies,
            s => $"{s.ProcedureName,-24} {s.Status,-10} {s.ExamDateTime?.ToString("MM/dd/yyyy") ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select study (1-{studies.Count})", 1, studies.Count);
        if (!choice.HasValue) return;

        RadiologySummary selected = studies[choice.Value - 1];
        RadiologyState detail = await ctx.GetWorkflow().GetRadiologyStudyAsync(selected.RadiologyId);

        TerminalIO.Clear();
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Procedure", detail.ProcedureName);
        TerminalIO.WriteDetail("Imaging Type", detail.ImagingType);
        TerminalIO.WriteDetail("CPT Code", detail.CptCode);
        TerminalIO.WriteDetail("Status", detail.Status);
        TerminalIO.WriteDetail("Exam Date", detail.ExamDateTime?.ToString("MM/dd/yyyy HH:mm"));
        TerminalIO.WriteDetail("Requesting Provider", detail.RequestingProviderName);
        TerminalIO.WriteDetail("Clinical History", detail.ClinicalHistory);
        TerminalIO.WriteDetail("Urgency", detail.Urgency);
        TerminalIO.WriteDivider('-');

        if (!string.IsNullOrWhiteSpace(detail.ReportText))
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("Report:");
            TerminalIO.WriteLine(detail.ReportText);
        }

        if (!string.IsNullOrWhiteSpace(detail.Impression))
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("Impression:");
            TerminalIO.WriteLine(detail.Impression);
        }

        TerminalIO.Pause();
    }

    private static async Task OrderStudyAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Order Radiology Study");

        string procedure = TerminalIO.Prompt("Procedure Name (e.g., CXR PA & Lateral)");
        if (string.IsNullOrWhiteSpace(procedure)) return;

        string cpt = TerminalIO.Prompt("CPT Code (optional)");
        string imagingType = TerminalIO.Prompt("Imaging Type (XR, CT, MRI, US, NM)", "XR");
        string urgency = TerminalIO.Prompt("Urgency (Routine, STAT, ASAP)", "Routine");
        string history = TerminalIO.Prompt("Clinical History");
        string reason = TerminalIO.Prompt("Reason for Study");

        if (!TerminalIO.PromptYesNo("Order this radiology study?")) return;

        string id = await ctx.GetWorkflow().OrderRadiologyStudyAsync(
            procedure, null,
            string.IsNullOrWhiteSpace(cpt) ? null : cpt,
            imagingType,
            ctx.Session.UserId, ctx.Session.UserName,
            urgency,
            string.IsNullOrWhiteSpace(history) ? null : history,
            string.IsNullOrWhiteSpace(reason) ? null : reason,
            null, null, null);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Radiology study ordered: {id}");
        TerminalIO.Pause();
    }
}
