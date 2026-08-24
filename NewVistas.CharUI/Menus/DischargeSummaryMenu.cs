// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Discharge Summary menu — mirrors VistA TIU discharge summary (fDCSumm.pas).
/// D/C Summaries are TIU documents of type "DISCHARGE SUMMARY".
/// </summary>
public class DischargeSummaryMenu : IMenu
{
    public string Title => "Discharge Summaries";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Discharge Summaries", [
            ("1", "List D/C Summaries", ListSummariesAsync),
            ("2", "View Summary Detail", ViewSummaryAsync),
            ("3", "Create New D/C Summary", CreateSummaryAsync),
            ("4", "Sign Summary", SignSummaryAsync),
            ("5", "Add Addendum", AddAddendumAsync),
        ], ctx);
    }

    private static async Task ListSummariesAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync("DISCHARGE SUMMARY", 50);
        TerminalIO.WriteHeader("Discharge Summaries");
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Subject", "Author", "Status", "Date"],
                [4, 24, 16, 12, 12],
                notes.Select((n, i) => new[]
                {
                    (i + 1).ToString(), n.Subject ?? "(no subject)",
                    n.AuthorName ?? "", n.Status,
                    n.ReferenceDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewSummaryAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync("DISCHARGE SUMMARY", 50);
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  No discharge summaries on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("View Discharge Summary");
        TerminalIO.WriteNumberedList(notes,
            n => $"{n.Subject ?? "(no subject)",-24} {n.AuthorName ?? "",-16} {n.ReferenceDate:MM/dd/yyyy}  {n.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select summary (1-{notes.Count})", 1, notes.Count);
        if (!choice.HasValue) return;

        TiuDocumentState doc = await ctx.GetWorkflow().GetNoteAsync(notes[choice.Value - 1].DocumentId);

        TerminalIO.Clear();
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Document Type", "DISCHARGE SUMMARY");
        TerminalIO.WriteDetail("Subject", doc.Subject);
        TerminalIO.WriteDetail("Author", doc.AuthorName);
        TerminalIO.WriteDetail("Cosigner", doc.CosignerName);
        TerminalIO.WriteDetail("Status", doc.Status);
        TerminalIO.WriteDetail("Reference Date", doc.ReferenceDate.ToString("MM/dd/yyyy"));
        TerminalIO.WriteDetail("Location", doc.LocationName);
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine(doc.ReportText ?? "(no text)");
        TerminalIO.Pause();
    }

    private static async Task CreateSummaryAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.PROVIDER))
        {
            TerminalIO.WriteLine("  You do not hold the PROVIDER key.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Create Discharge Summary");

        string subject = TerminalIO.Prompt("Subject (e.g., Primary Diagnosis)");
        string location = TerminalIO.Prompt("Discharge Location");
        string cosigner = TerminalIO.Prompt("Cosigner (optional)");

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Enter summary sections:");
        TerminalIO.WriteLine("  (Admitting Diagnosis, Hospital Course, Discharge Medications,");
        TerminalIO.WriteLine("   Follow-Up Instructions, Condition at Discharge)");
        TerminalIO.WriteBlank();
        string body = TerminalIO.PromptMultiline("Enter summary text:");
        if (string.IsNullOrWhiteSpace(body)) return;

        if (!TerminalIO.PromptYesNo("Save discharge summary?")) return;

        string id = await ctx.GetWorkflow().CreateNoteAsync(
            "DISCHARGE SUMMARY", null, body,
            string.IsNullOrWhiteSpace(subject) ? null : subject,
            ctx.Session.UserId, ctx.Session.UserName,
            string.IsNullOrWhiteSpace(cosigner) ? null : cosigner,
            string.IsNullOrWhiteSpace(cosigner) ? null : cosigner,
            null, string.IsNullOrWhiteSpace(location) ? null : location,
            null, DateTime.UtcNow);

        TerminalIO.WriteLine($"  D/C Summary created: {id}");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task SignSummaryAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.TIU_SIGN))
        {
            TerminalIO.WriteLine("  You do not hold the TIU SIGN key.");
            TerminalIO.Pause();
            return;
        }

        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync("DISCHARGE SUMMARY", 50);
        List<TiuNoteSummary> unsigned = notes.Where(n =>
            n.Status.Equals("UNSIGNED", StringComparison.OrdinalIgnoreCase)).ToList();

        if (unsigned.Count == 0)
        {
            TerminalIO.WriteLine("  No unsigned discharge summaries.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Sign D/C Summary");
        TerminalIO.WriteNumberedList(unsigned,
            n => $"{n.Subject ?? "",-24} {n.ReferenceDate:MM/dd/yyyy}  {n.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select summary (1-{unsigned.Count})", 1, unsigned.Count);
        if (!choice.HasValue) return;

        TerminalIO.Write("SIGNATURE CODE: ");
        string sig = ReadMasked();
        if (string.IsNullOrWhiteSpace(sig)) return;

        // The workflow grain verifies the code fail-closed; any keystroke used to sign.
        try
        {
            await ctx.GetWorkflow().SignNoteAsync(unsigned[choice.Value - 1].DocumentId, sig);
        }
        catch (UnauthorizedAccessException)
        {
            TerminalIO.WriteError("Signature code not accepted. Summary NOT signed.");
            TerminalIO.Pause();
            return;
        }
        TerminalIO.WriteLine("  D/C Summary signed.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task AddAddendumAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync("DISCHARGE SUMMARY", 50);
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  No discharge summaries on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Add Addendum to D/C Summary");
        TerminalIO.WriteNumberedList(notes,
            n => $"{n.Subject ?? "",-24} {n.ReferenceDate:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select (1-{notes.Count})", 1, notes.Count);
        if (!choice.HasValue) return;

        string text = TerminalIO.PromptMultiline("Enter addendum text:");
        if (string.IsNullOrWhiteSpace(text)) return;

        string id = await ctx.GetWorkflow().AddAddendumAsync(
            notes[choice.Value - 1].DocumentId, text,
            ctx.Session.UserId, ctx.Session.UserName, DateTime.UtcNow);

        TerminalIO.WriteLine($"  Addendum added: {id}");
        TerminalIO.Pause();
    }

    private static string ReadMasked()
    {
        var chars = new List<char>();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0) { chars.RemoveAt(chars.Count - 1); Console.Write("\b \b"); }
            }
            else if (!char.IsControl(key.KeyChar)) { chars.Add(key.KeyChar); Console.Write('*'); }
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}
