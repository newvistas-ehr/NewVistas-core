// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection.Extensions;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.Reporting;
using NewVistas.Abstractions.Security;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Configuration;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Wiring that every profile applies identically — call filters, log-consistency
/// providers, and the dev-only dashboard. Centralised so the three production
/// profiles can't drift on these.
/// </summary>
internal static class CommonSiloConfig
{
    /// <summary>
    /// Storage provider name used by <see cref="IPatientClinicalEventStreamGrain"/>
    /// for tamper-evident clinical event sourcing. Required on every profile.
    /// </summary>
    public const string ClinicalLogConsistencyProvider = "ClinicalLogConsistency";

    /// <summary>
    /// PubSub store and stream provider names. Both are memory-backed today
    /// regardless of profile — stream subscriptions are transient state that
    /// rebuilds on activation, so persisting them buys nothing.
    /// </summary>
    public const string PubSubStoreName = "PubSubStore";
    public const string LabStreamsProviderName = "LabStreams";

    /// <summary>
    /// Adds the audit + authorization call filters, the clinical log-consistency
    /// provider, and the default no-op clinical-event replication sink. Every
    /// profile must call this.
    ///
    /// Profiles that need a non-null replication sink must register their own
    /// <see cref="IClinicalEventReplicationSink"/> <b>before</b> calling this
    /// method — the registration here uses <c>TryAddSingleton</c> and so will
    /// be a no-op once another sink is in the container.
    /// </summary>
    public static ISiloBuilder AddCommonSiloServices(this ISiloBuilder siloBuilder)
    {
        siloBuilder.AddIncomingGrainCallFilter<AuthorizationCallFilter>();
        siloBuilder.AddIncomingGrainCallFilter<AuditCallFilter>();
        siloBuilder.AddLogStorageBasedLogConsistencyProvider(ClinicalLogConsistencyProvider);

        // Idle grain collection: the default is 2 hours, which keeps a whole
        // shift's worth of patient activations (large states) in silo memory.
        // 30 minutes covers an active encounter; reactivation is cheap.
        siloBuilder.Configure<GrainCollectionOptions>(options =>
            options.CollectionAge = TimeSpan.FromMinutes(30));

        // Response timeout: pinned explicitly (default 30s). Cover-sheet
        // builds fan out 10+ grain calls against large patient records; 60s
        // gives degraded-but-working responses under load instead of opaque
        // timeout failures. Clients pin the matching ClientMessagingOptions.
        siloBuilder.Configure<SiloMessagingOptions>(options =>
            options.ResponseTimeout = TimeSpan.FromSeconds(60));

        siloBuilder.Services.TryAddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();

        // Inbound applier — silo-side default registration so future receive
        // surfaces (file-watcher, in-process loopback for tests) can resolve it.
        // The HTTP controller in NewVistas.WebServer registers its own copy in
        // the client's DI.
        siloBuilder.Services.TryAddSingleton<IFederationInboundApplier, FederationInboundApplier>();

        // Outbox statistics — defaults to a no-op so the FederationStatsGrain
        // resolves cleanly on every profile. Outbox-using profiles register
        // SqlOutboxStatistics earlier in their chain (via AddFederationOutbox);
        // the TryAdd here is a no-op for them.
        siloBuilder.Services.TryAddSingleton<IOutboxStatistics, NoOpOutboxStatistics>();

        // Registration eligibility policy — defaults to no-op so non-VA
        // deployments (IHS, international, dev/test) do not run VA-specific
        // §17.36 rules. VA-aligned profiles (SqlExpressDemo, AzureCloud)
        // register VaRegistrationEligibilityPolicy earlier in their chain;
        // the TryAdd here is a no-op for them.
        siloBuilder.Services.TryAddSingleton<IRegistrationEligibilityPolicy, NoOpRegistrationEligibilityPolicy>();

        // GPRA submission formatter — defaults to CSV. Deployments that have
        // the authoritative IHS GPRA+ submission spec register a
        // spec-conformant IGpraSubmissionFormatter earlier in their chain;
        // this TryAdd is a no-op for them.
        siloBuilder.Services.TryAddSingleton<IGpraSubmissionFormatter, CsvGpraSubmissionFormatter>();

        // NDW (National Data Warehouse) export — defaults to per-domain CSV
        // and the patient-index source provider. Deployments with the
        // authoritative IHS NDW spec register their own formatter; large
        // deployments register their own source provider that filters by
        // active-user encounter activity in the period.
        siloBuilder.Services.TryAddSingleton<INdwExportFormatter, CsvNdwExportFormatter>();
        siloBuilder.Services.TryAddSingleton<INdwExportSourceProvider, PatientIndexNdwExportSourceProvider>();

        // MPI federation announcer — defaults to no-op. Single-cluster
        // deployments incur zero overhead. Federated multi-facility profiles
        // register an outbox-backed implementation earlier in their chain so
        // patient-registered and patient-merged events propagate to peer
        // clusters' MPI search and correlation grains.
        siloBuilder.Services.TryAddSingleton<IMpiFederationAnnouncer, NoOpMpiFederationAnnouncer>();

        // MPI inbound handler — applies federated MPI events on the receiving
        // cluster. Required by FederationInboundApplier. Default routes to
        // local IMpiSearchGrain + IMpiCorrelationGrain.
        siloBuilder.Services.TryAddSingleton<IMpiInboundHandler, DefaultMpiInboundHandler>();

        return siloBuilder;
    }

    /// <summary>
    /// Memory-backed PubSub + memory streams for the lab-result event provider.
    /// Used by every profile today; will likely diverge once cloud profiles move
    /// to Event Hubs / Cosmos change feed.
    /// </summary>
    public static ISiloBuilder AddInMemoryStreaming(this ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage(PubSubStoreName);
        siloBuilder.AddMemoryStreams(LabStreamsProviderName);
        return siloBuilder;
    }

    }
