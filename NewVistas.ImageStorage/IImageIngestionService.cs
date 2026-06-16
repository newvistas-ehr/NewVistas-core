// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.ImageStorage;

/// <summary>
/// Orchestrates the upload pipeline: parse → thumbnail → blob upload → grain
/// metadata write. Single entry point used by both Blazor (in-process from
/// the Imaging razor page) and the REST API (from ImagingController).
/// </summary>
public interface IImageIngestionService
{
    Task<ImageIngestionResult> IngestAsync(
        ImageIngestionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inputs to the ingestion pipeline. The caller opens a seekable stream over
/// the uploaded payload — DICOM parsing requires seeking to re-read the
/// dataset after pixel extraction.
/// </summary>
public record ImageIngestionRequest(
    string PatientId,
    string ObjectType,
    string FileName,
    string ContentType,
    Stream Content,
    string? ProcedureDescription = null,
    string? LocationName = null,
    string? Comments = null,
    string? CapturedById = null,
    string? CapturedByName = null);

/// <summary>
/// Result of a successful ingestion. Contains the generated image ID (matching
/// the grain key) and the paths to the uploaded blobs so the caller can hand
/// them to the view / download endpoints.
/// </summary>
public record ImageIngestionResult(
    string ImageId,
    string OriginalBlobPath,
    string? ThumbnailBlobPath,
    int Width,
    int Height,
    string? Modality);
