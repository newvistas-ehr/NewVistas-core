// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.ImageStorage;

/// <summary>
/// Top-level configuration for the imaging storage pipeline. Bound from the
/// <c>ImageStorage</c> section of appsettings. Drives which backend is wired
/// into DI and how signed read URIs are generated.
/// </summary>
public class ImageStorageOptions
{
    /// <summary>
    /// Which storage backend to use. <see cref="ImageStorageProvider.Filesystem"/>
    /// is the on-prem / local-dev default; <see cref="ImageStorageProvider.AzureBlob"/>
    /// targets Azure Blob Storage (or Azurite for local dev).
    /// </summary>
    public ImageStorageProvider Provider { get; set; } = ImageStorageProvider.Filesystem;

    /// <summary>
    /// Filesystem-provider settings. Ignored when <see cref="Provider"/> is AzureBlob.
    /// </summary>
    public FilesystemStorageOptions Filesystem { get; set; } = new();

    /// <summary>
    /// Azure Blob-provider settings. Ignored when <see cref="Provider"/> is Filesystem.
    /// </summary>
    public AzureBlobStorageOptions AzureBlob { get; set; } = new();
}

public enum ImageStorageProvider
{
    Filesystem = 0,
    AzureBlob = 1,
}

public class FilesystemStorageOptions
{
    /// <summary>
    /// Absolute or relative root directory where image blobs are written.
    /// Relative paths are resolved against the host process content root.
    /// </summary>
    public string RootPath { get; set; } = "./ImageStore";

    /// <summary>
    /// HMAC signing key used to mint and verify signed read URIs. Must be
    /// overridden in any non-development environment — a leaked key allows
    /// anyone to fabricate image URIs that bypass authentication.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// How long a minted signed URI remains valid.
    /// </summary>
    public int SignedLinkLifetimeMinutes { get; set; } = 10;

    /// <summary>
    /// Absolute base URL where <c>/api/imaging/signed/{token}</c> is hosted.
    /// Used when generating signed URIs that the browser can reach directly.
    /// Example: <c>https://localhost:5001</c>. If empty, relative URIs are
    /// returned and the caller must be on the same origin.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public class AzureBlobStorageOptions
{
    /// <summary>
    /// Connection string for dev / Azurite (e.g. <c>UseDevelopmentStorage=true</c>).
    /// Leave empty in prod and set <see cref="AccountUrl"/> instead to use
    /// <c>DefaultAzureCredential</c> / Managed Identity.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob endpoint URL (e.g. <c>https://mystorage.blob.core.windows.net</c>).
    /// Used with <c>DefaultAzureCredential</c> when <see cref="ConnectionString"/>
    /// is empty.
    /// </summary>
    public string AccountUrl { get; set; } = string.Empty;

    /// <summary>
    /// Container that holds all medical image blobs. Path prefixes within the
    /// container are <c>{patientId}/{imageId}/...</c>.
    /// </summary>
    public string ContainerName { get; set; } = "medical-images";

    /// <summary>
    /// Lifetime in minutes for SAS URIs generated for read access.
    /// </summary>
    public int SasLifetimeMinutes { get; set; } = 10;
}
