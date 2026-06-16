// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Composable building blocks for the outbound side of federation. Profiles
/// that participate in federation pick the pieces they need:
///
/// <list type="bullet">
///   <item><description>
///     <c>RemoteOnline</c>: <see cref="AddFederationOutbox"/> +
///     <see cref="AddFederationHttpOrLoggingTransport"/> +
///     <see cref="AddFederationDrainer"/> +
///     <see cref="AddFederationRenewal"/>.
///   </description></item>
///   <item><description>
///     <c>RemoteOffline</c>: <see cref="AddFederationOutbox"/> +
///     <see cref="AddFederationFileBundleTransport"/> +
///     <see cref="AddFederationDrainer"/> + <see cref="AddFileBundleInbound"/>.
///   </description></item>
///   <item><description>
///     <c>AzureCloud</c>: same as <c>RemoteOnline</c>.
///   </description></item>
/// </list>
///
/// Order within a profile: <see cref="AddFederationOutbox"/> first (registers
/// the sink), the chosen transport, then <see cref="AddFederationDrainer"/>
/// (depends on a transport being registered).
/// </summary>
public static class FederationOutboundExtensions
{
    /// <summary>
    /// Registers <see cref="OutboxOptions"/>, <see cref="IOutboxRepository"/>
    /// (SQL-backed), and <see cref="IClinicalEventReplicationSink"/>
    /// (SQL outbox sink). Caller picks a transport separately.
    /// </summary>
    public static ISiloBuilder AddFederationOutbox(
        this ISiloBuilder siloBuilder,
        HostApplicationBuilder host,
        string outboxConnectionString)
    {
        siloBuilder.Services.Configure<OutboxOptions>(
            host.Configuration.GetSection(OutboxOptions.SectionName));
        siloBuilder.Services.AddSingleton<IOutboxRepository>(sp =>
            new SqlOutboxRepository(
                outboxConnectionString,
                sp.GetRequiredService<ILogger<SqlOutboxRepository>>()));
        siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, SqlOutboxClinicalEventReplicationSink>();

        // Real stats backed by SQL aggregate queries. Registered before
        // AddCommonSiloServices's TryAdd default so this wins.
        siloBuilder.Services.AddSingleton<IOutboxStatistics>(_ => new SqlOutboxStatistics(outboxConnectionString));

        return siloBuilder;
    }

    /// <summary>
    /// Registers <see cref="OutboxDrainerService"/>. Must be called *after*
    /// a transport is registered — the drainer resolves
    /// <see cref="IFederationTransport"/> from DI.
    /// </summary>
    public static ISiloBuilder AddFederationDrainer(this ISiloBuilder siloBuilder)
    {
        siloBuilder.Services.AddHostedService<OutboxDrainerService>();
        return siloBuilder;
    }

