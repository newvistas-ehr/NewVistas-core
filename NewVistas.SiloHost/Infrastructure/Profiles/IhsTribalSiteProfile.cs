// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// IHS / tribal health authority deployment profile. Configures a silo to run
/// as a hub or per-clinic spoke under a tribal health authority, with:
///   • <see cref="IhsTribalEligibilityPolicy"/> wired into registration —
///     applies 38 CFR Part 136 IHS Beneficiary Eligibility rules
///   • Cluster identity from a 9xx ICN prefix (per
///     <c>ClusterPrefixAllocations.md</c>; default 910 for the hub)
///   • SQL-backed federation outbox + HTTP transport (use
///     <see cref="RemoteOfflineProfile"/> for offline / sneakernet sites)
///   • Pre-enabled site features that a tribal clinic relies on:
///     PATIENT_MERGE, IMMUNIZATION_FORECAST, EXTERNAL_REFERRAL_TRACKING,
///     APPOINTMENT_WAITLIST, PATIENT_RECALL, AUTO_REFILL,
///     ENCOUNTER_FORM_TEMPLATES, GPRA_REPORTING, ICARE_DASHBOARD
///
/// Real deployments override the defaults via <c>Federation:LocalClusterId</c>
/// and <c>Federation:IcnPrefix</c> in configuration. Each clinic in a
/// multi-facility tribal authority should use its own prefix from the 9xx
/// allocation block (e.g., 910 hub, 911-913 spokes).
///
/// See <see href="../../../NewVistas.Abstractions/Docs/Architect-decisions/ADR-001-Patient-Identity-Strategy.md">ADR-001</see>
/// and the tribal-deployment plan for context.
/// </summary>
public sealed class IhsTribalSiteProfile : ISiteProfile
{
    public string Name => "ihs-tribal";
    public bool UsesFederationOutbox => true;

    public const string ConnectionStringName = "SqlExpress";

    /// <summary>
    /// Site features baked in by this profile. Operators can still toggle
    /// these at runtime via the API; the profile just ensures they start on.
    /// </summary>
    public static readonly IReadOnlyList<string> PreEnabledFeatures = new[]
    {
        "PATIENT_MERGE",
        "IMMUNIZATION_FORECAST",
        "EXTERNAL_REFERRAL_TRACKING",
        "APPOINTMENT_WAITLIST",
        "PATIENT_RECALL",
        "AUTO_REFILL",
        "ENCOUNTER_FORM_TEMPLATES",
        "GPRA_REPORTING",
        "ICARE_DASHBOARD",
        "DIABETES_REGISTRY",
    };

    public void ConfigureSilo(ISiloBuilder siloBuilder, HostApplicationBuilder host, IReadOnlyList<string> storeNames)
    {
        siloBuilder.UseLocalhostClustering();

        string connStr = host.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringName} connection string not found. Ensure '{ConnectionStringName}' is defined in ConnectionStrings.");

        foreach (string storeName in storeNames)
        {
            siloBuilder.AddAdoNetGrainStorage(storeName, options =>
            {
                options.Invariant = "Microsoft.Data.SqlClient";
                options.ConnectionString = connStr;
            });
        }

        // Cluster identity. Defaults pin a tribal hub; per-clinic spokes
        // override Federation:LocalClusterId and Federation:IcnPrefix in their
        // own appsettings (one prefix per clinic from the 9xx block).
        string clusterId = host.Configuration["Federation:LocalClusterId"] ?? "TRIBAL-HUB";
        string icnPrefix = host.Configuration["Federation:IcnPrefix"] ?? "910";
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity(clusterId, icnPrefix));

        // IHS-aligned eligibility — 38 CFR Part 136. Registered BEFORE
        // AddCommonSiloServices so the TryAdd default (no-op) is skipped.
        siloBuilder.Services.AddSingleton<IRegistrationEligibilityPolicy, IhsTribalEligibilityPolicy>();

        // Outbox-backed MPI federation announcer — peer tribal-authority
        // clinics receive patient-registered and patient-merged announcements
        // via the same federation outbox + transport that clinical events
        // use. Registered BEFORE AddCommonSiloServices so the TryAdd default
        // (no-op) is skipped.
        siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, OutboxMpiFederationAnnouncer>();

        // Federation outbox: same shape as RemoteOnline (SQL outbox + HTTP
        // transport with mTLS when configured; logging fallback otherwise).
        siloBuilder
            .AddFederationOutbox(host, connStr)
            .AddFederationHttpOrLoggingTransport(host)
            .AddFederationDrainer()
            .AddFederationRenewal(host);

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();

        // Pre-enable the features tribal sites rely on. Operators can still
        // toggle these at runtime; the seeder runs once per startup and is
        // idempotent.
        host.Services.AddHostedService(sp => new FeatureFlagSeeder(
            sp.GetRequiredService<IGrainFactory>(),
            PreEnabledFeatures,
            sp.GetRequiredService<ILogger<FeatureFlagSeeder>>()));
    }
}
