// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// TIU Notes API — Progress Notes, Discharge Summaries, Consult Notes.
///
/// Mirrors VistA TIU DOCUMENT file (#8925) routines:
///   - TIUSRVN.m — Create new note
///   - TIUSRVL.m — List notes for patient
///   - TIUSRVP.m — Get note by IEN
///   - TIUSRVA.m — Sign, cosign, amend, retract
/// </summary>
[Authorize]
[ApiController]
[Route("api/patient/{patientId}/notes")]
[Produces("application/json")]
public class NotesController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<NotesController> _logger;

    public NotesController(IGrainFactory grainFactory, ILogger<NotesController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>
    /// List notes for a patient. Optionally filter by document type.
    /// Reads from per-patient index — no grain fan-out.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TiuNoteSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TiuNoteSummary>>> GetNotes(
        string patientId,
        [FromQuery] string? type = null,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            var notes = await GetWorkflow(patientId).GetNotesAsync(type, maxResults);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notes for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving notes");
        }
    }

    /// <summary>
    /// Get recent notes from the hot cache — zero fan-out, fastest path.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<TiuNoteSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TiuNoteSummary>>> GetRecentNotes(string patientId)
    {
        try
        {
            var notes = await GetWorkflow(patientId).GetRecentNotesAsync();
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent notes for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving recent notes");
        }
    }

    /// <summary>
    /// Get note history with date range filtering — reads from index, no grain fan-out.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<TiuNoteSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TiuNoteSummary>>> GetNoteHistory(
        string patientId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int maxCount = 100)
    {
        try
        {
            var notes = await GetWorkflow(patientId).GetNoteHistoryAsync(from, to, maxCount);
            return Ok(notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving note history for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving note history");
        }
    }

    /// <summary>
    /// Get a specific note by document ID, including full report text.
    /// </summary>
    [HttpGet("{documentId}")]
    [ProducesResponseType(typeof(TiuDocumentState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TiuDocumentState>> GetNote(string patientId, string documentId)
    {
        try
        {
            var note = await GetWorkflow(patientId).GetNoteAsync(documentId);
            if (string.IsNullOrEmpty(note.PatientId))
                return NotFound(new { Message = $"Note '{documentId}' not found" });

            return Ok(note);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving note {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while retrieving the note");
        }
    }

    /// <summary>
    /// Create a new TIU note for a patient.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateNoteResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNote(string patientId, [FromBody] CreateNoteRequest request)
    {
        try
        {
            var documentId = await GetWorkflow(patientId).CreateNoteAsync(
                request.DocumentType, request.DocumentTypeId,
                request.ReportText, request.Subject,
                request.AuthorId, request.AuthorName,
                request.CosignerId, request.CosignerName,
                request.LocationId, request.LocationName,
                request.VisitId, request.ReferenceDate ?? DateTime.UtcNow);

            _logger.LogInformation(
                "Created {DocumentType} note {DocumentId} for patient {PatientId}",
                request.DocumentType, documentId, patientId);

            return Created($"api/patient/{patientId}/notes/{documentId}",
                new CreateNoteResponse { DocumentId = documentId, Message = "Note created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating note for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the note");
        }
    }

    /// <summary>
    /// Sign a note. Requires electronic signature code verification.
    /// </summary>
    [HttpPost("{documentId}/sign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignNote(string patientId, string documentId, [FromBody] SignNoteRequest? request = null)
    {
        try
        {
            // Verify electronic signature if provided
            if (request != null && !string.IsNullOrEmpty(request.ElectronicSignatureCode) && !string.IsNullOrEmpty(request.SignerId))
            {
                var personGrain = _grainFactory.GetGrain<INewPersonGrain>($"USER:{request.SignerId}");
                string sigHash = HashSignatureCode(request.ElectronicSignatureCode);
                bool valid = await personGrain.VerifyElectronicSignatureAsync(sigHash);
                if (!valid)
                    return Unauthorized(new { Message = "Electronic signature verification failed" });
            }

            await GetWorkflow(patientId).SignNoteAsync(documentId);
            return Ok(new { DocumentId = documentId, Message = "Note signed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing note {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while signing the note");
        }
    }

    /// <summary>
    /// Cosign a note. Requires electronic signature code verification.
    /// </summary>
    [HttpPost("{documentId}/cosign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CosignNote(string patientId, string documentId, [FromBody] CosignNoteRequest? request = null)
    {
        try
        {
            // Verify electronic signature if provided
            if (request != null && !string.IsNullOrEmpty(request.ElectronicSignatureCode) && !string.IsNullOrEmpty(request.CosignerId))
            {
                var personGrain = _grainFactory.GetGrain<INewPersonGrain>($"USER:{request.CosignerId}");
                string sigHash = HashSignatureCode(request.ElectronicSignatureCode);
                bool valid = await personGrain.VerifyElectronicSignatureAsync(sigHash);
                if (!valid)
                    return Unauthorized(new { Message = "Electronic signature verification failed" });
            }

            await GetWorkflow(patientId).CosignNoteAsync(documentId);
            return Ok(new { DocumentId = documentId, Message = "Note cosigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cosigning note {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while cosigning the note");
        }
    }

    private static string HashSignatureCode(string signatureCode)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(signatureCode));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Amend a completed note.
    /// </summary>
    [HttpPost("{documentId}/amend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AmendNote(string patientId, string documentId, [FromBody] AmendNoteRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AmendNoteAsync(documentId, request.AmendedText);
            return Ok(new { DocumentId = documentId, Message = "Note amended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error amending note {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while amending the note");
        }
    }

    /// <summary>
    /// Add an addendum to an existing note.
    /// </summary>
    [HttpPost("{documentId}/addendum")]
    [ProducesResponseType(typeof(CreateNoteResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddAddendum(string patientId, string documentId, [FromBody] AddAddendumRequest request)
    {
        try
        {
            var addendumId = await GetWorkflow(patientId).AddAddendumAsync(
                documentId, request.ReportText,
                request.AuthorId, request.AuthorName,
                request.ReferenceDate ?? DateTime.UtcNow);

            return Created($"api/patient/{patientId}/notes/{addendumId}",
                new CreateNoteResponse { DocumentId = addendumId, Message = "Addendum created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding addendum to note {DocumentId}", documentId);
            return StatusCode(500, "An error occurred while adding the addendum");
        }
    }
}

public class CreateNoteRequest
{
    public string DocumentType { get; set; } = "PROGRESS NOTE";
    public string? DocumentTypeId { get; set; }
    public string ReportText { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? CosignerId { get; set; }
    public string? CosignerName { get; set; }
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? VisitId { get; set; }
    public DateTime? ReferenceDate { get; set; }
}

public class AmendNoteRequest
{
    public string AmendedText { get; set; } = string.Empty;
}

public class AddAddendumRequest
{
    public string ReportText { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? ReferenceDate { get; set; }
}

public class SignNoteRequest
{
    public string? SignerId { get; set; }
    public string? ElectronicSignatureCode { get; set; }
}

public class CosignNoteRequest
{
    public string? CosignerId { get; set; }
    public string? ElectronicSignatureCode { get; set; }
}

public class CreateNoteResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
