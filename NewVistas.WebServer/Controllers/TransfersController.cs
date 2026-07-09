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
/// Inter-facility Transfer Center — request → accept(with reserved bed) → complete,
/// or decline/cancel. The receiving facility controls its own beds. All actions
/// delegate to the patient's workflow grain (key enforcement: DG BED CONTROL at
/// the grain boundary).
/// </summary>
[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(IGrainFactory grainFactory, ILogger<TransfersController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Submit a new transfer request (sending side).</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateTransferRequest r)
    {
        try
        {
            string transferId = await W(r.PatientId).RequestInterfacilityTransferAsync(
                r.SendingInstitutionId, r.SendingUnitId, r.SendingAdmissionId,
                r.SendingAttendingId, r.SendingAttendingName,
                r.ReceivingInstitutionId,
                r.RequestedLevelOfCare, r.RequestedBedType, r.IsolationRequired,
                r.Urgency ?? "ROUTINE", r.ClinicalSummary, r.ReasonForTransfer);
            return Created($"api/transfers/{transferId}", new { TransferId = transferId });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transfer request for {PatientId}", r.PatientId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Full transfer request state (timeline included).</summary>
    [HttpGet("{transferId}")]
    public async Task<ActionResult> Get(string transferId, [FromQuery] string patientId)
    {
        try
        {
            TransferRequestState state = await W(patientId).GetInterfacilityTransferAsync(transferId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound($"Transfer '{transferId}' not found.");
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading transfer {TransferId}", transferId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Incoming queue for an institution's transfer center.</summary>
    [HttpGet("center/{institutionId}/incoming")]
    public async Task<ActionResult> Incoming(string institutionId, [FromQuery] bool activeOnly = true)
    {
        try
        {
            ITransferCenterGrain center = _grainFactory.GetGrain<ITransferCenterGrain>($"TRANSFER-CENTER:{institutionId}");
            return Ok(await center.GetIncomingAsync(activeOnly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading incoming transfers for {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Outgoing queue for an institution's transfer center.</summary>
    [HttpGet("center/{institutionId}/outgoing")]
    public async Task<ActionResult> Outgoing(string institutionId, [FromQuery] bool activeOnly = true)
    {
        try
        {
            ITransferCenterGrain center = _grainFactory.GetGrain<ITransferCenterGrain>($"TRANSFER-CENTER:{institutionId}");
            return Ok(await center.GetOutgoingAsync(activeOnly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading outgoing transfers for {InstitutionId}", institutionId);
            return StatusCode(500, "An error occurred.");
        }
    }

    /// <summary>Accept (receiving side): reserves the named bed, then flips to ACCEPTED.</summary>
    [HttpPost("{transferId}/accept")]
    public Task<ActionResult> Accept(string transferId, [FromBody] AcceptTransferRequest r)
        => Act(transferId, r.PatientId, "accept",
            w => w.AcceptInterfacilityTransferAsync(transferId, r.ActingInstitutionId, r.UnitId, r.BedId));

    /// <summary>Move the reservation when the accepted bed became unavailable.</summary>
    [HttpPost("{transferId}/reassign-bed")]
    public Task<ActionResult> ReassignBed(string transferId, [FromBody] ReassignTransferBedRequest r)
        => Act(transferId, r.PatientId, "reassign-bed",
            w => w.ReassignTransferBedAsync(transferId, r.NewUnitId, r.NewBedId));

    [HttpPost("{transferId}/decline")]
    public Task<ActionResult> Decline(string transferId, [FromBody] DeclineTransferRequest r)
        => Act(transferId, r.PatientId, "decline",
            w => w.DeclineInterfacilityTransferAsync(transferId, r.ActingInstitutionId, r.Reason));

    [HttpPost("{transferId}/cancel")]
    public Task<ActionResult> Cancel(string transferId, [FromBody] CancelTransferRequest r)
        => Act(transferId, r.PatientId, "cancel",
            w => w.CancelInterfacilityTransferAsync(transferId, r.Reason));

    /// <summary>Patient arrived — admission at receiver + discharge at sender.</summary>
    [HttpPost("{transferId}/complete")]
    public async Task<ActionResult> Complete(string transferId, [FromBody] CompleteTransferRequest r)
    {
        try
        {
            string admissionId = await W(r.PatientId).CompleteInterfacilityTransferAsync(
                transferId, r.ArrivalDateTime, r.ReceivingAttendingId, r.ReceivingAttendingName, r.AdmissionDiagnosis);
            return Ok(new { AdmissionMovementId = admissionId });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing transfer {TransferId}", transferId);
            return StatusCode(500, "An error occurred.");
        }
    }

    private async Task<ActionResult> Act(string transferId, string patientId, string action,
        Func<IPatientWorkflowGrain, Task> operation)
    {
        try
        {
            await operation(W(patientId));
            return Ok(new { Message = $"Transfer {transferId}: {action} succeeded." });
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error on transfer {TransferId} action {Action}", transferId, action);
            return StatusCode(500, "An error occurred.");
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record CreateTransferRequest
{
    public required string PatientId { get; init; }
    public required string SendingInstitutionId { get; init; }
    public string? SendingUnitId { get; init; }
    public string? SendingAdmissionId { get; init; }
    public string? SendingAttendingId { get; init; }
    public string? SendingAttendingName { get; init; }
    public required string ReceivingInstitutionId { get; init; }
    public string? RequestedLevelOfCare { get; init; }
    public BedType? RequestedBedType { get; init; }
    public BedIsolationType IsolationRequired { get; init; } = BedIsolationType.None;
    public string? Urgency { get; init; }
    public string? ClinicalSummary { get; init; }
    public string? ReasonForTransfer { get; init; }
}

public record AcceptTransferRequest
{
    public required string PatientId { get; init; }
    public required string ActingInstitutionId { get; init; }
    public required string UnitId { get; init; }
    public required string BedId { get; init; }
}

public record ReassignTransferBedRequest
{
    public required string PatientId { get; init; }
    public required string NewUnitId { get; init; }
    public required string NewBedId { get; init; }
}

public record DeclineTransferRequest
{
    public required string PatientId { get; init; }
    public required string ActingInstitutionId { get; init; }
    public required string Reason { get; init; }
}

public record CancelTransferRequest
{
    public required string PatientId { get; init; }
    public string? Reason { get; init; }
}

public record CompleteTransferRequest
{
    public required string PatientId { get; init; }
    public DateTime ArrivalDateTime { get; init; } = DateTime.UtcNow;
    public string? ReceivingAttendingId { get; init; }
    public string? ReceivingAttendingName { get; init; }
    public string? AdmissionDiagnosis { get; init; }
}
