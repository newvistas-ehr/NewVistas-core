// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NewVistas.ImageStorage;

/// <summary>
/// Filesystem-backed implementation of <see cref="IImageBlobStorageService"/>.
/// Writes blobs to disk under a configured root. The read URI is an HMAC-signed
/// link to a companion endpoint (see <see cref="FileSystemSignedLinkHandler"/>).
/// This is the on-prem default — zero external dependencies — and also the
/// default for local development.
/// </summary>
public sealed class FileSystemImageBlobStorageService : IImageBlobStorageService
{
    private readonly FilesystemStorageOptions _options;
    private readonly ILogger<FileSystemImageBlobStorageService> _logger;
    private readonly string _resolvedRoot;

    public FileSystemImageBlobStorageService(
        IOptions<ImageStorageOptions> options,
        ILogger<FileSystemImageBlobStorageService> logger)
    {
        _options = options.Value.Filesystem;
        _logger = logger;
        _resolvedRoot = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(_resolvedRoot);
    }

    public async Task<BlobUploadResult> UploadAsync(
        string patientId,
        string imageId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        string blobPath = BuildBlobPath(patientId, imageId, fileName);
        string absolutePath = ResolveAbsolutePath(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fs = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, cancellationToken);
        long size = fs.Length;

        _logger.LogInformation("Wrote image blob {BlobPath} ({Size} bytes)", blobPath, size);

        var canonical = new Uri("file:///" + absolutePath.Replace('\\', '/'));
        return new BlobUploadResult(blobPath, canonical, size);
    }

    public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        string absolutePath = ResolveAbsolutePath(blobPath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Image blob not found", blobPath);

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Uri GetReadSasUri(string blobPath, TimeSpan lifetime)
    {
        long expiresAt = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        string payload = $"{blobPath}|{expiresAt}";
        string signature = ComputeHmac(payload);
        string token = Base64UrlEncode(Encoding.UTF8.GetBytes($"{payload}|{signature}"));

        string baseUrl = _options.PublicBaseUrl?.TrimEnd('/') ?? string.Empty;
        string relative = $"/api/imaging/signed/{token}";
        return string.IsNullOrEmpty(baseUrl)
            ? new Uri(relative, UriKind.Relative)
            : new Uri(baseUrl + relative, UriKind.Absolute);
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        string absolutePath = ResolveAbsolutePath(blobPath);
        try
        {
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete image blob {BlobPath} (best-effort)", blobPath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies a signed token produced by <see cref="GetReadSasUri"/> and returns
    /// the underlying blob path, or null if the token is tampered, malformed, or expired.
    /// </summary>
    public string? VerifySignedToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Base64UrlDecode(token));
        }
        catch
        {
            return null;
        }

        string[] parts = decoded.Split('|');
        if (parts.Length != 3) return null;

        string blobPath = parts[0];
        string expiresAtStr = parts[1];
        string signature = parts[2];

        if (!long.TryParse(expiresAtStr, out long expiresAt)) return null;
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt) return null;

        string expected = ComputeHmac($"{blobPath}|{expiresAt}");
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expected)))
            return null;

        return blobPath;
    }

    private string ComputeHmac(string payload)
    {
        string key = string.IsNullOrEmpty(_options.SigningKey)
            ? "newvistas-dev-unsigned-key"
            : _options.SigningKey;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private string ResolveAbsolutePath(string blobPath)
    {
        string normalized = blobPath.Replace('/', Path.DirectorySeparatorChar);
        string absolute = Path.GetFullPath(Path.Combine(_resolvedRoot, normalized));
        // Guard against '../' escape — the resolved path must stay inside the root.
        if (!absolute.StartsWith(_resolvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Blob path escapes storage root: {blobPath}");
        return absolute;
    }

    private static string BuildBlobPath(string patientId, string imageId, string fileName)
    {
        string safePatient = SanitizeSegment(patientId);
        string safeImage = SanitizeSegment(imageId);
        string safeFile = SanitizeSegment(fileName);
        return $"{safePatient}/{safeImage}/{safeFile}";
    }

    private static string SanitizeSegment(string segment)
    {
        Span<char> buf = stackalloc char[segment.Length];
        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            buf[i] = char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'
                ? c
                : '_';
        }
        return new string(buf);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
