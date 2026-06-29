// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Components.Authorization;
using NewVistas.BlazorWeb.Components;
using NewVistas.BlazorWeb.Services;
using NewVistas.ImageStorage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── Orleans Client ──────────────────────────────────────────────────────────
// Direct grain access — analogous to VistA's RPC Broker (XWB) connecting CPRS
// to the M server. Pages inject IGrainFactory instead of calling the REST API.
builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    // Matches SiloMessagingOptions.ResponseTimeout in CommonSiloConfig.
    clientBuilder.Configure<Orleans.Configuration.ClientMessagingOptions>(options =>
        options.ResponseTimeout = TimeSpan.FromSeconds(60));

    // Required to invoke the transactional AR money-path grains (silo enables the
    // matching UseTransactions in CommonSiloConfig).
    clientBuilder.UseTransactions();

    if (context.HostingEnvironment.IsDevelopment())
    {
        // Development: connect to localhost silo (gateway port 30000)
        clientBuilder.UseLocalhostClustering();
    }
    else
    {
        // Production: discover silos via SQL Server clustering table
        var connectionString = context.Configuration.GetConnectionString("OrleansDatabase")
            ?? throw new InvalidOperationException("Orleans database connection string not found.");

        clientBuilder.UseAdoNetClustering(options =>
        {
            options.Invariant = "Microsoft.Data.SqlClient";
            options.ConnectionString = connectionString;
        });
    }
});

// HttpClient for calling the NewVistas API (retained during migration — pages
// will be converted from HttpClient to IGrainFactory incrementally)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7127";
builder.Services.AddHttpClient("NewVistasApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Allow self-signed certs in development
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("NewVistasApi"));

// Authentication state — JWT token stored per-circuit, attached to HttpClient
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());

// Orleans grain service — sets RequestContext (DUZ equivalent) from JWT claims
// before each grain call, so the silo's AuthorizationCallFilter sees the caller.
builder.Services.AddScoped<OrleansGrainService>();

// User security context — scoped (per-circuit) cache of the user's security keys
// and precomputed menu area access. Populated once after login, all subsequent
// checks are O(1) HashSet lookups.
builder.Services.AddScoped<UserSecurityContext>();

// Patient context — scoped (per-circuit) service that persists the selected patient
// across all pages within the same session, like CPRS's DFN patient pointer.
builder.Services.AddScoped<PatientContextService>();

// Imaging pipeline — blob storage (filesystem or Azure), DICOM parsing, thumbnail
// rendering, and the ingestion orchestrator. Selected provider comes from the
// ImageStorage:Provider config value.
builder.Services.AddImageStorage(builder.Configuration);

// Cookie scheme is not used for actual auth (JWT via JwtAuthenticationStateProvider handles that)
// but must be registered so [Authorize] challenges redirect to /login, not the default /Account/Login.
builder.Services.AddAuthentication("BlazorServer")
    .AddCookie("BlazorServer", options => options.LoginPath = "/login");

// Authorization for Blazor components — do NOT set FallbackPolicy here.
// FallbackPolicy applies to all HTTP requests including static files (JS/CSS),
// which breaks Blazor. Instead, use [Authorize] on components/pages and
// <AuthorizeRouteView> in Routes.razor to enforce auth at the component level.
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // HTTPS redirect only in production — development uses HTTP (port 5196)
    // and connects to the WebServer API via HttpClient at the ApiBaseUrl setting.
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// UseStaticFiles serves every physical file from wwwroot/ (including
// _framework/blazor.web.js) regardless of the MapStaticAssets fingerprint
// manifest.  This is required as a fallback because early .NET 10 preview SDK
// builds omit _framework/* entries from the manifest, causing those files to
// return 404 even when they are present in the publish output.
app.UseStaticFiles();

// Blazor Server uses AuthenticationStateProvider for auth, not HTTP-level middleware.
// Static assets and SignalR connections don't need HTTP auth — component-level
// authorization via <AuthorizeRouteView> and [Authorize] handles access control.
app.UseAntiforgery();

app.MapStaticAssets();

// ─── Imaging signed-link endpoint (filesystem provider only) ────────────────
// The filesystem provider's GetReadSasUri returns links that point here.
// The HMAC token encodes blob path + expiry and is verified on each request.
// Azure provider returns direct-to-blob SAS URIs and bypasses this endpoint.
//
// This is mapped unconditionally but only succeeds when FileSystemImageBlobStorageService
// is the active IImageBlobStorageService — the TryGetService check returns null
// under Azure, in which case no such URI is ever minted so nothing reaches here.
app.MapGet("/api/imaging/signed/{token}", async (
    string token,
    IServiceProvider sp) =>
{
    var fs = sp.GetService<FileSystemImageBlobStorageService>();
    if (fs is null)
        return Results.NotFound();

    string? blobPath = fs.VerifySignedToken(token);
    if (blobPath is null)
        return Results.Unauthorized();

    try
    {
        Stream stream = await fs.DownloadAsync(blobPath);
        string contentType = ImagingContentTypes.GuessFromPath(blobPath);
        return Results.File(stream, contentType, enableRangeProcessing: true);
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
}).AllowAnonymous();

// ─── Site editions endpoint (for the in-app user manual's site-aware badges) ──
// The static manual (wwwroot/manual) fetches this to show which feature modules
// (VistA core / RPMS / Modern) are enabled on this site and dim the rest. Anonymous +
// read-only — it exposes only the site's feature-flag names, no patient or config data.
app.MapGet("/api/site/features", async (Orleans.IGrainFactory grains) =>
{
    var site = grains.GetGrain<NewVistas.Abstractions.GrainInterfaces.ISiteParametersGrain>("SITE:DEFAULT");
    string[] all =
    {
        "PATIENT_MERGE", "EPCS",
        "IMMUNIZATION_FORECAST", "EXTERNAL_REFERRAL", "SUBSTANCE_ABUSE_TREATMENT", "PHARMACY_POS",
        "GPRA_REPORTING", "PCC_SURVEILLANCE", "ICARE_DASHBOARD", "APPOINTMENT_WAITLIST",
        "PROVIDER_AVAILABILITY", "PROVIDER_UNAVAILABILITY_BATCH", "PATIENT_SELF_SCHEDULING", "EXTERNAL_PHARMACY",
        "ONCOLOGY",
    };
    var enabled = new List<string>();
    foreach (string f in all)
    {
        if (await site.IsFeatureEnabledAsync(f)) enabled.Add(f);
    }
    return Results.Ok(new { enabled });
}).AllowAnonymous();

app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal static class ImagingContentTypes
{
    public static string GuessFromPath(string path)
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".dcm" => "application/dicom",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }
}
