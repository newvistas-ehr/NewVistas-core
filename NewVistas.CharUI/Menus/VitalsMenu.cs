// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Vitals menu — mirrors VistA GMRVFILE.m / GMRVED.m.
/// </summary>
public class VitalsMenu : IMenu
{
    public string Title => "Vitals";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Vitals", [
            ("1", "View Latest Vitals", ViewLatestAsync),
            ("2", "Record Vitals", RecordVitalsAsync),
        ], ctx);
    }

    private static async Task ViewLatestAsync(MenuContext ctx)
    {
        List<VitalSummary> vitals = await ctx.GetWorkflow().GetLatestVitalsAsync();
        TerminalIO.WriteHeader("Latest Vitals");
        if (vitals.Count == 0)
        {
            TerminalIO.WriteLine("  No vitals on file.");
        }
        else
        {
            foreach (VitalSummary v in vitals)
            {
                string flag = TerminalColor.AbnormalFlag(v.AbnormalFlag);
                TerminalIO.WriteLine($"  {v.VitalType,-18} {v.Value,8} {v.Units ?? "",-6}{(flag.Length > 0 ? " " : "")}{flag}   {v.DateTimeTaken:MM/dd/yyyy HH:mm}");
            }
        }
        TerminalIO.Pause();
    }

    private static async Task RecordVitalsAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Record Vitals");
        TerminalIO.WriteLine("  Enter values. Press Enter to skip a measurement.");
        TerminalIO.WriteBlank();

        var vitals = new Dictionary<string, string>();

        string temp = TerminalIO.Prompt("Temperature (F)");
        if (!string.IsNullOrWhiteSpace(temp)) vitals["TEMPERATURE"] = temp;

        string pulse = TerminalIO.Prompt("Pulse");
        if (!string.IsNullOrWhiteSpace(pulse)) vitals["PULSE"] = pulse;

        string resp = TerminalIO.Prompt("Respiration");
        if (!string.IsNullOrWhiteSpace(resp)) vitals["RESPIRATION"] = resp;

        string bp = TerminalIO.Prompt("Blood Pressure (systolic/diastolic)");
        if (!string.IsNullOrWhiteSpace(bp)) vitals["BLOOD PRESSURE"] = bp;

        string spo2 = TerminalIO.Prompt("SpO2 (%)");
        if (!string.IsNullOrWhiteSpace(spo2)) vitals["PULSE OXIMETRY"] = spo2;

        string pain = TerminalIO.Prompt("Pain (0-10)");
        if (!string.IsNullOrWhiteSpace(pain)) vitals["PAIN"] = pain;

        string weight = TerminalIO.Prompt("Weight (lb)");
        if (!string.IsNullOrWhiteSpace(weight)) vitals["WEIGHT"] = weight;

        string height = TerminalIO.Prompt("Height (in)");
        if (!string.IsNullOrWhiteSpace(height)) vitals["HEIGHT"] = height;

        if (vitals.Count == 0)
        {
            TerminalIO.WriteLine("  No vitals entered.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  Values to record:");
        foreach (var kv in vitals)
            TerminalIO.WriteLine($"    {kv.Key,-20} {kv.Value}");

        if (!TerminalIO.PromptYesNo("Save vitals?")) return;

        await ctx.GetWorkflow().RecordVitalsAsync(
            null, null,
            ctx.Session.UserId, ctx.Session.UserName,
            DateTime.UtcNow,
            vitals, null);

        TerminalIO.WriteBlank();
        TerminalIO.WriteSuccess("Vitals recorded successfully.");
        TerminalIO.Pause();
    }
}
