// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Patient Demographics display — mirrors VistA fPtDemo.pas.
/// Read-only view of patient demographic and administrative data.
/// </summary>
public class DemographicsMenu : IMenu
{
    public string Title => "Patient Demographics";

    public async Task RunAsync(MenuContext ctx)
    {
        if (!ctx.Patient.HasPatient)
        {
            TerminalIO.WriteLine("  No patient selected.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.Clear();
        TerminalIO.WritePatientBanner(ctx.Session, ctx.Patient);
        TerminalIO.WriteHeader("Patient Demographics");

        CoverSheetState cs = await ctx.GetWorkflow().GetCoverSheetAsync();
        PatientDemographicsSummary d = cs.Demographics;

        TerminalIO.WriteDetail("Patient Name", d.Name);
        TerminalIO.WriteDetail("Sex", d.Sex);
        TerminalIO.WriteDetail("Date of Birth", d.DateOfBirth?.ToString("MM/dd/yyyy"));
        TerminalIO.WriteDetail("Age", d.Age?.ToString());
        TerminalIO.WriteDetail("SSN", ctx.Patient.Ssn4 != null ? $"xxx-xx-{ctx.Patient.Ssn4}" : null);
        TerminalIO.WriteBlank();

        TerminalIO.WriteDivider('-');
        TerminalIO.WriteLine("  ADMISSION STATUS");
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Admitted", d.IsAdmitted ? "YES" : "NO");
        TerminalIO.WriteDetail("Room/Bed", d.RoomBed);
        TerminalIO.WriteDetail("Location", d.LocationName);
        TerminalIO.WriteBlank();

        TerminalIO.WriteDivider('-');
        TerminalIO.WriteLine("  ELIGIBILITY");
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteDetail("Service Connected", d.IsServiceConnected ? "YES" : "NO");
        TerminalIO.WriteDetail("SC Percent", d.ServiceConnectedPercent?.ToString());
        TerminalIO.WriteDetail("Veteran", d.IsVeteran ? "YES" : "NO");
        TerminalIO.WriteBlank();

        TerminalIO.WriteDivider('-');
        TerminalIO.WriteLine("  CWAD FLAGS");
        TerminalIO.WriteDivider('-');
        string cwad = ctx.Patient.CwadFlags;
        TerminalIO.WriteDetail("Crisis Note", cwad.Contains('C') ? "YES" : "No");
        TerminalIO.WriteDetail("Warning", cwad.Contains('W') ? "YES" : "No");
        TerminalIO.WriteDetail("Allergy", cwad.Contains('A') ? "YES" : "No");
        TerminalIO.WriteDetail("Adv. Directive", cwad.Contains('D') ? "YES" : "No");
        TerminalIO.WriteBlank();

        // Allergies summary
        TerminalIO.WriteDivider('-');
        TerminalIO.WriteLine("  ALLERGIES");
        TerminalIO.WriteDivider('-');
        if (cs.Allergies.Count == 0)
            TerminalIO.WriteLine("  No Known Allergies (NKA)");
        else
            foreach (AllergySummary a in cs.Allergies)
                TerminalIO.WriteLine($"  {a.Allergen,-22} {a.Severity ?? "",-10} {string.Join(", ", a.Reactions)}");

        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }
}
