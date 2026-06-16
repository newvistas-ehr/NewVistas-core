// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Consults menu — mirrors VistA GMRCACTM.m (File #123).
/// </summary>
public class ConsultsMenu : IMenu
{
    public string Title => "Consults";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Consults", [
            ("1", "List Active Consults", ListActiveAsync),
            ("2", "List All Consults", ListAllAsync),
            ("3", "View Consult Detail", ViewDetailAsync),
            ("4", "Request New Consult", RequestConsultAsync),
            ("5", "Complete Consult", CompleteConsultAsync),
        ], ctx);
    }

    private static async Task ListActiveAsync(MenuContext ctx)
    {
        List<ConsultSummary> consults = await ctx.GetWorkflow().GetConsultsAsync("Active", 50);
        DisplayConsults("Active Consults", consults);
    }

    private static async Task ListAllAsync(MenuContext ctx)
    {
        List<ConsultSummary> consults = await ctx.GetWorkflow().GetConsultsAsync(null, 50);
        DisplayConsults("All Consults", consults);
    }

    private static void DisplayConsults(string title, List<ConsultSummary> consults)
    {
        TerminalIO.WriteHeader(title);
        if (consults.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "To Service", "Status", "Urgency", "Date", "Requesting"],
                [4, 20, 12, 10, 12, 16],
                consults.Select((c, i) => new[]
                {
                    (i + 1).ToString(), c.ToService, c.Status, c.Urgency,
                    c.RequestDateTime.ToString("MM/dd/yyyy"),
                    c.RequestingProviderName ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewDetailAsync(MenuContext ctx)
    {
        List<ConsultSummary> consults = await ctx.GetWorkflow().GetConsultsAsync(null, 50);
        if (consults.Count == 0)
        {
            TerminalIO.WriteLine("  No consults on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("View Consult");
        TerminalIO.WriteNumberedList(consults,
            c => $"{c.ToService,-20} {c.Status,-12} {c.RequestDateTime:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select consult (1-{consults.Count})", 1, consults.Count);
        if (!choice.HasValue) return;

        ConsultSummary selected = consults[choice.Value - 1];
        ConsultState detail = await ctx.GetWorkflow().GetConsultAsync(selected.ConsultId);

        TerminalIO.Clear();
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("To Service", detail.ToService);
        TerminalIO.WriteDetail("From Service", detail.FromService);
        TerminalIO.WriteDetail("Status", detail.Status);
        TerminalIO.WriteDetail("Urgency", detail.Urgency);
        TerminalIO.WriteDetail("Date Requested", detail.RequestDateTime.ToString("MM/dd/yyyy HH:mm"));
        TerminalIO.WriteDetail("Requesting Provider", detail.RequestingProviderName);
        TerminalIO.WriteDetail("Attention", detail.AttentionProviderName);
        TerminalIO.WriteDetail("Provisional Dx", detail.ProvisionalDiagnosis);
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Reason for Request:");
        TerminalIO.WriteLine(detail.ReasonForRequest ?? "  (none)");
        TerminalIO.Pause();
    }

    private static async Task RequestConsultAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Request New Consult");

        string toService = TerminalIO.Prompt("To Service (e.g., CARDIOLOGY)");
        if (string.IsNullOrWhiteSpace(toService)) return;

        string urgency = TerminalIO.Prompt("Urgency (Routine, STAT, ASAP)", "Routine");
        string reason = TerminalIO.Prompt("Reason for Request");
        string dx = TerminalIO.Prompt("Provisional Diagnosis (optional)");
        string attention = TerminalIO.Prompt("Attention Provider (optional)");

        if (!TerminalIO.PromptYesNo("Submit this consult request?")) return;

        string id = await ctx.GetWorkflow().RequestConsultAsync(
            toService, null, null, null,
            urgency,
            ctx.Session.UserId, ctx.Session.UserName,
            string.IsNullOrWhiteSpace(attention) ? null : attention, null,
            string.IsNullOrWhiteSpace(reason) ? null : reason,
            string.IsNullOrWhiteSpace(dx) ? null : dx,
            null, null, null);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Consult requested: {id}");
        TerminalIO.Pause();
    }

    private static async Task CompleteConsultAsync(MenuContext ctx)
    {
        List<ConsultSummary> consults = await ctx.GetWorkflow().GetConsultsAsync("Active", 50);
        if (consults.Count == 0)
        {
            TerminalIO.WriteLine("  No active consults to complete.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Complete Consult");
        TerminalIO.WriteNumberedList(consults,
            c => $"{c.ToService,-20} {c.Status,-12} {c.RequestDateTime:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select consult (1-{consults.Count})", 1, consults.Count);
        if (!choice.HasValue) return;

        ConsultSummary selected = consults[choice.Value - 1];
        TerminalIO.WriteBlank();
        string resultText = TerminalIO.PromptMultiline("Enter result note text:");

        if (!TerminalIO.PromptYesNo("Complete this consult?")) return;

        await ctx.GetWorkflow().CompleteConsultAsync(
            selected.ConsultId,
            string.IsNullOrWhiteSpace(resultText) ? null : resultText,
            ctx.Session.UserId, ctx.Session.UserName);

        TerminalIO.WriteLine("  Consult completed.");
        TerminalIO.Pause();
    }
}
