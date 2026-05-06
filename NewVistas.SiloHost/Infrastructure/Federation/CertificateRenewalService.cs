// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Federation;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Background service that renews the spoke's federation client cert
/// before it expires. Wakes on <see cref="RenewalOptions.CheckIntervalHours"/>;
/// if the current cert's <c>NotAfter</c> is within
/// <see cref="RenewalOptions.RenewBeforeExpiryDays"/>, generates a CSR,
/// posts it to the hub, and atomically swaps the PFX file on disk. The
/// federation transport's <see cref="IHttpClientFactory"/> handler rotates
/// (default 2 minutes) and picks up the new cert without a service bounce.
///
/// Single-flight: a <see cref="SemaphoreSlim"/> ensures the service can't
/// race itself if a renewal cycle outlasts the check interval.
/// </summary>
public sealed class CertificateRenewalService : BackgroundService
{
    private readonly RenewalOptions _renewal;
    private readonly HttpFederationTransportOptions _httpOptions;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly ICertificateAuthorityClient _caClient;
    private readonly ILogger<CertificateRenewalService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public CertificateRenewalService(
        IOptions<RenewalOptions> renewal,
        IOptions<HttpFederationTransportOptions> httpOptions,
        IClusterIdentity clusterIdentity,
        ICertificateAuthorityClient caClient,
        ILogger<CertificateRenewalService> logger)
    {
        _renewal = renewal.Value;
        _httpOptions = httpOptions.Value;
        _clusterIdentity = clusterIdentity;
        _caClient = caClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_renewal.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_httpOptions.ClientCertPath))
        {
            _logger.LogWarning(
                "Renewal service started but Federation:Http:ClientCertPath is not configured; nothing to renew.");
            return;
        }

        _logger.LogInformation(
            "Cert renewal service started — checking every {Hours}h, renew within {Days}d of expiry",
            _renewal.CheckIntervalHours, _renewal.RenewBeforeExpiryDays);

        // Initial check on startup: log expiry, run a renewal if already due.
        await TryRenewOnceAsync(stoppingToken);

        TimeSpan interval = TimeSpan.FromHours(_renewal.CheckIntervalHours);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await TryRenewOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Run one check-and-maybe-renew cycle. Public so tests can drive the
    /// loop deterministically without the timer.
    /// </summary>
    public async Task TryRenewOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) return;

        try
        {
            string certPath = _httpOptions.ClientCertPath!;
            if (!File.Exists(certPath))
            {
                _logger.LogWarning("Cert file at {Path} not found; renewal skipped.", certPath);
                return;
            }

            DateTime notAfter;
            using (X509Certificate2 current = X509CertificateLoader.LoadPkcs12FromFile(
                certPath, _httpOptions.ClientCertPassword))
            {
                notAfter = current.NotAfter.ToUniversalTime();
            }

            TimeSpan untilExpiry = notAfter - DateTime.UtcNow;
            TimeSpan threshold = TimeSpan.FromDays(_renewal.RenewBeforeExpiryDays);
            if (untilExpiry > threshold)
            {
                _logger.LogDebug(
                    "Cert valid for {Days} more days; threshold is {ThresholdDays} — no renewal needed.",
                    untilExpiry.TotalDays, _renewal.RenewBeforeExpiryDays);
                return;
            }

            _logger.LogInformation(
                "Cert expires in {Days} days; renewing now.", untilExpiry.TotalDays);

            await PerformRenewalAsync(certPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cert renewal cycle failed; will retry next cycle.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PerformRenewalAsync(string certPath, CancellationToken cancellationToken)
    {
        string clusterId = _clusterIdentity.LocalClusterId;

        (string csrPem, byte[] pkcs8Key) = CertificateBundle.GenerateRenewalCsr(clusterId);

        RenewalResponse renewal;
        try
        {
            renewal = await _caClient.RenewAsync(csrPem, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub rejected renewal CSR; original cert untouched.");
            return;
        }

        // Sanity check: hub must have signed for our cluster id, not someone else's.
        using (X509Certificate2 returned = X509CertificateLoader.LoadCertificate(
            PemToDer(renewal.CertPem)))
        {
            string? returnedCn = ExtractCommonName(returned.Subject);
            if (!string.Equals(returnedCn, clusterId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(
                    "Hub returned a renewed cert with CN '{Returned}' but we asked for '{Requested}'; refusing to install.",
                    returnedCn, clusterId);
                return;
            }
        }

        byte[] pfxBytes = CertificateBundle.BuildPfx(renewal.CertPem, pkcs8Key, _httpOptions.ClientCertPassword);

        AtomicSwap(certPath, pfxBytes);

        _logger.LogInformation(
            "Cert renewed and installed at {Path}. Previous cert backed up to {Path}.previous.",
            certPath, certPath);
    }

    /// <summary>
    /// Write new bytes to <paramref name="certPath"/>+".new", move existing
    /// file to ".previous" (overwriting any prior backup), then move the
    /// ".new" over the live path. Single-process; the SemaphoreSlim above
    /// prevents concurrent renewals.
    /// </summary>
    public static void AtomicSwap(string certPath, byte[] newPfxBytes)
    {
        string newPath = certPath + ".new";
        string previousPath = certPath + ".previous";

        File.WriteAllBytes(newPath, newPfxBytes);

        if (File.Exists(certPath))
        {
            File.Move(certPath, previousPath, overwrite: true);
        }

        File.Move(newPath, certPath, overwrite: true);
    }

    private static byte[] PemToDer(string pem)
    {
        ReadOnlySpan<char> span = pem.AsSpan();
        PemFields fields = PemEncoding.Find(span);
        return Convert.FromBase64String(span[fields.Base64Data].ToString());
    }

    private static string? ExtractCommonName(string subject)
    {
        var match = System.Text.RegularExpressions.Regex.Match(subject, @"CN=(?<cn>[^,]+)");
        return match.Success ? match.Groups["cn"].Value.Trim() : null;
    }
}
