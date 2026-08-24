// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

// ═══════════════════════════════════════════════════════════════════════════
// Surgery Controller (#130)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/surgery")]
[Produces("application/json")]
public class SurgeryController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<SurgeryController> _log;
    public SurgeryController(IGrainFactory gf, ILogger<SurgeryController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId, [FromQuery] int max = 50)
        => Ok(await W(patientId).GetSurgeriesAsync(max));

    [HttpGet("{id}")] public async Task<IActionResult> Get(string patientId, string id)
        => Ok(await W(patientId).GetSurgeryAsync(id));

    [HttpPost] public async Task<IActionResult> Schedule(string patientId, [FromBody] ScheduleSurgeryReq r)
    {
        var id = await W(patientId).ScheduleSurgeryAsync(r.PrincipalProcedure, r.CptCode, r.DateOfOperation,
            r.SurgeonId, r.SurgeonName, r.AnesthesiaTechnique, r.SurgicalSpecialty, r.PreOpDiagnosis,
            r.LocationId, r.LocationName, r.Comments);
        return Created($"api/patient/{patientId}/surgery/{id}", new { SurgeryId = id });
    }

    [HttpPost("{id}/complete")] public async Task<IActionResult> Complete(string patientId, string id, [FromBody] CompleteSurgeryReq r)
    { await W(patientId).CompleteSurgeryAsync(id, r.OperativeReport, r.PostOpDiagnosis); return Ok(new { id, Message = "Completed" }); }

    [HttpPost("{id}/cancel")] public async Task<IActionResult> Cancel(string patientId, string id, [FromBody] CommentReq? r)
    { await W(patientId).CancelSurgeryAsync(id, r?.Comments); return Ok(new { id, Message = "Cancelled" }); }

    private ISurgeryGrain SurgGrain(string surgeryId) => _gf.GetGrain<ISurgeryGrain>(surgeryId);

    [HttpPost("{surgeryId}/preop-assessment")]
    public async Task<IActionResult> RecordPreOpAssessment(string patientId, string surgeryId, [FromBody] SurgPreOpAssessmentRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).RecordPreOpAssessmentAsync(
                request.AsaClassification, request.Notes, request.ProviderId, request.ProviderName,
                request.AssessmentDate ?? DateTime.UtcNow);
            return Ok(new { surgeryId, Message = "Pre-op assessment recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording pre-op assessment for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while recording the pre-op assessment.");
        }
    }

    [HttpPost("{surgeryId}/complications")]
    public async Task<IActionResult> AddComplication(string patientId, string surgeryId, [FromBody] SurgComplicationRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).AddComplicationAsync(
                request.ComplicationCode, request.Description, request.Severity,
                request.OccurrenceDate ?? DateTime.UtcNow, request.TreatmentAction);
            return Ok(new { surgeryId, Message = "Complication recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording complication for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while recording the complication.");
        }
    }

    [HttpPost("{surgeryId}/implants")]
    public async Task<IActionResult> AddImplant(string patientId, string surgeryId, [FromBody] SurgImplantRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).AddImplantAsync(
                request.DeviceName, request.Manufacturer, request.SerialNumber,
                request.LotNumber, request.BodySite);
            return Ok(new { surgeryId, Message = "Implant recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording implant for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while recording the implant.");
        }
    }

    [HttpPost("{surgeryId}/assistants")]
    public async Task<IActionResult> AddSurgicalAssistant(string patientId, string surgeryId, [FromBody] SurgAssistantRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).AddSurgicalAssistantAsync(
                request.AssistantId, request.AssistantName, request.Role);
            return Ok(new { surgeryId, Message = "Surgical assistant added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding assistant for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while adding the surgical assistant.");
        }
    }

    [HttpPost("{surgeryId}/intraop-details")]
    public async Task<IActionResult> RecordIntraOpDetails(string patientId, string surgeryId, [FromBody] SurgIntraOpDetailsRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).RecordIntraOpDetailsAsync(
                request.EstimatedBloodLoss, request.SpongeCountCorrect, request.NeedleCountCorrect,
                request.InstrumentCountCorrect, request.DispositionAfterSurgery);
            return Ok(new { surgeryId, Message = "Intra-op details recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording intra-op details for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while recording intra-op details.");
        }
    }

    [HttpPost("{surgeryId}/anesthesia-agents")]
    public async Task<IActionResult> AddAnesthesiaAgent(string patientId, string surgeryId, [FromBody] SurgAnesthesiaAgentRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).AddAnesthesiaAgentAsync(request.Agent);
            return Ok(new { surgeryId, Message = "Anesthesia agent added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding anesthesia agent for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while adding the anesthesia agent.");
        }
    }

    [HttpPost("{surgeryId}/specimens")]
    public async Task<IActionResult> AddSpecimen(string patientId, string surgeryId, [FromBody] SurgSpecimenRequest request)
    {
        try
        {
            await SurgGrain(surgeryId).AddSpecimenAsync(
                request.SpecimenType, request.BodySite, request.AccessionNumber,
                request.CollectionDateTime ?? DateTime.UtcNow);
            return Ok(new { surgeryId, Message = "Specimen recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording specimen for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while recording the specimen.");
        }
    }

    [HttpGet("{surgeryId}/complications")]
    public async Task<IActionResult> GetComplications(string patientId, string surgeryId)
    {
        try
        {
            var complications = await SurgGrain(surgeryId).GetComplicationsAsync();
            return Ok(complications);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving complications for surgery {SurgeryId}", surgeryId);
            return StatusCode(500, "An error occurred while retrieving complications.");
        }
    }
}
public class ScheduleSurgeryReq
{
    public string PrincipalProcedure { get; set; } = ""; public string? CptCode { get; set; }
    public DateTime DateOfOperation { get; set; } public string? SurgeonId { get; set; }
    public string? SurgeonName { get; set; } public string? AnesthesiaTechnique { get; set; }
    public string? SurgicalSpecialty { get; set; } public string? PreOpDiagnosis { get; set; }
    public string? LocationId { get; set; } public string? LocationName { get; set; }
    public string? Comments { get; set; }
}
public class CompleteSurgeryReq { public string? OperativeReport { get; set; } public string? PostOpDiagnosis { get; set; } }
public class CommentReq { public string? Comments { get; set; } }
public record SurgPreOpAssessmentRequest
{
    public int AsaClassification { get; init; }
    public string? Notes { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public DateTime? AssessmentDate { get; init; }
}
public record SurgComplicationRequest
{
    public string ComplicationCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Severity { get; init; }
    public DateTime? OccurrenceDate { get; init; }
    public string? TreatmentAction { get; init; }
}
public record SurgImplantRequest
{
    public string DeviceName { get; init; } = string.Empty;
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
    public string? LotNumber { get; init; }
    public string? BodySite { get; init; }
}
public record SurgAssistantRequest
{
    public string AssistantId { get; init; } = string.Empty;
    public string AssistantName { get; init; } = string.Empty;
    public string? Role { get; init; }
}
public record SurgIntraOpDetailsRequest
{
    public int? EstimatedBloodLoss { get; init; }
    public int? SpongeCountCorrect { get; init; }
    public int? NeedleCountCorrect { get; init; }
    public int? InstrumentCountCorrect { get; init; }
    public string? DispositionAfterSurgery { get; init; }
}
public record SurgAnesthesiaAgentRequest
{
    public string Agent { get; init; } = string.Empty;
}
public record SurgSpecimenRequest
{
    public string SpecimenType { get; init; } = string.Empty;
    public string? BodySite { get; init; }
    public string? AccessionNumber { get; init; }
    public DateTime? CollectionDateTime { get; init; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Radiology Controller (#75.1)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/radiology")]
[Produces("application/json")]
public class RadiologyController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<RadiologyController> _log;
    public RadiologyController(IGrainFactory gf, ILogger<RadiologyController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId, [FromQuery] int max = 50)
        => Ok(await W(patientId).GetRadiologyStudiesAsync(max));

    [HttpGet("{id}")] public async Task<IActionResult> Get(string patientId, string id)
        => Ok(await W(patientId).GetRadiologyStudyAsync(id));

    [HttpPost] public async Task<IActionResult> Order(string patientId, [FromBody] OrderRadiologyReq r)
    {
        // CPOE: creates the linked ORDER #100 so the study shows on the Orders tab.
        var id = await W(patientId).PlaceRadiologyOrderAsync(r.ProcedureName, r.ProcedureId, r.CptCode, r.ImagingType,
            r.RequestingProviderId, r.RequestingProviderName, r.Urgency, r.ClinicalHistory, r.ReasonForStudy,
            r.LocationId, r.LocationName);
        return Created($"api/patient/{patientId}/radiology/{id}", new { RadiologyId = id });
    }

    [HttpPost("{id}/complete")] public async Task<IActionResult> Complete(string patientId, string id, [FromBody] CompleteRadiologyReq r)
    { await W(patientId).CompleteRadiologyAsync(id, r.ReportText, r.Impression, r.InterpretingPhysicianId, r.InterpretingPhysicianName); return Ok(new { id }); }

    private IRadiologyGrain RadGrain(string studyId) => _gf.GetGrain<IRadiologyGrain>(studyId);

    [HttpPost("{studyId}/contrast")]
    public async Task<IActionResult> RecordContrast(string patientId, string studyId, [FromBody] RadContrastRequest request)
    {
        try
        {
            await RadGrain(studyId).RecordContrastAsync(request.ContrastAgent, request.Route, request.VolumeMl);
            return Ok(new { studyId, Message = "Contrast recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording contrast for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while recording contrast.");
        }
    }

    [HttpPost("{studyId}/contrast-reaction")]
    public async Task<IActionResult> RecordContrastReaction(string patientId, string studyId, [FromBody] RadContrastReactionRequest request)
    {
        try
        {
            await RadGrain(studyId).RecordContrastReactionAsync(request.ReactionDetails);
            return Ok(new { studyId, Message = "Contrast reaction recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording contrast reaction for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while recording the contrast reaction.");
        }
    }

    [HttpPost("{studyId}/radiation-dose")]
    public async Task<IActionResult> RecordRadiationDose(string patientId, string studyId, [FromBody] RadDoseRequest request)
    {
        try
        {
            await RadGrain(studyId).RecordRadiationDoseAsync(request.DoseMSv, request.CtdiVol, request.DoseLengthProduct);
            return Ok(new { studyId, Message = "Radiation dose recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording radiation dose for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while recording the radiation dose.");
        }
    }

    [HttpPost("{studyId}/sign")]
    public async Task<IActionResult> SignReport(string patientId, string studyId, [FromBody] RadSignRequest request)
    {
        try
        {
            await RadGrain(studyId).SignReportAsync(
                request.SignedById, request.SignedByName, request.SignedDateTime ?? DateTime.UtcNow);
            return Ok(new { studyId, Message = "Report signed" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error signing report for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while signing the report.");
        }
    }

    [HttpPost("{studyId}/flag-critical")]
    public async Task<IActionResult> FlagCritical(string patientId, string studyId)
    {
        try
        {
            await RadGrain(studyId).FlagCriticalResultAsync();
            return Ok(new { studyId, Message = "Flagged as critical" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error flagging critical result for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while flagging the critical result.");
        }
    }

    [HttpPost("{studyId}/critical-notification")]
    public async Task<IActionResult> RecordCriticalNotification(string patientId, string studyId, [FromBody] RadCriticalNotificationRequest request)
    {
        try
        {
            await RadGrain(studyId).RecordCriticalResultNotificationAsync(
                request.NotifiedTo, request.NotifiedDateTime ?? DateTime.UtcNow);
            return Ok(new { studyId, Message = "Critical notification recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording critical notification for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while recording the critical notification.");
        }
    }

    [HttpPost("{studyId}/acknowledge-critical")]
    public async Task<IActionResult> AcknowledgeCritical(string patientId, string studyId, [FromBody] RadAcknowledgeCriticalRequest request)
    {
        try
        {
            await RadGrain(studyId).AcknowledgeCriticalResultAsync(request.AcknowledgedBy);
            return Ok(new { studyId, Message = "Critical result acknowledged" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error acknowledging critical result for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while acknowledging the critical result.");
        }
    }

    [HttpPost("{studyId}/amend")]
    public async Task<IActionResult> AmendReport(string patientId, string studyId, [FromBody] RadAmendRequest request)
    {
        try
        {
            await RadGrain(studyId).AmendReportAsync(request.AmendmentText);
            return Ok(new { studyId, Message = "Report amended" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error amending report for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while amending the report.");
        }
    }

    [HttpGet("{studyId}/is-critical")]
    public async Task<IActionResult> IsCritical(string patientId, string studyId)
    {
        try
        {
            bool isCritical = await RadGrain(studyId).IsCriticalResultAsync();
            return Ok(new { studyId, IsCritical = isCritical });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error checking critical status for study {StudyId}", studyId);
            return StatusCode(500, "An error occurred while checking the critical status.");
        }
    }
}
public class OrderRadiologyReq
{
    public string ProcedureName { get; set; } = ""; public string? ProcedureId { get; set; }
    public string? CptCode { get; set; } public string? ImagingType { get; set; }
    public string? RequestingProviderId { get; set; } public string? RequestingProviderName { get; set; }
    public string? Urgency { get; set; } public string? ClinicalHistory { get; set; }
    public string? ReasonForStudy { get; set; } public string? OrderId { get; set; }
    public string? LocationId { get; set; } public string? LocationName { get; set; }
}
public class CompleteRadiologyReq
{
    public string? ReportText { get; set; } public string? Impression { get; set; }
    public string? InterpretingPhysicianId { get; set; } public string? InterpretingPhysicianName { get; set; }
}
public record RadContrastRequest
{
    public string ContrastAgent { get; init; } = string.Empty;
    public string? Route { get; init; }
    public double? VolumeMl { get; init; }
}
public record RadContrastReactionRequest
{
    public string ReactionDetails { get; init; } = string.Empty;
}
public record RadDoseRequest
{
    public double? DoseMSv { get; init; }
    public double? CtdiVol { get; init; }
    public double? DoseLengthProduct { get; init; }
}
public record RadSignRequest
{
    public string SignedById { get; init; } = string.Empty;
    public string SignedByName { get; init; } = string.Empty;
    public DateTime? SignedDateTime { get; init; }
}
public record RadCriticalNotificationRequest
{
    public string NotifiedTo { get; init; } = string.Empty;
    public DateTime? NotifiedDateTime { get; init; }
}
public record RadAcknowledgeCriticalRequest
{
    public string AcknowledgedBy { get; init; } = string.Empty;
}
public record RadAmendRequest
{
    public string AmendmentText { get; init; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════
// Imaging Controller (#2005)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/imaging")]
[Produces("application/json")]
public class ImagingController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<ImagingController> _log;
    private readonly NewVistas.ImageStorage.IImageIngestionService _ingestion;
    private readonly NewVistas.ImageStorage.IImageBlobStorageService _blobs;

    public ImagingController(
        IGrainFactory gf,
        ILogger<ImagingController> log,
        NewVistas.ImageStorage.IImageIngestionService ingestion,
        NewVistas.ImageStorage.IImageBlobStorageService blobs)
    {
        _gf = gf; _log = log; _ingestion = ingestion; _blobs = blobs;
    }

    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);
    private IImagingGrain ImgGrain(string imageId) => _gf.GetGrain<IImagingGrain>(imageId);

    [HttpGet] public async Task<IActionResult> List(string patientId, [FromQuery] int max = 50)
        => Ok(await W(patientId).GetImagesAsync(max));

    [HttpPost] public async Task<IActionResult> Capture(string patientId, [FromBody] CaptureImageReq r)
    {
        var id = await W(patientId).CaptureImageAsync(r.ObjectType, r.ProcedureDescription, r.SpecialtyIndex,
            r.ImageUrl, r.ThumbnailUrl, r.DicomSeriesUid, r.DicomStudyUid,
            r.ProcedureDate, r.CaptureDate, r.ImageCount,
            r.RadiologyId, r.TiuDocumentId, r.CapturedById, r.CapturedByName,
            r.LocationId, r.LocationName, r.Comments);
        return Created("", new { ImageId = id });
    }

    /// <summary>
    /// Uploads an image file (DICOM or raster). Runs the ingestion pipeline —
    /// parses DICOM if applicable, renders a thumbnail, writes both to blob
    /// storage, then records the grain metadata. Returns the generated
    /// imageId so the client can fetch view URIs and detail.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500 MB — large CT/MR studies
    [RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
    public async Task<IActionResult> Upload(
        string patientId,
        [FromForm] IFormFile file,
        [FromForm] string objectType,
        [FromForm] string? procedureDescription = null,
        [FromForm] string? locationName = null,
        [FromForm] string? comments = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required");

        try
        {
            await using Stream stream = file.OpenReadStream();
            var request = new NewVistas.ImageStorage.ImageIngestionRequest(
                PatientId: patientId,
                ObjectType: objectType,
                FileName: file.FileName,
                ContentType: file.ContentType,
                Content: stream,
                ProcedureDescription: procedureDescription,
                LocationName: locationName,
                Comments: comments,
                CapturedById: User?.Identity?.Name,
                CapturedByName: User?.Identity?.Name);

            var result = await _ingestion.IngestAsync(request, cancellationToken);
            return Created($"api/patient/{patientId}/imaging/{result.ImageId}", new
            {
                result.ImageId,
                result.OriginalBlobPath,
                result.ThumbnailBlobPath,
                result.Width,
                result.Height,
                result.Modality,
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Upload failed for patient {PatientId}", patientId);
            return StatusCode(500, "Image upload failed.");
        }
    }

    /// <summary>
    /// Returns a short-lived signed URI for the image thumbnail. The browser
    /// uses this URI to fetch the thumbnail directly from blob storage
    /// (filesystem provider: through the BlazorWeb signed-link endpoint;
    /// Azure provider: direct-to-blob SAS URI).
    /// </summary>
    [HttpGet("{imageId}/thumbnail-uri")]
    public async Task<IActionResult> GetThumbnailUri(string patientId, string imageId)
    {
        var state = await ImgGrain(imageId).GetImageAsync();
        if (string.IsNullOrEmpty(state.ThumbnailUrl))
            return NotFound();

        Uri uri = _blobs.GetReadSasUri(state.ThumbnailUrl, TimeSpan.FromMinutes(10));
        return Ok(new { uri = uri.ToString(), expiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
    }

    /// <summary>
    /// Authenticated proxy for the full-resolution original image. Streams
    /// the bytes from blob storage through this endpoint — every fetch
    /// re-checks the user's NewVistas JWT. Use this instead of a SAS URI
    /// when audit/compliance requires PHI bytes to cross an authenticated
    /// boundary.
    /// </summary>
    [HttpGet("{imageId}/original")]
    public async Task<IActionResult> GetOriginal(string patientId, string imageId)
    {
        var state = await ImgGrain(imageId).GetImageAsync();
        if (string.IsNullOrEmpty(state.ImageUrl))
            return NotFound();

        try
        {
            Stream stream = await _blobs.DownloadAsync(state.ImageUrl);
            string contentType = GuessContentType(state.ImageUrl);
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private static string GuessContentType(string path)
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".dcm" => "application/dicom",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }

    [HttpPost("{imageId}/dicom-metadata")]
    public async Task<IActionResult> RecordDicomMetadata(string patientId, string imageId, [FromBody] ImgDicomRequest request)
    {
        try
        {
            await ImgGrain(imageId).RecordDicomMetadataAsync(
                request.StudyUid, request.SeriesUid, request.InstanceUid,
                request.Modality, request.BodyPart, request.TransferSyntax);
            return Ok(new { imageId, Message = "DICOM metadata recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording DICOM metadata for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while recording DICOM metadata.");
        }
    }

    [HttpPost("{imageId}/dimensions")]
    public async Task<IActionResult> SetDimensions(string patientId, string imageId, [FromBody] ImgDimensionsRequest request)
    {
        try
        {
            await ImgGrain(imageId).SetImageDimensionsAsync(
                request.Width, request.Height, request.FileSizeBytes, request.CompressionType);
            return Ok(new { imageId, Message = "Image dimensions set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting dimensions for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while setting image dimensions.");
        }
    }

    [HttpPost("{imageId}/display-status")]
    public async Task<IActionResult> SetDisplayStatus(string patientId, string imageId, [FromBody] ImgDisplayStatusRequest request)
    {
        try
        {
            await ImgGrain(imageId).SetClinicalDisplayStatusAsync(request.Status);
            return Ok(new { imageId, Message = "Display status updated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting display status for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while setting display status.");
        }
    }

    [HttpPost("{imageId}/link-package")]
    public async Task<IActionResult> LinkToPackage(string patientId, string imageId, [FromBody] ImgLinkPackageRequest request)
    {
        try
        {
            await ImgGrain(imageId).LinkToPackageAsync(request.PackageType, request.PackageReference);
            return Ok(new { imageId, Message = "Linked to package" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error linking package for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while linking to the package.");
        }
    }

    [HttpPost("{imageId}/acquisition")]
    public async Task<IActionResult> RecordAcquisition(string patientId, string imageId, [FromBody] ImgAcquisitionRequest request)
    {
        try
        {
            await ImgGrain(imageId).RecordAcquisitionAsync(
                request.AcquisitionSite, request.AcquisitionDateTime, request.PatientOrientation);
            return Ok(new { imageId, Message = "Acquisition recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording acquisition for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while recording the acquisition.");
        }
    }

    [HttpPost("{imageId}/annotations")]
    public async Task<IActionResult> AddAnnotation(string patientId, string imageId, [FromBody] ImgAnnotationRequest request)
    {
        try
        {
            await ImgGrain(imageId).AddAnnotationAsync(
                request.AnnotationType, request.Content, request.AuthorName);
            return Ok(new { imageId, Message = "Annotation added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding annotation for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while adding the annotation.");
        }
    }

    [HttpPost("{imageId}/series")]
    public async Task<IActionResult> AddSeries(string patientId, string imageId, [FromBody] ImgSeriesRequest request)
    {
        try
        {
            await ImgGrain(imageId).AddSeriesInfoAsync(
                request.SeriesUid, request.SeriesDescription, request.Modality,
                request.ImageCount, request.SeriesNumber);
            return Ok(new { imageId, Message = "Series info added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding series info for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while adding series info.");
        }
    }

    [HttpPost("{imageId}/laterality")]
    public async Task<IActionResult> SetLaterality(string patientId, string imageId, [FromBody] ImgLateralityRequest request)
    {
        try
        {
            await ImgGrain(imageId).SetLateralityAsync(request.Laterality);
            return Ok(new { imageId, Message = "Laterality set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting laterality for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while setting laterality.");
        }
    }

    [HttpGet("{imageId}/annotations")]
    public async Task<IActionResult> GetAnnotations(string patientId, string imageId)
    {
        try
        {
            var annotations = await ImgGrain(imageId).GetAnnotationsAsync();
            return Ok(annotations);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving annotations for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while retrieving annotations.");
        }
    }

    [HttpGet("{imageId}/series")]
    public async Task<IActionResult> GetSeries(string patientId, string imageId)
    {
        try
        {
            var series = await ImgGrain(imageId).GetSeriesAsync();
            return Ok(series);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving series for image {ImageId}", imageId);
            return StatusCode(500, "An error occurred while retrieving series info.");
        }
    }
}
public class CaptureImageReq
{
    public string ObjectType { get; set; } = ""; public string? ProcedureDescription { get; set; }
    public string? SpecialtyIndex { get; set; } public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; } public string? DicomSeriesUid { get; set; }
    public string? DicomStudyUid { get; set; } public DateTime? ProcedureDate { get; set; }
    public DateTime CaptureDate { get; set; } public int ImageCount { get; set; }
    public string? RadiologyId { get; set; } public string? TiuDocumentId { get; set; }
    public string? CapturedById { get; set; } public string? CapturedByName { get; set; }
    public string? LocationId { get; set; } public string? LocationName { get; set; }
    public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Clinical Reminders Controller (#811.9)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/reminders")]
[Produces("application/json")]
public class RemindersController : ControllerBase
{
    private readonly IGrainFactory _gf;
    public RemindersController(IGrainFactory gf) { _gf = gf; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetRemindersAsync());

    [HttpPost] public async Task<IActionResult> Create(string patientId, [FromBody] CreateReminderReq r)
    {
        var id = await W(patientId).CreateReminderAsync(r.ReminderName, r.ReminderDefinitionId, r.Category, r.Priority, r.Frequency, r.DueDate);
        return Created("", new { ReminderId = id });
    }

    [HttpPost("{id}/complete")] public async Task<IActionResult> Complete(string patientId, string id, [FromBody] CompleteReminderReq? r)
    { await W(patientId).CompleteReminderAsync(id, r?.ProviderId, r?.ProviderName); return Ok(new { id }); }
}
public class CreateReminderReq
{
    public string ReminderName { get; set; } = ""; public string? ReminderDefinitionId { get; set; }
    public string? Category { get; set; } public string? Priority { get; set; }
    public string? Frequency { get; set; } public DateTime? DueDate { get; set; }
}
public class CompleteReminderReq { public string? ProviderId { get; set; } public string? ProviderName { get; set; } }

// ═══════════════════════════════════════════════════════════════════════════
// Immunizations Controller (#9000010.11)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/immunizations")]
[Produces("application/json")]
public class ImmunizationsController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<ImmunizationsController> _log;
    public ImmunizationsController(IGrainFactory gf, ILogger<ImmunizationsController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetImmunizationsAsync());

    [HttpPost] public async Task<IActionResult> Record(string patientId, [FromBody] RecordImmunizationReq r)
    {
        var id = await W(patientId).RecordImmunizationAsync(r.ImmunizationName, r.CvxCode, r.EventDateTime,
            r.Series, r.LotNumber, r.Manufacturer, r.AdministeredById, r.AdministeredByName,
            r.AdministrationSite, r.Route, r.Dose, r.LocationId, r.LocationName, r.Comments);
        return Created("", new { ImmunizationId = id });
    }

    [HttpPost("{immunizationId}/historical")]
    public async Task<IActionResult> MarkHistorical(string patientId, string immunizationId, [FromBody] ImmHistoricalRequest request)
    {
        try
        {
            await W(patientId).MarkImmunizationHistoricalAsync(immunizationId, request.InformationSource);
            return Ok(new { immunizationId, Message = "Marked as historical" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error marking immunization {ImmunizationId} as historical", immunizationId);
            return StatusCode(500, "An error occurred while marking the immunization as historical.");
        }
    }

    [HttpPost("{immunizationId}/vis")]
    public async Task<IActionResult> RecordVIS(string patientId, string immunizationId, [FromBody] ImmVISRequest request)
    {
        try
        {
            await W(patientId).RecordImmunizationVISAsync(immunizationId, request.VisDateOffered, request.VisDatePublished);
            return Ok(new { immunizationId, Message = "VIS recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording VIS for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while recording the VIS.");
        }
    }

    [HttpPost("{immunizationId}/series-info")]
    public async Task<IActionResult> SetSeriesInfo(string patientId, string immunizationId, [FromBody] ImmSeriesRequest request)
    {
        try
        {
            await W(patientId).SetImmunizationSeriesInfoAsync(immunizationId, request.DoseNumber, request.DosesInSeries, request.SeriesComplete);
            return Ok(new { immunizationId, Message = "Series info set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting series info for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while setting series info.");
        }
    }

    [HttpPost("{immunizationId}/administration-details")]
    public async Task<IActionResult> SetAdministrationDetails(string patientId, string immunizationId, [FromBody] ImmAdminDetailsRequest request)
    {
        try
        {
            await W(patientId).SetImmunizationAdministrationDetailsAsync(immunizationId, request.Site, request.Route);
            return Ok(new { immunizationId, Message = "Administration details set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting administration details for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while setting administration details.");
        }
    }

    [HttpPost("{immunizationId}/vaccine-group")]
    public async Task<IActionResult> SetVaccineGroup(string patientId, string immunizationId, [FromBody] ImmVaccineGroupRequest request)
    {
        try
        {
            await W(patientId).SetImmunizationVaccineGroupAsync(immunizationId, request.GroupName, request.GroupCode);
            return Ok(new { immunizationId, Message = "Vaccine group set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting vaccine group for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while setting the vaccine group.");
        }
    }

    [HttpPost("{immunizationId}/manufacturer")]
    public async Task<IActionResult> SetManufacturer(string patientId, string immunizationId, [FromBody] ImmManufacturerRequest request)
    {
        try
        {
            await W(patientId).SetImmunizationManufacturerAsync(immunizationId, request.Name, request.Code);
            return Ok(new { immunizationId, Message = "Manufacturer set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting manufacturer for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while setting the manufacturer.");
        }
    }

    [HttpPost("{immunizationId}/registry-status")]
    public async Task<IActionResult> UpdateRegistryStatus(string patientId, string immunizationId, [FromBody] ImmRegistryStatusRequest request)
    {
        try
        {
            await W(patientId).UpdateImmunizationRegistryStatusAsync(immunizationId, request.Status);
            return Ok(new { immunizationId, Message = "Registry status updated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error updating registry status for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while updating registry status.");
        }
    }

    [HttpPost("{immunizationId}/comments")]
    public async Task<IActionResult> AddComment(string patientId, string immunizationId, [FromBody] ImmCommentRequest request)
    {
        try
        {
            await W(patientId).AddImmunizationCommentAsync(immunizationId, request.AuthorName, request.CommentText);
            return Ok(new { immunizationId, Message = "Comment added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding comment for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while adding the comment.");
        }
    }

    [HttpGet("{immunizationId}/comments")]
    public async Task<IActionResult> GetComments(string patientId, string immunizationId)
    {
        try
        {
            var comments = await W(patientId).GetImmunizationCommentsAsync(immunizationId);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving comments for immunization {ImmunizationId}", immunizationId);
            return StatusCode(500, "An error occurred while retrieving comments.");
        }
    }
}
public class RecordImmunizationReq
{
    public string ImmunizationName { get; set; } = ""; public string? CvxCode { get; set; }
    public DateTime EventDateTime { get; set; } public string? Series { get; set; }
    public string? LotNumber { get; set; } public string? Manufacturer { get; set; }
    public string? AdministeredById { get; set; } public string? AdministeredByName { get; set; }
    public string? AdministrationSite { get; set; } public string? Route { get; set; }
    public string? Dose { get; set; } public string? LocationId { get; set; }
    public string? LocationName { get; set; } public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Health Factors Controller (#9000010.23)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/health-factors")]
[Produces("application/json")]
public class HealthFactorsController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<HealthFactorsController> _log;
    public HealthFactorsController(IGrainFactory gf, ILogger<HealthFactorsController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);
    private IHealthFactorGrain HfGrain(string healthFactorId) => _gf.GetGrain<IHealthFactorGrain>(healthFactorId);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetHealthFactorsAsync());

    [HttpPost] public async Task<IActionResult> Record(string patientId, [FromBody] RecordHealthFactorReq r)
    {
        var id = await W(patientId).RecordHealthFactorAsync(r.HealthFactorName, r.Category, r.EventDateTime,
            r.LevelSeverity, r.VisitId, r.LocationId, r.LocationName, r.EnteredById, r.EnteredByName, r.Comments);
        return Created("", new { HealthFactorId = id });
    }

    [HttpPost("{healthFactorId}/severity")]
    public async Task<IActionResult> UpdateSeverity(string patientId, string healthFactorId, [FromBody] HfSeverityRequest request)
    {
        try
        {
            await HfGrain(healthFactorId).UpdateSeverityAsync(request.SeverityLevel);
            return Ok(new { healthFactorId, Message = "Severity updated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error updating severity for health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while updating severity.");
        }
    }

    [HttpPost("{healthFactorId}/category")]
    public async Task<IActionResult> SetCategory(string patientId, string healthFactorId, [FromBody] HfCategoryRequest request)
    {
        try
        {
            await HfGrain(healthFactorId).SetCategoryAsync(request.Category, request.Subcategory);
            return Ok(new { healthFactorId, Message = "Category set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting category for health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while setting the category.");
        }
    }

    [HttpPost("{healthFactorId}/value")]
    public async Task<IActionResult> SetValue(string patientId, string healthFactorId, [FromBody] HfValueRequest request)
    {
        try
        {
            await HfGrain(healthFactorId).SetValueAsync(request.Value, request.Magnitude);
            return Ok(new { healthFactorId, Message = "Value set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting value for health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while setting the value.");
        }
    }

    [HttpPost("{healthFactorId}/resolve")]
    public async Task<IActionResult> Resolve(string patientId, string healthFactorId, [FromBody] HfResolveRequest request)
    {
        try
        {
            await HfGrain(healthFactorId).ResolveAsync(request.ResolvedByName);
            return Ok(new { healthFactorId, Message = "Health factor resolved" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error resolving health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while resolving the health factor.");
        }
    }

    [HttpPost("{healthFactorId}/reactivate")]
    public async Task<IActionResult> Reactivate(string patientId, string healthFactorId)
    {
        try
        {
            await HfGrain(healthFactorId).ReactivateAsync();
            return Ok(new { healthFactorId, Message = "Health factor reactivated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error reactivating health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while reactivating the health factor.");
        }
    }

    [HttpPost("{healthFactorId}/history")]
    public async Task<IActionResult> AddHistoryEntry(string patientId, string healthFactorId, [FromBody] HfHistoryEntryRequest request)
    {
        try
        {
            await HfGrain(healthFactorId).AddHistoryEntryAsync(
                request.Value, request.SeverityLevel, request.Comment, request.RecordedByName);
            return Ok(new { healthFactorId, Message = "History entry added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding history entry for health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while adding the history entry.");
        }
    }

    [HttpGet("{healthFactorId}/history")]
    public async Task<IActionResult> GetHistory(string patientId, string healthFactorId)
    {
        try
        {
            var history = await HfGrain(healthFactorId).GetHistoryAsync();
            return Ok(history);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving history for health factor {HealthFactorId}", healthFactorId);
            return StatusCode(500, "An error occurred while retrieving the history.");
        }
    }
}
public class RecordHealthFactorReq
{
    public string HealthFactorName { get; set; } = ""; public string? Category { get; set; }
    public DateTime EventDateTime { get; set; } public string? LevelSeverity { get; set; }
    public string? VisitId { get; set; } public string? LocationId { get; set; }
    public string? LocationName { get; set; } public string? EnteredById { get; set; }
    public string? EnteredByName { get; set; } public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Mental Health Controller (#601.71)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/mental-health")]
[Produces("application/json")]
public class MentalHealthController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<MentalHealthController> _log;
    public MentalHealthController(IGrainFactory gf, ILogger<MentalHealthController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetMentalHealthScreensAsync());

    [HttpPost] public async Task<IActionResult> Record(string patientId, [FromBody] RecordMentalHealthReq r)
    {
        var id = await W(patientId).RecordMentalHealthScreenAsync(r.InstrumentName, r.AdministrationDateTime,
            r.TotalScore, r.ScoreInterpretation, r.IsPositiveScreen, r.Responses,
            r.AdministeredById, r.AdministeredByName, r.LocationId, r.LocationName, r.Comments);
        return Created("", new { InstrumentId = id });
    }

    [HttpGet("{instrumentId}")] public async Task<IActionResult> GetDetail(string patientId, string instrumentId)
    {
        try
        {
            var state = await MhGrain(instrumentId).GetInstrumentAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving mental health instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while retrieving the instrument.");
        }
    }

    private IMentalHealthGrain MhGrain(string instrumentId) => _gf.GetGrain<IMentalHealthGrain>(instrumentId);

    [HttpPost("{instrumentId}/risk-assessment")]
    public async Task<IActionResult> RecordRiskAssessment(string patientId, string instrumentId, [FromBody] MhRiskAssessmentRequest request)
    {
        try
        {
            await MhGrain(instrumentId).RecordRiskAssessmentAsync(request.RiskLevel, request.RiskNotes);
            return Ok(new { instrumentId, Message = "Risk assessment recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording risk assessment for instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while recording the risk assessment.");
        }
    }

    [HttpPost("{instrumentId}/follow-up")]
    public async Task<IActionResult> SetFollowUp(string patientId, string instrumentId, [FromBody] MhFollowUpRequest request)
    {
        try
        {
            await MhGrain(instrumentId).SetFollowUpAsync(request.RequiresFollowUp, request.FollowUpDueDate, request.FollowUpPlan);
            return Ok(new { instrumentId, Message = "Follow-up recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting follow-up for instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while setting the follow-up.");
        }
    }

    [HttpPost("{instrumentId}/item-response")]
    public async Task<IActionResult> AddItemResponse(string patientId, string instrumentId, [FromBody] MhItemResponseRequest request)
    {
        try
        {
            await MhGrain(instrumentId).AddItemResponseAsync(
                request.ItemNumber, request.QuestionText, request.ResponseValue, request.ResponseText);
            return Ok(new { instrumentId, Message = "Item response recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding item response for instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while adding the item response.");
        }
    }

    [HttpPost("{instrumentId}/score")]
    public async Task<IActionResult> ScoreInstrument(string patientId, string instrumentId)
    {
        try
        {
            await MhGrain(instrumentId).ScoreInstrumentAsync();
            return Ok(new { instrumentId, Message = "Instrument scored" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error scoring instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while scoring the instrument.");
        }
    }

    [HttpPost("{instrumentId}/previous-score")]
    public async Task<IActionResult> SetPreviousScore(string patientId, string instrumentId, [FromBody] MhPreviousScoreRequest request)
    {
        try
        {
            await MhGrain(instrumentId).SetPreviousScoreAsync(request.PreviousScore, request.PreviousDate);
            return Ok(new { instrumentId, Message = "Previous score recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting previous score for instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while setting the previous score.");
        }
    }

    [HttpGet("{instrumentId}/score-change")]
    public async Task<IActionResult> GetScoreChange(string patientId, string instrumentId)
    {
        try
        {
            decimal? change = await MhGrain(instrumentId).CalculateScoreChangeAsync();
            return Ok(new { instrumentId, ScoreChange = change });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error calculating score change for instrument {InstrumentId}", instrumentId);
            return StatusCode(500, "An error occurred while calculating the score change.");
        }
    }
}
public class RecordMentalHealthReq
{
    public string InstrumentName { get; set; } = ""; public DateTime AdministrationDateTime { get; set; }
    public decimal? TotalScore { get; set; } public string? ScoreInterpretation { get; set; }
    public bool? IsPositiveScreen { get; set; } public Dictionary<string, string>? Responses { get; set; }
    public string? AdministeredById { get; set; } public string? AdministeredByName { get; set; }
    public string? LocationId { get; set; } public string? LocationName { get; set; }
    public string? Comments { get; set; }
}
public record MhRiskAssessmentRequest
{
    public int RiskLevel { get; init; }
    public string? RiskNotes { get; init; }
}
public record MhFollowUpRequest
{
    public bool RequiresFollowUp { get; init; }
    public DateTime? FollowUpDueDate { get; init; }
    public string? FollowUpPlan { get; init; }
}
public record MhItemResponseRequest
{
    public int ItemNumber { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public int ResponseValue { get; init; }
    public string? ResponseText { get; init; }
}
public record MhPreviousScoreRequest
{
    public decimal PreviousScore { get; init; }
    public DateTime PreviousDate { get; init; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Dietetics Controller (#115.2)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/dietetics")]
[Produces("application/json")]
public class DieteticsController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<DieteticsController> _log;
    public DieteticsController(IGrainFactory gf, ILogger<DieteticsController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetDietOrdersAsync());

    [HttpPost] public async Task<IActionResult> Create(string patientId, [FromBody] CreateDietReq r)
    {
        var id = await W(patientId).CreateDietOrderAsync(r.DietType, r.CurrentDiet, r.Modifications,
            r.Texture, r.FluidConsistency, r.CalorieLevel, r.SpecialInstructions, r.StartDateTime,
            r.ProviderId, r.ProviderName, r.Comments);
        return Created("", new { DietOrderId = id });
    }

    [HttpPost("{id}/discontinue")] public async Task<IActionResult> Discontinue(string patientId, string id)
    { await W(patientId).DiscontinueDietOrderAsync(id); return Ok(new { id, Message = "Discontinued" }); }

    [HttpPost("{dietOrderId}/nutrition-goals")]
    public async Task<IActionResult> SetNutritionGoals(string patientId, string dietOrderId, [FromBody] DietGoalsRequest request)
    {
        try
        {
            await W(patientId).SetDietNutritionGoalsAsync(dietOrderId, request.CalorieTarget, request.TargetWeight);
            return Ok(new { dietOrderId, Message = "Nutrition goals set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting nutrition goals for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting nutrition goals.");
        }
    }

    [HttpPost("{dietOrderId}/fluid-restriction")]
    public async Task<IActionResult> SetFluidRestriction(string patientId, string dietOrderId, [FromBody] DietFluidRequest request)
    {
        try
        {
            await W(patientId).SetDietFluidRestrictionAsync(dietOrderId, request.FluidRestrictionMl);
            return Ok(new { dietOrderId, Message = "Fluid restriction set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting fluid restriction for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting the fluid restriction.");
        }
    }

    [HttpPost("{dietOrderId}/texture")]
    public async Task<IActionResult> SetTexture(string patientId, string dietOrderId, [FromBody] DietTextureRequest request)
    {
        try
        {
            await W(patientId).SetDietTextureConsistencyAsync(dietOrderId, request.TextureConsistency);
            return Ok(new { dietOrderId, Message = "Texture consistency set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting texture for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting the texture consistency.");
        }
    }

    [HttpPost("{dietOrderId}/tube-feeding")]
    public async Task<IActionResult> SetTubeFeeding(string patientId, string dietOrderId, [FromBody] DietTubeFeedingRequest request)
    {
        try
        {
            await W(patientId).SetDietTubeFeedingAsync(dietOrderId, request.IsTubeFeeding, request.Formula, request.RateMlHr);
            return Ok(new { dietOrderId, Message = "Tube feeding set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting tube feeding for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting tube feeding.");
        }
    }

    [HttpPost("{dietOrderId}/npo")]
    public async Task<IActionResult> SetNPO(string patientId, string dietOrderId, [FromBody] DietNpoRequest request)
    {
        try
        {
            await W(patientId).SetDietNPOAsync(dietOrderId, request.IsNPO, request.StartDate, request.EndDate);
            return Ok(new { dietOrderId, Message = "NPO status set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting NPO for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting NPO status.");
        }
    }

    [HttpPost("{dietOrderId}/meal-preferences")]
    public async Task<IActionResult> RecordMealPreference(string patientId, string dietOrderId, [FromBody] DietMealPrefRequest request)
    {
        try
        {
            await W(patientId).RecordDietMealPreferenceAsync(dietOrderId, request.Preferences);
            return Ok(new { dietOrderId, Message = "Meal preferences recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording meal preferences for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while recording meal preferences.");
        }
    }

    [HttpPost("{dietOrderId}/nutrition-assessment")]
    public async Task<IActionResult> RecordNutritionAssessment(string patientId, string dietOrderId, [FromBody] DietAssessmentRequest request)
    {
        try
        {
            await W(patientId).RecordDietNutritionAssessmentAsync(dietOrderId, request.Score, request.AssessedByName);
            return Ok(new { dietOrderId, Message = "Nutrition assessment recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording nutrition assessment for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while recording the nutrition assessment.");
        }
    }

    [HttpPost("{dietOrderId}/bmi")]
    public async Task<IActionResult> RecordBMI(string patientId, string dietOrderId, [FromBody] DietBmiRequest request)
    {
        try
        {
            await W(patientId).RecordDietBMIAsync(dietOrderId, request.Bmi);
            return Ok(new { dietOrderId, Message = "BMI recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording BMI for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while recording the BMI.");
        }
    }

    [HttpPost("{dietOrderId}/allergy-considerations")]
    public async Task<IActionResult> SetAllergyConsiderations(string patientId, string dietOrderId, [FromBody] DietAllergyRequest request)
    {
        try
        {
            await W(patientId).SetDietAllergyConsiderationsAsync(dietOrderId, request.AllergyConsiderations);
            return Ok(new { dietOrderId, Message = "Allergy considerations set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting allergy considerations for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while setting allergy considerations.");
        }
    }

    [HttpGet("{dietOrderId}/modification-history")]
    public async Task<IActionResult> GetModificationHistory(string patientId, string dietOrderId)
    {
        try
        {
            var history = await W(patientId).GetDietModificationHistoryAsync(dietOrderId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving modification history for diet order {DietOrderId}", dietOrderId);
            return StatusCode(500, "An error occurred while retrieving the modification history.");
        }
    }
}
public class CreateDietReq
{
    public string DietType { get; set; } = ""; public string? CurrentDiet { get; set; }
    public List<string>? Modifications { get; set; } public string? Texture { get; set; }
    public string? FluidConsistency { get; set; } public string? CalorieLevel { get; set; }
    public string? SpecialInstructions { get; set; } public DateTime StartDateTime { get; set; }
    public string? ProviderId { get; set; } public string? ProviderName { get; set; }
    public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Prosthetics Controller (#669.1)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/prosthetics")]
[Produces("application/json")]
public class ProstheticsController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<ProstheticsController> _log;
    public ProstheticsController(IGrainFactory gf, ILogger<ProstheticsController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetProstheticsAsync());

    [HttpPost] public async Task<IActionResult> Issue(string patientId, [FromBody] IssueProstheticReq r)
    {
        var id = await W(patientId).IssueProstheticAsync(r.ItemDescription, r.HcpcsCode, r.ItemCategory,
            r.DateIssued, r.Quantity, r.Cost, r.ProviderId, r.ProviderName,
            r.LocationId, r.LocationName, r.IsServiceConnected, r.Comments);
        return Created("", new { ProstheticsId = id });
    }

    [HttpPost("{prostheticsId}/hcpcs")]
    public async Task<IActionResult> SetHcpcsCode(string patientId, string prostheticsId, [FromBody] ProsHcpcsRequest request)
    {
        try
        {
            await W(patientId).SetProstheticsHcpcsCodeAsync(prostheticsId, request.HcpcsCode);
            return Ok(new { prostheticsId, Message = "HCPCS code set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting HCPCS code for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while setting the HCPCS code.");
        }
    }

    [HttpPost("{prostheticsId}/cost")]
    public async Task<IActionResult> RecordCost(string patientId, string prostheticsId, [FromBody] ProsCostRequest request)
    {
        try
        {
            await W(patientId).RecordProstheticsCostAsync(prostheticsId, request.Cost, request.VendorName, request.VendorId);
            return Ok(new { prostheticsId, Message = "Cost recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording cost for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while recording the cost.");
        }
    }

    [HttpPost("{prostheticsId}/warranty")]
    public async Task<IActionResult> SetWarranty(string patientId, string prostheticsId, [FromBody] ProsWarrantyRequest request)
    {
        try
        {
            await W(patientId).SetProstheticsWarrantyAsync(prostheticsId, request.StartDate, request.EndDate);
            return Ok(new { prostheticsId, Message = "Warranty set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting warranty for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while setting the warranty.");
        }
    }

    [HttpPost("{prostheticsId}/delivery")]
    public async Task<IActionResult> RecordDelivery(string patientId, string prostheticsId, [FromBody] ProsDeliveryRequest request)
    {
        try
        {
            await W(patientId).RecordProstheticsDeliveryAsync(prostheticsId, request.DeliveryDate, request.DeliveryMethod, request.TrackingNumber);
            return Ok(new { prostheticsId, Message = "Delivery recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording delivery for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while recording the delivery.");
        }
    }

    [HttpPost("{prostheticsId}/fitting")]
    public async Task<IActionResult> RecordFitting(string patientId, string prostheticsId, [FromBody] ProsFittingRequest request)
    {
        try
        {
            await W(patientId).RecordProstheticsFittingAsync(prostheticsId, request.FittingNotes, request.FittedByName);
            return Ok(new { prostheticsId, Message = "Fitting recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording fitting for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while recording the fitting.");
        }
    }

    [HttpPost("{prostheticsId}/satisfaction")]
    public async Task<IActionResult> RecordSatisfaction(string patientId, string prostheticsId, [FromBody] ProsSatisfactionRequest request)
    {
        try
        {
            await W(patientId).RecordProstheticsSatisfactionAsync(prostheticsId, request.Rating);
            return Ok(new { prostheticsId, Message = "Satisfaction recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording satisfaction for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while recording the satisfaction.");
        }
    }

    [HttpPost("{prostheticsId}/schedule-maintenance")]
    public async Task<IActionResult> ScheduleMaintenance(string patientId, string prostheticsId, [FromBody] ProsScheduleMaintenanceRequest request)
    {
        try
        {
            await W(patientId).ScheduleProstheticsMaintenanceAsync(prostheticsId, request.NextMaintenanceDate, request.MaintenanceNotes);
            return Ok(new { prostheticsId, Message = "Maintenance scheduled" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error scheduling maintenance for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while scheduling maintenance.");
        }
    }

    [HttpPost("{prostheticsId}/maintenance-records")]
    public async Task<IActionResult> AddMaintenanceRecord(string patientId, string prostheticsId, [FromBody] ProsMaintenanceRecordRequest request)
    {
        try
        {
            await W(patientId).AddProstheticsMaintenanceRecordAsync(
                prostheticsId, request.MaintenanceType, request.TechnicianName, request.Notes, request.Cost);
            return Ok(new { prostheticsId, Message = "Maintenance record added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding maintenance record for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while adding the maintenance record.");
        }
    }

    [HttpGet("{prostheticsId}/maintenance-records")]
    public async Task<IActionResult> GetMaintenanceHistory(string patientId, string prostheticsId)
    {
        try
        {
            var history = await W(patientId).GetProstheticsMaintenanceHistoryAsync(prostheticsId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving maintenance history for prosthetics {ProstheticsId}", prostheticsId);
            return StatusCode(500, "An error occurred while retrieving the maintenance history.");
        }
    }
}
public class IssueProstheticReq
{
    public string ItemDescription { get; set; } = ""; public string? HcpcsCode { get; set; }
    public string? ItemCategory { get; set; } public DateTime DateIssued { get; set; }
    public int Quantity { get; set; } = 1; public decimal? Cost { get; set; }
    public string? ProviderId { get; set; } public string? ProviderName { get; set; }
    public string? LocationId { get; set; } public string? LocationName { get; set; }
    public bool IsServiceConnected { get; set; } public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Means Test Controller (#408.31)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/means-test")]
[Produces("application/json")]
public class MeansTestController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<MeansTestController> _log;
    public MeansTestController(IGrainFactory gf, ILogger<MeansTestController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetMeansTestsAsync());

    [HttpPost] public async Task<IActionResult> Record(string patientId, [FromBody] RecordMeansTestReq r)
    {
        var id = await W(patientId).RecordMeansTestAsync(r.TestType, r.DateOfTest, r.AnnualIncome, r.NetWorth,
            r.NumberOfDependents, r.EligibilityStatus, r.PriorityGroup, r.CompletedById, r.CompletedByName, r.Comments);
        return Created("", new { MeansTestId = id });
    }

    [HttpPost("{meansTestId}/income")]
    public async Task<IActionResult> RecordIncome(string patientId, string meansTestId, [FromBody] MtIncomeRequest request)
    {
        try
        {
            await W(patientId).RecordMeansTestIncomeAsync(meansTestId, request.VeteranGrossIncome, request.SpouseGrossIncome, request.DependentIncome);
            return Ok(new { meansTestId, Message = "Income recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording income for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while recording income.");
        }
    }

    [HttpPost("{meansTestId}/assets")]
    public async Task<IActionResult> RecordAssets(string patientId, string meansTestId, [FromBody] MtAssetsRequest request)
    {
        try
        {
            await W(patientId).RecordMeansTestAssetsAsync(meansTestId, request.TotalNetWorth, request.PropertyValue, request.OtherAssets);
            return Ok(new { meansTestId, Message = "Assets recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording assets for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while recording assets.");
        }
    }

    [HttpPost("{meansTestId}/expenses")]
    public async Task<IActionResult> RecordExpenses(string patientId, string meansTestId, [FromBody] MtExpensesRequest request)
    {
        try
        {
            await W(patientId).RecordMeansTestExpensesAsync(meansTestId, request.DeductibleExpenses);
            return Ok(new { meansTestId, Message = "Expenses recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording expenses for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while recording expenses.");
        }
    }

    [HttpPost("{meansTestId}/calculate-adjusted")]
    public async Task<IActionResult> CalculateAdjustedIncome(string patientId, string meansTestId)
    {
        try
        {
            await W(patientId).CalculateMeansTestAdjustedIncomeAsync(meansTestId);
            return Ok(new { meansTestId, Message = "Adjusted income calculated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error calculating adjusted income for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while calculating adjusted income.");
        }
    }

    [HttpPost("{meansTestId}/gmt-threshold")]
    public async Task<IActionResult> SetGmtThreshold(string patientId, string meansTestId, [FromBody] MtGmtThresholdRequest request)
    {
        try
        {
            await W(patientId).SetMeansTestGmtThresholdAsync(meansTestId, request.GmtThreshold);
            return Ok(new { meansTestId, Message = "GMT threshold set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting GMT threshold for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while setting the GMT threshold.");
        }
    }

    [HttpPost("{meansTestId}/hardship")]
    public async Task<IActionResult> DetermineHardship(string patientId, string meansTestId, [FromBody] MtHardshipRequest request)
    {
        try
        {
            await W(patientId).DetermineMeansTestHardshipAsync(meansTestId, request.Determination);
            return Ok(new { meansTestId, Message = "Hardship determination recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error determining hardship for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while recording the hardship determination.");
        }
    }

    [HttpPost("{meansTestId}/copay-result")]
    public async Task<IActionResult> SetCopayTestResult(string patientId, string meansTestId, [FromBody] MtCopayResultRequest request)
    {
        try
        {
            await W(patientId).SetMeansTestCopayResultAsync(meansTestId, request.Result);
            return Ok(new { meansTestId, Message = "Copay test result set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting copay result for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while setting the copay test result.");
        }
    }

    [HttpPost("{meansTestId}/dependents")]
    public async Task<IActionResult> AddDependent(string patientId, string meansTestId, [FromBody] MtDependentRequest request)
    {
        try
        {
            await W(patientId).AddMeansTestDependentAsync(
                meansTestId, request.Name, request.Relationship, request.Income, request.NetWorth, request.DateOfBirth);
            return Ok(new { meansTestId, Message = "Dependent added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding dependent for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while adding the dependent.");
        }
    }

    [HttpGet("{meansTestId}/dependents")]
    public async Task<IActionResult> GetDependents(string patientId, string meansTestId)
    {
        try
        {
            var dependents = await W(patientId).GetMeansTestDependentsAsync(meansTestId);
            return Ok(dependents);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving dependents for means test {MeansTestId}", meansTestId);
            return StatusCode(500, "An error occurred while retrieving dependents.");
        }
    }
}
public class RecordMeansTestReq
{
    public string TestType { get; set; } = ""; public DateTime DateOfTest { get; set; }
    public decimal? AnnualIncome { get; set; } public decimal? NetWorth { get; set; }
    public int? NumberOfDependents { get; set; } public string? EligibilityStatus { get; set; }
    public string? PriorityGroup { get; set; } public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; } public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Connected Conditions Controller (#2.04)
// ═══════════════════════════════════════════════════════════════════════════

[Authorize]
[ApiController]
[Route("api/patient/{patientId}/service-connected")]
[Produces("application/json")]
public class ServiceConnectedController : ControllerBase
{
    private readonly IGrainFactory _gf;
    private readonly ILogger<ServiceConnectedController> _log;
    public ServiceConnectedController(IGrainFactory gf, ILogger<ServiceConnectedController> log) { _gf = gf; _log = log; }
    private IPatientWorkflowGrain W(string pid) => _gf.GetGrain<IPatientWorkflowGrain>(pid);

    [HttpGet] public async Task<IActionResult> List(string patientId)
        => Ok(await W(patientId).GetServiceConnectedConditionsAsync());

    [HttpPost] public async Task<IActionResult> Record(string patientId, [FromBody] RecordSCReq r)
    {
        var id = await W(patientId).RecordServiceConnectedConditionAsync(r.Condition, r.DiagnosisCode,
            r.DisabilityPercentage, r.IsServiceConnected, r.EffectiveDate, r.ExtremityAffected, r.Comments);
        return Created("", new { ConditionId = id });
    }

    [HttpPost("{conditionId}/percentage")]
    public async Task<IActionResult> SetPercentage(string patientId, string conditionId, [FromBody] ScPercentageRequest request)
    {
        try
        {
            await W(patientId).SetServiceConnectedPercentageAsync(conditionId, request.Percentage);
            return Ok(new { conditionId, Message = "Percentage set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting percentage for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while setting the percentage.");
        }
    }

    [HttpPost("{conditionId}/rating-decision")]
    public async Task<IActionResult> RecordRatingDecision(string patientId, string conditionId, [FromBody] ScRatingDecisionRequest request)
    {
        try
        {
            await W(patientId).RecordScRatingDecisionAsync(conditionId, request.DecisionDate, request.DecisionId);
            return Ok(new { conditionId, Message = "Rating decision recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording rating decision for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while recording the rating decision.");
        }
    }

    [HttpPost("{conditionId}/disabilities")]
    public async Task<IActionResult> AddRatedDisability(string patientId, string conditionId, [FromBody] ScDisabilityRequest request)
    {
        try
        {
            await W(patientId).AddRatedDisabilityAsync(
                conditionId, request.ConditionName, request.RatingPercentage, request.EffectiveDate,
                request.DiagnosticCode, request.IsStatic);
            return Ok(new { conditionId, Message = "Rated disability added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding rated disability for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while adding the rated disability.");
        }
    }

    [HttpPost("{conditionId}/disabilities/remove")]
    public async Task<IActionResult> RemoveRatedDisability(string patientId, string conditionId, [FromBody] ScRemoveDisabilityRequest request)
    {
        try
        {
            await W(patientId).RemoveScRatedDisabilityAsync(conditionId, request.ConditionName);
            return Ok(new { conditionId, Message = "Rated disability removed" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error removing rated disability for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while removing the rated disability.");
        }
    }

    [HttpPost("{conditionId}/calculate-combined")]
    public async Task<IActionResult> CalculateCombinedRating(string patientId, string conditionId)
    {
        try
        {
            await W(patientId).CalculateScCombinedRatingAsync(conditionId);
            return Ok(new { conditionId, Message = "Combined rating calculated" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error calculating combined rating for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while calculating the combined rating.");
        }
    }

    [HttpPost("{conditionId}/appeal-status")]
    public async Task<IActionResult> SetAppealStatus(string patientId, string conditionId, [FromBody] ScAppealStatusRequest request)
    {
        try
        {
            await W(patientId).SetScAppealStatusAsync(conditionId, request.Status, request.AppealFiledDate);
            return Ok(new { conditionId, Message = "Appeal status set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting appeal status for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while setting the appeal status.");
        }
    }

    [HttpPost("{conditionId}/exam")]
    public async Task<IActionResult> RecordExam(string patientId, string conditionId, [FromBody] ScExamRequest request)
    {
        try
        {
            await W(patientId).RecordScExamAsync(conditionId, request.ExamDate, request.ExaminingFacility, request.NextExamDueDate);
            return Ok(new { conditionId, Message = "Exam recorded" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error recording exam for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while recording the exam.");
        }
    }

    [HttpPost("{conditionId}/permanent-total")]
    public async Task<IActionResult> SetPermanentAndTotal(string patientId, string conditionId, [FromBody] ScPermanentTotalRequest request)
    {
        try
        {
            await W(patientId).SetScPermanentAndTotalAsync(conditionId, request.IsPermanentAndTotal);
            return Ok(new { conditionId, Message = "Permanent and total status set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting permanent and total for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while setting permanent and total status.");
        }
    }

    [HttpPost("{conditionId}/smc")]
    public async Task<IActionResult> SetSpecialMonthlyCompensation(string patientId, string conditionId, [FromBody] ScSmcRequest request)
    {
        try
        {
            await W(patientId).SetScSpecialMonthlyCompensationAsync(conditionId, request.SmcLevel);
            return Ok(new { conditionId, Message = "SMC level set" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error setting SMC for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while setting the SMC level.");
        }
    }

    [HttpPost("{conditionId}/notes")]
    public async Task<IActionResult> AddNote(string patientId, string conditionId, [FromBody] ScNoteRequest request)
    {
        try
        {
            await W(patientId).AddScConditionNoteAsync(conditionId, request.AuthorName, request.NoteText);
            return Ok(new { conditionId, Message = "Note added" });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error adding note for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while adding the note.");
        }
    }

    [HttpGet("{conditionId}/disabilities")]
    public async Task<IActionResult> GetRatedDisabilities(string patientId, string conditionId)
    {
        try
        {
            var disabilities = await W(patientId).GetScRatedDisabilitiesAsync(conditionId);
            return Ok(disabilities);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error retrieving disabilities for condition {ConditionId}", conditionId);
            return StatusCode(500, "An error occurred while retrieving rated disabilities.");
        }
    }
}
public class RecordSCReq
{
    public string Condition { get; set; } = ""; public string? DiagnosisCode { get; set; }
    public int? DisabilityPercentage { get; set; } public bool IsServiceConnected { get; set; }
    public DateTime? EffectiveDate { get; set; } public string? ExtremityAffected { get; set; }
    public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Phase 5 Request DTOs
// ═══════════════════════════════════════════════════════════════════════════

// Imaging DTOs
public record ImgDicomRequest(string? StudyUid, string? SeriesUid, string? InstanceUid, string? Modality, string? BodyPart, string? TransferSyntax);
public record ImgDimensionsRequest(int Width, int Height, long? FileSizeBytes, string? CompressionType);
public record ImgDisplayStatusRequest(string Status);
public record ImgLinkPackageRequest(string PackageType, string PackageReference);
public record ImgAcquisitionRequest(string? AcquisitionSite, DateTime AcquisitionDateTime, string? PatientOrientation);
public record ImgAnnotationRequest(string AnnotationType, string Content, string? AuthorName);
public record ImgSeriesRequest(string SeriesUid, string? SeriesDescription, string? Modality, int ImageCount, int? SeriesNumber);
public record ImgLateralityRequest(string Laterality);

// Immunization DTOs
public record ImmHistoricalRequest(string InformationSource);
public record ImmVISRequest(DateTime VisDateOffered, DateTime VisDatePublished);
public record ImmSeriesRequest(int DoseNumber, int DosesInSeries, bool SeriesComplete);
public record ImmAdminDetailsRequest(string Site, string Route);
public record ImmVaccineGroupRequest(string GroupName, string GroupCode);
public record ImmManufacturerRequest(string Name, string Code);
public record ImmRegistryStatusRequest(string Status);
public record ImmCommentRequest(string AuthorName, string CommentText);

// Health Factor DTOs
public record HfSeverityRequest(string SeverityLevel);
public record HfCategoryRequest(string Category, string? Subcategory);
public record HfValueRequest(string Value, string? Magnitude);
public record HfResolveRequest(string ResolvedByName);
public record HfHistoryEntryRequest(string Value, string? SeverityLevel, string? Comment, string? RecordedByName);

// Dietetics DTOs
public record DietGoalsRequest(int? CalorieTarget, decimal? TargetWeight);
public record DietFluidRequest(int? FluidRestrictionMl);
public record DietTextureRequest(string TextureConsistency);
public record DietTubeFeedingRequest(bool IsTubeFeeding, string? Formula, decimal? RateMlHr);
public record DietNpoRequest(bool IsNPO, DateTime? StartDate, DateTime? EndDate);
public record DietMealPrefRequest(string Preferences);
public record DietAssessmentRequest(decimal Score, string AssessedByName);
public record DietBmiRequest(decimal Bmi);
public record DietAllergyRequest(string AllergyConsiderations);

// Prosthetics DTOs
public record ProsHcpcsRequest(string HcpcsCode);
public record ProsCostRequest(decimal Cost, string? VendorName, string? VendorId);
public record ProsWarrantyRequest(DateTime StartDate, DateTime EndDate);
public record ProsDeliveryRequest(DateTime DeliveryDate, string DeliveryMethod, string? TrackingNumber);
public record ProsFittingRequest(string FittingNotes, string FittedByName);
public record ProsSatisfactionRequest(int Rating);
public record ProsScheduleMaintenanceRequest(DateTime NextMaintenanceDate, string? MaintenanceNotes);
public record ProsMaintenanceRecordRequest(string MaintenanceType, string? TechnicianName, string? Notes, decimal? Cost);

// Means Test DTOs
public record MtIncomeRequest(decimal VeteranGrossIncome, decimal? SpouseGrossIncome, decimal? DependentIncome);
public record MtAssetsRequest(decimal? TotalNetWorth, decimal? PropertyValue, decimal? OtherAssets);
public record MtExpensesRequest(decimal DeductibleExpenses);
public record MtGmtThresholdRequest(decimal GmtThreshold);
public record MtHardshipRequest(string Determination);
public record MtCopayResultRequest(string Result);
public record MtDependentRequest(string Name, string Relationship, decimal Income, decimal NetWorth, DateTime? DateOfBirth);

// SC Condition DTOs
public record ScPercentageRequest(int Percentage);
public record ScRatingDecisionRequest(DateTime DecisionDate, string? DecisionId);
public record ScDisabilityRequest(string ConditionName, int RatingPercentage, DateTime EffectiveDate, string? DiagnosticCode, bool IsStatic);
public record ScRemoveDisabilityRequest(string ConditionName);
public record ScAppealStatusRequest(string Status, DateTime? AppealFiledDate);
public record ScExamRequest(DateTime ExamDate, string? ExaminingFacility, DateTime? NextExamDueDate);
public record ScPermanentTotalRequest(bool IsPermanentAndTotal);
public record ScSmcRequest(string? SmcLevel);
public record ScNoteRequest(string AuthorName, string NoteText);

// ADT Controller moved to AdtController.cs
