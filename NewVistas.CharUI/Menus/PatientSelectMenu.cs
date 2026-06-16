// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Tiered patient search and selection — mirrors VistA ORWPT LOOKUP RPC
/// with provider-centric "My Patients" and ward census quick-pick tiers.
///
/// Selection flow:
///   1. My Patients      — IProviderPatientIndexGrain (always available)
///   2. Ward Census       — IWardCensusGrain (only if user has a DefaultWardId)
///   3. Search All        — global search via IPatientWorkflowGrain
///
/// Security additions over CPRS:
///   - DG SENSITIVITY check on patient selection (restricted records)
///   - Break-the-glass audit logging for sensitive patients
///   - Session heartbeat on every selection
/// </summary>
public class PatientSelectMenu : IMenu
{
    public string Title => "Patient Selection";

    public async Task RunAsync(MenuContext ctx)
    {
        // ── Pre-fetch ward assignment from the user's NewPerson record ──
        string? defaultWardId = null;
        string? defaultWardName = null;
        try
        {
            INewPersonGrain personGrain = ctx.GetGrain<INewPersonGrain>(ctx.Session.UserId);
            NewPersonState person = await personGrain.GetPersonAsync();
            if (!string.IsNullOrWhiteSpace(person.DefaultWardId))
            {
                defaultWardId = person.DefaultWardId;
                defaultWardName = person.DefaultWardName;
            }
        }
        catch
        {
            // If we can't load the person record, ward census is unavailable
        }

        bool hasWardCensus = !string.IsNullOrWhiteSpace(defaultWardId);

        while (true)
        {
            // ── Tier selection menu ─────────────────────────────────────
            TerminalIO.Clear();
            TerminalIO.WriteDivider('=');
            TerminalIO.WriteLine("  PATIENT SELECTION");
            TerminalIO.WriteDivider('=');
            TerminalIO.WriteBlank();

            if (ctx.Patient.HasPatient)
                TerminalIO.WriteLine($"  Current Patient: {ctx.Patient.PatientName}");

            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("  1. My Patients");

            if (hasWardCensus)
            {
                TerminalIO.WriteLine($"  2. Ward Census ({defaultWardName})");
                TerminalIO.WriteLine("  3. Search All Patients");
            }
            else
            {
                TerminalIO.WriteLine("  2. Search All Patients");
            }

            TerminalIO.WriteBlank();

            int maxOption = hasWardCensus ? 3 : 2;
            int? tierChoice = TerminalIO.PromptSelection(
                $"Select Action (1-{maxOption})", 1, maxOption);

            if (!tierChoice.HasValue)
                return;

            // Map the user's choice to a logical action
            int action = tierChoice.Value;
            if (!hasWardCensus && action == 2)
                action = 3; // "Search All" is option 2 when ward census is hidden

            bool selected;
            switch (action)
            {
                case 1:
                    selected = await ShowMyPatientsAsync(ctx);
                    break;
                case 2:
                    selected = await ShowWardCensusAsync(ctx, defaultWardId!);
                    break;
                case 3:
                    selected = await ShowSearchAllAsync(ctx);
                    break;
                default:
                    continue;
            }

            if (selected)
                return; // patient was selected and loaded
            // otherwise loop back to the tier menu
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  Option 1 — My Patients (IProviderPatientIndexGrain)
    // ════════════════════════════════════════════════════════════════════

    private async Task<bool> ShowMyPatientsAsync(MenuContext ctx)
    {
        TerminalIO.Clear();
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteLine("  MY PATIENTS");
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Loading...");

        IProviderPatientIndexGrain indexGrain =
            ctx.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{ctx.Session.UserId}");
        List<ProviderPatientEntry> patients = await indexGrain.GetActivePatientsAsync();

        if (patients.Count == 0)
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("  No patients currently assigned to you.");
            TerminalIO.Pause();
            return false;
        }

        TerminalIO.Clear();
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteLine("  MY PATIENTS");
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteBlank();

        TerminalIO.WriteTable(
            ["#", "Patient Name", "DOB", "SSN4", "Role"],
            [4, 30, 12, 6, 14],
            patients.Select((p, i) => new[]
            {
                (i + 1).ToString(),
                p.PatientName,
                p.DateOfBirth?.ToString("MM/dd/yyyy") ?? "",
                p.SsnLast4 ?? "",
                p.Relationship
            }));

        TerminalIO.WriteBlank();
        int? choice = TerminalIO.PromptSelection(
            $"Choose Patient (1-{patients.Count})", 1, patients.Count);

        if (!choice.HasValue)
            return false;

        ProviderPatientEntry entry = patients[choice.Value - 1];
        ctx.Patient.PatientId = entry.PatientId;
        ctx.Patient.PatientName = entry.PatientName ?? entry.PatientId;

        return await FinalizeSelectionAsync(ctx);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Option 2 — Ward Census (IWardCensusGrain)
    // ════════════════════════════════════════════════════════════════════

    private async Task<bool> ShowWardCensusAsync(MenuContext ctx, string wardId)
    {
        TerminalIO.Clear();
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteLine("  WARD CENSUS");
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Loading...");

        IWardCensusGrain censusGrain =
            ctx.GetGrain<IWardCensusGrain>($"WARD-CENSUS:{wardId}");
        List<WardCensusEntry> census = await censusGrain.GetCensusAsync();

        if (census.Count == 0)
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("  No patients currently on this ward.");
            TerminalIO.Pause();
            return false;
        }

        TerminalIO.Clear();
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteLine("  WARD CENSUS");
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteBlank();

        TerminalIO.WriteTable(
            ["#", "Patient Name", "Room-Bed", "Specialty", "Attending"],
            [4, 28, 10, 18, 22],
            census.Select((c, i) => new[]
            {
                (i + 1).ToString(),
                c.PatientName ?? c.PatientId,
                c.RoomBed ?? "",
                c.TreatingSpecialty ?? "",
                c.AttendingPhysicianName ?? ""
            }));

        TerminalIO.WriteBlank();
        int? choice = TerminalIO.PromptSelection(
            $"Choose Patient (1-{census.Count})", 1, census.Count);

        if (!choice.HasValue)
            return false;

        WardCensusEntry entry = census[choice.Value - 1];
        ctx.Patient.PatientId = entry.PatientId;
        ctx.Patient.PatientName = entry.PatientName ?? entry.PatientId;

        return await FinalizeSelectionAsync(ctx);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Option 3 — Search All Patients (existing global search)
    // ════════════════════════════════════════════════════════════════════

    private async Task<bool> ShowSearchAllAsync(MenuContext ctx)
    {
        TerminalIO.Clear();
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteLine("  SEARCH ALL PATIENTS");
        TerminalIO.WriteDivider('─');
        TerminalIO.WriteBlank();

        string query = TerminalIO.Prompt("Select PATIENT NAME (or ^ to cancel)");

        if (string.IsNullOrWhiteSpace(query) || query == "^")
            return false;

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Searching...");

        // Use a well-known key for the search workflow grain
        IPatientWorkflowGrain searchGrain =
            ctx.GetGrain<IPatientWorkflowGrain>("SEARCH");
        List<PatientIndexEntry> results = await searchGrain.SearchPatientsAsync(query);

        if (results.Count == 0)
        {
            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("  No patients found matching that search.");
            TerminalIO.Pause();
            return false;
        }

        TerminalIO.WriteBlank();
        TerminalIO.WriteTable(
            ["#", "Patient Name", "DOB", "Sex", "SSN Last 4"],
            [4, 30, 12, 4, 10],
            results.Select((p, i) => new[]
            {
                (i + 1).ToString(),
                p.Name,
                p.DateOfBirth?.ToString("MM/dd/yyyy") ?? "",
                p.Sex,
                p.SsnLast4
            }));

        TerminalIO.WriteBlank();
        int? choice = TerminalIO.PromptSelection(
            $"Choose Patient (1-{results.Count})", 1, results.Count);

        if (!choice.HasValue)
        {
            TerminalIO.WriteLine("  ?? Invalid selection.");
            TerminalIO.Pause();
            return false;
        }

        PatientIndexEntry selected = results[choice.Value - 1];
        ctx.Patient.SetFromIndexEntry(selected);

        return await FinalizeSelectionAsync(ctx);
    }

    // ════════════════════════════════════════════════════════════════════
    //  Shared: sensitivity check + cover sheet + heartbeat
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Common post-selection logic for all three tiers:
    /// sensitivity check, cover sheet load, session heartbeat.
    /// Returns true if patient was successfully selected, false if denied/cancelled.
    /// </summary>
    private async Task<bool> FinalizeSelectionAsync(MenuContext ctx)
    {
        // ── Patient Sensitivity Check (DG SENSITIVITY) ────────────
        bool accessGranted = await CheckPatientSensitivityAsync(ctx);
        if (!accessGranted)
        {
            ctx.Patient.Clear();
            TerminalIO.Pause();
            return false;
        }

        TerminalIO.WriteBlank();
        TerminalIO.WriteSuccess($"*** PATIENT SELECTED: {TerminalColor.BrightWhite(ctx.Patient.PatientName ?? ctx.Patient.PatientId ?? "")} ***");
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("Loading cover sheet...");

        // Load cover sheet to populate demographics and CWAD
        try
        {
            CoverSheetState coverSheet = await ctx.GetWorkflow().GetCoverSheetAsync();
            ctx.Patient.SetFromCoverSheet(coverSheet);
        }
        catch
        {
            // Cover sheet may fail if patient has no data yet; that's OK
        }

        // Session heartbeat
        await ctx.TouchSessionAsync();

        TerminalIO.WriteLine("Done.");
        TerminalIO.Pause();
        return true;
    }

    /// <summary>
    /// Checks patient sensitivity level and enforces break-the-glass.
    /// Mirrors VistA DG SENSITIVITY logic (File #38.1).
    ///
    /// Returns true if access is granted, false if denied.
    /// </summary>
    private static async Task<bool> CheckPatientSensitivityAsync(MenuContext ctx)
    {
        try
        {
            IPatientAccessControlGrain pac =
                ctx.GetGrain<IPatientAccessControlGrain>($"PAC:{ctx.Patient.PatientId}");

            PatientAccessControlState access = await pac.GetAccessControlAsync();

            if (!access.IsSensitive)
                return true;

            // Patient record is flagged as sensitive/restricted
            TerminalIO.WriteBlank();
            TerminalIO.WriteDivider('*');
            TerminalIO.WriteLine(TerminalColor.BrightRed("  *** RESTRICTED RECORD ***"));
            TerminalIO.WriteBlank();
            TerminalIO.WriteWarning("This patient's record is flagged as SENSITIVE.");
            TerminalIO.WriteWarning("Access to this record is monitored and logged.");

            if (access.SensitivityCategories.Count > 0)
                TerminalIO.WriteLine($"  Category: {string.Join(", ", access.SensitivityCategories)}");

            TerminalIO.WriteDivider('*');
            TerminalIO.WriteBlank();

            // Check if user is on the authorized provider list
            bool isAuthorized = access.AuthorizedProviderIds
                .Any(p => p.Equals(ctx.Session.UserId, StringComparison.OrdinalIgnoreCase));

            if (isAuthorized)
            {
                TerminalIO.WriteLine("  You are on the authorized provider list.");
                TerminalIO.WriteLine("  Access is permitted. This access has been logged.");

                // Record the access in the audit trail
                await pac.RecordAccessAsync(ctx.Session.UserId, ctx.Session.UserName,
                    "AUTHORIZED_PROVIDER", false, "Authorized provider access");
                return true;
            }

            // Break-the-glass: user must acknowledge and provide reason
            TerminalIO.WriteLine("  You are NOT on the authorized provider list.");
            TerminalIO.WriteBlank();
            bool proceed = TerminalIO.PromptYesNo(
                "Do you wish to access this record? (This will be logged)", false);

            if (!proceed)
            {
                TerminalIO.WriteLine("  Access denied by user.");
                return false;
            }

            string reason = TerminalIO.Prompt("Reason for access");
            if (string.IsNullOrWhiteSpace(reason))
            {
                TerminalIO.WriteLine("  A reason is required for break-the-glass access.");
                return false;
            }

            // Log the break-the-glass event
            await pac.RecordAccessAsync(ctx.Session.UserId, ctx.Session.UserName,
                "BREAK_THE_GLASS", true, reason);

            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("  Access granted. This access has been logged.");
            return true;
        }
        catch
        {
            // If sensitivity check fails (grain not found, etc.), allow access
            // to avoid blocking clinical workflow
            return true;
        }
    }
}
