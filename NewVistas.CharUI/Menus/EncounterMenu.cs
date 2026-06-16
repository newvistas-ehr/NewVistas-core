// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Encounter/PCE menu — mirrors VistA fEncnt.pas and Encounter/ directory.
/// Manages visits, diagnoses, procedures, and encounter documentation.
/// </summary>
public class EncounterMenu : IMenu
{
    public string Title => "Encounter / PCE";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Encounter / PCE", [
            ("1", "List Encounters", ListEncountersAsync),
            ("2", "View Encounter Detail", ViewEncounterAsync),
            ("3", "Create New Encounter", CreateEncounterAsync),
            ("4", "Add Diagnosis to Encounter", AddDiagnosisAsync),
            ("5", "Add Procedure to Encounter", AddProcedureAsync),
            ("6", "Check Out Encounter", CheckOutAsync),
        ], ctx);
    }

    private static async Task ListEncountersAsync(MenuContext ctx)
    {
        List<PceVisitEntry> visits = await ctx.GetWorkflow().GetEncounterListAsync(50);
        TerminalIO.WriteHeader("Encounters");
        if (visits.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Date/Time", "Category", "Location", "Status", "Dx", "Px"],
                [4, 18, 8, 18, 10, 4, 4],
                visits.Select((v, i) => new[]
                {
                    (i + 1).ToString(),
                    v.VisitDateTime.ToString("MM/dd/yyyy HH:mm"),
                    v.ServiceCategory ?? "",
                    v.LocationName ?? "",
                    v.Status,
                    v.DiagnosisCount.ToString(),
                    v.ProcedureCount.ToString()
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ViewEncounterAsync(MenuContext ctx)
    {
        List<PceVisitEntry> visits = await ctx.GetWorkflow().GetEncounterListAsync(50);
        if (visits.Count == 0)
        {
            TerminalIO.WriteLine("  No encounters on file.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("View Encounter");
        TerminalIO.WriteNumberedList(visits,
            v => $"{v.VisitDateTime:MM/dd/yyyy HH:mm}  {v.ServiceCategory ?? "",-8} {v.LocationName ?? "",-18} {v.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select encounter (1-{visits.Count})", 1, visits.Count);
        if (!choice.HasValue) return;

        VisitState enc = await ctx.GetWorkflow().GetEncounterAsync(visits[choice.Value - 1].VisitId);

        TerminalIO.Clear();
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Visit ID", enc.VisitId);
        TerminalIO.WriteDetail("Date/Time", enc.VisitDateTime.ToString("MM/dd/yyyy HH:mm"));
        TerminalIO.WriteDetail("Category", enc.ServiceCategory);
        TerminalIO.WriteDetail("Location", enc.LocationName);
        TerminalIO.WriteDetail("Status", enc.Status);
        TerminalIO.WriteDivider('-');

        // Diagnoses
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  DIAGNOSES:");
        if (enc.Diagnoses.Count == 0)
            TerminalIO.WriteLine("    (none)");
        else
            foreach (var dx in enc.Diagnoses)
            {
                string primary = dx.IsPrimary ? " [PRIMARY]" : "";
                TerminalIO.WriteLine($"    {dx.Icd10Code,-10} {dx.Description}{primary}");
            }

        // Procedures
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  PROCEDURES:");
        if (enc.Procedures.Count == 0)
            TerminalIO.WriteLine("    (none)");
        else
            foreach (var px in enc.Procedures)
                TerminalIO.WriteLine($"    {px.CptCode,-10} {px.Description}  Qty: {px.Quantity}");

        // Providers
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  PROVIDERS:");
        if (enc.Providers.Count == 0)
            TerminalIO.WriteLine("    (none)");
        else
            foreach (var pv in enc.Providers)
            {
                string primary = pv.IsPrimary ? " [PRIMARY]" : "";
                TerminalIO.WriteLine($"    {pv.ProviderName}{primary}");
            }

        TerminalIO.Pause();
    }

    private static async Task CreateEncounterAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.PROVIDER))
        {
            TerminalIO.WriteLine("  You do not hold the PROVIDER key.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Create New Encounter");
        TerminalIO.WriteLine("  Service Categories: A=Ambulatory, T=Telehealth, H=Hospitalization,");
        TerminalIO.WriteLine("                      C=Consult, E=Event, O=Observation, R=Referral");
        TerminalIO.WriteBlank();

        string category = TerminalIO.Prompt("Service Category", "A");
        string location = TerminalIO.Prompt("Location Name");
        if (string.IsNullOrWhiteSpace(location)) return;

        DateTime? visitDate = TerminalIO.PromptDate("Visit Date/Time", DateTime.Now);

        if (!TerminalIO.PromptYesNo("Create this encounter?")) return;

        string id = await ctx.GetWorkflow().CreateEncounterAsync(
            visitDate ?? DateTime.UtcNow,
            category,
            null, location,
            null, null,
            ctx.Session.UserId, ctx.Session.UserName,
            null, null);

        TerminalIO.WriteLine($"  Encounter created: {id}");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task AddDiagnosisAsync(MenuContext ctx)
    {
        List<PceVisitEntry> visits = await ctx.GetWorkflow().GetEncounterListAsync(50);
        List<PceVisitEntry> open = visits.Where(v =>
            v.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)).ToList();

        if (open.Count == 0)
        {
            TerminalIO.WriteLine("  No open encounters.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Add Diagnosis");
        TerminalIO.WriteNumberedList(open,
            v => $"{v.VisitDateTime:MM/dd/yyyy HH:mm}  {v.LocationName ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select encounter (1-{open.Count})", 1, open.Count);
        if (!choice.HasValue) return;

        string code = TerminalIO.Prompt("ICD-10 Code");
        if (string.IsNullOrWhiteSpace(code)) return;

        string description = TerminalIO.Prompt("Diagnosis Description");
        if (string.IsNullOrWhiteSpace(description)) return;

        bool isPrimary = TerminalIO.PromptYesNo("Primary diagnosis?", false);

        await ctx.GetWorkflow().AddEncounterDiagnosisAsync(
            open[choice.Value - 1].VisitId, code, description, isPrimary,
            ctx.Session.UserId, ctx.Session.UserName);

        TerminalIO.WriteLine("  Diagnosis added.");
        TerminalIO.Pause();
    }

    private static async Task AddProcedureAsync(MenuContext ctx)
    {
        List<PceVisitEntry> visits = await ctx.GetWorkflow().GetEncounterListAsync(50);
        List<PceVisitEntry> open = visits.Where(v =>
            v.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)).ToList();

        if (open.Count == 0)
        {
            TerminalIO.WriteLine("  No open encounters.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Add Procedure");
        TerminalIO.WriteNumberedList(open,
            v => $"{v.VisitDateTime:MM/dd/yyyy HH:mm}  {v.LocationName ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select encounter (1-{open.Count})", 1, open.Count);
        if (!choice.HasValue) return;

        string code = TerminalIO.Prompt("CPT Code");
        if (string.IsNullOrWhiteSpace(code)) return;

        string description = TerminalIO.Prompt("Procedure Description");
        string qty = TerminalIO.Prompt("Quantity", "1");

        await ctx.GetWorkflow().AddEncounterProcedureAsync(
            open[choice.Value - 1].VisitId, code, description ?? "",
            int.TryParse(qty, out int q) ? q : 1,
            null, ctx.Session.UserId, ctx.Session.UserName);

        TerminalIO.WriteLine("  Procedure added.");
        TerminalIO.Pause();
    }

    private static async Task CheckOutAsync(MenuContext ctx)
    {
        List<PceVisitEntry> visits = await ctx.GetWorkflow().GetEncounterListAsync(50);
        List<PceVisitEntry> open = visits.Where(v =>
            v.Status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)).ToList();

        if (open.Count == 0)
        {
            TerminalIO.WriteLine("  No open encounters to check out.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Check Out Encounter");
        TerminalIO.WriteNumberedList(open,
            v => $"{v.VisitDateTime:MM/dd/yyyy HH:mm}  {v.LocationName ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select encounter (1-{open.Count})", 1, open.Count);
        if (!choice.HasValue) return;

        if (!TerminalIO.PromptYesNo("Check out this encounter?")) return;

        await ctx.GetWorkflow().CheckOutEncounterAsync(
            open[choice.Value - 1].VisitId, DateTime.UtcNow);
        TerminalIO.WriteLine("  Encounter checked out.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }
}
