// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewVistas.CharUI.Core;
using NewVistas.CharUI.Menus;

Console.Title = "NewVistas Character UI";

// Initialize ANSI color support (respects --no-color flag and NO_COLOR env var)
TerminalColor.Initialize(args);

TerminalIO.Clear();
TerminalIO.WriteDivider('=');
TerminalIO.WriteLine("  NEWVISTAS CLINICAL INFORMATION SYSTEM");
TerminalIO.WriteLine("  VistA-Style Character User Interface");
TerminalIO.WriteDivider('=');
TerminalIO.WriteBlank();
TerminalIO.WriteLine("Connecting to NewVistas silo...");

var host = Host.CreateDefaultBuilder(args)
    .UseOrleansClient(clientBuilder =>
    {
        // Matches the silo's UseTransactions (CommonSiloConfig) so AR money-path
        // grain calls can run in an Orleans ACID transaction.
        clientBuilder.UseTransactions();
        clientBuilder.UseLocalhostClustering();
    })
    .Build();

await host.StartAsync();
TerminalIO.WriteLine("Connected.");
TerminalIO.WriteBlank();

IClusterClient client = host.Services.GetRequiredService<IClusterClient>();
var session = new SessionState();
var patient = new PatientContext();
var ctx = new MenuContext { Client = client, Patient = patient, Session = session };

// ── VistA-style Sign-On ────────────────────────────────────────────
// Mirrors XU Kernel: XUSRB validates Access/Verify codes,
// SETUP^XUSRB loads DUZ, keys, division.
var login = new LoginMenu();
await login.RunAsync(ctx);

if (!session.IsLoggedIn)
{
    // Login failed or user cancelled — exit immediately
    await host.StopAsync(TimeSpan.FromSeconds(5));
    host.Dispose();
    return;
}

// ── Main Menu Loop ─────────────────────────────────────────────────
var mainMenu = new MainMenu();
await mainMenu.RunAsync(ctx);

// ── Sign-Off ───────────────────────────────────────────────────────
TerminalIO.WriteBlank();
TerminalIO.WriteLine("Ending session...");

try
{
    // End the Orleans session (mirrors VistA automatic logoff)
    await ctx.GetAccessControl().EndSessionAsync();
}
catch { /* ignore — silo may already be stopped */ }

await host.StopAsync(TimeSpan.FromSeconds(5));
host.Dispose();
TerminalIO.WriteLine("Goodbye.");
