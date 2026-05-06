// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.CharUI.Core;

/// <summary>
/// Reusable VistA-style menu loop engine.
/// Shows options, prompts "Select Action: Quit//", dispatches, repeats until Quit.
/// </summary>
public static class MenuRunner
{
    public static async Task RunMenuLoopAsync(
        string title,
        (string Key, string Label, Func<MenuContext, Task> Handler)[] options,
        MenuContext ctx,
        bool requiresPatient = true,
        bool showBanner = true)
    {
        while (true)
        {
            if (requiresPatient && !ctx.Patient.HasPatient)
            {
                TerminalIO.WriteLine("No patient selected.");
                TerminalIO.Pause();
                return;
            }

            TerminalIO.Clear();
            if (showBanner && ctx.Patient.HasPatient)
                TerminalIO.WritePatientBanner(ctx.Session, ctx.Patient);

            TerminalIO.WriteHeader(title);
            foreach (var opt in options)
                TerminalIO.WriteLine($"   {opt.Key,-4} {opt.Label}");
            TerminalIO.WriteBlank();

            string input = TerminalIO.Prompt("Select Action", "Quit");

            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("Q", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("Quit", StringComparison.OrdinalIgnoreCase))
                return;

            var match = options.FirstOrDefault(o =>
                o.Key.Equals(input, StringComparison.OrdinalIgnoreCase));

            if (match.Handler != null)
            {
                try
                {
                    await match.Handler(ctx);
                }
                catch (Exception ex)
                {
                    TerminalIO.WriteBlank();
                    TerminalIO.WriteError($"*** ERROR: {ex.Message} ***");
                    TerminalIO.Pause();
                }
            }
            else
            {
                TerminalIO.WriteLine($"  ?? Unknown option: {input}");
                TerminalIO.Pause();
            }
        }
    }
}
