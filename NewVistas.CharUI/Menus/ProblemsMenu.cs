// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Problem List menu — mirrors VistA GMPLSAVE.m / GMPLEDIT.m.
/// List active/all problems, add new, inactivate existing.
/// </summary>
public class ProblemsMenu : IMenu
{
    public string Title => "Problem List";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Problem List", [
            ("1", "List Active Problems", ListActiveAsync),
            ("2", "List All Problems", ListAllAsync),
            ("3", "Add New Problem", AddProblemAsync),
            ("4", "Inactivate Problem", InactivateProblemAsync),
        ], ctx);
    }

    private static async Task ListActiveAsync(MenuContext ctx)
    {
        List<ProblemSummary> problems = await ctx.GetWorkflow().GetActiveProblemsAsync();
        TerminalIO.WriteHeader("Active Problems");
        TerminalIO.WriteTable(
            ["#", "ICD", "Problem", "Status", "Onset"],
            [4, 8, 34, 10, 12],
            problems.Select((p, i) => new[]
            {
                (i + 1).ToString(), p.DiagnosisCode ?? "", p.Diagnosis,
                p.Status, p.DateOfOnset?.ToString("MM/dd/yyyy") ?? ""
            }));
        TerminalIO.Pause();
    }

    private static async Task ListAllAsync(MenuContext ctx)
    {
        List<ProblemSummary> problems = await ctx.GetWorkflow().GetAllProblemsAsync();
        TerminalIO.WriteHeader("All Problems");
        TerminalIO.WriteTable(
            ["#", "ICD", "Problem", "Status", "Onset"],
            [4, 8, 34, 10, 12],
            problems.Select((p, i) => new[]
            {
                (i + 1).ToString(), p.DiagnosisCode ?? "", p.Diagnosis,
                p.Status, p.DateOfOnset?.ToString("MM/dd/yyyy") ?? ""
            }));
        TerminalIO.Pause();
    }

    private static async Task AddProblemAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Add New Problem");

        string diagnosis = TerminalIO.Prompt("Diagnosis");
        if (string.IsNullOrWhiteSpace(diagnosis)) return;

        string code = TerminalIO.Prompt("ICD-10 Code (optional)");
        string priority = TerminalIO.Prompt("Priority (A=Acute, C=Chronic)", "C");
        DateTime? onset = TerminalIO.PromptDate("Date of Onset", DateTime.Today);
        bool sc = TerminalIO.PromptYesNo("Service Connected?", false);
        string comments = TerminalIO.Prompt("Comments (optional)");

        if (!TerminalIO.PromptYesNo("Save this problem?")) return;

        string id = await ctx.GetWorkflow().AddProblemAsync(
            diagnosis,
            string.IsNullOrWhiteSpace(code) ? null : code,
            null, priority, onset,
            ctx.Session.UserId, ctx.Session.UserName,
            null, null, sc,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Problem added: {id}");
        TerminalIO.Pause();
    }

    private static async Task InactivateProblemAsync(MenuContext ctx)
    {
        List<ProblemSummary> problems = await ctx.GetWorkflow().GetActiveProblemsAsync();
        if (problems.Count == 0)
        {
            TerminalIO.WriteLine("  No active problems to inactivate.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Inactivate Problem");
        TerminalIO.WriteNumberedList(problems, p => $"{p.DiagnosisCode ?? "---",-8} {p.Diagnosis}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select problem (1-{problems.Count})", 1, problems.Count);
        if (!choice.HasValue) return;

        ProblemSummary selected = problems[choice.Value - 1];
        DateTime? resolved = TerminalIO.PromptDate("Date Resolved", DateTime.Today);
        if (!resolved.HasValue) return;

        if (!TerminalIO.PromptYesNo($"Inactivate '{selected.Diagnosis}'?")) return;

        await ctx.GetWorkflow().InactivateProblemAsync(selected.ProblemId, resolved.Value);
        TerminalIO.WriteLine("  Problem inactivated.");
        TerminalIO.Pause();
    }
}
