// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orleans.Configuration;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Importers;
using NewVistas.Abstractions.Security;
using NewVistas.ImageStorage;
using NewVistas.WebServer.Infrastructure;
using NewVistas.WebServer.Infrastructure.Federation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

// Parse --dataset command-line argument before building the app
string? datasetOverride = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--dataset" && i + 1 < args.Length)
        datasetOverride = args[i + 1];
}

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ─── Layer 1: ASP.NET Core Identity + JWT Authentication ─────────────────────
// Replaces VistA Access/Verify codes (File #200 fields .02/.11) with modern auth.

// Identity database — InMemory for dev, SQL Server for production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<NewVistasIdentityDbContext>(options =>
        options.UseInMemoryDatabase("NewVistasIdentity"));
}
else
{
    string identityConnStr = builder.Configuration.GetConnectionString("IdentityDatabase")
        ?? builder.Configuration.GetConnectionString("OrleansDatabase")
        ?? throw new InvalidOperationException("No database connection string found for Identity.");
    builder.Services.AddDbContext<NewVistasIdentityDbContext>(options =>
        options.UseSqlServer(identityConnStr));
}

builder.Services.AddIdentity<NewVistasUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<NewVistasIdentityDbContext>()
.AddDefaultTokenProviders();

string jwtKey = builder.Configuration["Jwt:Key"] ?? "NewVistas-Dev-Key-Must-Be-At-Least-32-Bytes!";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NewVistas";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NewVistas";

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

builder.Services.AddAuthorizationBuilder()
    // Layer 2 policies mapped from VistA Security Keys
    .AddPolicy("CanSignOrders", policy => policy.RequireRole("Provider", "Pharmacist", "Nurse"))
    .AddPolicy("CanVerifyAllergies", policy => policy.RequireRole("Provider", "Pharmacist", "Nurse"))
    .AddPolicy("CanVerifyPharmacy", policy => policy.RequireRole("Pharmacist"))
    .AddPolicy("CanRegisterPatients", policy => policy.RequireRole("RegistrationClerk", "Administrator"))
    .AddPolicy("CanViewAuditTrail", policy => policy.RequireRole("PrivacyOfficer", "Administrator", "ChiefOfStaff"))
    .AddPolicy("CanManageControlledSubstances", policy => policy.RequireRole("Pharmacist", "Provider"));

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Drug safety advisory ingestion seam. The offline default returns a curated seed
// (incl. the 2010 PPI/fracture DSC); a live openFDA/DailyMed client can replace it.
builder.Services.AddSingleton<NewVistas.Abstractions.Services.IFdaDrugWarningSource,
                              NewVistas.Abstractions.Services.StaticFdaDrugWarningSource>();

// MPI inbound handler — applied by FederationInboundApplier for Domain=="MPI"
// envelopes (patient registered, patient merged). DefaultMpiInboundHandler is
// the standard implementation; deployments needing additional MPI event types
// can replace this registration with their own IMpiInboundHandler.
builder.Services.AddSingleton<NewVistas.Abstractions.Federation.IMpiInboundHandler,
                              NewVistas.Abstractions.Federation.DefaultMpiInboundHandler>();

// Federation inbound applier — receives clinical event envelopes shipped from
// peer clusters via FederationController. Resolves IGrainFactory from the
// Orleans client below.
builder.Services.AddSingleton<NewVistas.Abstractions.Federation.IFederationInboundApplier,
                              NewVistas.Abstractions.Federation.FederationInboundApplier>();

// Federation inbound auth (mTLS). When Federation:Inbound:TrustedCaPath is set,
// this configures cert-based authentication and the FederationPeer policy
// that the inbound controller depends on. When unset, the policy is
// allow-all and a startup warning is logged below.
builder.Services.AddFederationInboundAuth(builder.Configuration);

// Hub-CA — issues federation client certs to spokes on demand. Only enabled
// when Federation:HubCa:Enabled=true; the HubCaController checks this flag
// and 404s on every endpoint when the CA service isn't registered.
bool hubCaEnabled = builder.Services.TryAddHubCa(builder.Configuration);

string? federationTrustedCaPath = builder.Configuration[
    $"{NewVistas.WebServer.Infrastructure.Federation.InboundAuthOptions.SectionName}:{nameof(NewVistas.WebServer.Infrastructure.Federation.InboundAuthOptions.TrustedCaPath)}"];

// When mTLS is enabled, request (don't require) client certs at the Kestrel
// level. The Certificate auth scheme is only consulted by endpoints that
// opt in via [Authorize(AuthenticationSchemes = "Certificate")] — JWT-
// authenticated endpoints continue to work as today.
if (!string.IsNullOrWhiteSpace(federationTrustedCaPath))
{
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ConfigureHttpsDefaults(https =>
        {
            https.ClientCertificateMode =
                Microsoft.AspNetCore.Server.Kestrel.Https.ClientCertificateMode.AllowCertificate;
        });
    });
}

// Imaging pipeline — blob storage (filesystem or Azure), DICOM parsing, thumbnail
// rendering, and the ingestion orchestrator. Selected provider comes from the
// ImageStorage:Provider config value.
builder.Services.AddImageStorage(builder.Configuration);

// Response compression for the external surfaces this server exists for
// (FHIR bundles, portal payloads, lab acknowledgments) — large JSON compresses
// 70-85%. EnableForHttps is safe here: responses are bearer-token-authenticated
// API payloads, not cookie-personalized pages mixing attacker-controlled input.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// ─── Orleans Client ──────────────────────────────────────────────────────────
// Connects to the Orleans SiloHost. Controllers use IGrainFactory to call grains.
builder.Host.UseOrleansClient((context, clientBuilder) =>
{
    // Matches SiloMessagingOptions.ResponseTimeout in CommonSiloConfig.
    clientBuilder.Configure<ClientMessagingOptions>(options =>
        options.ResponseTimeout = TimeSpan.FromSeconds(60));

    // Required because the AR controller invokes transactional AR grain methods
    // (PostPayment/RecordTopOffset/...) directly. Silo enables UseTransactions in
    // CommonSiloConfig.
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

var app = builder.Build();

// Loud-by-default warning: the federation inbound endpoint is open if no
// trust anchor is configured. Production deployments of RemoteOnline must
// configure Federation:Inbound:TrustedCaPath; see the auth plan.
if (string.IsNullOrWhiteSpace(federationTrustedCaPath))
{
    app.Logger.LogWarning(
        "Federation inbound endpoint is UNAUTHENTICATED — '{Setting}' is not configured. " +
        "Set this on RemoteOnline deployments. Production deployments must front this endpoint with a network ACL.",
        $"{InboundAuthOptions.SectionName}:{nameof(InboundAuthOptions.TrustedCaPath)}");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

// Layer 1: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Layer 2: Bridge JWT claims → Orleans RequestContext for grain-level authorization.
// Must run after UseAuthentication (so HttpContext.User is populated) and before
// controllers execute (so RequestContext is set when grain calls happen).
app.UseOrleansRequestContext();

app.MapDefaultEndpoints();
app.MapControllers();

// Ensure Orleans schema tables exist before starting the Orleans client.
// Both SiloHost and WebServer run this idempotently so the schema is guaranteed
// to exist whichever container starts first in Azure Container Apps.
if (!app.Environment.IsDevelopment())
{
    string orleansConnStr = app.Configuration.GetConnectionString("OrleansDatabase")
        ?? throw new InvalidOperationException("Orleans database connection string not found.");
    ILogger schemaLogger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseInitializer");
    await DatabaseInitializer.EnsureSchemaAsync(orleansConnStr, schemaLogger);
}

// Start the host first so the Orleans client connects to the silo and
// downloads the cluster manifest. Grain type resolution requires a live
// connection — calling GetGrain before StartAsync fails with
// "Could not find an implementation for interface".
await app.StartAsync();

// Ensure Identity database is created and seed demo users on startup.
//
// EnsureCreated() calls HasTables() which returns true as soon as *any* table exists.
// Identity and Orleans share the same Azure SQL database, so Orleans tables are already
// present → HasTables() returns true → Identity tables (AspNetUsers, AspNetRoles, …)
// are never created → SqlException 208 "Invalid object name 'AspNetRoles'" at first login.
//
// Fix: check specifically for AspNetUsers, then call CreateTablesAsync() which creates
// only the EF-model tables without the HasTables() guard.
Dictionary<int, string> userIenMap;
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NewVistasIdentityDbContext>();

    if (app.Environment.IsDevelopment())
    {
        // InMemory provider in dev — EnsureCreated() has no HasTables() side-effects.
        dbContext.Database.EnsureCreated();
    }
    else
    {
        // SQL Server: bypass HasTables() with a targeted existence check.
        string connStr = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Identity database connection string is null.");

        using var identityConn = new SqlConnection(connStr);
        await identityConn.OpenAsync();
        using var tableCheckCmd = new SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'AspNetUsers', 'U') IS NOT NULL THEN 1 ELSE 0 END",
            identityConn);
        bool identitySchemaExists = Convert.ToInt32(await tableCheckCmd.ExecuteScalarAsync()) == 1;

        if (!identitySchemaExists)
        {
            // CreateTablesAsync creates all tables defined in the EF model without
            // inspecting whether the database already contains other (Orleans) tables.
            var creator = dbContext.Database.GetService<IRelationalDatabaseCreator>();
            await creator.CreateTablesAsync();
        }
    }

    userIenMap = await SeedDemoUsersAsync(scope.ServiceProvider);
    await SeedDemoSecurityKeysAsync(scope.ServiceProvider);
}

