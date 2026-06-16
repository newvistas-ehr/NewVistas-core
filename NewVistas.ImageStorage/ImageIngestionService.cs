// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using Orleans;

namespace NewVistas.ImageStorage;

/// <summary>
/// Orchestrator for the imaging upload pipeline. Parses DICOM (if applicable),
/// renders a thumbnail, uploads original + thumbnail to blob storage, then
/// calls the Orleans workflow grain and imaging grain to persist metadata.
/// Compensates blob uploads on grain-write failure so partial state is not
/// left behind.
/// </summary>
public sealed class ImageIngestionService : IImageIngestionService
{
    private readonly IImageBlobStorageService _blobs;
    private readonly DicomParsingService _dicomParser;
    private readonly RasterImageService _raster;
    private readonly IGrainFactory _grains;
    private readonly ILogger<ImageIngestionService> _logger;

    public ImageIngestionService(
        IImageBlobStorageService blobs,
        DicomParsingService dicomParser,
        RasterImageService raster,
        IGrainFactory grains,
        ILogger<ImageIngestionService> logger)
    {
        _blobs = blobs;
        _dicomParser = dicomParser;
        _raster = raster;
        _grains = grains;
        _logger = logger;
    }

    public async Task<ImageIngestionResult> IngestAsync(
        ImageIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PatientId))
            throw new ArgumentException("PatientId is required", nameof(request));

        // Generate the image ID up front so the blob path is knowable before
        // any grain call. This matches the IMG- prefix used by the workflow
        // grain's CaptureImageAsync so we stay consistent with existing IDs.
        string imageId = $"IMG-{Guid.NewGuid()}";

        // Ensure the stream is seekable — DICOM parser and raster thumbnailer
        // both require seek. Upload streams from HttpRequest.Body and Blazor
        // IBrowserFile.OpenReadStream are not seekable, so buffer first.
        Stream seekable = request.Content;
        bool ownsSeekable = false;
        if (!seekable.CanSeek)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            seekable = ms;
            ownsSeekable = true;
        }

        string? originalBlobPath = null;
        string? thumbBlobPath = null;
        try
        {
            bool isDicom = IsDicomPayload(request);

            ParsedDicom? parsed = null;
            Stream? thumbStream = null;
            int width = 0, height = 0;

            if (isDicom)
            {
                var parseResult = await _dicomParser.ParseAsync(seekable, cancellationToken);
                parsed = parseResult.Parsed;
                width = parsed.Width;
                height = parsed.Height;
                thumbStream = await _dicomParser.RenderThumbnailPngAsync(parseResult.File, 256, cancellationToken);
                seekable.Position = 0;
            }
            else if (IsRasterPayload(request))
            {
                var thumb = await _raster.GenerateThumbnailAsync(seekable, 256, cancellationToken);
                thumbStream = thumb.Thumbnail;
                width = thumb.OriginalWidth;
                height = thumb.OriginalHeight;
                seekable.Position = 0;
            }

            // Upload original
            string originalFileName = PickOriginalFileName(request.FileName, isDicom);
            BlobUploadResult uploadedOriginal = await _blobs.UploadAsync(
                request.PatientId, imageId, originalFileName,
                seekable, request.ContentType, cancellationToken);
            originalBlobPath = uploadedOriginal.BlobPath;

            // Upload thumbnail if we produced one
            BlobUploadResult? uploadedThumb = null;
            if (thumbStream != null)
            {
                await using (thumbStream)
                {
                    uploadedThumb = await _blobs.UploadAsync(
                        request.PatientId, imageId, "thumb.png",
                        thumbStream, "image/png", cancellationToken);
                    thumbBlobPath = uploadedThumb.BlobPath;
                }
            }

            // Workflow grain: capture + register with patient grain. Pass blob
            // paths as URLs — the grain stores them as canonical identifiers
            // and knows nothing about whether they're Azure or filesystem.
            var workflow = _grains.GetGrain<IPatientWorkflowGrain>(request.PatientId);
            string grainImageId = await workflow.CaptureImageAsync(
                objectType: request.ObjectType,
                procedureDescription: request.ProcedureDescription,
                specialtyIndex: null,
                imageUrl: originalBlobPath,
                thumbnailUrl: thumbBlobPath,
                dicomSeriesUid: parsed?.SeriesUid,
                dicomStudyUid: parsed?.StudyUid,
                procedureDate: parsed?.AcquisitionDateTime,
                captureDate: DateTime.UtcNow,
                imageCount: parsed?.NumberOfFrames ?? 1,
                radiologyId: null,
                tiuDocumentId: null,
                capturedById: request.CapturedById,
                capturedByName: request.CapturedByName,
                locationId: null,
                locationName: request.LocationName,
                comments: request.Comments);

            // Additional metadata via direct grain calls (the workflow
            // grain's CaptureImageAsync signature doesn't take everything).
            var imagingGrain = _grains.GetGrain<IImagingGrain>(grainImageId);

            if (parsed != null)
            {
                await imagingGrain.RecordDicomMetadataAsync(
                    parsed.StudyUid, parsed.SeriesUid, parsed.InstanceUid,
                    parsed.Modality, parsed.BodyPart, parsed.TransferSyntax);

                if (parsed.AcquisitionDateTime.HasValue)
                {
                    await imagingGrain.RecordAcquisitionAsync(
                        null, parsed.AcquisitionDateTime.Value, parsed.PatientOrientation);
                }

                if (!string.IsNullOrEmpty(parsed.Laterality))
                {
                    await imagingGrain.SetLateralityAsync(parsed.Laterality);
                }
            }

            if (width > 0 && height > 0)
            {
                await imagingGrain.SetImageDimensionsAsync(
                    width, height, uploadedOriginal.SizeBytes, parsed?.TransferSyntax);
            }

            await imagingGrain.SetClinicalDisplayStatusAsync("VIEWABLE");

            _logger.LogInformation(
                "Ingested image {ImageId} for patient {PatientId}, isDicom={IsDicom}",
                grainImageId, request.PatientId, isDicom);

            return new ImageIngestionResult(
                ImageId: grainImageId,
                OriginalBlobPath: originalBlobPath,
                ThumbnailBlobPath: thumbBlobPath,
                Width: width,
                Height: height,
                Modality: parsed?.Modality);
        }
        catch
        {
            // Compensating delete — avoid leaving orphan blobs if the grain
            // write failed after upload.
            if (originalBlobPath != null)
                await SafeDeleteAsync(originalBlobPath);
            if (thumbBlobPath != null)
                await SafeDeleteAsync(thumbBlobPath);
            throw;
        }
        finally
        {
            if (ownsSeekable)
                await seekable.DisposeAsync();
        }
    }

    private async Task SafeDeleteAsync(string blobPath)
    {
        try
        {
            await _blobs.DeleteAsync(blobPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Compensating delete failed for {BlobPath}", blobPath);
        }
    }

    private static bool IsDicomPayload(ImageIngestionRequest request)
    {
        if (request.ContentType.Contains("dicom", StringComparison.OrdinalIgnoreCase))
            return true;
        if (request.FileName.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase))
            return true;
        // Heuristic: CT/MRI/XRAY/ULTRASOUND object types are DICOM in practice.
        return request.ObjectType is "CT" or "MRI" or "XRAY" or "ULTRASOUND" or "MR" or "CR" or "DX" or "US";
    }

    private static bool IsRasterPayload(ImageIngestionRequest request)
    {
        if (request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return true;
        string ext = Path.GetExtension(request.FileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tif" or ".tiff";
    }

    private static string PickOriginalFileName(string uploadedName, bool isDicom)
    {
        if (isDicom) return "original.dcm";
        string ext = Path.GetExtension(uploadedName);
        return string.IsNullOrEmpty(ext) ? "original" : $"original{ext}";
    }
}
