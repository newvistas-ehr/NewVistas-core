// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Incomplete Records Controller — chart deficiency tracking.
/// Maps to VistA DGPT package. Joint Commission compliance requirement.
/// </summary>
[ApiController]
[Route("api/incomplete-records")]
[Authorize]
public class IncompleteRecordsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IncompleteRecordsController> _logger;

    public IncompleteRecordsController(IGrainFactory grainFactory, ILogger<IncompleteRecordsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    [HttpGet("provider/{providerId}")]
    public async Task<ActionResult> GetProviderDeficiencies(string providerId, [FromQuery] string? status = null)
    {
        try
        {
            var index = _grainFactory.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:{providerId}");
            List<IncompleteRecordEntry> list = status switch
            {
                "OPEN" => await index.GetOpenDeficienciesAsync(),
                "DELINQUENT" => await index.GetDelinquentDeficienciesAsync(),
                _ => await index.GetAllDeficienciesAsync()
            };
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incomplete records for provider {ProviderId}", providerId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("{deficiencyId}")]
    public async Task<ActionResult> GetDeficiency(string deficiencyId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{deficiencyId}");
            IncompleteRecordState state = await grain.GetDeficiencyAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deficiency {DeficiencyId}", deficiencyId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateDeficiency([FromBody] IrCreateRequest request)
    {
        try
        {
            string defId = $"DGPT-{Guid.NewGuid():N}";
            var grain = _grainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
            await grain.CreateDeficiencyAsync(
                request.PatientId, request.PatientName,
                request.ProviderId, request.ProviderName,
                request.DeficiencyType, request.Description,
                request.DocumentId, request.AdmissionId,
                request.DischargeDate, request.DelinquentAfterDays ?? 30);

            IncompleteRecordState state = await grain.GetDeficiencyAsync();
            await SyncIndexAsync(request.ProviderId, state);

            return Created("", new { DeficiencyId = defId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deficiency");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{deficiencyId}/complete")]
    public async Task<ActionResult> CompleteDeficiency(string deficiencyId, [FromBody] IrCompleteRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{deficiencyId}");
            await grain.CompleteAsync(request.CompletedBy);
            IncompleteRecordState state = await grain.GetDeficiencyAsync();
            await SyncIndexAsync(state.ProviderId, state);
            return Ok(new { Message = "Deficiency completed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing deficiency {DeficiencyId}", deficiencyId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("{deficiencyId}/waive")]
    public async Task<ActionResult> WaiveDeficiency(string deficiencyId, [FromBody] IrWaiveRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{deficiencyId}");
            await grain.WaiveAsync(request.Reason, request.AuthorizedBy);
            IncompleteRecordState state = await grain.GetDeficiencyAsync();
            await SyncIndexAsync(state.ProviderId, state);
            return Ok(new { Message = "Deficiency waived." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiving deficiency {DeficiencyId}", deficiencyId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo([FromQuery] string providerId = "PROV-001")
    {
        try
        {
            await _grainFactory.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:{providerId}").SeedDemoDataAsync();
            return Ok(new { Message = "Loaded demo deficiencies." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading incomplete records demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    private async Task SyncIndexAsync(string providerId, IncompleteRecordState state)
    {
        var index = _grainFactory.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:{providerId}");
        await index.AddOrUpdateAsync(new IncompleteRecordEntry
        {
            DeficiencyId = state.DeficiencyId,
            PatientName = state.PatientName,
            ProviderName = state.ProviderName,
            DeficiencyType = state.DeficiencyType,
            Status = state.Status,
            DaysOutstanding = state.DaysOutstanding,
            IsDelinquent = state.Status == "DELINQUENT" || state.DaysOutstanding > state.DelinquentAfterDays,
            DischargeDate = state.DischargeDate
        });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record IrCreateRequest
{
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required string DeficiencyType { get; init; }
    public required string Description { get; init; }
    public string? DocumentId { get; init; }
    public string? AdmissionId { get; init; }
    public DateTime? DischargeDate { get; init; }
    public int? DelinquentAfterDays { get; init; }
}

public record IrCompleteRequest { public required string CompletedBy { get; init; } }
public record IrWaiveRequest { public required string Reason { get; init; } public required string AuthorizedBy { get; init; } }