    /// <summary>
    /// Registers <see cref="HttpFederationTransport"/> when
    /// <c>Federation:Http:InboundUrl</c> is set, with optional client-cert
    /// mTLS via <c>Federation:Http:ClientCertPath</c>. Falls back to
    /// <see cref="LoggingFederationTransport"/> when no URL is configured.
    /// </summary>
    public static ISiloBuilder AddFederationHttpOrLoggingTransport(
        this ISiloBuilder siloBuilder,
        HostApplicationBuilder host)
    {
        string? inboundUrl = host.Configuration[
            $"{HttpFederationTransportOptions.SectionName}:{nameof(HttpFederationTransportOptions.InboundUrl)}"];

        if (string.IsNullOrWhiteSpace(inboundUrl))
        {
            siloBuilder.Services.AddSingleton<IFederationTransport, LoggingFederationTransport>();
            return siloBuilder;
        }

        siloBuilder.Services.Configure<HttpFederationTransportOptions>(
            host.Configuration.GetSection(HttpFederationTransportOptions.SectionName));

        string? clientCertPath = host.Configuration[
            $"{HttpFederationTransportOptions.SectionName}:{nameof(HttpFederationTransportOptions.ClientCertPath)}"];
        string? clientCertPassword = host.Configuration[
            $"{HttpFederationTransportOptions.SectionName}:{nameof(HttpFederationTransportOptions.ClientCertPassword)}"];

        IHttpClientBuilder clientBuilder = siloBuilder.Services.AddHttpClient(HttpFederationTransport.HttpClientName);
        if (!string.IsNullOrWhiteSpace(clientCertPath))
        {
            // Read cert inside the lambda — supports auto-renewal: the
            // IHttpClientFactory's default 2-minute handler rotation picks
            // up new bytes when the renewal service swaps the file.
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
            {
                System.Security.Cryptography.X509Certificates.X509Certificate2 clientCert =
                    System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                        clientCertPath, clientCertPassword);
                var handler = new SocketsHttpHandler();
                handler.SslOptions.ClientCertificates ??= new System.Security.Cryptography.X509Certificates.X509CertificateCollection();
                handler.SslOptions.ClientCertificates.Add(clientCert);
                return handler;
            });
        }

        siloBuilder.Services.AddSingleton<IFederationTransport, HttpFederationTransport>();
        return siloBuilder;
    }

    /// <summary>
    /// Registers <see cref="FileBundleFederationTransport"/> when
    /// <c>Federation:FileBundle:OutboundDirectory</c> is set. Falls back to
    /// <see cref="LoggingFederationTransport"/> when not.
    /// </summary>
    public static ISiloBuilder AddFederationFileBundleTransport(
        this ISiloBuilder siloBuilder,
        HostApplicationBuilder host)
    {
        string? outboundDir = host.Configuration[
            $"{FileBundleOptions.SectionName}:{nameof(FileBundleOptions.OutboundDirectory)}"];

        if (string.IsNullOrWhiteSpace(outboundDir))
        {
            siloBuilder.Services.AddSingleton<IFederationTransport, LoggingFederationTransport>();
            return siloBuilder;
        }

        siloBuilder.Services.Configure<FileBundleOptions>(
            host.Configuration.GetSection(FileBundleOptions.SectionName));
        siloBuilder.Services.AddSingleton<IFederationTransport, FileBundleFederationTransport>();
        return siloBuilder;
    }

    /// <summary>
    /// Registers <see cref="CertificateRenewalService"/> + its CA client when
    /// <c>Federation:Renewal:Enabled=true</c>. Profiles using HTTP transport
    /// with mTLS call this to enable zero-touch cert refresh.
    /// </summary>
    public static ISiloBuilder AddFederationRenewal(
        this ISiloBuilder siloBuilder,
        HostApplicationBuilder host)
    {
        bool renewalEnabled = host.Configuration.GetValue<bool>(
            $"{RenewalOptions.SectionName}:{nameof(RenewalOptions.Enabled)}");
        if (!renewalEnabled) return siloBuilder;

        siloBuilder.Services.Configure<RenewalOptions>(
            host.Configuration.GetSection(RenewalOptions.SectionName));
        siloBuilder.Services.AddSingleton<ICertificateAuthorityClient, CertificateAuthorityClient>();
        siloBuilder.Services.AddHostedService<CertificateRenewalService>();
        return siloBuilder;
    }

    /// <summary>
    /// Registers <see cref="FileBundleInboundService"/> when
    /// <c>Federation:FileBundle:InboundDirectory</c> is set. Watches the
    /// directory for incoming bundles and applies them via
    /// <see cref="IFederationInboundApplier"/>.
    /// </summary>
    public static ISiloBuilder AddFileBundleInbound(
        this ISiloBuilder siloBuilder,
        HostApplicationBuilder host)
    {
        string? inboundDir = host.Configuration[
            $"{FileBundleOptions.SectionName}:{nameof(FileBundleOptions.InboundDirectory)}"];

        if (string.IsNullOrWhiteSpace(inboundDir)) return siloBuilder;

        // Options binding may already be wired by AddFederationFileBundleTransport;
        // calling Configure twice is a no-op for the same section.
        siloBuilder.Services.Configure<FileBundleOptions>(
            host.Configuration.GetSection(FileBundleOptions.SectionName));
        siloBuilder.Services.AddHostedService<FileBundleInboundService>();
        return siloBuilder;
    }
}
