// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using NewVistas.PatientPortal.Components;
using NewVistas.PatientPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ─── Razor Components (Blazor Server) ────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── API Controllers ─────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ─── Patient JWT Authentication ──────────────────────────────────────────────
// Separate from clinician auth — patients get their own JWT with a "patient_id" claim.
// Uses a different issuer/audience from the clinician WebServer.
string jwtKey = builder.Configuration["Jwt:Key"] ?? "NewVistas-Patient-Portal-Key-Must-Be-32-Bytes!";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NewVistas-PatientPortal";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NewVistas-PatientPortal";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorizationCore(options =>
{
    // Blazor pages require auth by default; Login/Register use [AllowAnonymous]
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

// ─── Blazor Auth State (JWT in-memory per circuit) ───────────────────────────
builder.Services.AddScoped<PatientAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<PatientAuthStateProvider>());

// ─── HttpClient (calls our own API controllers on the same host) ─────────────
builder.Services.AddHttpClient("PatientPortalApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var nav = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    var client = factory.CreateClient("PatientPortalApi");
    client.BaseAddress = new Uri(nav.BaseUri);
    return client;
});

// ─── Orleans Client ──────────────────────────────────────────────────────────
// Connects to the same Orleans silo cluster as WebServer, but only exposes
// patient-scoped grain calls through the portal controllers.
builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    if (context.HostingEnvironment.IsDevelopment())
    {
        clientBuilder.UseLocalhostClustering();
    }
    else
    {
        var connectionString = context.Configuration.GetConnectionString("OrleansDatabase")
            ?? throw new InvalidOperationException("Orleans database connection string not found.");
        clientBuilder.UseAdoNetClustering(options =>
        {
            options.Invariant = "Microsoft.Data.SqlClient";
            options.ConnectionString = connectionString;
        });
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
