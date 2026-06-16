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
/// Blood Bank API — patient crossmatches, transfusions, and blood inventory.
/// VistA File #65 (Blood Bank) — BBAPI.m, BBTM.m, BBCM.m, BBTRAN.m
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class BloodBankController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<BloodBankController> _logger;

    public BloodBankController(IGrainFactory grainFactory, ILogger<BloodBankController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Patient Blood Bank Record ────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/bloodbank")]
    [ProducesResponseType(typeof(BloodBankPatientState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BloodBankPatientState>> GetBloodBankPatient(string patientId)
    {
        try
        {
            BloodBankPatientState record = await GetWorkflow(patientId).GetBloodBankPatientAsync();
            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blood bank record for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving the blood bank record");
        }
    }

    [HttpPut("api/patient/{patientId}/bloodbank/type")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateBloodType(string patientId, [FromBody] UpdateBloodTypeRequest request)
    {
        try
        {
            AboBloodType aboType = Enum.TryParse<AboBloodType>(request.AboType, out AboBloodType a) ? a : AboBloodType.Unknown;
            RhBloodType rhType = Enum.TryParse<RhBloodType>(request.RhType, out RhBloodType r) ? r : RhBloodType.Unknown;
            AntibodyScreenResult screenResult = Enum.TryParse<AntibodyScreenResult>(request.AntibodyScreenResult, out AntibodyScreenResult s) ? s : AntibodyScreenResult.NotDone;

            await GetWorkflow(patientId).UpdateBloodTypeAsync(
                aboType, rhType, screenResult,
                request.AntibodyScreenDate,
                request.DirectAntibodyTest,
                request.SpecialRequirements,
                request.Notes);

            _logger.LogInformation("Updated blood type for patient {PatientId} to {AboType} {RhType}", patientId, request.AboType, request.RhType);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating blood type for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while updating blood type");
        }
    }

    // ─── Crossmatches ─────────────────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/bloodbank/crossmatches")]
    [ProducesResponseType(typeof(List<CrossmatchIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CrossmatchIndexEntry>>> GetCrossmatches(string patientId)
    {
        try
        {
            List<CrossmatchIndexEntry> entries = await GetWorkflow(patientId).GetCrossmatchesAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving crossmatches for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving crossmatches");
        }
    }

    [HttpGet("api/patient/{patientId}/bloodbank/crossmatches/{crossmatchId}")]
    [ProducesResponseType(typeof(CrossmatchState), StatusCodes.Status200OK)]
    public async Task<ActionResult<CrossmatchState>> GetCrossmatch(string patientId, string crossmatchId)
    {
        try
        {
            ICrossmatchGrain xm = _grainFactory.GetGrain<ICrossmatchGrain>(crossmatchId);
            CrossmatchState state = await xm.GetCrossmatchAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving crossmatch {CrossmatchId}", crossmatchId);
            return StatusCode(500, "An error occurred while retrieving the crossmatch");
        }
    }

    [HttpPost("api/patient/{patientId}/bloodbank/crossmatches")]
    [ProducesResponseType(typeof(CrossmatchResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestCrossmatch(string patientId, [FromBody] RequestCrossmatchRequest request)
    {
        try
        {
            CrossmatchUrgency urgency = Enum.TryParse<CrossmatchUrgency>(request.Urgency, out CrossmatchUrgency u) ? u : CrossmatchUrgency.Routine;
            string crossmatchId = await GetWorkflow(patientId).RequestCrossmatchAsync(
                request.UnitId, urgency,
                request.RequestedByUserId, request.RequestedByUserName,
                request.Notes);

            _logger.LogInformation("Crossmatch {CrossmatchId} requested for patient {PatientId} on unit {UnitId}", crossmatchId, patientId, request.UnitId);
            return Created($"api/patient/{patientId}/bloodbank/crossmatches/{crossmatchId}",
                new CrossmatchResponse { CrossmatchId = crossmatchId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting crossmatch for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while requesting the crossmatch");
        }
    }

    [HttpPut("api/patient/{patientId}/bloodbank/crossmatches/{crossmatchId}/result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordCrossmatchResult(string patientId, string crossmatchId, [FromBody] CrossmatchResultRequest request)
    {
        try
        {
            CrossmatchResult result = Enum.TryParse<CrossmatchResult>(request.Result, out CrossmatchResult r) ? r : CrossmatchResult.Pending;
            CrossmatchMethod method = Enum.TryParse<CrossmatchMethod>(request.Method, out CrossmatchMethod m) ? m : CrossmatchMethod.Electronic;

            await GetWorkflow(patientId).RecordCrossmatchResultAsync(
                crossmatchId, result, method,
                request.TechnicianId, request.TechnicianName,
                request.AntibodyIdentification);

            _logger.LogInformation("Crossmatch {CrossmatchId} result recorded: {Result}", crossmatchId, request.Result);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording crossmatch result {CrossmatchId}", crossmatchId);
            return StatusCode(500, "An error occurred while recording the crossmatch result");
        }
    }

    // ─── Transfusions ─────────────────────────────────────────────────────────────

    [HttpGet("api/patient/{patientId}/bloodbank/transfusions")]
    [ProducesResponseType(typeof(List<TransfusionIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransfusionIndexEntry>>> GetTransfusions(string patientId)
    {
        try
        {
            List<TransfusionIndexEntry> entries = await GetWorkflow(patientId).GetTransfusionHistoryAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfusion history for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving transfusion history");
        }
    }

    [HttpGet("api/patient/{patientId}/bloodbank/transfusions/{transfusionId}")]
    [ProducesResponseType(typeof(TransfusionState), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransfusionState>> GetTransfusion(string patientId, string transfusionId)
    {
        try
        {
            ITransfusionGrain tx = _grainFactory.GetGrain<ITransfusionGrain>(transfusionId);
            TransfusionState state = await tx.GetTransfusionAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transfusion {TransfusionId}", transfusionId);
            return StatusCode(500, "An error occurred while retrieving the transfusion");
        }
    }

    [HttpPost("api/patient/{patientId}/bloodbank/transfusions")]
    [ProducesResponseType(typeof(TransfusionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> StartTransfusion(string patientId, [FromBody] StartTransfusionRequest request)
    {
        try
        {
            string transfusionId = await GetWorkflow(patientId).StartTransfusionAsync(
                request.CrossmatchId, request.UnitId,
                request.AdministeredByUserId, request.AdministeredByUserName,
                request.OrderedByUserId, request.OrderedByUserName,
                request.InfusionSite, request.PreTransfusionVitals);

            _logger.LogInformation("Transfusion {TransfusionId} started for patient {PatientId}", transfusionId, patientId);
            return Created($"api/patient/{patientId}/bloodbank/transfusions/{transfusionId}",
                new TransfusionResponse { TransfusionId = transfusionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting transfusion for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while starting the transfusion");
        }
    }

    [HttpPut("api/patient/{patientId}/bloodbank/transfusions/{transfusionId}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CompleteTransfusion(string patientId, string transfusionId, [FromBody] CompleteTransfusionRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CompleteTransfusionAsync(
                transfusionId,
                request.EndDateTime ?? DateTime.UtcNow,
                request.VolumeML,
                request.PostTransfusionVitals);

            _logger.LogInformation("Transfusion {TransfusionId} completed for patient {PatientId}", transfusionId, patientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing transfusion {TransfusionId}", transfusionId);
            return StatusCode(500, "An error occurred while completing the transfusion");
        }
    }

    [HttpPut("api/patient/{patientId}/bloodbank/transfusions/{transfusionId}/stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StopTransfusion(string patientId, string transfusionId, [FromBody] StopTransfusionRequest request)
    {
        try
        {
            TransfusionReactionType reactionType = Enum.TryParse<TransfusionReactionType>(request.ReactionType, out TransfusionReactionType rt)
                ? rt
                : TransfusionReactionType.None;

            await GetWorkflow(patientId).StopTransfusionAsync(
                transfusionId,
                request.EndDateTime ?? DateTime.UtcNow,
                request.StopReason,
                reactionType,
                request.ReactionNotes);

            _logger.LogInformation("Transfusion {TransfusionId} stopped for patient {PatientId}: {StopReason}", transfusionId, patientId, request.StopReason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping transfusion {TransfusionId}", transfusionId);
            return StatusCode(500, "An error occurred while stopping the transfusion");
        }
    }

    // ─── Blood Inventory (system-level, not patient-specific) ────────────────────

    [HttpGet("api/bloodbank/inventory")]
    [ProducesResponseType(typeof(List<BloodUnitIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BloodUnitIndexEntry>>> GetInventory(
        [FromQuery] string? productType,
        [FromQuery] string? aboType,
        [FromQuery] string? rhType,
        [FromQuery] bool availableOnly = true)
    {
        try
        {
            IBloodUnitIndexGrain idx = _grainFactory.GetGrain<IBloodUnitIndexGrain>("BB-UNIT-IDX");
            BloodProductType? pt = Enum.TryParse<BloodProductType>(productType, out BloodProductType p) ? p : null;
            AboBloodType? at = Enum.TryParse<AboBloodType>(aboType, out AboBloodType a) ? a : null;
            RhBloodType? rt = Enum.TryParse<RhBloodType>(rhType, out RhBloodType r) ? r : null;

            List<BloodUnitIndexEntry> units = await idx.SearchAsync(pt, at, rt, null, availableOnly);
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blood inventory");
            return StatusCode(500, "An error occurred while retrieving blood inventory");
        }
    }

    [HttpPost("api/bloodbank/units")]
    [ProducesResponseType(typeof(BloodUnitResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddBloodUnit([FromBody] AddBloodUnitRequest request)
    {
        try
        {
            string unitId = $"BB-UNIT:{Guid.NewGuid()}";
            BloodProductType productType = Enum.TryParse<BloodProductType>(request.ProductType, out BloodProductType pt) ? pt : BloodProductType.Other;
            AboBloodType aboType = Enum.TryParse<AboBloodType>(request.AboType, out AboBloodType a) ? a : AboBloodType.Unknown;
            RhBloodType rhType = Enum.TryParse<RhBloodType>(request.RhType, out RhBloodType r) ? r : RhBloodType.Unknown;

            IBloodUnitGrain unit = _grainFactory.GetGrain<IBloodUnitGrain>(unitId);
            await unit.CreateAsync(productType, aboType, rhType,
                request.CollectionDate, request.ExpirationDate,
                request.SourceFacility, request.DonorId, request.ProductCode,
                request.VolumeML, request.IsIrradiated, request.IsLeukoreduced,
                request.IsWashed, request.IsAntigenNegative, request.AntigenNegativeFor,
                request.Notes);

            IBloodUnitIndexGrain idx = _grainFactory.GetGrain<IBloodUnitIndexGrain>("BB-UNIT-IDX");
            await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
            {
                UnitId             = unitId,
                ProductType        = productType,
                AboType            = aboType,
                RhType             = rhType,
                Status             = BloodUnitStatus.Available,
                ExpirationDate     = request.ExpirationDate,
                IsIrradiated       = request.IsIrradiated,
                IsLeukoreduced     = request.IsLeukoreduced,
                IsAntigenNegative  = request.IsAntigenNegative,
                AntigenNegativeFor = request.AntigenNegativeFor
            });

            _logger.LogInformation("Blood unit {UnitId} added to inventory ({ProductType} {AboType}{RhType})", unitId, request.ProductType, request.AboType, request.RhType);
            return Created($"api/bloodbank/units/{Uri.EscapeDataString(unitId)}",
                new BloodUnitResponse { UnitId = unitId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding blood unit to inventory");
            return StatusCode(500, "An error occurred while adding the blood unit");
        }
    }

    [HttpGet("api/bloodbank/units/{unitId}")]
    [ProducesResponseType(typeof(BloodUnitState), StatusCodes.Status200OK)]
    public async Task<ActionResult<BloodUnitState>> GetBloodUnit(string unitId)
    {
        try
        {
            IBloodUnitGrain unit = _grainFactory.GetGrain<IBloodUnitGrain>(unitId);
            BloodUnitState state = await unit.GetUnitAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blood unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while retrieving the blood unit");
        }
    }

    [HttpPut("api/bloodbank/units/{unitId}/discard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DiscardBloodUnit(string unitId, [FromBody] DiscardUnitRequest request)
    {
        try
        {
            IBloodUnitGrain unit = _grainFactory.GetGrain<IBloodUnitGrain>(unitId);
            await unit.DiscardAsync(request.DisposalReason);

            BloodUnitState state = await unit.GetUnitAsync();
            IBloodUnitIndexGrain idx = _grainFactory.GetGrain<IBloodUnitIndexGrain>("BB-UNIT-IDX");
            await idx.AddOrUpdateAsync(new BloodUnitIndexEntry
            {
                UnitId        = unitId,
                ProductType   = state.ProductType,
                AboType       = state.AboType,
                RhType        = state.RhType,
                Status        = BloodUnitStatus.Discarded,
                ExpirationDate = state.ExpirationDate,
                IsIrradiated  = state.IsIrradiated,
                IsLeukoreduced = state.IsLeukoreduced,
                IsAntigenNegative = state.IsAntigenNegative,
                AntigenNegativeFor = state.AntigenNegativeFor
            });

            _logger.LogInformation("Blood unit {UnitId} discarded: {Reason}", unitId, request.DisposalReason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding blood unit {UnitId}", unitId);
            return StatusCode(500, "An error occurred while discarding the blood unit");
        }
    }
}

// ─── Request / Response DTOs ──────────────────────────────────────────────────

public class UpdateBloodTypeRequest
{
    public string AboType { get; set; } = string.Empty;
    public string RhType { get; set; } = string.Empty;
    public string AntibodyScreenResult { get; set; } = "NotDone";
    public DateTime? AntibodyScreenDate { get; set; }
    public string? DirectAntibodyTest { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? Notes { get; set; }
}

public class RequestCrossmatchRequest
{
    public string UnitId { get; set; } = string.Empty;
    public string Urgency { get; set; } = "Routine";
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class CrossmatchResultRequest
{
    public string Result { get; set; } = string.Empty;
    public string Method { get; set; } = "Electronic";
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string? AntibodyIdentification { get; set; }
}

public class StartTransfusionRequest
{
    public string CrossmatchId { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string AdministeredByUserId { get; set; } = string.Empty;
    public string AdministeredByUserName { get; set; } = string.Empty;
    public string OrderedByUserId { get; set; } = string.Empty;
    public string OrderedByUserName { get; set; } = string.Empty;
    public string? InfusionSite { get; set; }
    public string? PreTransfusionVitals { get; set; }
}

public class CompleteTransfusionRequest
{
    public DateTime? EndDateTime { get; set; }
    public decimal? VolumeML { get; set; }
    public string? PostTransfusionVitals { get; set; }
}

public class StopTransfusionRequest
{
    public DateTime? EndDateTime { get; set; }
    public string StopReason { get; set; } = string.Empty;
    public string ReactionType { get; set; } = "None";
    public string? ReactionNotes { get; set; }
}

public class AddBloodUnitRequest
{
    public string ProductType { get; set; } = string.Empty;
    public string AboType { get; set; } = string.Empty;
    public string RhType { get; set; } = string.Empty;
    public DateTime CollectionDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string? SourceFacility { get; set; }
    public string? DonorId { get; set; }
    public string? ProductCode { get; set; }
    public decimal? VolumeML { get; set; }
    public bool IsIrradiated { get; set; }
    public bool IsLeukoreduced { get; set; }
    public bool IsWashed { get; set; }
    public bool IsAntigenNegative { get; set; }
    public string? AntigenNegativeFor { get; set; }
    public string? Notes { get; set; }
}

public class DiscardUnitRequest
{
    public string DisposalReason { get; set; } = string.Empty;
}

public class CrossmatchResponse
{
    public string CrossmatchId { get; set; } = string.Empty;
}

public class TransfusionResponse
{
    public string TransfusionId { get; set; } = string.Empty;
}

public class BloodUnitResponse
{
    public string UnitId { get; set; } = string.Empty;
}
