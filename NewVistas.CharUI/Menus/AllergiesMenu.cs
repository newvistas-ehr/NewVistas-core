// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Allergies menu — mirrors VistA CWAD allergy documentation.
/// </summary>
public class AllergiesMenu : IMenu
{
    public string Title => "Allergies";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Allergies", [
            ("1", "List Allergies", ListAllergiesAsync),
            ("2", "Record New Allergy", RecordAllergyAsync),
        ], ctx);
    }

    private static async Task ListAllergiesAsync(MenuContext ctx)
    {
        List<AllergySummary> allergies = await ctx.GetWorkflow().GetAllergiesAsync();
        TerminalIO.WriteHeader("Allergies");
        if (allergies.Count == 0)
        {
            TerminalIO.WriteLine("  No Known Allergies (NKA)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Allergen", "Severity", "Reactions"],
                [4, 22, 12, 36],
                allergies.Select((a, i) => new[]
                {
                    (i + 1).ToString(), a.Allergen, a.Severity ?? "",
                    string.Join(", ", a.Reactions)
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task RecordAllergyAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Record New Allergy");

        string allergen = TerminalIO.Prompt("Allergen");
        if (string.IsNullOrWhiteSpace(allergen)) return;

        string type = TerminalIO.Prompt("Type (D=Drug, F=Food, O=Other)", "D");
        string allergenType = type.ToUpperInvariant() switch
        {
            "D" => "Drug",
            "F" => "Food",
            "O" => "Other",
            _ => type
        };

        string severity = TerminalIO.Prompt("Severity (Mild, Moderate, Severe)", "Moderate");
        string reactions = TerminalIO.Prompt("Reactions (comma-separated)");
        string comments = TerminalIO.Prompt("Comments (optional)");

        List<string> reactionList = string.IsNullOrWhiteSpace(reactions)
            ? [] : reactions.Split(',', StringSplitOptions.TrimEntries).ToList();

        if (!TerminalIO.PromptYesNo("Save this allergy?")) return;

        string id = await ctx.GetWorkflow().RecordAllergyAsync(
            allergen, allergenType, null, "OBSERVED",
            reactionList, severity,
            ctx.Session.UserId, ctx.Session.UserName,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Allergy recorded: {id}");
        TerminalIO.Pause();
    }
}
