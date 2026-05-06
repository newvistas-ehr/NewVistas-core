// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Collection Letter Generation — dunning letters, AR statements,
/// and collection correspondence (VistA PRCA — RCCLLT*.m, RCCL*.m).
/// </summary>
[Authorize]
[ApiController]
[Route("api/collections")]
[Produces("application/json")]
public class CollectionLetterController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CollectionLetterController> _logger;

    public CollectionLetterController(IGrainFactory grainFactory, ILogger<CollectionLetterController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Collection Letters ─────────────────────────────────────────────────

    [HttpPost("{patientId}/letters")]
    public async Task<IActionResult> GenerateLetter(string patientId, [FromBody] GenerateLetterRequest req)
    {
        try
        {
            string letterId = await W(patientId).GenerateCollectionLetterAsync(
                req.LetterType, req.GeneratedByUserId, req.GeneratedByUserName, req.Notes);
            return Created("", new { LetterId = letterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating collection letter for {PatientId}", patientId);
            return StatusCode(500, "Failed to generate collection letter.");
        }
    }

    [HttpGet("{patientId}/letters")]
    public async Task<IActionResult> GetLetters(string patientId)
    {
        try { return Ok(await W(patientId).GetCollectionLettersAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting collection letters for {PatientId}", patientId);
            return StatusCode(500, "Failed to get collection letters.");
        }
    }

    [HttpGet("{patientId}/letters/{letterId}")]
    public async Task<IActionResult> GetLetter(string patientId, string letterId)
    {
        try { return Ok(await W(patientId).GetCollectionLetterAsync(letterId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting letter {LetterId}", letterId);
            return StatusCode(500, "Failed to get collection letter.");
        }
    }

    [HttpPost("{patientId}/letters/{letterId}/print")]
    public async Task<IActionResult> MarkPrinted(string patientId, string letterId)
    {
        try { await W(patientId).MarkCollectionLetterPrintedAsync(letterId); return Ok(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking letter {LetterId} as printed", letterId);
            return StatusCode(500, "Failed to mark letter as printed.");
        }
    }

    [HttpPost("{patientId}/letters/{letterId}/mail")]
    public async Task<IActionResult> MarkMailed(string patientId, string letterId)
    {
        try { await W(patientId).MarkCollectionLetterMailedAsync(letterId); return Ok(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking letter {LetterId} as mailed", letterId);
            return StatusCode(500, "Failed to mark letter as mailed.");
        }
    }

    [HttpPost("{patientId}/letters/{letterId}/return")]
    public async Task<IActionResult> MarkReturned(string patientId, string letterId)
    {
        try { await W(patientId).MarkCollectionLetterReturnedAsync(letterId); return Ok(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking letter {LetterId} as returned", letterId);
            return StatusCode(500, "Failed to mark letter as returned.");
        }
    }

    [HttpPost("{patientId}/letters/{letterId}/cancel")]
    public async Task<IActionResult> CancelLetter(string patientId, string letterId, [FromBody] CancelLetterRequest? req)
    {
        try { await W(patientId).CancelCollectionLetterAsync(letterId, req?.Reason); return Ok(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling letter {LetterId}", letterId);
            return StatusCode(500, "Failed to cancel letter.");
        }
    }

    // ─── Demo Data ──────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    public async Task<IActionResult> LoadDemoData([FromQuery] string patientId)
    {
        try
        {
            IPatientWorkflowGrain wf = W(patientId);

            // Create demo AR accounts first
            await wf.CreateARAccountAsync(
                null, "CopayOutpatient", 50m,
                DateTime.UtcNow.AddDays(-70));
            await wf.CreateARAccountAsync(
                null, "CopayPharmacy", 24m,
                DateTime.UtcNow.AddDays(-15));

            // Generate collection letters in sequence
            await wf.GenerateCollectionLetterAsync(
                CollectionLetterType.FirstNotice, "DEMO-USER", "Demo User", "Auto-generated first notice");
            await wf.GenerateCollectionLetterAsync(
                CollectionLetterType.SecondNotice, "DEMO-USER", "Demo User", "Auto-generated second notice");
            await wf.GenerateCollectionLetterAsync(
                CollectionLetterType.Statement, "DEMO-USER", "Demo User", "Monthly statement");

            return Ok(new { Message = "Demo collection letter data loaded." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo data for {PatientId}", patientId);
            return StatusCode(500, "Failed to load demo data.");
        }
    }

    // ─── Request DTOs ───────────────────────────────────────────────────────

    public record GenerateLetterRequest
    {
        public CollectionLetterType LetterType { get; init; }
        public string? GeneratedByUserId { get; init; }
        public string? GeneratedByUserName { get; init; }
        public string? Notes { get; init; }
    }

    public record CancelLetterRequest
    {
        public string? Reason { get; init; }
    }
}