// Seed reference data and demo patients when none exist (all environments).
// In production the database starts empty, so we need the same seeding logic.
{
    var grainFactory = app.Services.GetRequiredService<IGrainFactory>();
    var seedLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ReferenceDataSeed");
    var contentRoot = app.Environment.ContentRootPath;
    await SeedReferenceDataAsync(grainFactory, seedLogger, contentRoot);

    // Seed the marquee demo patient (SICK,EXTREME LEE / P9001) and put him on a demo
    // provider's panel FIRST — before the slow bulk import below. The app is already
    // serving (app.StartAsync above), so a clinician who logs straight in can Open P9001,
    // find him by name, and see him in "My Patients" within seconds, while the P1..P500
    // cohort streams in behind. Both calls are idempotent.
    await ExtremeLeeSickSeed.SeedAsync(grainFactory, seedLogger);
    await MaternalNewbornSeed.SeedAsync(grainFactory, seedLogger);   // P9002 mother + newborn (maternity + neonatal demo)
    await PretermNicuSeed.SeedAsync(grainFactory, seedLogger);       // P9003 mother + preterm NICU newborn (Phase 2 NICU depth)
    await PharmacogenomicsSeed.SeedAsync(grainFactory, seedLogger);  // P9001 PGx profile (drug-gene DUR demo)
    await SeedDemoCareTeamsAsync(app.Services, seedLogger);   // assigns P9001 now; P1..P30 skipped until imported

    // Auto-import demo patients if none exist
    var patientCheck = grainFactory.GetGrain<IPatientGrain>("PATIENT-check");
    var checkState = await patientCheck.GetPatientAsync();
    bool hasPatients = !string.IsNullOrEmpty(checkState.Name);

    if (!hasPatients)
    {
        string? exportsDir = FindExportsDirectory(contentRoot);
        if (exportsDir != null)
        {
            string dataset = datasetOverride
                ?? app.Configuration["Dataset"]
                ?? Environment.GetEnvironmentVariable("NEWVISTAS_DATASET")
                ?? "fivehundred";

            string datasetDir = dataset.ToLowerInvariant() switch
            {
                "fifty" or "50" => Path.Combine(exportsDir, "Fifty"),
                "fivehundred" or "500" => Path.Combine(exportsDir, "FiveHundred"),
                "onethousand" or "1000" => Path.Combine(exportsDir, "OneThousand"),
                _ => Path.Combine(exportsDir, "Fifty")
            };

            if (Directory.Exists(datasetDir))
            {
                seedLogger.LogInformation("Importing {Dataset} dataset from {Path}...", dataset, datasetDir);

                // Convert int-keyed user map to long-keyed for the orchestrator
                var userIenToGrainKey = new Dictionary<long, string>();
                foreach (var kvp in userIenMap)
                    userIenToGrainKey[kvp.Key] = kvp.Value;

                var orchestrator = new ZwrImportOrchestrator(grainFactory, seedLogger);
                var importResult = await orchestrator.ImportAsync(datasetDir, userIenToGrainKey);
                seedLogger.LogInformation(importResult.GetSummaryText());
            }
            else
            {
                seedLogger.LogWarning("Dataset directory not found: {Path} — skipping auto-import", datasetDir);
            }
        }
        else
        {
            seedLogger.LogInformation("No exports directory found — skipping auto-import");
        }
    }

    // Now the imported cohort exists — (re)apply the care-team panels (idempotent; this
    // fills the P1..P30 assignments that were skipped before the import) and seed the
    // per-patient clinical demo data (scheduling, vitals, problems, allergies).
    await SeedDemoCareTeamsAsync(app.Services, seedLogger);
    await SeedDemoClinicalDataAsync(grainFactory, seedLogger);
}

await app.WaitForShutdownAsync();

