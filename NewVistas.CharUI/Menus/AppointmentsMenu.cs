// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Appointments/Scheduling menu — mirrors VistA SDAM2.m / SDAMEVT.m.
/// </summary>
public class AppointmentsMenu : IMenu
{
    public string Title => "Scheduling";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Scheduling", [
            ("1", "Upcoming Appointments", ListUpcomingAsync),
            ("2", "All Appointments", ListAllAsync),
            ("3", "Schedule New Appointment", ScheduleAsync),
            ("4", "Check In", CheckInAsync),
            ("5", "Cancel Appointment", CancelAsync),
        ], ctx);
    }

    private static async Task ListUpcomingAsync(MenuContext ctx)
    {
        List<VisitSummary> visits = await ctx.GetWorkflow().GetUpcomingAppointmentsAsync();
        TerminalIO.WriteHeader("Upcoming Appointments");
        if (visits.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Date/Time", "Clinic", "Provider", "Status"],
                [4, 18, 22, 18, 10],
                visits.Select((v, i) => new[]
                {
                    (i + 1).ToString(),
                    v.AppointmentDateTime.ToString("MM/dd/yyyy HH:mm"),
                    v.ClinicName ?? "", v.ProviderName ?? "", v.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ListAllAsync(MenuContext ctx)
    {
        List<AppointmentEntry> entries = await ctx.GetWorkflow().GetAllAppointmentsAsync();
        TerminalIO.WriteHeader("All Appointments");
        if (entries.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Date/Time", "Clinic", "Type", "Status"],
                [4, 18, 22, 14, 14],
                entries.Select((e, i) => new[]
                {
                    (i + 1).ToString(),
                    e.AppointmentDateTime.ToString("MM/dd/yyyy HH:mm"),
                    e.ClinicName ?? "", e.AppointmentType ?? "", e.Status
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task ScheduleAsync(MenuContext ctx)
    {
        TerminalIO.WriteHeader("Schedule New Appointment");

        // Show available clinics
        List<ClinicEntry> clinics = await ctx.GetWorkflow().GetClinicListAsync();
        if (clinics.Count > 0)
        {
            TerminalIO.WriteLine("  Available Clinics:");
            TerminalIO.WriteNumberedList(clinics, c => $"{c.Name}");
            TerminalIO.WriteBlank();
        }

        string clinicName = TerminalIO.Prompt("Clinic Name");
        if (string.IsNullOrWhiteSpace(clinicName)) return;

        DateTime? apptDate = TerminalIO.PromptDate("Appointment Date");
        if (!apptDate.HasValue) return;

        string time = TerminalIO.Prompt("Time (HH:mm)", "09:00");
        if (TimeSpan.TryParse(time, out TimeSpan ts))
            apptDate = apptDate.Value.Add(ts);

        string duration = TerminalIO.Prompt("Duration (minutes)", "30");
        int.TryParse(duration, out int durationMin);
        if (durationMin <= 0) durationMin = 30;

        string purpose = TerminalIO.Prompt("Purpose (optional)");
        string apptType = TerminalIO.Prompt("Type (REGULAR, WALKIN, TELEPHONE)", "REGULAR");

        if (!TerminalIO.PromptYesNo("Schedule this appointment?")) return;

        string id = await ctx.GetWorkflow().ScheduleAppointmentAsync(
            null!, clinicName, apptDate.Value, durationMin,
            ctx.Session.UserId, ctx.Session.UserName,
            string.IsNullOrWhiteSpace(purpose) ? null : purpose,
            apptType);

        TerminalIO.WriteBlank();
        TerminalIO.WriteLine($"  Appointment scheduled: {id}");
        TerminalIO.Pause();
    }

    private static async Task CheckInAsync(MenuContext ctx)
    {
        List<VisitSummary> visits = await ctx.GetWorkflow().GetUpcomingAppointmentsAsync();
        if (visits.Count == 0)
        {
            TerminalIO.WriteLine("  No upcoming appointments to check in.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Check In");
        TerminalIO.WriteNumberedList(visits,
            v => $"{v.AppointmentDateTime:MM/dd/yyyy HH:mm}  {v.ClinicName ?? ""}  {v.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select appointment (1-{visits.Count})", 1, visits.Count);
        if (!choice.HasValue) return;

        VisitSummary selected = visits[choice.Value - 1];
        await ctx.GetWorkflow().CheckInAsync(selected.AppointmentId, DateTime.UtcNow);
        TerminalIO.WriteLine("  Patient checked in.");
        TerminalIO.Pause();
    }

    private static async Task CancelAsync(MenuContext ctx)
    {
        List<VisitSummary> visits = await ctx.GetWorkflow().GetUpcomingAppointmentsAsync();
        if (visits.Count == 0)
        {
            TerminalIO.WriteLine("  No upcoming appointments to cancel.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Cancel Appointment");
        TerminalIO.WriteNumberedList(visits,
            v => $"{v.AppointmentDateTime:MM/dd/yyyy HH:mm}  {v.ClinicName ?? ""}  {v.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select appointment (1-{visits.Count})", 1, visits.Count);
        if (!choice.HasValue) return;

        VisitSummary selected = visits[choice.Value - 1];
        if (!TerminalIO.PromptYesNo("Cancel this appointment?")) return;

        await ctx.GetWorkflow().CancelAppointmentAsync(selected.AppointmentId);
        TerminalIO.WriteLine("  Appointment cancelled.");
        TerminalIO.Pause();
    }
}
