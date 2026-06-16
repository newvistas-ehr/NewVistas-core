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
/// REST API for Nursing Shift Handoff / Report — SBAR-based nurse-to-nurse transfer.
/// </summary>
[Authorize]
[ApiController]
[Route("api/nursing/handoff")]
[Produces("application/json")]
public class ShiftHandoffController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ShiftHandoffController> _logger;

    public ShiftHandoffController(IGrainFactory grainFactory, ILogger<ShiftHandoffController> logger)
    { _grainFactory = grainFactory; _logger = logger; }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}")]
    public async Task<IActionResult> Create(string patientId, [FromBody] CreateHandoffRequest req)
    {
        try
        {
            string id = await W(patientId).CreateShiftHandoffAsync(
                req.Shift, req.ShiftDate ?? DateTime.UtcNow.Date,
                req.OutgoingNurseId, req.OutgoingNurseName,
                req.LocationId, req.LocationName, req.BedNumber,
                req.Sbar ?? new SbarPatientSummary(), req.SafetyConcerns, req.Notes);
            return Created("", new { HandoffId = id });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating handoff for {PatientId}", patientId); return StatusCode(500, "Failed to create handoff."); }
    }

    [HttpGet("{patientId}")]
    public async Task<IActionResult> GetAll(string patientId)
    {
        try { return Ok(await W(patientId).GetShiftHandoffsAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting handoffs"); return StatusCode(500, "Failed to get handoffs."); }
    }

    [HttpGet("{patientId}/{handoffId}")]
    public async Task<IActionResult> Get(string patientId, string handoffId)
    {
        try { return Ok(await W(patientId).GetShiftHandoffAsync(handoffId)); }
        catch (Exception ex) { _logger.LogError(ex, "Error getting handoff {HandoffId}", handoffId); return StatusCode(500, "Failed to get handoff."); }
    }

    [HttpPost("{patientId}/{handoffId}/complete")]
    public async Task<IActionResult> Complete(string patientId, string handoffId)
    {
        try { await W(patientId).CompleteShiftHandoffAsync(handoffId); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error completing handoff"); return StatusCode(500, "Failed to complete handoff."); }
    }

    [HttpPost("{patientId}/{handoffId}/acknowledge")]
    public async Task<IActionResult> Acknowledge(string patientId, string handoffId, [FromBody] AcknowledgeRequest req)
    {
        try { await W(patientId).AcknowledgeShiftHandoffAsync(handoffId, req.IncomingNurseId, req.IncomingNurseName); return Ok(); }
        catch (Exception ex) { _logger.LogError(ex, "Error acknowledging handoff"); return StatusCode(500, "Failed to acknowledge handoff."); }
    }

    public record CreateHandoffRequest
    {
        public NursingShift Shift { get; init; }
        public DateTime? ShiftDate { get; init; }
        public string OutgoingNurseId { get; init; } = string.Empty;
        public string OutgoingNurseName { get; init; } = string.Empty;
        public string? LocationId { get; init; }
        public string? LocationName { get; init; }
        public string? BedNumber { get; init; }
        public SbarPatientSummary? Sbar { get; init; }
        public List<string>? SafetyConcerns { get; init; }
        public string? Notes { get; init; }
    }
    public record AcknowledgeRequest { public string IncomingNurseId { get; init; } = string.Empty; public string IncomingNurseName { get; init; } = string.Empty; }
}