static string? FindExportsDirectory(string startDir)
{
    // Walk up from content root looking for "exports" directory
    var dir = new DirectoryInfo(startDir);
    while (dir != null)
    {
        string candidate = Path.Combine(dir.FullName, "exports");
        if (Directory.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }
    return null;
}

// ─── Demo user seeding ──────────────────────────────────────────────────────
// Creates VistA-faithful demo users on startup if they don't already exist.
// Each user gets a NewPersonGrain (File #200) with staff directory data.
// Returns a mapping of 1-based index to ASP.NET Identity user ID for IEN mapping.

static async Task<Dictionary<int, string>> SeedDemoUsersAsync(IServiceProvider services)
{
    var userManager = services.GetRequiredService<UserManager<NewVistasUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var grainFactory = services.GetRequiredService<IGrainFactory>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoUserSeed");
    var userIenMap = new Dictionary<int, string>();

    // ── Seed all VistA-mapped roles ──────────────────────────────────────────
    string[] allRoles =
    [
        "Provider", "Nurse", "Pharmacist", "RegistrationClerk", "OrderEntry",
        "LabTechnician", "Radiologist", "Surgeon", "MentalHealth", "SocialWorker",
        "Dietitian", "Administrator", "ChiefOfStaff", "PrivacyOfficer", "ARSupervisor", "Oncologist"
    ];

    foreach (string role in allRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ── Demo users — all use password: smythVista1 ───────────────────────────
    // (meets policy: 8+ chars, uppercase, lowercase, digit, no special char)
    // 72 users: 5 per each of 14 role types + 2 Nurse Practitioners
    const string demoPassword = "smythVista1";

    var demoUsers = new (string UserName, string DisplayName, string[] Roles,
        string Title, string Degree, string ServiceSection, string UserClass,
        string ProviderType, string Specialty)[]
    {
        // Physicians (5)
        ("DOCTOR1", "SMITH,JOHN A", ["Provider", "OrderEntry"],
            "Staff Physician", "MD", "MEDICINE", "PHYSICIAN", "STAFF", "Internal Medicine"),
        ("DOCTOR2", "CHEN,MICHAEL L", ["Provider", "OrderEntry"],
            "Staff Physician", "MD", "MEDICINE", "PHYSICIAN", "STAFF", "Family Medicine"),
        ("DOCTOR3", "PATEL,ARUN K", ["Provider", "OrderEntry"],
            "Staff Physician", "MD", "MEDICINE", "PHYSICIAN", "STAFF", "Cardiology"),
        ("DOCTOR4", "NGUYEN,TRANG T", ["Provider", "OrderEntry"],
            "Staff Physician", "MD", "MEDICINE", "PHYSICIAN", "STAFF", "Pulmonology"),
        ("DOCTOR5", "JACKSON,WILLIAM R", ["Provider", "OrderEntry"],
            "Staff Physician", "MD", "MEDICINE", "PHYSICIAN", "STAFF", "Neurology"),

        // Nurses (5)
        ("NURSE1", "JOHNSON,MARY R", ["Nurse", "OrderEntry"],
            "Registered Nurse", "RN", "NURSING", "NURSE", "STAFF", "Medical-Surgical"),
        ("NURSE2", "THOMPSON,PATRICIA A", ["Nurse", "OrderEntry"],
            "Registered Nurse", "RN", "NURSING", "NURSE", "STAFF", "ICU"),
        ("NURSE3", "RODRIGUEZ,MARIA L", ["Nurse", "OrderEntry"],
            "Registered Nurse", "RN", "NURSING", "NURSE", "STAFF", "Emergency"),
        ("NURSE4", "WILLIAMS,KAREN S", ["Nurse", "OrderEntry"],
            "Registered Nurse", "RN", "NURSING", "NURSE", "STAFF", "Ambulatory Care"),
        ("NURSE5", "DAVIS,ANGELA M", ["Nurse", "OrderEntry"],
            "Licensed Practical Nurse", "LPN", "NURSING", "NURSE", "STAFF", "Primary Care"),

        // Nurse Practitioners (2)
        ("NP1", "CHEN,LISA M", ["Provider", "OrderEntry", "Nurse"],
            "Primary Care NP", "DNP", "PRIMARY CARE", "NURSE PRACTITIONER", "STAFF", "Family Practice"),
        ("NP2", "RIVERA,CARLOS J", ["Provider", "OrderEntry", "Nurse"],
            "Cardiology NP", "MSN", "CARDIOLOGY", "NURSE PRACTITIONER", "STAFF", "Cardiology"),

        // Pharmacists (5)
        ("PHARM1", "WILLIAMS,ROBERT L", ["Pharmacist", "OrderEntry"],
            "Clinical Pharmacist", "PharmD", "PHARMACY", "PHARMACIST", "STAFF", "Clinical Pharmacy"),
        ("PHARM2", "LEE,SANDRA K", ["Pharmacist", "OrderEntry"],
            "Clinical Pharmacist", "PharmD", "PHARMACY", "PHARMACIST", "STAFF", "Oncology Pharmacy"),
        ("PHARM3", "MARTINEZ,CARLOS R", ["Pharmacist", "OrderEntry"],
            "Clinical Pharmacist", "PharmD", "PHARMACY", "PHARMACIST", "STAFF", "Ambulatory Pharmacy"),
        ("PHARM4", "KIM,JENNY H", ["Pharmacist", "OrderEntry"],
            "Clinical Pharmacist", "PharmD", "PHARMACY", "PHARMACIST", "STAFF", "Inpatient Pharmacy"),
        ("PHARM5", "BROWN,DAVID A", ["Pharmacist", "OrderEntry"],
            "Clinical Pharmacist", "PharmD", "PHARMACY", "PHARMACIST", "STAFF", "Psychiatric Pharmacy"),

        // Surgeons (5)
        ("SURGEON1", "DAVIS,SARAH K", ["Surgeon", "Provider", "OrderEntry"],
            "Staff Surgeon", "MD", "SURGERY", "PHYSICIAN", "STAFF", "General Surgery"),
        ("SURGEON2", "MILLER,JAMES P", ["Surgeon", "Provider", "OrderEntry"],
            "Staff Surgeon", "MD", "SURGERY", "PHYSICIAN", "STAFF", "Orthopedic Surgery"),
        ("SURGEON3", "WILSON,ROBERT T", ["Surgeon", "Provider", "OrderEntry"],
            "Staff Surgeon", "MD", "SURGERY", "PHYSICIAN", "STAFF", "Cardiac Surgery"),
        ("SURGEON4", "MOORE,ELIZABETH A", ["Surgeon", "Provider", "OrderEntry"],
            "Staff Surgeon", "MD", "SURGERY", "PHYSICIAN", "STAFF", "Vascular Surgery"),
        ("SURGEON5", "TAYLOR,DAVID L", ["Surgeon", "Provider", "OrderEntry"],
            "Staff Surgeon", "MD", "SURGERY", "PHYSICIAN", "STAFF", "Urology"),

        // Lab Technicians (5)
        ("LABTECH1", "BROWN,JAMES D", ["LabTechnician"],
            "Lab Technologist", "MT", "PATHOLOGY & LAB", "TECHNICIAN", "STAFF", "Clinical Laboratory"),
        ("LABTECH2", "GARCIA,ANA M", ["LabTechnician"],
            "Lab Technologist", "MT", "PATHOLOGY & LAB", "TECHNICIAN", "STAFF", "Hematology"),
        ("LABTECH3", "HARRIS,THOMAS R", ["LabTechnician"],
            "Lab Technologist", "MT", "PATHOLOGY & LAB", "TECHNICIAN", "STAFF", "Microbiology"),
        ("LABTECH4", "CLARK,SUSAN L", ["LabTechnician"],
            "Lab Technologist", "MT", "PATHOLOGY & LAB", "TECHNICIAN", "STAFF", "Chemistry"),
        ("LABTECH5", "LEWIS,MARK A", ["LabTechnician"],
            "Lab Technologist", "MT", "PATHOLOGY & LAB", "TECHNICIAN", "STAFF", "Blood Bank"),

        // Radiologists (5)
        ("RAD1", "GARCIA,MARIA T", ["Radiologist", "Provider"],
            "Radiologist", "MD", "RADIOLOGY", "PHYSICIAN", "STAFF", "Diagnostic Radiology"),
        ("RAD2", "ANDERSON,PAUL R", ["Radiologist", "Provider"],
            "Radiologist", "MD", "RADIOLOGY", "PHYSICIAN", "STAFF", "Interventional Radiology"),
        ("RAD3", "WHITE,LAURA K", ["Radiologist", "Provider"],
            "Radiologist", "MD", "RADIOLOGY", "PHYSICIAN", "STAFF", "Nuclear Medicine"),
        ("RAD4", "THOMAS,KEVIN M", ["Radiologist", "Provider"],
            "Radiologist", "MD", "RADIOLOGY", "PHYSICIAN", "STAFF", "CT/MRI"),
        ("RAD5", "ROBINSON,AMY S", ["Radiologist", "Provider"],
            "Radiologist", "MD", "RADIOLOGY", "PHYSICIAN", "STAFF", "Mammography"),

        // Oncologists (3)
        ("ONC1", "BENNETT,SARAH J", ["Oncologist", "Provider", "OrderEntry"],
            "Medical Oncologist", "MD", "ONCOLOGY", "PHYSICIAN", "STAFF", "Medical Oncology"),
        ("ONC2", "OKAFOR,DANIEL E", ["Oncologist", "Provider", "OrderEntry"],
            "Hematologist-Oncologist", "MD", "ONCOLOGY", "PHYSICIAN", "STAFF", "Hematology-Oncology"),
        ("ONC3", "REYES,MARIA L", ["Oncologist", "Provider", "OrderEntry"],
            "Radiation Oncologist", "MD", "RADIATION ONCOLOGY", "PHYSICIAN", "STAFF", "Radiation Oncology"),

        // Registration Clerks (5)
        ("CLERK1", "MARTINEZ,ANA P", ["RegistrationClerk", "OrderEntry"],
            "Registration Clerk", "", "HEALTH ADMIN", "CLERK", "STAFF", "Patient Registration"),
        ("CLERK2", "YOUNG,STEVEN R", ["RegistrationClerk", "OrderEntry"],
            "Registration Clerk", "", "HEALTH ADMIN", "CLERK", "STAFF", "Admissions"),
        ("CLERK3", "KING,DEBORAH L", ["RegistrationClerk", "OrderEntry"],
            "Registration Clerk", "", "HEALTH ADMIN", "CLERK", "STAFF", "Eligibility"),
        ("CLERK4", "WRIGHT,JENNIFER M", ["RegistrationClerk", "OrderEntry"],
            "Registration Clerk", "", "HEALTH ADMIN", "CLERK", "STAFF", "Scheduling"),
        ("CLERK5", "HILL,ROBERT J", ["RegistrationClerk", "OrderEntry"],
            "Registration Clerk", "", "HEALTH ADMIN", "CLERK", "STAFF", "Medical Records"),

        // Billing Specialists (5)
        ("BILLING1", "SCOTT,MICHELLE A", ["ARSupervisor"],
            "Billing Specialist", "", "FISCAL", "CLERK", "STAFF", "Revenue Cycle"),
        ("BILLING2", "GREEN,RICHARD P", ["ARSupervisor"],
            "Billing Specialist", "", "FISCAL", "CLERK", "STAFF", "Accounts Receivable"),
        ("BILLING3", "BAKER,NANCY L", ["ARSupervisor"],
            "Billing Specialist", "", "FISCAL", "CLERK", "STAFF", "Third Party"),
        ("BILLING4", "ADAMS,CHARLES M", ["ARSupervisor"],
            "Billing Specialist", "", "FISCAL", "CLERK", "STAFF", "Fee Basis"),
        ("BILLING5", "NELSON,CAROL S", ["ARSupervisor"],
            "Billing Specialist", "", "FISCAL", "CLERK", "STAFF", "Collections"),

        // HIM Specialists (5)
        ("HIM1", "HALL,PATRICIA R", ["PrivacyOfficer"],
            "HIM Specialist", "", "HIM", "CLERK", "STAFF", "Medical Records"),
        ("HIM2", "ALLEN,BARBARA J", ["PrivacyOfficer"],
            "HIM Specialist", "", "HIM", "CLERK", "STAFF", "Release of Information"),
        ("HIM3", "YOUNG,TIMOTHY K", ["PrivacyOfficer"],
            "Coding Specialist", "CCS", "HIM", "CLERK", "STAFF", "ICD-10 Coding"),
        ("HIM4", "HERNANDEZ,ROSA M", ["PrivacyOfficer"],
            "HIM Specialist", "", "HIM", "CLERK", "STAFF", "Transcription"),
        ("HIM5", "KING,LAWRENCE A", ["PrivacyOfficer"],
            "HIM Specialist", "", "HIM", "CLERK", "STAFF", "Record Tracking"),

        // Quality/Safety Officers (5)
        ("QM1", "TURNER,JANET L", ["Administrator"],
            "Quality Manager", "", "QUALITY MGMT", "ADMINISTRATOR", "STAFF", "Patient Safety"),
        ("QM2", "PHILLIPS,MARK D", ["Administrator"],
            "Quality Analyst", "", "QUALITY MGMT", "ADMINISTRATOR", "STAFF", "Performance Improvement"),
        ("QM3", "CAMPBELL,DIANE S", ["Administrator"],
            "Infection Preventionist", "CIC", "QUALITY MGMT", "ADMINISTRATOR", "STAFF", "Infection Control"),
        ("QM4", "PARKER,JAMES R", ["Administrator"],
            "Risk Manager", "", "QUALITY MGMT", "ADMINISTRATOR", "STAFF", "Risk Management"),
        ("QM5", "EVANS,SUSAN M", ["Administrator"],
            "Patient Advocate", "", "QUALITY MGMT", "ADMINISTRATOR", "STAFF", "Patient Relations"),

        // Mental Health Providers (5)
        ("MH1", "WILSON,THOMAS E", ["MentalHealth", "Provider", "OrderEntry"],
            "Psychiatrist", "MD", "MENTAL HEALTH", "PHYSICIAN", "STAFF", "Psychiatry"),
        ("MH2", "MOORE,ELIZABETH K", ["MentalHealth", "Provider", "OrderEntry"],
            "Psychologist", "PhD", "MENTAL HEALTH", "PSYCHOLOGIST", "STAFF", "Clinical Psychology"),
        ("MH3", "JACKSON,BRIAN L", ["MentalHealth", "Provider", "OrderEntry"],
            "Psychiatrist", "MD", "MENTAL HEALTH", "PHYSICIAN", "STAFF", "Addiction Psychiatry"),
        ("MH4", "MARTIN,LAURA A", ["MentalHealth", "Provider", "OrderEntry"],
            "Psychologist", "PsyD", "MENTAL HEALTH", "PSYCHOLOGIST", "STAFF", "PTSD Clinical Team"),
        ("MH5", "WHITE,DAVID R", ["MentalHealth", "Provider", "OrderEntry"],
            "Psychiatric NP", "DNP", "MENTAL HEALTH", "NURSE PRACTITIONER", "STAFF", "Mental Health"),

        // Social Workers (5)
        ("SW1", "TAYLOR,LISA M", ["SocialWorker"],
            "Licensed Social Worker", "LCSW", "SOCIAL WORK", "SOCIAL WORKER", "STAFF", "Clinical Social Work"),
        ("SW2", "HARRIS,DONNA J", ["SocialWorker"],
            "Licensed Social Worker", "LCSW", "SOCIAL WORK", "SOCIAL WORKER", "STAFF", "Discharge Planning"),
        ("SW3", "CLARK,RAYMOND A", ["SocialWorker"],
            "Social Worker", "MSW", "SOCIAL WORK", "SOCIAL WORKER", "STAFF", "Homeless Program"),
        ("SW4", "LEWIS,SANDRA K", ["SocialWorker"],
            "Social Worker", "MSW", "SOCIAL WORK", "SOCIAL WORKER", "STAFF", "Substance Abuse"),
        ("SW5", "ROBINSON,KEITH M", ["SocialWorker"],
            "Social Worker", "MSW", "SOCIAL WORK", "SOCIAL WORKER", "STAFF", "Caregiver Support"),

        // Dentists (5)
        ("DENTIST1", "WALKER,MICHAEL J", ["Provider", "OrderEntry"],
            "Dentist", "DDS", "DENTAL", "DENTIST", "STAFF", "General Dentistry"),
        ("DENTIST2", "PEREZ,MARIA C", ["Provider", "OrderEntry"],
            "Dentist", "DMD", "DENTAL", "DENTIST", "STAFF", "Periodontics"),
        ("DENTIST3", "HALL,RICHARD T", ["Provider", "OrderEntry"],
            "Dental Hygienist", "RDH", "DENTAL", "DENTAL HYGIENIST", "STAFF", "Dental Hygiene"),
        ("DENTIST4", "YOUNG,PATRICIA A", ["Provider", "OrderEntry"],
            "Dentist", "DDS", "DENTAL", "DENTIST", "STAFF", "Oral Surgery"),
        ("DENTIST5", "ALLEN,GEORGE M", ["Provider", "OrderEntry"],
            "Dentist", "DMD", "DENTAL", "DENTIST", "STAFF", "Prosthodontics"),

        // System Administrators (5)
        ("ADMIN1", "SMYTH,JAMES B", ["Administrator", "ChiefOfStaff", "PrivacyOfficer"],
            "System Administrator", "", "IRM", "ADMINISTRATOR", "STAFF", "Information Technology"),
        ("ADMIN2", "WRIGHT,KAREN L", ["Administrator"],
            "System Analyst", "", "IRM", "ADMINISTRATOR", "STAFF", "Application Support"),
        ("ADMIN3", "LOPEZ,MIGUEL R", ["Administrator"],
            "Network Specialist", "", "IRM", "ADMINISTRATOR", "STAFF", "Network Operations"),
        ("ADMIN4", "GREEN,SANDRA J", ["Administrator", "PrivacyOfficer"],
            "Security Officer", "", "IRM", "ADMINISTRATOR", "STAFF", "Information Security"),
        ("ADMIN5", "BAKER,PAUL D", ["Administrator"],
            "Database Administrator", "", "IRM", "ADMINISTRATOR", "STAFF", "Database Management"),
    };

    int userIndex = 0;
    foreach (var u in demoUsers)
    {
        userIndex++;

        var existingUser = await userManager.FindByNameAsync(u.UserName);
        if (existingUser != null)
        {
            // Already exists — still record the mapping for IEN resolution, and re-sync
            // the NEW PERSON staff record so the searchable provider directory is
            // populated even on a persistent DB where the user isn't recreated each run.
            userIenMap[userIndex] = existingUser.Id;
            var existingPerson = grainFactory.GetGrain<
                NewVistas.Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{existingUser.Id}");
            await existingPerson.UpdateProfileAsync(
                name: u.DisplayName, title: u.Title, degree: u.Degree,
                serviceSection: u.ServiceSection, userClass: u.UserClass,
                providerType: u.ProviderType, specialty: u.Specialty,
                institutionId: "INST-500", institutionName: "VA MEDICAL CENTER",
                divisionId: "DIV-500", divisionName: "MAIN DIVISION");
            continue;
        }

        var user = new NewVistasUser
        {
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = $"{u.UserName.ToLowerInvariant()}@newvistas.demo"
        };

        IdentityResult result = await userManager.CreateAsync(user, demoPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to create demo user {User}: {Errors}",
                u.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
            continue;
        }

        userIenMap[userIndex] = user.Id;

        foreach (string role in u.Roles)
            await userManager.AddToRoleAsync(user, role);

        // Create the NewPersonGrain staff directory entry (File #200)
        var personGrain = grainFactory.GetGrain<
            NewVistas.Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{user.Id}");
        await personGrain.UpdateProfileAsync(
            name: u.DisplayName,
            title: u.Title,
            degree: u.Degree,
            serviceSection: u.ServiceSection,
            userClass: u.UserClass,
            providerType: u.ProviderType,
            specialty: u.Specialty,
            institutionId: "INST-500",
            institutionName: "VA MEDICAL CENTER",
            divisionId: "DIV-500",
            divisionName: "MAIN DIVISION");

        logger.LogInformation("Seeded demo user: {User} ({Name}) — roles: {Roles}",
            u.UserName, u.DisplayName, string.Join(", ", u.Roles));
    }

    logger.LogInformation("Seeded {Count} demo users ({Mapped} mapped for IEN resolution)",
        userIndex, userIenMap.Count);

    // ── Assign default wards to floor nurses ────────────────────────────────
    var nurseWards = new Dictionary<string, (string WardId, string WardName)>
    {
        ["NURSE1"] = ("WARD-MED-3A", "MEDICAL 3A"),
        ["NURSE2"] = ("WARD-ICU-1", "ICU 1"),
    };

    foreach (var (userName, (wardId, wardName)) in nurseWards)
    {
        var wardUser = await userManager.FindByNameAsync(userName);
        if (wardUser != null)
        {
            var personGrain = grainFactory.GetGrain<
                NewVistas.Abstractions.GrainInterfaces.INewPersonGrain>($"USER:{wardUser.Id}");
            await personGrain.SetDefaultWardAsync(wardId, wardName);
            logger.LogInformation("Assigned {User} to ward {Ward}", userName, wardName);
        }
    }

    return userIenMap;
}

// ─── Demo security key seeding ───────────────────────────────────────────────
// Loads VistA-faithful security keys for all demo users based on their roles.
// Called at startup so the CharUI and other clients see keys without a manual API call.
// Idempotent — overwrites keys on each restart (safe for demo environments).

static async Task SeedDemoSecurityKeysAsync(IServiceProvider services)
{
    var userManager = services.GetRequiredService<UserManager<NewVistasUser>>();
    var grainFactory = services.GetRequiredService<IGrainFactory>();
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSecurityKeySeed");

    var roleKeyMap = new Dictionary<string, string[]>
    {
        ["Provider"]          = [SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMRV_VITALS, SecurityKeys.GMPL_PROBLEM, SecurityKeys.HBHC_MANAGER],
        ["Nurse"]             = [SecurityKeys.ORELSE, SecurityKeys.GMRV_VITALS, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMPL_PROBLEM, SecurityKeys.SD_SCHEDULING, SecurityKeys.HBHC_MANAGER],
        ["Pharmacist"]        = [SecurityKeys.PSO_PHARMACY, SecurityKeys.PSJ_RPHARM, SecurityKeys.PSA_ORDERS, SecurityKeys.PSB_MANAGER],
        ["LabTechnician"]     = [SecurityKeys.LRLAB, SecurityKeys.LRVERIFY],
        ["Radiologist"]       = [SecurityKeys.RA_VERIFY, SecurityKeys.PROVIDER, SecurityKeys.TIU_SIGN],
        ["Surgeon"]           = [SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.SR_SURGERY],
        ["Administrator"]     = [SecurityKeys.XUMGR, SecurityKeys.XUAUDIT, SecurityKeys.DG_SENSITIVITY],
        ["RegistrationClerk"] = [SecurityKeys.SD_SCHEDULING, SecurityKeys.DG_ADMIT],
        ["MentalHealth"]      = [SecurityKeys.PROVIDER, SecurityKeys.TIU_SIGN, SecurityKeys.YS_MH_INSTRUMENT],
        ["Oncologist"]        = [SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.TIU_SIGN, SecurityKeys.GMRA_ALLERGY, SecurityKeys.GMRV_VITALS, SecurityKeys.GMPL_PROBLEM, SecurityKeys.ONCO_MANAGER],
    };

    var users = userManager.Users.ToList();
    int usersKeyed = 0;

    foreach (var user in users)
    {
        var roles = await userManager.GetRolesAsync(user);
        var allKeys = new HashSet<string>();
        foreach (string role in roles)
        {
            if (roleKeyMap.TryGetValue(role, out string[]? keys))
                allKeys.UnionWith(keys);
        }

        if (allKeys.Count > 0)
        {
            var acl = grainFactory.GetGrain<IAccessControlGrain>($"ACL:{user.Id}");
            await acl.SetKeysAsync(allKeys.ToList(), "SYSTEM", "SYSTEM", "Demo provisioning");
            usersKeyed++;
        }
    }

    logger.LogInformation("Seeded demo security keys for {Count}/{Total} users", usersKeyed, users.Count);

    // ── SYSTEM-SEED account ────────────────────────────────────────────────
    // VistA equivalent: POSTMASTER (DUZ=.5) — system-level identity for
    // background operations like demo data seeding. Holds XUPROG (superuser)
    // so demo/load endpoints can call workflow methods regardless of the
    // calling user's keys.
    var systemAcl = grainFactory.GetGrain<IAccessControlGrain>($"ACL:{DemoSeedHelper.SystemUserId}");
    await systemAcl.SetKeysAsync(
        [SecurityKeys.XUPROG], "SYSTEM", "SYSTEM", "System seed account — superuser");
    await systemAcl.StartSessionAsync("500", "NEWVISTAS MEDICAL CENTER", "SystemSeed", "127.0.0.1");
    logger.LogInformation("SYSTEM-SEED account created with XUPROG key");
}

// ─── Reference data seeding
// Seeds reference data that would otherwise be empty with in-memory storage.
// Idempotent — checks if data already exists before loading.

static async Task SeedReferenceDataAsync(IGrainFactory grainFactory, ILogger logger, string contentRoot)
{
    // ── ICD-10-CM codes ─────────────────────────────────────────────────────
    try
    {
        var icd10Index = grainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        var icd10Status = await icd10Index.GetStatusAsync();
        if (icd10Status.TotalCodes == 0)
        {
            var filePath = Path.Combine(contentRoot, "icd10cm-order-2023.txt");
            if (File.Exists(filePath))
            {
                var entries = new List<Icd10IndexEntry>();
                var lines = await File.ReadAllLinesAsync(filePath);
                foreach (var line in lines)
                {
                    if (line.Length < 17) continue;
                    if (!int.TryParse(line[..5].Trim(), out _)) continue;

                    var rawCode = line.Substring(6, 7).Trim();
                    var billableFlag = line[14];
                    var shortDesc = line.Length >= 77 ? line.Substring(16, 60).Trim() : line[16..].Trim();
                    var longDesc = line.Length > 77 ? line[77..].Trim() : shortDesc;
                    var code = rawCode.Length > 3 ? rawCode[..3] + "." + rawCode[3..] : rawCode;

                    entries.Add(new Icd10IndexEntry
                    {
                        Code = code.ToUpperInvariant(),
                        ShortDescription = shortDesc,
                        LongDescription = longDesc,
                        IsBillable = billableFlag == '1'
                    });
                }

                await icd10Index.LoadCodesAsync(entries);
                logger.LogInformation("Seeded {Count} ICD-10-CM codes", entries.Count);
            }
            else
            {
                logger.LogWarning("ICD-10-CM file not found at {Path} — skipping", filePath);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding ICD-10-CM codes");
    }

    // ── NDF Drug Formulary (classes, generics, products) ────────────────────
    try
    {
        var productIndex = grainFactory.GetGrain<IVaProductIndexGrain>("NDF-PRODUCT-INDEX");
        var productStatus = await productIndex.GetStatusAsync();
        if (productStatus.TotalProducts == 0)
        {
            var classIndex = grainFactory.GetGrain<IDrugClassIndexGrain>("NDF-CLASS-INDEX");
            var genericIndex = grainFactory.GetGrain<IVaGenericIndexGrain>("NDF-GENERIC-INDEX");

            List<DrugClassEntry> classes =
            [
                new() { Code = "AM100", Name = "AMINOGLYCOSIDES", IsActive = true },
                new() { Code = "AM110", Name = "AMINOGLYCOSIDES,OTHER", ParentCode = "AM100", IsActive = true },
                new() { Code = "AM200", Name = "CEPHALOSPORINS", IsActive = true },
                new() { Code = "AM210", Name = "CEPHALOSPORINS,1ST GEN", ParentCode = "AM200", IsActive = true },
                new() { Code = "CV000", Name = "CARDIOVASCULAR AGENTS", IsActive = true },
                new() { Code = "CV200", Name = "ANTIHYPERTENSIVE AGENTS", ParentCode = "CV000", IsActive = true },
                new() { Code = "CV400", Name = "ANTIARRHYTHMIC AGENTS", ParentCode = "CV000", IsActive = true },
                new() { Code = "HS000", Name = "HORMONES/SYNTHETICS/MODIFIERS", IsActive = true },
                new() { Code = "HS502", Name = "THYROID", ParentCode = "HS000", IsActive = true },
                new() { Code = "CN000", Name = "CENTRAL NERVOUS SYSTEM AGENTS", IsActive = true },
                new() { Code = "CN101", Name = "ANTIDEPRESSANTS,OTHER", ParentCode = "CN000", IsActive = true },
                new() { Code = "CN104", Name = "ANTIDEPRESSANTS,SSRI", ParentCode = "CN000", IsActive = true },
                new() { Code = "AU000", Name = "AUTONOMIC AGENTS", IsActive = true },
                new() { Code = "AU300", Name = "PARASYMPATHOMIMETICS", ParentCode = "AU000", IsActive = true },
            ];

            List<VaGenericEntry> generics =
            [
                new() { Ien = "1", Name = "ATROPINE", IsActive = true, Vuid = "4019591" },
                new() { Ien = "2", Name = "AMOXICILLIN", IsActive = true, Vuid = "4019679" },
                new() { Ien = "3", Name = "METOPROLOL", IsActive = true, Vuid = "4019800" },
                new() { Ien = "4", Name = "LISINOPRIL", IsActive = true, Vuid = "4019825" },
                new() { Ien = "5", Name = "SERTRALINE", IsActive = true, Vuid = "4021053" },
                new() { Ien = "6", Name = "LEVOTHYROXINE", IsActive = true, Vuid = "4021060" },
                new() { Ien = "7", Name = "GENTAMICIN", IsActive = true, Vuid = "4019700" },
                new() { Ien = "8", Name = "CEPHALEXIN", IsActive = true, Vuid = "4019710" },
                new() { Ien = "9", Name = "AMIODARONE", IsActive = true, Vuid = "4019715" },
                new() { Ien = "10", Name = "WARFARIN", IsActive = true, Vuid = "4019760" },
            ];

            List<VaProductIndexEntry> products =
            [
                new() { Ien = "1001", Name = "ATROPINE SO4 0.4MG/ML INJ", VaGenericIen = "1", VaGenericName = "ATROPINE", DosageFormName = "INJECTION", Strength = "0.4", StrengthUnitName = "MG/ML", PrimaryDrugClassCode = "AU300", PrimaryDrugClassName = "PARASYMPATHOMIMETICS", FormularyIndicator = true, IsActive = true, RxNormCode = "1190692", NdcCodes = ["0002-1200-01", "0002-1200-10"] },
                new() { Ien = "1002", Name = "ATROPINE SO4 0.4MG TAB", VaGenericIen = "1", VaGenericName = "ATROPINE", DosageFormName = "TABLET", Strength = "0.4", StrengthUnitName = "MG", PrimaryDrugClassCode = "AU300", PrimaryDrugClassName = "PARASYMPATHOMIMETICS", FormularyIndicator = true, IsActive = true, NdcCodes = ["0002-1201-01"] },
                new() { Ien = "2001", Name = "AMOXICILLIN 250MG CAP", VaGenericIen = "2", VaGenericName = "AMOXICILLIN", DosageFormName = "CAPSULE", Strength = "250", StrengthUnitName = "MG", PrimaryDrugClassCode = "AM200", PrimaryDrugClassName = "CEPHALOSPORINS", FormularyIndicator = true, IsActive = true, NdcCodes = ["0093-3107-01"] },
                new() { Ien = "2002", Name = "AMOXICILLIN 500MG CAP", VaGenericIen = "2", VaGenericName = "AMOXICILLIN", DosageFormName = "CAPSULE", Strength = "500", StrengthUnitName = "MG", PrimaryDrugClassCode = "AM200", PrimaryDrugClassName = "CEPHALOSPORINS", FormularyIndicator = true, IsActive = true, NdcCodes = ["0093-3108-01", "0093-3108-05"] },
                new() { Ien = "3001", Name = "METOPROLOL TARTRATE 25MG TAB", VaGenericIen = "3", VaGenericName = "METOPROLOL", DosageFormName = "TABLET", Strength = "25", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS", FormularyIndicator = true, IsActive = true, RxNormCode = "866514", NdcCodes = ["0006-0059-54"] },
                new() { Ien = "3002", Name = "METOPROLOL TARTRATE 50MG TAB", VaGenericIen = "3", VaGenericName = "METOPROLOL", DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS", FormularyIndicator = true, IsActive = true, RxNormCode = "866516", NdcCodes = ["0006-0060-54"] },
                new() { Ien = "4001", Name = "LISINOPRIL 5MG TAB", VaGenericIen = "4", VaGenericName = "LISINOPRIL", DosageFormName = "TABLET", Strength = "5", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS", FormularyIndicator = true, IsActive = true, RxNormCode = "311353", NdcCodes = ["0006-0019-54"] },
                new() { Ien = "4002", Name = "LISINOPRIL 10MG TAB", VaGenericIen = "4", VaGenericName = "LISINOPRIL", DosageFormName = "TABLET", Strength = "10", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS", FormularyIndicator = true, IsActive = true, RxNormCode = "311354", NdcCodes = ["0006-0020-54"] },
                new() { Ien = "5001", Name = "SERTRALINE HCL 50MG TAB", VaGenericIen = "5", VaGenericName = "SERTRALINE", DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG", PrimaryDrugClassCode = "CN104", PrimaryDrugClassName = "ANTIDEPRESSANTS,SSRI", FormularyIndicator = true, IsActive = true, RxNormCode = "312940", NdcCodes = ["0049-4900-30"] },
                new() { Ien = "5002", Name = "SERTRALINE HCL 100MG TAB", VaGenericIen = "5", VaGenericName = "SERTRALINE", DosageFormName = "TABLET", Strength = "100", StrengthUnitName = "MG", PrimaryDrugClassCode = "CN104", PrimaryDrugClassName = "ANTIDEPRESSANTS,SSRI", FormularyIndicator = true, IsActive = true, NdcCodes = ["0049-4960-30"] },
                new() { Ien = "6001", Name = "LEVOTHYROXINE NA 0.05MG TAB", VaGenericIen = "6", VaGenericName = "LEVOTHYROXINE", DosageFormName = "TABLET", Strength = "0.05", StrengthUnitName = "MG", PrimaryDrugClassCode = "HS502", PrimaryDrugClassName = "THYROID", FormularyIndicator = true, IsActive = true, NdcCodes = ["0048-1040-03"] },
                new() { Ien = "7001", Name = "GENTAMICIN SO4 40MG/ML INJ", VaGenericIen = "7", VaGenericName = "GENTAMICIN", DosageFormName = "INJECTION", Strength = "40", StrengthUnitName = "MG/ML", PrimaryDrugClassCode = "AM100", PrimaryDrugClassName = "AMINOGLYCOSIDES", FormularyIndicator = true, IsActive = true, NdcCodes = ["0002-7232-50"] },
                new() { Ien = "8001", Name = "CEPHALEXIN 250MG CAP", VaGenericIen = "8", VaGenericName = "CEPHALEXIN", DosageFormName = "CAPSULE", Strength = "250", StrengthUnitName = "MG", PrimaryDrugClassCode = "AM210", PrimaryDrugClassName = "CEPHALOSPORINS,1ST GEN", FormularyIndicator = true, IsActive = true, NdcCodes = ["0093-3140-01"] },
                new() { Ien = "9001", Name = "AMIODARONE HCL 200MG TAB", VaGenericIen = "9", VaGenericName = "AMIODARONE", DosageFormName = "TABLET", Strength = "200", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV400", PrimaryDrugClassName = "ANTIARRHYTHMIC AGENTS", FormularyIndicator = false, IsActive = true, NdcCodes = ["0187-0090-90"] },
                new() { Ien = "10001", Name = "WARFARIN NA 5MG TAB", VaGenericIen = "10", VaGenericName = "WARFARIN", DosageFormName = "TABLET", Strength = "5", StrengthUnitName = "MG", PrimaryDrugClassCode = "CV000", PrimaryDrugClassName = "CARDIOVASCULAR AGENTS", FormularyIndicator = true, IsActive = true, RxNormCode = "855332", NdcCodes = ["0056-0170-70"] },
            ];

            await Task.WhenAll(
                classIndex.LoadClassesAsync(classes),
                genericIndex.LoadGenericsAsync(generics)
            );
            await productIndex.LoadProductsAsync(products);
            logger.LogInformation("Seeded NDF formulary: {Products} products, {Generics} generics, {Classes} classes",
                products.Count, generics.Count, classes.Count);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding NDF formulary data");
    }

    // ── Drug File (File #50 drugs + orderable items) ────────────────────────
    try
    {
        var drugIndex = grainFactory.GetGrain<IDrugIndexGrain>("DRUG-INDEX");
        var drugStatus = await drugIndex.GetStatusAsync();
        if (drugStatus.TotalDrugs == 0)
        {
            var oiIndex = grainFactory.GetGrain<IOrderableItemIndexGrain>("OI-INDEX");

            List<DrugIndexEntry> drugs =
            [
                new() { Ien = "D001", LocalName = "ASPIRIN TAB,EC 325MG",        VaGenericName = "ASPIRIN",       PrimaryDrugClassCode = "CN104", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D002", LocalName = "METFORMIN HCL TAB 500MG",     VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D003", LocalName = "METFORMIN HCL TAB 1000MG",    VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D004", LocalName = "LISINOPRIL TAB 10MG",         VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D005", LocalName = "LISINOPRIL TAB 20MG",         VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D006", LocalName = "ATORVASTATIN TAB 40MG",       VaGenericName = "ATORVASTATIN",  PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D007", LocalName = "OMEPRAZOLE CAP,DR 20MG",      VaGenericName = "OMEPRAZOLE",    PrimaryDrugClassCode = "GA301", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D008", LocalName = "METOPROLOL SUCC TAB,SA 50MG", VaGenericName = "METOPROLOL",    PrimaryDrugClassCode = "CV100", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D009", LocalName = "AMLODIPINE TAB 5MG",          VaGenericName = "AMLODIPINE",    PrimaryDrugClassCode = "CV200", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D010", LocalName = "LEVOTHYROXINE TAB 50MCG",     VaGenericName = "LEVOTHYROXINE", PrimaryDrugClassCode = "TH900", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D011", LocalName = "SERTRALINE HCL TAB 50MG",     VaGenericName = "SERTRALINE",    PrimaryDrugClassCode = "CN609", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D012", LocalName = "ALBUTEROL INH SOLN 0.083%",   VaGenericName = "ALBUTEROL",     PrimaryDrugClassCode = "RE102", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D013", LocalName = "FUROSEMIDE TAB 40MG",         VaGenericName = "FUROSEMIDE",    PrimaryDrugClassCode = "CV702", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D014", LocalName = "WARFARIN SODIUM TAB 5MG",     VaGenericName = "WARFARIN",      PrimaryDrugClassCode = "BL110", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "D015", LocalName = "PREDNISONE TAB 10MG",         VaGenericName = "PREDNISONE",    PrimaryDrugClassCode = "HS051", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = false },
            ];

            List<OrderableItemIndexEntry> ois =
            [
                new() { Ien = "OI001", Name = "ASPIRIN TAB",         VaGenericName = "ASPIRIN",       PrimaryDrugClassCode = "CN104", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI002", Name = "METFORMIN HCL TAB",   VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI003", Name = "LISINOPRIL TAB",      VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI004", Name = "ATORVASTATIN TAB",    VaGenericName = "ATORVASTATIN",  PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI005", Name = "OMEPRAZOLE CAP",      VaGenericName = "OMEPRAZOLE",    PrimaryDrugClassCode = "GA301", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI006", Name = "METOPROLOL SUCC TAB", VaGenericName = "METOPROLOL",    PrimaryDrugClassCode = "CV100", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI007", Name = "ALBUTEROL INH",       VaGenericName = "ALBUTEROL",     PrimaryDrugClassCode = "RE102", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true },
                new() { Ien = "OI008", Name = "WARFARIN SODIUM TAB", VaGenericName = "WARFARIN",      PrimaryDrugClassCode = "BL110", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true },
            ];

            await drugIndex.LoadDrugsAsync(drugs);
            await oiIndex.LoadItemsAsync(ois);

            // Persist individual drug grains for detail lookups
            foreach (DrugIndexEntry entry in drugs)
            {
                var drug = new DrugState
                {
                    Ien = entry.Ien,
                    LocalName = entry.LocalName,
                    VaProductIen = entry.VaProductIen,
                    VaGenericIen = entry.VaGenericIen,
                    VaGenericName = entry.VaGenericName,
                    PrimaryDrugClassCode = entry.PrimaryDrugClassCode,
                    SecondaryDrugClassCodes = new List<string>(entry.SecondaryDrugClassCodes),
                    PharmacyType = entry.PharmacyType,
                    IsActive = entry.IsActive,
                    IsNationalFormulary = entry.IsNationalFormulary
                };
                await grainFactory.GetGrain<IDrugGrain>(entry.Ien).SaveDrugAsync(drug);
            }

            // Persist individual orderable item grains
            foreach (OrderableItemIndexEntry entry in ois)
            {
                var item = new OrderableItemState
                {
                    Ien = entry.Ien,
                    Name = entry.Name,
                    PharmacyType = entry.PharmacyType,
                    VaGenericIen = entry.VaGenericIen,
                    VaGenericName = entry.VaGenericName,
                    PrimaryDrugClassCode = entry.PrimaryDrugClassCode,
                    IsActive = entry.IsActive,
                    IsNationalFormulary = entry.IsNationalFormulary
                };
                await grainFactory.GetGrain<IPharmacyOrderableItemGrain>(entry.Ien).SaveItemAsync(item);
            }

            logger.LogInformation("Seeded Drug File: {Drugs} drugs, {OIs} orderable items", drugs.Count, ois.Count);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding Drug File data");
    }
}

// ─── Demo care team seeding ─────────────────────────────────────────────────
// Seeds care team assignments linking demo providers/NPs/nurses to demo patients.
// Runs under SYSTEM-SEED context for unrestricted access. Idempotent.

static async Task SeedDemoCareTeamsAsync(IServiceProvider services, ILogger logger)
{
    // UserManager is scoped — create a scope for the lookup
    using var scope = services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<NewVistasUser>>();
    var grainFactory = services.GetRequiredService<IGrainFactory>();

    // Build lookup: username → (userId, displayName)
    string[] careTeamUserNames = ["DOCTOR1", "NP1", "NP2", "NURSE1", "NURSE2"];
    var userLookup = new Dictionary<string, (string UserId, string DisplayName)>();
    foreach (string name in careTeamUserNames)
    {
        var user = await userManager.FindByNameAsync(name);
        if (user != null)
            userLookup[name] = (user.Id, user.DisplayName ?? name);
    }

    if (userLookup.Count == 0)
    {
        logger.LogWarning("No care team users found — skipping care team seeding");
        return;
    }

    // Care team assignments: (patientId, userName, role, specialty, isPcp).
    // Built so several providers have realistic panels backing the "My Patients"
    // list in Patient Lookup. The demo previously assigned only P1–P3, leaving
    // every panel nearly empty. Patient ids P1–P500 are seeded by the FiveHundred
    // dataset; assignments to patients that don't exist are skipped below.
    var assignments = new List<(string PatientId, string UserName, string Role, string? Specialty, bool IsPcp)>();

    // DOCTOR1 — internal-medicine PCP panel: P1–P20.
    for (int i = 1; i <= 20; i++)
        assignments.Add(($"P{i}", "DOCTOR1", "PRIMARY CARE PROVIDER", "Internal Medicine", true));

    // NP1 — family-practice nurse-practitioner panel: P21–P35.
    for (int i = 21; i <= 35; i++)
        assignments.Add(($"P{i}", "NP1", "NURSE PRACTITIONER", "Family Practice", false));

    // NP2 — cardiology nurse practitioner, smaller consult panel.
    foreach (int i in new[] { 2, 5, 8 })
        assignments.Add(($"P{i}", "NP2", "NURSE PRACTITIONER", "Cardiology", false));

    // NURSE1 — Med-Surg ward (WARD-MED-3A): P1–P15.
    for (int i = 1; i <= 15; i++)
        assignments.Add(($"P{i}", "NURSE1", "NURSE", "Medical-Surgical", false));

    // NURSE2 — ICU (WARD-ICU-1): P16–P30.
    for (int i = 16; i <= 30; i++)
        assignments.Add(($"P{i}", "NURSE2", "NURSE", "ICU", false));

    // The rich narrative demo patient SICK,EXTREME LEE (P9001). His chart is attributed
    // to pseudonymous specialists, so without this he is reachable only by direct ID
    // lookup. Put him on DOCTOR1's panel (and a nurse's) so he appears in "My Patients".
    assignments.Add(("P9001", "DOCTOR1", "PRIMARY CARE PROVIDER", "Internal Medicine", true));
    assignments.Add(("P9001", "NURSE1", "NURSE", "Medical-Surgical", false));

    var saved = DemoSeedHelper.SetSystemContext();
    try
    {
        int seeded = 0;
        foreach (var (patientId, userName, role, specialty, isPcp) in assignments)
        {
            if (!userLookup.TryGetValue(userName, out var info))
                continue;

            // Verify patient exists before assigning
            var patient = grainFactory.GetGrain<IPatientGrain>(patientId);
            var patientState = await patient.GetPatientAsync();
            if (string.IsNullOrEmpty(patientState.Name))
                continue;

            var workflow = grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

            if (isPcp)
            {
                await workflow.SetPcpAsync(info.UserId, info.DisplayName, specialty);
            }
            else
            {
                await workflow.AddCareTeamMemberAsync(
                    info.UserId, info.DisplayName,
                    role, specialty,
                    "DEMO SEED", null);
            }

            seeded++;
        }

        logger.LogInformation("Seeded {Count} care team assignments for demo patients", seeded);
    }
    finally
    {
        DemoSeedHelper.RestoreContext(saved);
    }
}

// ─── Auto-seed clinical demo data ──────────────────────────────────────────
// Creates basic clinical data for demo patients so Cover Sheet displays useful
// information without needing the "Load All Demo Data" button.
// Idempotent — skips patients that already have clinical data.

static async Task SeedDemoClinicalDataAsync(IGrainFactory grainFactory, ILogger logger)
{
    string[] demoPatients = ["P1", "P2", "P3"];

    var saved = DemoSeedHelper.SetSystemContext();
    try
    {
        // Seed clinic index for scheduling
        var clinicIndex = grainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");
        await clinicIndex.SeedDemoClinicsAsync();

        foreach (string pid in demoPatients)
        {
            // Check if patient exists and has data already
            var workflow = grainFactory.GetGrain<IPatientWorkflowGrain>(pid);
            var patient = await workflow.GetPatientAsync();
            if (string.IsNullOrEmpty(patient.Name))
            {
                logger.LogInformation("Demo patient {Id} not found — skipping clinical data seed", pid);
                continue;
            }

            // Check if already seeded (use problems as indicator)
            var problems = await workflow.GetActiveProblemsAsync();
            if (problems.Count > 0)
            {
                logger.LogInformation("Demo patient {Id} already has clinical data — skipping", pid);
                continue;
            }

            logger.LogInformation("Seeding clinical demo data for patient {Id} ({Name})...", pid, patient.Name);

            DateTime now = DateTime.UtcNow;

            // ── Problems ──────────────────────────────────────────────
            await workflow.AddProblemAsync(
                "Type 2 diabetes mellitus without complications", "E11.9",
                "C", "CHRONIC", DateTime.Today.AddMonths(-6),
                "PROV-001", "Dr. Reynolds",
                null, null, false, null);
            await workflow.AddProblemAsync(
                "Essential (primary) hypertension", "I10",
                "C", "CHRONIC", DateTime.Today.AddYears(-2),
                "PROV-001", "Dr. Reynolds",
                null, null, false, null);

            // ── Allergies ─────────────────────────────────────────────
            await workflow.RecordAllergyAsync(
                "PENICILLIN", "DRUG", null,
                "O", ["RASH", "HIVES"], "MODERATE",
                "PROV-001", "Dr. Reynolds", null);
            await workflow.RecordAllergyAsync(
                "SULFA DRUGS", "DRUG CLASS", null,
                "H", ["NAUSEA"], "MILD",
                "PROV-001", "Dr. Reynolds", null);

            // ── Vitals ────────────────────────────────────────────────
            await workflow.RecordVitalsAsync(
                null, null,
                "PROV-001", "Dr. Reynolds",
                now.AddDays(-1),
                new Dictionary<string, string>
                {
                    ["BLOOD PRESSURE"] = "138/88",
                    ["PULSE"] = "76",
                    ["TEMPERATURE"] = "98.4",
                    ["RESPIRATION"] = "18",
                    ["PULSE OXIMETRY"] = "97",
                    ["HEIGHT"] = "68",
                    ["WEIGHT"] = "185",
                },
                null);

            // ── Scheduling (2 upcoming appointments) ──────────────────
            await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-001", "PRIMARY CARE",
                now.AddDays(14).Date.AddHours(9), 30,
                "PROV-001", "Dr. Reynolds",
                "Follow-up visit", "REGULAR");
            await workflow.ScheduleAppointmentAsync(
                "SD-CLINIC-003", "CARDIOLOGY",
                now.AddDays(30).Date.AddHours(10).AddMinutes(30), 45,
                "PROV-002", "Dr. Patel",
                "Annual cardiac evaluation", "FOLLOW-UP");

            logger.LogInformation("Clinical demo data seeded for patient {Id}", pid);
        }

        logger.LogInformation("Clinical demo data seeding complete");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error seeding clinical demo data (non-fatal)");
    }
    finally
    {
        DemoSeedHelper.RestoreContext(saved);
    }
}

// Make Program class public for testing
public partial class Program { }
