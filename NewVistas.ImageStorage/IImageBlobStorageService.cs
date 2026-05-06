// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.ImageStorage;

/// <summary>
/// Provider-agnostic contract for storing and retrieving medical image binaries
/// outside of Orleans grain state. Grains store only scalar metadata and a
/// <see cref="BlobUploadResult.BlobPath"/> — pixel bytes never cross the grain
/// boundary. Implementations exist for local filesystem (on-prem default) and
/// Azure Blob Storage.
/// </summary>
public interface IImageBlobStorageService
{
    /// <summary>
    /// Uploads a stream to the backing store under a deterministic path derived
    /// from <paramref name="patientId"/> and <paramref name="imageId"/>.
    /// </summary>
    Task<BlobUploadResult> UploadAsync(
        string patientId,
        string imageId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for the blob at <paramref name="blobPath"/>. The
    /// caller owns the stream and must dispose it.
    /// </summary>
    Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Produces a time-limited read URI that the browser can fetch directly,
    /// without re-authenticating against NewVistas. Azure returns a user-delegation
    /// SAS; the filesystem provider returns an HMAC-signed link to its own
    /// signed-link endpoint.
    /// </summary>
    Uri GetReadSasUri(string blobPath, TimeSpan lifetime);

    /// <summary>
    /// Deletes the blob at <paramref name="blobPath"/>. Safe to call on a
    /// missing blob — providers suppress not-found errors so this can be used
    /// as a compensating action from failed ingest flows.
    /// </summary>
    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Return value of <see cref="IImageBlobStorageService.UploadAsync"/>. The
/// <see cref="BlobPath"/> is the canonical identifier stored in grain state;
/// <see cref="CanonicalUri"/> is a human-readable locator that may or may not
/// be directly fetchable (Azure blob URLs are not, filesystem paths are not).
/// </summary>
public record BlobUploadResult(string BlobPath, Uri CanonicalUri, long SizeBytes);
