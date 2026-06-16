// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Security;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Top-level CPRS-style main menu — mirrors VistA CPRS main menu (XUSHM).
///
/// Tab order matches CPRS Delphi frame (fFrame.pas):
///   Cover Sheet → Problems → Meds → Orders → Notes → Consults →
///   Surgery → D/C Summ → Labs → Reports
///
/// Additional modules: Encounter/PCE, Scheduling, Radiology, ADT, Demographics.
///
/// Security-gated: write-access menus are only shown if the user holds
/// the corresponding security key. Read-only menus are always visible.
/// </summary>
public class MainMenu : IMenu
{
    public string Title => "Main Menu";

    private readonly PatientSelectMenu _patientSelect = new();
    private readonly CoverSheetMenu _coverSheet = new();
    private readonly ProblemsMenu _problems = new();
    private readonly AllergiesMenu _allergies = new();
    private readonly VitalsMenu _vitals = new();
    private readonly MedicationsMenu _medications = new();
    private readonly OrdersMenu _orders = new();
    private readonly NotesMenu _notes = new();
    private readonly LabsMenu _labs = new();
    private readonly ConsultsMenu _consults = new();
    private readonly AppointmentsMenu _appointments = new();
    private readonly SurgeryMenu _surgery = new();
    private readonly RadiologyMenu _radiology = new();
    private readonly AdtMenu _adt = new();
    private readonly DischargeSummaryMenu _dcSumm = new();
    private readonly ReportsMenu _reports = new();
    private readonly DemographicsMenu _demographics = new();
    private readonly EncounterMenu _encounter = new();
    private readonly SystemModulesMenu _systemModules = new();

    public async Task RunAsync(MenuContext ctx)
    {
        // On first entry, prompt for patient selection
        if (!ctx.Patient.HasPatient)
            await _patientSelect.RunAsync(ctx);

        while (true)
        {
            TerminalIO.Clear();

            if (ctx.Patient.HasPatient)
                TerminalIO.WritePatientBanner(ctx.Session, ctx.Patient);
            else
            {
                TerminalIO.WriteDivider('=');
                TerminalIO.WriteLine("  NEWVISTAS CLINICAL INFORMATION SYSTEM");
                TerminalIO.WriteDivider('=');
            }

            // Show user identity in menu header
            TerminalIO.WriteHeader("Main Menu");
            TerminalIO.WriteLine($"  User: {ctx.Session.UserName}   Div: {ctx.Session.Division}");
            TerminalIO.WriteBlank();

            // ── Clinical Tabs (matches CPRS tab order) ─────────────────
            TerminalIO.WriteLine("   CV   Cover Sheet            (Ctrl+G)");
            TerminalIO.WriteLine("   PL   Problem List           (Ctrl+P)");
            TerminalIO.WriteLine("   AL   Allergies");
            TerminalIO.WriteLine("   VT   Vitals");
            TerminalIO.WriteLine("   ME   Medications            (Ctrl+M)");
            TerminalIO.WriteLine("   OR   Orders                 (Ctrl+O)");
            TerminalIO.WriteLine("   NO   Notes                  (Ctrl+N)");
            TerminalIO.WriteLine("   CO   Consults               (Ctrl+T)");
            TerminalIO.WriteLine("   SU   Surgery                (Ctrl+U)");
            TerminalIO.WriteLine("   DC   D/C Summaries          (Ctrl+D)");
            TerminalIO.WriteLine("   LA   Labs                   (Ctrl+L)");
            TerminalIO.WriteLine("   RP   Reports                (Ctrl+R)");
            TerminalIO.WriteBlank();

            // ── Additional Modules ─────────────────────────────────────
            TerminalIO.WriteLine("   EN   Encounter / PCE");
            TerminalIO.WriteLine("   SC   Scheduling");
            TerminalIO.WriteLine("   RD   Radiology");
            TerminalIO.WriteLine("   AT   ADT Movements");
            TerminalIO.WriteLine("   DM   Demographics");
            TerminalIO.WriteBlank();

            // ── System ─────────────────────────────────────────────────
            TerminalIO.WriteLine("   SP   Select New Patient");
            TerminalIO.WriteLine("   SM   System Modules >>");
            TerminalIO.WriteBlank();

            string input = TerminalIO.Prompt("Select Action", "Quit");

            if (input.Equals("Q", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("Quit", StringComparison.OrdinalIgnoreCase))
                return;

            // Patient selection and system modules don't require a patient
            if (input.Equals("SP", StringComparison.OrdinalIgnoreCase))
            {
                await _patientSelect.RunAsync(ctx);
                continue;
            }

            if (input.Equals("SM", StringComparison.OrdinalIgnoreCase))
            {
                await _systemModules.RunAsync(ctx);
                continue;
            }

            // All other options require a patient
            if (!ctx.Patient.HasPatient)
            {
                TerminalIO.WriteLine("  Please select a patient first (SP).");
                TerminalIO.Pause();
                continue;
            }

            // Session heartbeat on every menu action
            await ctx.TouchSessionAsync();

            try
            {
                switch (input.ToUpperInvariant())
                {
                    case "CV": await _coverSheet.RunAsync(ctx); break;
                    case "PL": await _problems.RunAsync(ctx); break;
                    case "AL": await _allergies.RunAsync(ctx); break;
                    case "VT": await _vitals.RunAsync(ctx); break;
                    case "ME": await _medications.RunAsync(ctx); break;
                    case "OR": await _orders.RunAsync(ctx); break;
                    case "NO": await _notes.RunAsync(ctx); break;
                    case "CO": await _consults.RunAsync(ctx); break;
                    case "SU": await _surgery.RunAsync(ctx); break;
                    case "DC": await _dcSumm.RunAsync(ctx); break;
                    case "LA": await _labs.RunAsync(ctx); break;
                    case "RP": await _reports.RunAsync(ctx); break;
                    case "EN": await _encounter.RunAsync(ctx); break;
                    case "SC": await _appointments.RunAsync(ctx); break;
                    case "RD": await _radiology.RunAsync(ctx); break;
                    case "AT": await _adt.RunAsync(ctx); break;
                    case "DM": await _demographics.RunAsync(ctx); break;
                    default:
                        TerminalIO.WriteLine($"  ?? Unknown option: {input}");
                        TerminalIO.Pause();
                        break;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                TerminalIO.WriteBlank();
                TerminalIO.WriteError($"*** ACCESS DENIED: {ex.Message} ***");
                TerminalIO.Pause();
            }
            catch (Exception ex)
            {
                TerminalIO.WriteBlank();
                TerminalIO.WriteError($"*** ERROR: {ex.Message} ***");
                TerminalIO.Pause();
            }
        }
    }
}
