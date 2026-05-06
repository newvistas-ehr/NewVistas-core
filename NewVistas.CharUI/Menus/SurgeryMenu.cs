// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Surgery menu — mirrors VistA Surgery file (#130).
/// </summary>
public class SurgeryMenu : IMenu
{
    public string Title => "Surgery";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Surgery", [
            ("1", "List Surgeries", ListSurgeriesAsync),
            ("2", "Schedule Surgery", ScheduleAsync),
            ("3", "Complete Surgery", CompleteAsync),
            ("4", "Cancel Surgery", CancelAsync),
        ], ctx);
    }

    private static async Task ListSurgeriesAsync(MenuContext ctx)
    {
        List<SurgerySummary> surgeries = await ctx.GetWorkflow().GetSurgeriesAsync(50);
        TerminalIO.WriteHeader("Surgeries");
        if (surgeries.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Procedure", "Date", "Surgeon", "Status"],
                [4, 28, 12, 18, 12],
                surgeries.Select((s, i) => new[]
                {
                    (i + 1).ToString(), s.PrincipalProcedure,
                    s.DateOfOperation.ToString("MM/dd/yyyy"),
                    s.SurgeonName ?? "", s.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ScheduleAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Schedule Surgery");

        string procedure = TerminalIO.Prompt("Principal Procedure");
        if (string.IsNullOrWhiteSpace(procedure)) return;

        string cpt = TerminalIO.Prompt("CPT Code (optional)");
        DateTime? dateOp = TerminalIO.PromptDate("Date of Operation");
        if (!dateOp.HasValue) return;

        string surgeon = TerminalIO.Prompt("Surgeon Name");
        string anesthesia = TerminalIO.Prompt("Anesthesia Technique (General, Spinal, Local, MAC)", "General");
        string specialty = TerminalIO.Prompt("Surgical Specialty (optional)");
        string preOpDx = TerminalIO.Prompt("Pre-Op Diagnosis (optional)");
        string comments = TerminalIO.Prompt("Comments (optional)");

        if (!TerminalIO.PromptYesNo("Schedule this surgery?")) return;

        string id = await ctx.GetWorkflow().ScheduleSurgeryAsync(
            procedure,
            string.IsNullOrWhiteSpace(cpt) ? null : cpt,
            dateOp.Value,
            ctx.Session.UserId, string.IsNullOrWhiteSpace(surgeon) ? ctx.Session.UserName : surgeon,
            anesthesia,
            string.IsNullOrWhiteSpace(specialty) ? null : specialty,
            string.IsNullOrWhiteSpace(preOpDx) ? null : preOpDx,
            null, null,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Surgery scheduled: {id}");
        TerminalIO.Pause();
    }

    private static async Task CompleteAsync(MenuContext ctx)
    {
        List<SurgerySummary> surgeries = await ctx.GetWorkflow().GetSurgeriesAsync(50);
        List<SurgerySummary> active = surgeries.Where(s =>
            !s.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) &&
            !s.Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase)).ToList();

        if (active.Count == 0)
        {
            TerminalIO.WriteLine("  No scheduled surgeries to complete.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Complete Surgery");
        TerminalIO.WriteNumberedList(active,
            s => $"{s.PrincipalProcedure,-28} {s.DateOfOperation:MM/dd/yyyy}  {s.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select surgery (1-{active.Count})", 1, active.Count);
        if (!choice.HasValue) return;

        SurgerySummary selected = active[choice.Value - 1];
        string opReport = TerminalIO.PromptMultiline("Operative Report:");
        string postOpDx = TerminalIO.Prompt("Post-Op Diagnosis (optional)");

        if (!TerminalIO.PromptYesNo("Complete this surgery?")) return;

        await ctx.GetWorkflow().CompleteSurgeryAsync(
            selected.SurgeryId,
            string.IsNullOrWhiteSpace(opReport) ? null : opReport,
            string.IsNullOrWhiteSpace(postOpDx) ? null : postOpDx);

        TerminalIO.WriteLine("  Surgery completed.");
        TerminalIO.Pause();
    }

    private static async Task CancelAsync(MenuContext ctx)
    {
        List<SurgerySummary> surgeries = await ctx.GetWorkflow().GetSurgeriesAsync(50);
        List<SurgerySummary> scheduled = surgeries.Where(s =>
            s.Status.Equals("SCHEDULED", StringComparison.OrdinalIgnoreCase) ||
            s.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase)).ToList();

        if (scheduled.Count == 0)
        {
            TerminalIO.WriteLine("  No scheduled surgeries to cancel.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Cancel Surgery");
        TerminalIO.WriteNumberedList(scheduled,
            s => $"{s.PrincipalProcedure,-28} {s.DateOfOperation:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select surgery (1-{scheduled.Count})", 1, scheduled.Count);
        if (!choice.HasValue) return;

        string reason = TerminalIO.Prompt("Reason for cancellation");
        if (!TerminalIO.PromptYesNo("Cancel this surgery?")) return;

        await ctx.GetWorkflow().CancelSurgeryAsync(
            scheduled[choice.Value - 1].SurgeryId, reason);

        TerminalIO.WriteLine("  Surgery cancelled.");
        TerminalIO.Pause();
    }
}
