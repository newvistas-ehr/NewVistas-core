// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Imaging Grain implementation based on VistA IMAGE file (#2005)
/// </summary>
public class ImagingGrain : Grain, IImagingGrain
{
    private readonly IPersistentState<ImagingState> _state;

    public ImagingGrain(
        [PersistentState("imagingState", "imagingStore")] IPersistentState<ImagingState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ImageId))
        {
            _state.State.ImageId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ImagingState> GetImageAsync() => Task.FromResult(_state.State);

    public async Task CaptureImageAsync(
        string patientId, string objectType,
        string? procedureDescription, string? specialtyIndex,
        string? imageUrl, string? thumbnailUrl,
        string? dicomSeriesUid, string? dicomStudyUid,
        DateTime? procedureDate, DateTime captureDate,
        int imageCount,
        string? radiologyId, string? tiuDocumentId,
        string? capturedById, string? capturedByName,
        string? locationId, string? locationName, string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.ObjectType = objectType;
        _state.State.ProcedureDescription = procedureDescription;
        _state.State.SpecialtyIndex = specialtyIndex;
        _state.State.ImageUrl = imageUrl;
        _state.State.ThumbnailUrl = thumbnailUrl;
        _state.State.DicomSeriesUid = dicomSeriesUid;
        _state.State.DicomStudyUid = dicomStudyUid;
        _state.State.ProcedureDate = procedureDate;
        _state.State.CaptureDate = captureDate;
        _state.State.ImageCount = imageCount;
        _state.State.RadiologyId = radiologyId;
        _state.State.TiuDocumentId = tiuDocumentId;
        _state.State.CapturedById = capturedById;
        _state.State.CapturedByName = capturedByName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Comments = comments;
        _state.State.Status = "VIEWABLE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkForReviewAsync()
    {
        _state.State.Status = "NEEDS REVIEW";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task QaReviewAsync()
    {
        _state.State.Status = "QA REVIEWED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeleteImageAsync()
    {
        _state.State.Status = "DELETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordDicomMetadataAsync(string? studyUid, string? seriesUid, string? instanceUid, string? modality, string? bodyPart, string? transferSyntax)
    {
        _state.State.DicomStudyUid = studyUid;
        _state.State.DicomSeriesUid = seriesUid;
        _state.State.DicomInstanceUid = instanceUid;
        _state.State.Modality = modality;
        _state.State.BodyPartExamined = bodyPart;
        _state.State.TransferSyntax = transferSyntax;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetImageDimensionsAsync(int width, int height, long? fileSizeBytes, string? compressionType)
    {
        _state.State.ImageWidth = width;
        _state.State.ImageHeight = height;
        _state.State.FileSizeBytes = fileSizeBytes;
        _state.State.CompressionType = compressionType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetClinicalDisplayStatusAsync(string status)
    {
        _state.State.ClinicalDisplayStatus = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkToPackageAsync(string packageType, string packageReference)
    {
        _state.State.PackageType = packageType;
        _state.State.PackageReference = packageReference;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAcquisitionAsync(string? acquisitionSite, DateTime acquisitionDateTime, string? patientOrientation)
    {
        _state.State.AcquisitionSite = acquisitionSite;
        _state.State.AcquisitionDateTime = acquisitionDateTime;
        _state.State.PatientOrientation = patientOrientation;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAnnotationAsync(string annotationType, string content, string? authorName)
    {
        ImageAnnotation annotation = new ImageAnnotation
        {
            AnnotationId = $"ANN-{Guid.NewGuid():N}",
            AnnotationType = annotationType,
            Content = content,
            AuthorName = authorName,
            CreatedDateTime = DateTime.UtcNow
        };
        _state.State.Annotations.Add(annotation);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAnnotationAsync(string annotationId)
    {
        ImageAnnotation? annotation = _state.State.Annotations.Find(a => a.AnnotationId == annotationId);
        if (annotation != null)
        {
            _state.State.Annotations.Remove(annotation);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task AddSeriesInfoAsync(string seriesUid, string? seriesDescription, string? modality, int imageCount, int? seriesNumber)
    {
        ImageSeriesInfo series = new ImageSeriesInfo
        {
            SeriesUid = seriesUid,
            SeriesDescription = seriesDescription,
            Modality = modality,
            ImageCount = imageCount,
            SeriesNumber = seriesNumber
        };
        _state.State.Series.Add(series);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetLateralityAsync(string laterality)
    {
        _state.State.Laterality = laterality;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<ImageAnnotation>> GetAnnotationsAsync()
        => Task.FromResult(_state.State.Annotations);

    public Task<List<ImageSeriesInfo>> GetSeriesAsync()
        => Task.FromResult(_state.State.Series);
}
