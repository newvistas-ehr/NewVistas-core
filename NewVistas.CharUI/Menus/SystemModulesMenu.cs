// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// System Modules sub-menu — system-level modules that operate outside patient context.
/// These access grains directly via IClusterClient rather than through IPatientWorkflowGrain.
/// </summary>
public class SystemModulesMenu : IMenu
{
    public string Title => "System Modules";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("System Modules", [
            ("IC", "Infection Control", PlaceholderAsync),
            ("SP", "Suicide Prevention", PlaceholderAsync),
            ("QM", "Quality Management", PlaceholderAsync),
            ("EN", "Engineering", PlaceholderAsync),
            ("PA", "Patient Advocate", PlaceholderAsync),
            ("RI", "Release of Information", PlaceholderAsync),
            ("TP", "Transplant", PlaceholderAsync),
            ("CS", "Controlled Substances", PlaceholderAsync),
            ("CR", "Clinical Case Registries", PlaceholderAsync),
            ("PT", "Polytrauma/TBI", PlaceholderAsync),
            ("RT", "Record Tracking", PlaceholderAsync),
            ("GE", "Geriatrics & Extended Care", PlaceholderAsync),
            ("HH", "Home Health", PlaceholderAsync),
            ("RB", "Research/IRB", PlaceholderAsync),
            ("VS", "Voluntary Service", PlaceholderAsync),
            ("IB", "Integrated Billing", PlaceholderAsync),
            ("AR", "Accounts Receivable", PlaceholderAsync),
            ("FB", "Fee Basis", PlaceholderAsync),
            ("AC", "Agent Cashier", PlaceholderAsync),
            ("ED", "EDI / Electronic Billing", PlaceholderAsync),
            ("IF", "IFCAP (Procurement)", PlaceholderAsync),
        ], ctx, requiresPatient: false, showBanner: false);
    }

    private static Task PlaceholderAsync(MenuContext ctx)
    {
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  This module is not yet implemented in the Character UI.");
        TerminalIO.WriteLine("  Use the Blazor or WPF interface for this functionality.");
        TerminalIO.Pause();
        return Task.CompletedTask;
    }
}
