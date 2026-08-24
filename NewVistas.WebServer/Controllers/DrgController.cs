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
/// DRG Controller — DRG Grouper for inpatient billing.
/// Maps to VistA ICD DRG Grouper.
/// </summary>
[ApiController]
[Route("api/drg")]
[Authorize]
public class DrgController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DrgController> _logger;

    public DrgController(IGrainFactory grainFactory, ILogger<DrgController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    [HttpGet("assignments")]
    public async Task<ActionResult> GetAssignments([FromQuery] string facilityId = "MAIN", [FromQuery] string? status = null)
    {
        try
        {
            var index = _grainFactory.GetGrain<IDrgIndexGrain>($"DRG-INDEX:{facilityId}");
            List<DrgAssignmentEntry> list = status != null
                ? await index.GetAssignmentsByStatusAsync(status)
                : await index.GetAllAssignmentsAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting DRG assignments");
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpGet("assignments/{admissionId}")]
    public async Task<ActionResult> GetAssignment(string admissionId)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:{admissionId}");
            DrgAssignmentState state = await grain.GetAssignmentAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting DRG assignment {AdmissionId}", admissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("assign")]
    public async Task<ActionResult> AssignDrg([FromBody] DrgAssignRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:{request.AdmissionId}");
            await grain.AssignDrgAsync(
                request.PatientId, request.PatientName,
                request.PrincipalDiagnosis, request.PrincipalDiagnosisDescription ?? "",
                request.SecondaryDiagnoses ?? new(), request.Procedures ?? new(),
                request.PatientAge, request.PatientSex,
                request.DischargeStatus ?? "HOME", request.ActualLOS);

            DrgAssignmentState state = await grain.GetAssignmentAsync();
            await SyncIndexAsync(request.FacilityId ?? "MAIN", state);

            return Created("", new { DrgCode = state.DrgCode, DrgDescription = state.DrgDescription, RelativeWeight = state.RelativeWeight });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning DRG for {AdmissionId}", request.AdmissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("assignments/{admissionId}/review")]
    public async Task<ActionResult> ReviewAssignment(string admissionId, [FromBody] DrgReviewRequest request)
    {
        try
        {
            var grain = _grainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:{admissionId}");
            await grain.ReviewAsync(request.ReviewerId);
            return Ok(new { Message = "DRG reviewed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing DRG {AdmissionId}", admissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    [HttpPost("assignments/{admissionId}/finalize")]
    public async Task<ActionResult> FinalizeAssignment(string admissionId, [FromQuery] string facilityId = "MAIN")
    {
        try
        {
            var grain = _grainFactory.GetGrain<IDrgAssignmentGrain>($"DRG:{admissionId}");
            await grain.FinalizeAsync();
            DrgAssignmentState state = await grain.GetAssignmentAsync();
            await SyncIndexAsync(facilityId, state);
            return Ok(new { Message = "DRG finalized." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing DRG {AdmissionId}", admissionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    // ─── Demo ────────────────────────────────────────────────────────────────

    [HttpPost("demo/load")]
    [AllowAnonymous]
    public async Task<ActionResult> LoadDemo([FromQuery] string facilityId = "MAIN")
    {
        try
        {
            // Delegates to the index grain, which owns the seed. Internal UIs call that grain
            // directly; this endpoint exists for external callers and scripted setup.
            await _grainFactory.GetGrain<IDrgIndexGrain>($"DRG-INDEX:{facilityId}").SeedDemoDataAsync();
            return Ok(new { Message = "Loaded demo DRG assignments." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading DRG demo data");
            return StatusCode(500, "An error occurred.");
        }
    }

    private async Task SyncIndexAsync(string facilityId, DrgAssignmentState state)
    {
        var index = _grainFactory.GetGrain<IDrgIndexGrain>($"DRG-INDEX:{facilityId}");
        await index.AddOrUpdateAsync(new DrgAssignmentEntry
        {
            AdmissionId = state.AdmissionId,
            PatientName = state.PatientName,
            DrgCode = state.DrgCode,
            DrgDescription = state.DrgDescription,
            RelativeWeight = state.RelativeWeight,
            ActualLOS = state.ActualLOS,
            Status = state.Status,
            CcMccLevel = state.CcMccLevel
        });
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record DrgAssignRequest
{
    public required string AdmissionId { get; init; }
    public required string PatientId { get; init; }
    public required string PatientName { get; init; }
    public required string PrincipalDiagnosis { get; init; }
    public string? PrincipalDiagnosisDescription { get; init; }
    public List<string>? SecondaryDiagnoses { get; init; }
    public List<string>? Procedures { get; init; }
    public required int PatientAge { get; init; }
    public required string PatientSex { get; init; }
    public string? DischargeStatus { get; init; }
    public required int ActualLOS { get; init; }
    public string? FacilityId { get; init; }
}

public record DrgReviewRequest { public required string ReviewerId { get; init; } }
