// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Labs menu — mirrors VistA LRWU.m / LRFN.m lab ordering and results.
/// </summary>
public class LabsMenu : IMenu
{
    public string Title => "Labs";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Laboratory", [
            ("1", "View Lab Results", ViewResultsAsync),
            ("2", "View Lab Summary", ViewSummaryAsync),
            ("3", "View Abnormal Results", ViewAbnormalAsync),
            ("4", "Order Lab Test", OrderLabAsync),
        ], ctx);
    }

    private static async Task ViewResultsAsync(MenuContext ctx)
    {
        List<LabResultSummary> results = await ctx.GetWorkflow().GetLabResultsAsync();
        TerminalIO.WriteHeader("Lab Results");
        if (results.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Test", "Result", "Units", "Flag", "Status", "Date"],
                [4, 20, 10, 8, 5, 8, 12],
                results.Select((r, i) => new[]
                {
                    (i + 1).ToString(), r.TestName, r.ResultValue ?? "",
                    r.Units ?? "", TerminalColor.AbnormalFlag(r.Flag),
                    TerminalColor.Status(r.Status),
                    r.CollectionDate?.ToString("MM/dd/yyyy") ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewSummaryAsync(MenuContext ctx)
    {
        List<LabTestSummaryEntry> summary = await ctx.GetWorkflow().GetLabSummaryAsync();
        TerminalIO.WriteHeader("Lab Summary (Most Recent per Test)");
        if (summary.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Test", "Result", "Units", "Flag", "Date"],
                [4, 22, 12, 8, 6, 12],
                summary.Select((s, i) => new[]
                {
                    (i + 1).ToString(), s.TestName, s.Value,
                    s.Units, TerminalColor.AbnormalFlag(s.AbnormalFlag.ToString()),
                    s.ResultDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewAbnormalAsync(MenuContext ctx)
    {
        List<LabTestSummaryEntry> abnormal = await ctx.GetWorkflow().GetAbnormalLabResultsAsync();
        TerminalIO.WriteHeader("Abnormal Lab Results");
        if (abnormal.Count == 0)
        {
            TerminalIO.WriteLine("  No abnormal results.");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Test", "Result", "Units", "Flag", "Date"],
                [4, 22, 12, 8, 6, 12],
                abnormal.Select((s, i) => new[]
                {
                    (i + 1).ToString(), s.TestName, s.Value,
                    s.Units, TerminalColor.AbnormalFlag(s.AbnormalFlag.ToString()),
                    s.ResultDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task OrderLabAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Order Lab Test");

        string testName = TerminalIO.Prompt("Test Name");
        if (string.IsNullOrWhiteSpace(testName)) return;

        string testCode = TerminalIO.Prompt("LOINC/Test Code (optional)");
        string specimenType = TerminalIO.Prompt("Specimen Type (optional)");
        string category = TerminalIO.Prompt("Category (Chemistry, Hematology, Micro, Other)", "Chemistry");

        if (!TerminalIO.PromptYesNo("Order this lab test?")) return;

        string id = await ctx.GetWorkflow().OrderLabTestAsync(
            null!, testName,
            string.IsNullOrWhiteSpace(testCode) ? null : testCode,
            null, ctx.Session.UserId, ctx.Session.UserName,
            string.IsNullOrWhiteSpace(specimenType) ? null : specimenType,
            category);

        TerminalIO.WriteBlank();
        TerminalIO.WriteSuccess($"Lab test ordered: {id}");
        TerminalIO.Pause();
    }
}
