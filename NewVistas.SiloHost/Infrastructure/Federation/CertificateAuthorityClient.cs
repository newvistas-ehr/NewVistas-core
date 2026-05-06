// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// HTTP implementation of <see cref="ICertificateAuthorityClient"/>. Reuses
/// the federation outbound named <see cref="HttpClient"/>, which is already
/// configured with the mTLS handler that attaches the spoke's current cert.
/// </summary>
public sealed class CertificateAuthorityClient : ICertificateAuthorityClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RenewalOptions _options;

    public CertificateAuthorityClient(
        IHttpClientFactory httpClientFactory,
        IOptions<RenewalOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            throw new InvalidOperationException(
                $"{nameof(CertificateAuthorityClient)} registered but '{RenewalOptions.SectionName}:{nameof(RenewalOptions.Url)}' is not configured.");
        }
    }

    public async Task<RenewalResponse> RenewAsync(string csrPem, CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient(HttpFederationTransport.HttpClientName);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            _options.Url!,
            new { CsrPem = csrPem },
            FederationJsonOptions.Default,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        RenewalResponse? body = await response.Content.ReadFromJsonAsync<RenewalResponse>(
            FederationJsonOptions.Default, cancellationToken);

        if (body is null || string.IsNullOrEmpty(body.CertPem))
        {
            throw new InvalidOperationException("Hub returned 2xx but no cert in body.");
        }

        return body;
    }
}
