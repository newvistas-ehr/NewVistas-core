// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Imaging Grain Interface based on VistA IMAGE file (#2005)
/// </summary>
public interface IImagingGrain : IGrainWithStringKey
{
    Task<GrainStates.ImagingState> GetImageAsync();

    /// <summary>
    /// Captures an image and stores it in blob storage (Azure, AWS S3, or CDN).
    /// The imageUrl and thumbnailUrl should point to the blob storage location where the image is stored.
    /// </summary>
    /// <param name="imageUrl">Full URL to the image in blob storage (e.g., Azure Blob Storage, AWS S3, CloudFront)</param>
    /// <param name="thumbnailUrl">URL to the thumbnail image in blob storage or CDN</param>
    Task CaptureImageAsync(
        string patientId,
        string objectType,
        string? procedureDescription,
        string? specialtyIndex,
        string? imageUrl,
        string? thumbnailUrl,
        string? dicomSeriesUid,
        string? dicomStudyUid,
        DateTime? procedureDate,
        DateTime captureDate,
        int imageCount,
        string? radiologyId,
        string? tiuDocumentId,
        string? capturedById,
        string? capturedByName,
        string? locationId,
        string? locationName,
        string? comments);

    Task MarkForReviewAsync();
    Task QaReviewAsync();
    Task DeleteImageAsync();

    /// <summary>
    /// Records DICOM metadata for this image.
    /// </summary>
    Task RecordDicomMetadataAsync(string? studyUid, string? seriesUid, string? instanceUid, string? modality, string? bodyPart, string? transferSyntax);

    /// <summary>
    /// Sets image pixel dimensions and file information.
    /// </summary>
    Task SetImageDimensionsAsync(int width, int height, long? fileSizeBytes, string? compressionType);

    /// <summary>
    /// Sets the clinical display status (VIEWABLE, NEEDS_REVIEW, RESTRICTED, DELETED).
    /// </summary>
    Task SetClinicalDisplayStatusAsync(string status);

    /// <summary>
    /// Links this image to a clinical package (RADIOLOGY, SURGERY, etc.).
    /// </summary>
    Task LinkToPackageAsync(string packageType, string packageReference);

    /// <summary>
    /// Records acquisition site, date/time, and patient orientation.
    /// </summary>
    Task RecordAcquisitionAsync(string? acquisitionSite, DateTime acquisitionDateTime, string? patientOrientation);

    /// <summary>
    /// Adds an annotation overlay to this image.
    /// </summary>
    Task AddAnnotationAsync(string annotationType, string content, string? authorName);

    /// <summary>
    /// Removes an annotation by ID.
    /// </summary>
    Task RemoveAnnotationAsync(string annotationId);

    /// <summary>
    /// Adds series information for multi-series studies.
    /// </summary>
    Task AddSeriesInfoAsync(string seriesUid, string? seriesDescription, string? modality, int imageCount, int? seriesNumber);

    /// <summary>
    /// Sets the laterality (LEFT, RIGHT, BILATERAL).
    /// </summary>
    Task SetLateralityAsync(string laterality);

    /// <summary>
    /// Gets all annotations on this image.
    /// </summary>
    Task<List<GrainStates.ImageAnnotation>> GetAnnotationsAsync();

    /// <summary>
    /// Gets all series info for this image.
    /// </summary>
    Task<List<GrainStates.ImageSeriesInfo>> GetSeriesAsync();
}
