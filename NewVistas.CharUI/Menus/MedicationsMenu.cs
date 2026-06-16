// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Medications menu — read-only list of active medications.
/// </summary>
public class MedicationsMenu : IMenu
{
    public string Title => "Medications";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Medications", [
            ("1", "List Active Medications", ListActiveAsync),
        ], ctx);
    }

    private static async Task ListActiveAsync(MenuContext ctx)
    {
        List<MedicationSummary> meds = await ctx.GetWorkflow().GetActiveMedicationsAsync();
        TerminalIO.WriteHeader("Active Medications");
        if (meds.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Drug", "Sig", "Status", "Fill Date", "Refills"],
                [4, 26, 20, 8, 12, 7],
                meds.Select((m, i) => new[]
                {
                    (i + 1).ToString(), m.DrugName, m.Sig ?? "", m.Status,
                    m.FillDate?.ToString("MM/dd/yyyy") ?? "",
                    m.RefillsRemaining?.ToString() ?? ""
                }));
        }
        TerminalIO.Pause();
    }
}
