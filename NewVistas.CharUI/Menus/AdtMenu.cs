// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// ADT (Admit/Discharge/Transfer) menu — mirrors VistA File #405.
/// Placement targets inpatient units (IInpatientUnitGrain) at institution 500.
/// </summary>
public class AdtMenu : IMenu
{
    /// <summary>Institution for all admissions/transfers from this client (File #4).</summary>
    private const string InstitutionId = "500";

    public string Title => "ADT Movements";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("ADT Movements", [
            ("1", "List Movements", ListMovementsAsync),
            ("2", "Record Admission", AdmitAsync),
            ("3", "Record Discharge", DischargeAsync),
            ("4", "Record Transfer", TransferAsync),
        ], ctx);
    }

    private static async Task ListMovementsAsync(MenuContext ctx)
    {
        List<AdtSummary> movements = await ctx.GetWorkflow().GetAdtMovementsAsync();
        TerminalIO.WriteHeader("ADT Movements");
        if (movements.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Type", "Date/Time", "Ward", "Room-Bed", "Physician", "Status"],
                [4, 10, 18, 14, 8, 14, 8],
                movements.Select((m, i) => new[]
                {
                    (i + 1).ToString(), m.MovementType,
                    m.MovementDateTime.ToString("MM/dd/yyyy HH:mm"),
                    m.WardLocationName ?? "", m.RoomBed ?? "",
                    m.AttendingPhysicianName ?? "", m.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task AdmitAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Record Admission");

        await ShowUnitDirectoryAsync(ctx);

        string unit = TerminalIO.Prompt("Unit ID (e.g., MED-3A, ICU-1)");
        if (string.IsNullOrWhiteSpace(unit)) return;

        string bed = TerminalIO.Prompt("Bed ID (optional)");
        string specialty = TerminalIO.Prompt("Treating Specialty (optional)");
        string physician = TerminalIO.Prompt("Attending Physician");
        string diagnosis = TerminalIO.Prompt("Admission Diagnosis");
        string comments = TerminalIO.Prompt("Comments (optional)");

        if (!TerminalIO.PromptYesNo("Record this admission?")) return;

        string id = await ctx.GetWorkflow().RecordAdmissionAsync(
            DateTime.UtcNow, InstitutionId, ToUnitId(unit),
            string.IsNullOrWhiteSpace(bed) ? null : bed.Trim(),
            string.IsNullOrWhiteSpace(specialty) ? null : specialty,
            ctx.Session.UserId,
            string.IsNullOrWhiteSpace(physician) ? ctx.Session.UserName : physician,
            string.IsNullOrWhiteSpace(diagnosis) ? null : diagnosis,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Admission recorded: {id}");
        TerminalIO.Pause();
    }

    /// <summary>
    /// List the institution's units with live availability so the user can pick
    /// a valid unit id (the capacity grain doubles as the unit directory).
    /// </summary>
    private static async Task ShowUnitDirectoryAsync(MenuContext ctx)
    {
        try
        {
            IBedCapacityGrain capacity =
                ctx.GetGrain<IBedCapacityGrain>($"BED-CAPACITY:{InstitutionId}");
            List<UnitCapacitySummary> units = await capacity.GetUnitsAsync(true);
            if (units.Count == 0) return;

            TerminalIO.WriteLine("  Units: " + string.Join(", ",
                units.Select(u => $"{u.UnitId} ({u.Available} avail)")));
            TerminalIO.WriteBlank();
        }
        catch
        {
            // Directory is a convenience; admission can proceed without it.
        }
    }

    /// <summary>Interpret legacy "WARD-" prefixed ids as unit ids (e.g. "WARD-MED-3A" → "MED-3A").</summary>
    private static string ToUnitId(string unitId)
    {
        string id = unitId.Trim();
        return id.StartsWith("WARD-", StringComparison.OrdinalIgnoreCase) ? id[5..] : id;
    }

    private static async Task DischargeAsync(MenuContext ctx)
    {
        List<AdtSummary> movements = await ctx.GetWorkflow().GetAdtMovementsAsync();
        List<AdtSummary> admissions = movements.Where(m =>
            m.MovementType.Equals("ADMISSION", StringComparison.OrdinalIgnoreCase) ||
            m.MovementType.Equals("TRANSFER", StringComparison.OrdinalIgnoreCase)).ToList();

        if (admissions.Count == 0)
        {
            TerminalIO.WriteLine("  No active admissions to discharge.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Record Discharge");
        TerminalIO.WriteNumberedList(admissions,
            m => $"{m.MovementType,-10} {m.MovementDateTime:MM/dd/yyyy HH:mm}  {m.WardLocationName ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select movement (1-{admissions.Count})", 1, admissions.Count);
        if (!choice.HasValue) return;

        AdtSummary selected = admissions[choice.Value - 1];
        string dischargeDx = TerminalIO.Prompt("Discharge Diagnosis (optional)");
        string disposition = TerminalIO.Prompt("Disposition (optional)");
        string comments = TerminalIO.Prompt("Comments (optional)");

        if (!TerminalIO.PromptYesNo("Record this discharge?")) return;

        await ctx.GetWorkflow().RecordDischargeAsync(
            selected.MovementId, DateTime.UtcNow,
            string.IsNullOrWhiteSpace(dischargeDx) ? null : dischargeDx,
            string.IsNullOrWhiteSpace(disposition) ? null : disposition,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteLine("  Discharge recorded.");
        TerminalIO.Pause();
    }

    private static async Task TransferAsync(MenuContext ctx)
    {
        List<AdtSummary> movements = await ctx.GetWorkflow().GetAdtMovementsAsync();
        List<AdtSummary> current = movements.Where(m =>
            m.MovementType.Equals("ADMISSION", StringComparison.OrdinalIgnoreCase) ||
            m.MovementType.Equals("TRANSFER", StringComparison.OrdinalIgnoreCase)).ToList();

        if (current.Count == 0)
        {
            TerminalIO.WriteLine("  No active admissions to transfer.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Record Transfer");
        TerminalIO.WriteNumberedList(current,
            m => $"{m.MovementType,-10} {m.WardLocationName ?? "",-14} {m.RoomBed ?? ""}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select movement (1-{current.Count})", 1, current.Count);
        if (!choice.HasValue) return;

        AdtSummary selected = current[choice.Value - 1];

        await ShowUnitDirectoryAsync(ctx);

        string toUnit = TerminalIO.Prompt("To Unit ID (e.g., MED-3A, ICU-1)");
        if (string.IsNullOrWhiteSpace(toUnit)) return;

        string toBed = TerminalIO.Prompt("To Bed ID (optional)");
        string toSpecialty = TerminalIO.Prompt("To Specialty (optional)");
        string physician = TerminalIO.Prompt("Attending Physician (optional)");
        string comments = TerminalIO.Prompt("Comments (optional)");

        if (!TerminalIO.PromptYesNo("Record this transfer?")) return;

        await ctx.GetWorkflow().RecordTransferAsync(
            selected.MovementId, DateTime.UtcNow,
            InstitutionId, ToUnitId(toUnit),
            string.IsNullOrWhiteSpace(toBed) ? null : toBed.Trim(),
            null, string.IsNullOrWhiteSpace(toSpecialty) ? null : toSpecialty,
            string.IsNullOrWhiteSpace(physician) ? null : ctx.Session.UserId,
            string.IsNullOrWhiteSpace(physician) ? null : physician,
            string.IsNullOrWhiteSpace(comments) ? null : comments);

        TerminalIO.WriteLine("  Transfer recorded.");
        TerminalIO.Pause();
    }
}
