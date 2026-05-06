// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// TIU Notes menu — mirrors VistA TIUSRVN.m / TIUSRVL.m / TIUSRVP.m.
///
/// Security:
///   - Writing notes requires PROVIDER key
///   - Signing notes requires TIU SIGN key + electronic signature
///   - Cosigning requires TIU COSIGN key + electronic signature
///   - Viewing notes is read-only (no key required)
/// </summary>
public class NotesMenu : IMenu
{
    public string Title => "Notes";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Clinical Notes", [
            ("1", "List Recent Notes", ListNotesAsync),
            ("2", "View Note Detail", ViewNoteAsync),
            ("3", "Write New Note", WriteNoteAsync),
            ("4", "Sign Note", SignNoteAsync),
            ("5", "Cosign Note", CosignNoteAsync),
            ("6", "Add Addendum", AddAddendumAsync),
            ("7", "Amend Note", AmendNoteAsync),
        ], ctx);
    }

    private static async Task ListNotesAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        TerminalIO.WriteHeader("Recent Notes");
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Type", "Subject", "Author", "Status", "Date"],
                [4, 16, 16, 14, 10, 12],
                notes.Select((n, i) => new[]
                {
                    (i + 1).ToString(), n.DocumentType, n.Subject ?? "",
                    n.AuthorName ?? "", n.Status,
                    n.ReferenceDate.ToString("MM/dd/yyyy")
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewNoteAsync(MenuContext ctx)
    {
        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  No notes on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("View Note");
        TerminalIO.WriteNumberedList(notes,
            n => $"{n.DocumentType,-16} {n.AuthorName ?? "",-14} {n.ReferenceDate:MM/dd/yyyy}  {n.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select note (1-{notes.Count})", 1, notes.Count);
        if (!choice.HasValue) return;

        TiuNoteSummary selected = notes[choice.Value - 1];
        TiuDocumentState doc = await ctx.GetWorkflow().GetNoteAsync(selected.DocumentId);

        TerminalIO.Clear();
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Document Type", doc.DocumentType);
        TerminalIO.WriteDetail("Subject", doc.Subject);
        TerminalIO.WriteDetail("Author", doc.AuthorName);
        TerminalIO.WriteDetail("Cosigner", doc.CosignerName);
        TerminalIO.WriteDetail("Status", doc.Status);
        TerminalIO.WriteDetail("Date", doc.ReferenceDate.ToString("MM/dd/yyyy HH:mm"));
        TerminalIO.WriteDetail("Location", doc.LocationName);
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine(doc.ReportText ?? "(no text)");

        // Show addenda if present
        if (doc.AddendumIds.Count > 0)
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteDivider('-');
            TerminalIO.WriteLine($"  {doc.AddendumIds.Count} addendum(a) attached.");
        }

        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task WriteNoteAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.PROVIDER))
        {
            TerminalIO.WriteError("You do not hold the PROVIDER key. Note entry is not permitted.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Write New Note");

        TerminalIO.WriteLine("  Document Types: PROGRESS NOTE, CONSULT NOTE, H&P,");
        TerminalIO.WriteLine("                  DISCHARGE SUMMARY, CRISIS NOTE, ADVANCE DIRECTIVE");
        TerminalIO.WriteBlank();

        string docType = TerminalIO.Prompt("Document Type", "PROGRESS NOTE");
        string subject = TerminalIO.Prompt("Subject (optional)");
        string location = TerminalIO.Prompt("Location (optional)");
        string cosigner = TerminalIO.Prompt("Cosigner (optional — required for trainees)");

        TerminalIO.WriteBlank();
        string body = TerminalIO.PromptMultiline("Enter note text:");

        if (string.IsNullOrWhiteSpace(body))
        {
            TerminalIO.WriteLine("  No text entered. Note not created.");
            TerminalIO.Pause();
            return;
        }

        if (!TerminalIO.PromptYesNo("Save this note?")) return;

        string? cosignerName = string.IsNullOrWhiteSpace(cosigner) ? null : cosigner;

        string id = await ctx.GetWorkflow().CreateNoteAsync(
            docType, null, body,
            string.IsNullOrWhiteSpace(subject) ? null : subject,
            ctx.Session.UserId, ctx.Session.UserName,
            cosignerName != null ? cosignerName : null,
            cosignerName,
            null, string.IsNullOrWhiteSpace(location) ? null : location,
            null, DateTime.UtcNow);

        TerminalIO.WriteBlank();
        TerminalIO.WriteSuccess($"Note created: {id}");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task SignNoteAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.TIU_SIGN))
        {
            TerminalIO.WriteError("You do not hold the TIU SIGN key.");
            TerminalIO.Pause();
            return;
        }

        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        List<TiuNoteSummary> unsigned = notes.Where(n =>
            n.Status.Equals("UNSIGNED", StringComparison.OrdinalIgnoreCase)).ToList();

        if (unsigned.Count == 0)
        {
            TerminalIO.WriteLine("  No unsigned notes.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Sign Note");
        TerminalIO.WriteNumberedList(unsigned,
            n => $"{n.DocumentType,-16} {n.AuthorName ?? "",-14} {n.ReferenceDate:MM/dd/yyyy}  {n.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select note (1-{unsigned.Count})", 1, unsigned.Count);
        if (!choice.HasValue) return;

        TiuNoteSummary selected = unsigned[choice.Value - 1];

        // Electronic signature required
        TerminalIO.WriteBlank();
        TerminalIO.Write("SIGNATURE CODE: ");
        string sig = ReadMaskedInput();
        if (string.IsNullOrWhiteSpace(sig))
        {
            TerminalIO.WriteLine("  Signature not entered. Note NOT signed.");
            TerminalIO.Pause();
            return;
        }

        await ctx.GetWorkflow().SignNoteAsync(selected.DocumentId);
        TerminalIO.WriteSuccess("Note signed.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task CosignNoteAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.TIU_COSIGN))
        {
            TerminalIO.WriteError("You do not hold the TIU COSIGN key.");
            TerminalIO.Pause();
            return;
        }

        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        List<TiuNoteSummary> uncosigned = notes.Where(n =>
            n.Status.Equals("UNCOSIGNED", StringComparison.OrdinalIgnoreCase)).ToList();

        if (uncosigned.Count == 0)
        {
            TerminalIO.WriteLine("  No notes requiring cosignature.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Cosign Note");
        TerminalIO.WriteNumberedList(uncosigned,
            n => $"{n.DocumentType,-16} {n.AuthorName ?? "",-14} {n.ReferenceDate:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select note (1-{uncosigned.Count})", 1, uncosigned.Count);
        if (!choice.HasValue) return;

        TiuNoteSummary selected = uncosigned[choice.Value - 1];

        TerminalIO.Write("SIGNATURE CODE: ");
        string sig = ReadMaskedInput();
        if (string.IsNullOrWhiteSpace(sig)) return;

        await ctx.GetWorkflow().CosignNoteAsync(selected.DocumentId);
        TerminalIO.WriteSuccess("Note cosigned.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task AddAddendumAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.PROVIDER))
        {
            TerminalIO.WriteError("You do not hold the PROVIDER key.");
            TerminalIO.Pause();
            return;
        }

        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        if (notes.Count == 0)
        {
            TerminalIO.WriteLine("  No notes on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Add Addendum");
        TerminalIO.WriteNumberedList(notes,
            n => $"{n.DocumentType,-16} {n.AuthorName ?? "",-14} {n.ReferenceDate:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select note (1-{notes.Count})", 1, notes.Count);
        if (!choice.HasValue) return;

        TiuNoteSummary selected = notes[choice.Value - 1];
        TerminalIO.WriteBlank();
        string text = TerminalIO.PromptMultiline("Enter addendum text:");
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!TerminalIO.PromptYesNo("Save addendum?")) return;

        string id = await ctx.GetWorkflow().AddAddendumAsync(
            selected.DocumentId, text,
            ctx.Session.UserId, ctx.Session.UserName,
            DateTime.UtcNow);

        TerminalIO.WriteSuccess($"Addendum added: {id}");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task AmendNoteAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.PROVIDER))
        {
            TerminalIO.WriteError("You do not hold the PROVIDER key.");
            TerminalIO.Pause();
            return;
        }

        List<TiuNoteSummary> notes = await ctx.GetWorkflow().GetNotesAsync(null, 50);
        List<TiuNoteSummary> signed = notes.Where(n =>
            n.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)).ToList();

        if (signed.Count == 0)
        {
            TerminalIO.WriteLine("  No completed notes to amend.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Amend Note");
        TerminalIO.WriteNumberedList(signed,
            n => $"{n.DocumentType,-16} {n.AuthorName ?? "",-14} {n.ReferenceDate:MM/dd/yyyy}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select note (1-{signed.Count})", 1, signed.Count);
        if (!choice.HasValue) return;

        TiuNoteSummary selected = signed[choice.Value - 1];
        string amendText = TerminalIO.PromptMultiline("Enter amendment text:");
        if (string.IsNullOrWhiteSpace(amendText)) return;

        TerminalIO.Write("SIGNATURE CODE: ");
        string sig = ReadMaskedInput();
        if (string.IsNullOrWhiteSpace(sig)) return;

        await ctx.GetWorkflow().AmendNoteAsync(selected.DocumentId, amendText);
        TerminalIO.WriteSuccess("Note amended.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static string ReadMaskedInput()
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
            else if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}
