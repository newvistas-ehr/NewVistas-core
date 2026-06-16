// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of an Imaging grain based on VistA IMAGE file (#2005)
/// DICOM images linked to patient
/// </summary>
[GenerateSerializer]
public class ImagingState
{
    [Id(0)]
    public string ImageId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Object Type � STILL IMAGE, DOCUMENT, XRAY, MRI, CT, etc.
    /// </summary>
    [Id(2)]
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Procedure (.01) � description of the imaging procedure
    /// </summary>
    [Id(3)]
    public string? ProcedureDescription { get; set; }

    /// <summary>
    /// Specialty Index � e.g., RADIOLOGY, SURGERY, DERMATOLOGY, CARDIOLOGY, etc.
    /// </summary>
    [Id(4)]
    public string? SpecialtyIndex { get; set; }

    /// <summary>
    /// DICOM Series Instance UID
    /// </summary>
    [Id(5)]
    public string? DicomSeriesUid { get; set; }

    /// <summary>
    /// DICOM Study Instance UID
    /// </summary>
    [Id(6)]
    public string? DicomStudyUid { get; set; }

    /// <summary>
    /// Image URL � full URL to blob storage (Azure Blob Storage, AWS S3, or CDN)
    /// Example: https://mystorageaccount.blob.core.windows.net/images/chest_001.dcm
    /// </summary>
    [Id(7)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Thumbnail URL � URL to thumbnail in blob storage or CDN
    /// Example: https://cdn.example.com/thumbs/chest_001.jpg
    /// </summary>
    [Id(8)]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Procedure Date
    /// </summary>
    [Id(9)]
    public DateTime? ProcedureDate { get; set; }

    /// <summary>
    /// Capture Date
    /// </summary>
    [Id(10)]
    public DateTime? CaptureDate { get; set; }

    /// <summary>
    /// Status � VIEWABLE, NEEDS REVIEW, QA REVIEWED, DELETED
    /// </summary>
    [Id(11)]
    public string Status { get; set; } = "VIEWABLE";

    /// <summary>
    /// Linked Radiology Order/Report ID
    /// </summary>
    [Id(12)]
    public string? RadiologyId { get; set; }

    /// <summary>
    /// Linked TIU Document ID
    /// </summary>
    [Id(13)]
    public string? TiuDocumentId { get; set; }

    /// <summary>
    /// Number of images in group
    /// </summary>
    [Id(14)]
    public int ImageCount { get; set; } = 1;

    [Id(15)]
    public string? CapturedById { get; set; }

    [Id(16)]
    public string? CapturedByName { get; set; }

    [Id(17)]
    public string? LocationId { get; set; }

    [Id(18)]
    public string? LocationName { get; set; }

    [Id(19)]
    public string? Comments { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// DICOM SOP Instance UID
    /// </summary>
    [Id(22)]
    public string? DicomInstanceUid { get; set; }

    /// <summary>
    /// Modality — CT, MR, XR, US, NM, CR, DX, MG, PT
    /// </summary>
    [Id(23)]
    public string? Modality { get; set; }

    /// <summary>
    /// DICOM body part examined
    /// </summary>
    [Id(24)]
    public string? BodyPartExamined { get; set; }

    /// <summary>
    /// Laterality — LEFT, RIGHT, BILATERAL
    /// </summary>
    [Id(25)]
    public string? Laterality { get; set; }

    /// <summary>
    /// Image pixel width
    /// </summary>
    [Id(26)]
    public int? ImageWidth { get; set; }

    /// <summary>
    /// Image pixel height
    /// </summary>
    [Id(27)]
    public int? ImageHeight { get; set; }

    /// <summary>
    /// Compression type — NONE, JPEG, JPEG2000, RLE
    /// </summary>
    [Id(28)]
    public string? CompressionType { get; set; }

    /// <summary>
    /// DICOM transfer syntax UID
    /// </summary>
    [Id(29)]
    public string? TransferSyntax { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    [Id(30)]
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Clinical display status — VIEWABLE, NEEDS_REVIEW, RESTRICTED, DELETED
    /// </summary>
    [Id(31)]
    public string? ClinicalDisplayStatus { get; set; }

    /// <summary>
    /// Linked package reference (e.g., "RAD:STUDY-123", "SURG:CASE-456")
    /// </summary>
    [Id(32)]
    public string? PackageReference { get; set; }

    /// <summary>
    /// Package type — RADIOLOGY, SURGERY, CONSULT, TIU_NOTE, LAB
    /// </summary>
    [Id(33)]
    public string? PackageType { get; set; }

    /// <summary>
    /// Facility where image was acquired
    /// </summary>
    [Id(34)]
    public string? AcquisitionSite { get; set; }

    /// <summary>
    /// Date and time image was acquired
    /// </summary>
    [Id(35)]
    public DateTime? AcquisitionDateTime { get; set; }

    /// <summary>
    /// Patient orientation (e.g., HFS = Head First Supine)
    /// </summary>
    [Id(36)]
    public string? PatientOrientation { get; set; }

    /// <summary>
    /// Annotation overlays on this image
    /// </summary>
    [Id(37)]
    public List<ImageAnnotation> Annotations { get; set; } = new();

    /// <summary>
    /// Series information for multi-series studies
    /// </summary>
    [Id(38)]
    public List<ImageSeriesInfo> Series { get; set; } = new();
}

/// <summary>
/// Annotation overlay on an image
/// </summary>
[GenerateSerializer]
public class ImageAnnotation
{
    /// <summary>
    /// Unique annotation identifier
    /// </summary>
    [Id(0)]
    public string AnnotationId { get; set; } = string.Empty;

    /// <summary>
    /// Annotation type — TEXT, ARROW, MEASUREMENT, ROI
    /// </summary>
    [Id(1)]
    public string AnnotationType { get; set; } = string.Empty;

    /// <summary>
    /// Annotation text or value
    /// </summary>
    [Id(2)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Author of the annotation
    /// </summary>
    [Id(3)]
    public string? AuthorName { get; set; }

    /// <summary>
    /// When the annotation was created
    /// </summary>
    [Id(4)]
    public DateTime CreatedDateTime { get; set; }
}

/// <summary>
/// Series information within a DICOM study
/// </summary>
[GenerateSerializer]
public class ImageSeriesInfo
{
    /// <summary>
    /// DICOM Series Instance UID
    /// </summary>
    [Id(0)]
    public string SeriesUid { get; set; } = string.Empty;

    /// <summary>
    /// Series description
    /// </summary>
    [Id(1)]
    public string? SeriesDescription { get; set; }

    /// <summary>
    /// Modality for this series
    /// </summary>
    [Id(2)]
    public string? Modality { get; set; }

    /// <summary>
    /// Number of images in this series
    /// </summary>
    [Id(3)]
    public int ImageCount { get; set; }

    /// <summary>
    /// Series number
    /// </summary>
    [Id(4)]
    public int? SeriesNumber { get; set; }
}
