// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Imaging — VistA IMAGE File #2005.
/// Tests ImagingGrain methods directly for capture, DICOM metadata,
/// dimensions, annotations, series, acquisition, and full lifecycle workflows.
/// </summary>
[TestFixture]
public class ImagingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IImagingGrain NewImagingGrain()
        => _cluster.GrainFactory.GetGrain<IImagingGrain>($"IMG-{Guid.NewGuid():N}");

    private async Task CaptureStandardAsync(IImagingGrain grain, string objectType = "XRAY", string patientId = "PAT-001")
    {
        await grain.CaptureImageAsync(
            patientId, objectType,
            "Chest PA and Lateral",
            "RADIOLOGY",
            "https://storage.example.com/images/chest_001.dcm",
            "https://storage.example.com/thumbs/chest_001.jpg",
            "1.2.840.113619.2.55.1234",
            "1.2.840.113619.2.55.5678",
            new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            DateTime.UtcNow,
            2,
            "RAD-001",
            "TIU-001",
            "TECH-001", "John Tech",
            "LOC-001", "Radiology Dept",
            "Two views obtained");
    }

    // ─── 1. Capture Image ─────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanCaptureImage()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();

        // Act
        await CaptureStandardAsync(grain);
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.ObjectType, Is.EqualTo("XRAY"));
        Assert.That(state.Status, Is.EqualTo("VIEWABLE"));
        Assert.That(state.ProcedureDescription, Is.EqualTo("Chest PA and Lateral"));
        Assert.That(state.ImageCount, Is.EqualTo(2));
    }

    // ─── 2. Get Image ─────────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanGetImage()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.SpecialtyIndex, Is.EqualTo("RADIOLOGY"));
        Assert.That(state.ImageUrl, Is.EqualTo("https://storage.example.com/images/chest_001.dcm"));
        Assert.That(state.ThumbnailUrl, Is.EqualTo("https://storage.example.com/thumbs/chest_001.jpg"));
        Assert.That(state.RadiologyId, Is.EqualTo("RAD-001"));
        Assert.That(state.TiuDocumentId, Is.EqualTo("TIU-001"));
        Assert.That(state.CapturedByName, Is.EqualTo("John Tech"));
        Assert.That(state.Comments, Is.EqualTo("Two views obtained"));
    }

    // ─── 3. Mark for Review ───────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanMarkForReview()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.MarkForReviewAsync();
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("NEEDS REVIEW"));
    }

    // ─── 4. QA Review ─────────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanQaReview()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);
        await grain.MarkForReviewAsync();

        // Act
        await grain.QaReviewAsync();
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("QA REVIEWED"));
    }

    // ─── 5. Delete Image ──────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanDeleteImage()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.DeleteImageAsync();
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo("DELETED"));
    }

    // ─── 6. Record DICOM Metadata ─────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanRecordDicomMetadata()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.RecordDicomMetadataAsync(
            "1.2.840.10008.5.1.4.1.1.2",
            "1.2.840.10008.5.1.4.1.1.2.1",
            "1.2.840.10008.5.1.4.1.1.2.2",
            "CT",
            "CHEST",
            "1.2.840.10008.1.2.1");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.DicomStudyUid, Is.EqualTo("1.2.840.10008.5.1.4.1.1.2"));
        Assert.That(state.DicomSeriesUid, Is.EqualTo("1.2.840.10008.5.1.4.1.1.2.1"));
        Assert.That(state.DicomInstanceUid, Is.EqualTo("1.2.840.10008.5.1.4.1.1.2.2"));
        Assert.That(state.Modality, Is.EqualTo("CT"));
        Assert.That(state.BodyPartExamined, Is.EqualTo("CHEST"));
        Assert.That(state.TransferSyntax, Is.EqualTo("1.2.840.10008.1.2.1"));
    }

    // ─── 7. Set Image Dimensions ──────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanSetImageDimensions()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.SetImageDimensionsAsync(2048, 2048, 15728640L, "JPEG2000");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.ImageWidth, Is.EqualTo(2048));
        Assert.That(state.ImageHeight, Is.EqualTo(2048));
        Assert.That(state.FileSizeBytes, Is.EqualTo(15728640L));
        Assert.That(state.CompressionType, Is.EqualTo("JPEG2000"));
    }

    // ─── 8. Set Clinical Display Status ───────────────────────────────────────

    [Test]
    public async Task Imaging_CanSetClinicalDisplayStatus()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.SetClinicalDisplayStatusAsync("VIEWABLE");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.ClinicalDisplayStatus, Is.EqualTo("VIEWABLE"));
    }

    // ─── 9. Link to Package ──────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanLinkToPackage()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.LinkToPackageAsync("RADIOLOGY", "RAD:STUDY-123");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.PackageType, Is.EqualTo("RADIOLOGY"));
        Assert.That(state.PackageReference, Is.EqualTo("RAD:STUDY-123"));
    }

    // ─── 10. Record Acquisition ───────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanRecordAcquisition()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);
        DateTime acquisitionTime = new DateTime(2024, 11, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        await grain.RecordAcquisitionAsync("VA Medical Center Tampa", acquisitionTime, "HFS");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.AcquisitionSite, Is.EqualTo("VA Medical Center Tampa"));
        Assert.That(state.AcquisitionDateTime, Is.EqualTo(acquisitionTime));
        Assert.That(state.PatientOrientation, Is.EqualTo("HFS"));
    }

    // ─── 11. Add Annotation ──────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanAddAnnotation()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.AddAnnotationAsync("TEXT", "Suspicious nodule in right lower lobe", "Dr. Smith");
        List<ImageAnnotation> annotations = await grain.GetAnnotationsAsync();

        // Assert
        Assert.That(annotations, Has.Count.EqualTo(1));
        Assert.That(annotations[0].AnnotationId, Does.StartWith("ANN-"));
        Assert.That(annotations[0].AnnotationType, Is.EqualTo("TEXT"));
        Assert.That(annotations[0].Content, Is.EqualTo("Suspicious nodule in right lower lobe"));
        Assert.That(annotations[0].AuthorName, Is.EqualTo("Dr. Smith"));
        Assert.That(annotations[0].CreatedDateTime, Is.Not.EqualTo(default(DateTime)));
    }

    // ─── 12. Add Multiple Annotations ─────────────────────────────────────────

    [Test]
    public async Task Imaging_CanAddMultipleAnnotations()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.AddAnnotationAsync("TEXT", "Nodule identified", "Dr. Smith");
        await grain.AddAnnotationAsync("MEASUREMENT", "12mm x 8mm", "Dr. Smith");
        await grain.AddAnnotationAsync("ARROW", "Points to area of concern", "Dr. Jones");
        List<ImageAnnotation> annotations = await grain.GetAnnotationsAsync();

        // Assert
        Assert.That(annotations, Has.Count.EqualTo(3));
        Assert.That(annotations[0].AnnotationType, Is.EqualTo("TEXT"));
        Assert.That(annotations[1].AnnotationType, Is.EqualTo("MEASUREMENT"));
        Assert.That(annotations[2].AnnotationType, Is.EqualTo("ARROW"));
    }

    // ─── 13. Remove Annotation ────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanRemoveAnnotation()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);
        await grain.AddAnnotationAsync("TEXT", "To be removed", "Dr. Smith");
        await grain.AddAnnotationAsync("ARROW", "Keep this one", "Dr. Jones");
        List<ImageAnnotation> before = await grain.GetAnnotationsAsync();
        string removeId = before[0].AnnotationId;

        // Act
        await grain.RemoveAnnotationAsync(removeId);
        List<ImageAnnotation> after = await grain.GetAnnotationsAsync();

        // Assert
        Assert.That(after, Has.Count.EqualTo(1));
        Assert.That(after[0].Content, Is.EqualTo("Keep this one"));
    }

    // ─── 14. Add Series Info ──────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanAddSeriesInfo()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.AddSeriesInfoAsync(
            "1.2.840.113619.2.55.3333",
            "Axial CT Chest",
            "CT",
            85,
            1);
        List<ImageSeriesInfo> series = await grain.GetSeriesAsync();

        // Assert
        Assert.That(series, Has.Count.EqualTo(1));
        Assert.That(series[0].SeriesUid, Is.EqualTo("1.2.840.113619.2.55.3333"));
        Assert.That(series[0].SeriesDescription, Is.EqualTo("Axial CT Chest"));
        Assert.That(series[0].Modality, Is.EqualTo("CT"));
        Assert.That(series[0].ImageCount, Is.EqualTo(85));
        Assert.That(series[0].SeriesNumber, Is.EqualTo(1));
    }

    // ─── 15. Set Laterality ───────────────────────────────────────────────────

    [Test]
    public async Task Imaging_CanSetLaterality()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();
        await CaptureStandardAsync(grain);

        // Act
        await grain.SetLateralityAsync("LEFT");
        ImagingState state = await grain.GetImageAsync();

        // Assert
        Assert.That(state.Laterality, Is.EqualTo("LEFT"));
    }

    // ─── 16. Full Workflow ────────────────────────────────────────────────────

    [Test]
    public async Task Imaging_FullWorkflow()
    {
        // Arrange
        IImagingGrain grain = NewImagingGrain();

        // Capture
        await grain.CaptureImageAsync(
            "PAT-FULL", "CT",
            "CT Chest with contrast",
            "RADIOLOGY",
            "https://storage.example.com/images/ct_chest.dcm",
            "https://storage.example.com/thumbs/ct_chest.jpg",
            null, null,
            new DateTime(2024, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            DateTime.UtcNow,
            120,
            "RAD-100", null,
            "TECH-010", "Jane Tech",
            "LOC-010", "CT Suite",
            "Contrast administered");
        ImagingState s1 = await grain.GetImageAsync();
        Assert.That(s1.Status, Is.EqualTo("VIEWABLE"));
        Assert.That(s1.ObjectType, Is.EqualTo("CT"));

        // DICOM metadata
        await grain.RecordDicomMetadataAsync(
            "1.2.840.10008.STUDY.001",
            "1.2.840.10008.SERIES.001",
            "1.2.840.10008.INSTANCE.001",
            "CT",
            "CHEST",
            "1.2.840.10008.1.2.1");
        ImagingState s2 = await grain.GetImageAsync();
        Assert.That(s2.Modality, Is.EqualTo("CT"));
        Assert.That(s2.BodyPartExamined, Is.EqualTo("CHEST"));

        // Dimensions
        await grain.SetImageDimensionsAsync(512, 512, 52428800L, "JPEG2000");
        ImagingState s3 = await grain.GetImageAsync();
        Assert.That(s3.ImageWidth, Is.EqualTo(512));
        Assert.That(s3.FileSizeBytes, Is.EqualTo(52428800L));

        // Acquisition
        DateTime acqTime = new DateTime(2024, 12, 1, 10, 30, 0, DateTimeKind.Utc);
        await grain.RecordAcquisitionAsync("VA Tampa", acqTime, "HFS");

        // Package link
        await grain.LinkToPackageAsync("RADIOLOGY", "RAD:STUDY-100");

        // Annotations
        await grain.AddAnnotationAsync("TEXT", "Right hilar mass identified", "Dr. Radiology");
        await grain.AddAnnotationAsync("MEASUREMENT", "3.2cm x 2.8cm", "Dr. Radiology");

        // Series
        await grain.AddSeriesInfoAsync("1.2.840.SERIES.001", "Axial", "CT", 85, 1);
        await grain.AddSeriesInfoAsync("1.2.840.SERIES.002", "Coronal Reformat", "CT", 35, 2);

        // Display status
        await grain.SetClinicalDisplayStatusAsync("VIEWABLE");

        // QA Review
        await grain.QaReviewAsync();

        // Final assertions
        ImagingState finalState = await grain.GetImageAsync();
        Assert.That(finalState.Status, Is.EqualTo("QA REVIEWED"));
        Assert.That(finalState.ClinicalDisplayStatus, Is.EqualTo("VIEWABLE"));
        Assert.That(finalState.PackageType, Is.EqualTo("RADIOLOGY"));
        Assert.That(finalState.PackageReference, Is.EqualTo("RAD:STUDY-100"));
        Assert.That(finalState.AcquisitionSite, Is.EqualTo("VA Tampa"));
        Assert.That(finalState.PatientOrientation, Is.EqualTo("HFS"));

        List<ImageAnnotation> annotations = await grain.GetAnnotationsAsync();
        Assert.That(annotations, Has.Count.EqualTo(2));

        List<ImageSeriesInfo> series = await grain.GetSeriesAsync();
        Assert.That(series, Has.Count.EqualTo(2));
        Assert.That(series[0].SeriesDescription, Is.EqualTo("Axial"));
        Assert.That(series[1].SeriesDescription, Is.EqualTo("Coronal Reformat"));
    }
}
