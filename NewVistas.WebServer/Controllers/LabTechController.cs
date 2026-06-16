// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Lab Technologist workflows — Worklist, Accessioning, QC, Specimen Rejection, Delta Checks.
/// VistA LR package — LRACE.m, LRVER.m, LR QC module.
/// </summary>
[Authorize]
[ApiController]
[Route("api/lab-tech")]
[Produces("application/json")]
public class LabTechController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LabTechController> _logger;

    public LabTechController(IGrainFactory grainFactory, ILogger<LabTechController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain W(string patientId) => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Worklist ───────────────────────────────────────────────────────────

    [HttpPost("{patientId}/worklist/{locationId}/refresh")]
    public async Task<IActionResult> RefreshWorklist(string patientId, string locationId)
    {
        try { return Ok(await W(patientId).RefreshLabWorklistAsync(locationId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error refreshing worklist"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("worklist/{locationId}")]
    public async Task<IActionResult> GetWorklist(string locationId)
    {
        try { return Ok(await _grainFactory.GetGrain<ILabWorklistGrain>($"LAB-WORKLIST:{locationId}").GetAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting worklist"); return StatusCode(500, "Failed."); }
    }

    // ─── Accessioning ───────────────────────────────────────────────────────

    [HttpPost("{patientId}/accession")]
    public async Task<IActionResult> Accession(string patientId, [FromBody] AccessionRequest req)
    {
        try
        {
            string num = await W(patientId).AccessionSpecimenAsync(
                req.LabTestIds, req.SpecimenType, req.CollectionTube,
                req.CollectionDateTime ?? DateTime.UtcNow, req.LabSection,
                req.AccessionedByUserId, req.AccessionedByUserName, req.Notes);
            return Created("", new { AccessionNumber = num });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error accessioning"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("{patientId}/accessions")]
    public async Task<IActionResult> GetAccessions(string patientId)
    {
        try { return Ok(await W(patientId).GetAccessionsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("{patientId}/accessions/pending")]
    public async Task<IActionResult> GetPending(string patientId)
    {
        try { return Ok(await W(patientId).GetPendingAccessionsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("{patientId}/accessions/{accessionNumber}")]
    public async Task<IActionResult> GetAccession(string patientId, string accessionNumber)
    {
        try { return Ok(await W(patientId).GetAccessionAsync(accessionNumber)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    // ─── Specimen Rejection ─────────────────────────────────────────────────

    [HttpPost("{patientId}/accessions/{accessionNumber}/reject")]
    public async Task<IActionResult> Reject(string patientId, string accessionNumber, [FromBody] RejectRequest req)
    {
        try { await W(patientId).RejectSpecimenAsync(accessionNumber, req.Reason, req.RejectNotes, req.RejectedByUserId, req.OrderRecollect); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error rejecting"); return StatusCode(500, "Failed."); }
    }

    // ─── QC ─────────────────────────────────────────────────────────────────

    [HttpPost("{patientId}/qc")]
    public async Task<IActionResult> RecordQc(string patientId, [FromBody] QcRunRequest req)
    {
        try { return Ok(await W(patientId).RecordLabQcRunAsync(req.InstrumentId, req.LoincCode, req.TestName, req.QcLevel, req.LotNumber, req.MeasuredValue, req.ExpectedMean, req.StandardDeviation, req.TechId, req.TechName, req.Notes)); }
        catch (Exception ex) { _logger.LogError(ex, "Error recording QC"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("qc/{instrumentId}/{loincCode}")]
    public async Task<IActionResult> GetQc(string instrumentId, string loincCode)
    {
        try { return Ok(await _grainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{instrumentId}:{loincCode}").GetAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting QC"); return StatusCode(500, "Failed."); }
    }

    [HttpGet("qc/{instrumentId}/{loincCode}/allowed")]
    public async Task<IActionResult> IsAllowed(string instrumentId, string loincCode)
    {
        try { return Ok(new { Allowed = await _grainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{instrumentId}:{loincCode}").IsTestingAllowedAsync() }); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Failed."); }
    }

    // ─── Delta Checks ───────────────────────────────────────────────────────

    [HttpPost("{patientId}/delta-check")]
    public async Task<IActionResult> DeltaCheck(string patientId, [FromBody] DeltaCheckRequest req)
    {
        try { return Ok(await W(patientId).PerformDeltaCheckAsync(req.LabTestId, req.LoincCode, req.TestName, req.CurrentValue, req.ResultDate ?? DateTime.UtcNow, req.InstrumentId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error performing delta check"); return StatusCode(500, "Failed."); }
    }

    // ─── DTOs ───────────────────────────────────────────────────────────────

    public record AccessionRequest { public List<string> LabTestIds { get; init; } = new(); public string SpecimenType { get; init; } = string.Empty; public string? CollectionTube { get; init; } public DateTime? CollectionDateTime { get; init; } public string? LabSection { get; init; } public string? AccessionedByUserId { get; init; } public string? AccessionedByUserName { get; init; } public string? Notes { get; init; } }
    public record RejectRequest { public SpecimenRejectReason Reason { get; init; } public string? RejectNotes { get; init; } public string RejectedByUserId { get; init; } = string.Empty; public bool OrderRecollect { get; init; } }
    public record QcRunRequest { public string InstrumentId { get; init; } = string.Empty; public string LoincCode { get; init; } = string.Empty; public string TestName { get; init; } = string.Empty; public string QcLevel { get; init; } = string.Empty; public string LotNumber { get; init; } = string.Empty; public decimal MeasuredValue { get; init; } public decimal ExpectedMean { get; init; } public decimal StandardDeviation { get; init; } public string TechId { get; init; } = string.Empty; public string TechName { get; init; } = string.Empty; public string? Notes { get; init; } }
    public record DeltaCheckRequest { public string LabTestId { get; init; } = string.Empty; public string LoincCode { get; init; } = string.Empty; public string TestName { get; init; } = string.Empty; public decimal CurrentValue { get; init; } public DateTime? ResultDate { get; init; } public string? InstrumentId { get; init; } }
}
