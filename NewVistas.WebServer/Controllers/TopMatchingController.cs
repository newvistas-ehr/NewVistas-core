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
/// REST API for TOP Federal Debt Matching — RCTP*.m, RCTOP*.m.
/// </summary>
[Authorize]
[ApiController]
[Route("api/top-matching")]
[Produces("application/json")]
public class TopMatchingController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<TopMatchingController> _logger;

    public TopMatchingController(IGrainFactory grainFactory, ILogger<TopMatchingController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    [HttpPost("{patientId}/process")]
    public async Task<IActionResult> ProcessOffset(string patientId, [FromBody] ProcessOffsetRequest req)
    {
        try
        {
            string matchId = await W(patientId).ProcessTopOffsetMatchAsync(
                req.TreasuryTransactionId, req.TaxpayerIdNumber, req.TreasuryPatientName,
                req.OffsetAmount, req.OffsetSource, req.OffsetReceivedDate,
                req.ProcessedByUserId, req.ProcessedByUserName, req.Notes);
            return Created("", new { MatchId = matchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TOP offset for {PatientId}", patientId);
            return StatusCode(500, "Failed to process TOP offset.");
        }
    }

    [HttpGet("{patientId}/matches")]
    public async Task<IActionResult> GetMatches(string patientId)
    {
        try { return Ok(await W(patientId).GetTopMatchRecordsAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TOP matches for {PatientId}", patientId);
            return StatusCode(500, "Failed to get TOP match records.");
        }
    }

    [HttpGet("{patientId}/matches/{matchId}")]
    public async Task<IActionResult> GetMatch(string patientId, string matchId)
    {
        try { return Ok(await W(patientId).GetTopMatchRecordAsync(matchId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TOP match {MatchId}", matchId);
            return StatusCode(500, "Failed to get TOP match record.");
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        try
        {
            ITopMatchIndexGrain idx = _grainFactory.GetGrain<ITopMatchIndexGrain>("TOP-MATCH-IDX");
            return Ok(await idx.GetPendingAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending TOP matches");
            return StatusCode(500, "Failed to get pending matches.");
        }
    }

    public record ProcessOffsetRequest
    {
        public string TreasuryTransactionId { get; init; } = string.Empty;
        public string TaxpayerIdNumber { get; init; } = string.Empty;
        public string TreasuryPatientName { get; init; } = string.Empty;
        public decimal OffsetAmount { get; init; }
        public string OffsetSource { get; init; } = string.Empty;
        public DateTime OffsetReceivedDate { get; init; }
        public string? ProcessedByUserId { get; init; }
        public string? ProcessedByUserName { get; init; }
        public string? Notes { get; init; }
    }
}
