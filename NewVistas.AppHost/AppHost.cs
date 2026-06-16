// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
var builder = DistributedApplication.CreateBuilder(args);

// ─── Orleans Silo Host ──────────────────────────────────────────────────────
// Must be running before any Orleans client can connect.
// Exposes silo port 11111 and gateway port 30000 for Orleans clustering.
// Orleans Dashboard is available at port 8080 in development.
var silohost = builder.AddProject<Projects.NewVistas_SiloHost>("silohost");

// ─── REST API Server ────────────────────────────────────────────────────────
// ASP.NET Core API with Identity, JWT auth, and Orleans client.
// Seeds demo users and patient data on first run.
var webserver = builder.AddProject<Projects.NewVistas_WebServer>("webserver")
    .WaitFor(silohost);

// ─── Blazor Server UI (Clinician) ───────────────────────────────────────────
// Clinician-facing Blazor Server app with direct Orleans grain access.
// Also uses HttpClient to call the WebServer API for some operations.
var blazorweb = builder.AddProject<Projects.NewVistas_BlazorWeb>("blazorweb")
    .WaitFor(silohost)
    .WaitFor(webserver);

// ─── Patient Portal ─────────────────────────────────────────────────────────
// Patient-facing Blazor Server app with separate JWT auth.
// Connects to the same Orleans cluster as WebServer.
var patientportal = builder.AddProject<Projects.NewVistas_PatientPortal>("patientportal")
    .WaitFor(silohost);

builder.Build().Run();
