// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Pharmacy Point of Sale (POS) API — RPMS ABSP Claims (File #9002313.02)
/// and ABSP INSURER (File #9002313.4).
///
/// Covers NCPDP real-time claim submission (B1), adjudication, reversal (B2),
/// and insurer configuration.
/// MUMPS routines: ABSPOSQ.m, ABSPOSQP.m, ABSPOSI.m.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class PharmacyPosController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PharmacyPosController> _logger;

    public PharmacyPosController(IGrainFactory grainFactory, ILogger<PharmacyPosController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPharmacyPosInsurerIndexGrain InsurerIndex =>
        _grainFactory.GetGrain<IPharmacyPosInsurerIndexGrain>("POS-INSURER-IDX");

    private IPharmacyPosInsurerGrain GetInsurerGrain(string insurerId) =>
        _grainFactory.GetGrain<IPharmacyPosInsurerGrain>($"POS-INSURER:{insurerId}");

    // ── POS Claims (patient-scoped) ─────────────────────────────────────────

    /// <summary>List all POS claims for a patient, optionally filtered by status.</summary>
    [HttpGet("api/patient/{patientId}/pharmacypos/claims")]
    [ProducesResponseType(typeof(List<PosClaimIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PosClaimIndexEntry>>> GetClaims(
        string patientId,
        [FromQuery] PosClaimStatus? status = null)
    {
        try
        {
            List<PosClaimIndexEntry> results = status.HasValue
                ? await GetWorkflow(patientId).GetPosClaimsByStatusAsync(status.Value)
                : await GetWorkflow(patientId).GetPosClaimsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving POS claims for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving POS claims");
        }
    }

    /// <summary>Get the full state of a single POS claim.</summary>
    [HttpGet("api/patient/{patientId}/pharmacypos/claims/{claimId}")]
    [ProducesResponseType(typeof(PharmacyPosClaimState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PharmacyPosClaimState>> GetClaim(string patientId, string claimId)
    {
        try
        {
            PharmacyPosClaimState state = await GetWorkflow(patientId).GetPosClaimAsync(claimId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"POS claim '{claimId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving POS claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred while retrieving the POS claim");
        }
    }

    /// <summary>Submit a POS claim (B1 billing, B2 reversal, E1 eligibility).</summary>
    [HttpPost("api/patient/{patientId}/pharmacypos/claims")]
    [ProducesResponseType(typeof(PosResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitClaim(
        string patientId,
        [FromBody] SubmitPosClaimRequest request)
    {
        try
        {
            string claimId = await GetWorkflow(patientId).SubmitPosClaimAsync(
                request.PrescriptionId,
                request.TransactionType,
                request.Bin, request.Pcn, request.NcpdpVersion,
                request.GroupNumber, request.CardholderId, request.RelationshipCode,
                request.InsurerId, request.InsurerName,
                request.Ndc, request.DrugName, request.QuantityDispensed, request.DaysSupply,
                request.DateOfService,
                request.IngredientCostSubmitted, request.DispensingFeeSubmitted,
                request.UsualAndCustomary,
                request.PharmacyNcpdpId, request.PharmacistName,
                request.PrescriberNpi, request.PrescriberName,
                request.OriginalClaimId);

            _logger.LogInformation(
                "Submitted POS claim {ClaimId} ({TransactionType}) for patient {PatientId}",
                claimId, request.TransactionType, patientId);

            return Created(
                $"api/patient/{patientId}/pharmacypos/claims/{claimId}",
                new PosResponse { Id = claimId, Message = "POS claim submitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting POS claim for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while submitting the POS claim");
        }
    }

    /// <summary>Record adjudication response on a POS claim.</summary>
    [HttpPost("api/patient/{patientId}/pharmacypos/claims/{claimId}/adjudicate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjudicateClaim(
        string patientId, string claimId,
        [FromBody] AdjudicatePosClaimRequest request)
    {
        try
        {
            await GetWorkflow(patientId).AdjudicatePosClaimAsync(
                claimId,
                request.Status,
                request.InsurancePaidAmount, request.PatientResponsibility,
                request.CopayAmount, request.CoinsuranceAmount, request.DeductibleAmount,
                request.AuthorizationNumber,
                request.Rejections, request.DurMessages);

            _logger.LogInformation(
                "Adjudicated POS claim {ClaimId} → {Status} for patient {PatientId}",
                claimId, request.Status, patientId);

            return Ok(new { ClaimId = claimId, Message = "Claim adjudicated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjudicating POS claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred while adjudicating the POS claim");
        }
    }

    /// <summary>Reverse a POS claim (B2 reversal).</summary>
    [HttpPost("api/patient/{patientId}/pharmacypos/claims/{claimId}/reverse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReverseClaim(string patientId, string claimId)
    {
        try
        {
            await GetWorkflow(patientId).ReversePosClaimAsync(claimId);

            _logger.LogInformation(
                "Reversed POS claim {ClaimId} for patient {PatientId}",
                claimId, patientId);

            return Ok(new { ClaimId = claimId, Message = "Claim reversed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reversing POS claim {ClaimId}", claimId);
            return StatusCode(500, "An error occurred while reversing the POS claim");
        }
    }

    // ── Insurers (system-level) ─────────────────────────────────────────────

    /// <summary>List all POS insurers.</summary>
    [HttpGet("api/pharmacypos/insurers")]
    [ProducesResponseType(typeof(List<PosInsurerIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PosInsurerIndexEntry>>> GetInsurers()
    {
        try
        {
            List<PosInsurerIndexEntry> results = await InsurerIndex.GetAllAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving POS insurers");
            return StatusCode(500, "An error occurred while retrieving POS insurers");
        }
    }

    /// <summary>List active POS insurers only.</summary>
    [HttpGet("api/pharmacypos/insurers/active")]
    [ProducesResponseType(typeof(List<PosInsurerIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PosInsurerIndexEntry>>> GetActiveInsurers()
    {
        try
        {
            List<PosInsurerIndexEntry> results = await InsurerIndex.GetActiveAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active POS insurers");
            return StatusCode(500, "An error occurred while retrieving active POS insurers");
        }
    }

    /// <summary>Get a single POS insurer by ID.</summary>
    [HttpGet("api/pharmacypos/insurers/{insurerId}")]
    [ProducesResponseType(typeof(PharmacyPosInsurerState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PharmacyPosInsurerState>> GetInsurer(string insurerId)
    {
        try
        {
            PharmacyPosInsurerState state = await GetInsurerGrain(insurerId).GetAsync();
            if (string.IsNullOrEmpty(state.InsurerId))
                return NotFound(new { Message = $"Insurer '{insurerId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving POS insurer {InsurerId}", insurerId);
            return StatusCode(500, "An error occurred while retrieving the POS insurer");
        }
    }

    /// <summary>Create or update a POS insurer configuration.</summary>
    [HttpPost("api/pharmacypos/insurers")]
    [ProducesResponseType(typeof(PosResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> SaveInsurer([FromBody] SavePosInsurerRequest request)
    {
        try
        {
            IPharmacyPosInsurerGrain grain = GetInsurerGrain(request.InsurerId);
            await grain.SaveAsync(
                request.InsurerName, request.Bin, request.Pcn, request.NcpdpVersion,
                request.PharmacyNcpdpId, request.ServiceProviderIdQualifier,
                request.PlanName, request.HelpDeskPhone, request.IsActive);

            PosInsurerIndexEntry indexEntry = new()
            {
                InsurerId = request.InsurerId,
                InsurerName = request.InsurerName,
                Bin = request.Bin,
                Pcn = request.Pcn,
                IsActive = request.IsActive,
            };
            await InsurerIndex.UpsertAsync(indexEntry);

            _logger.LogInformation(
                "Saved POS insurer {InsurerId} ({InsurerName})",
                request.InsurerId, request.InsurerName);

            return Created(
                $"api/pharmacypos/insurers/{request.InsurerId}",
                new PosResponse { Id = request.InsurerId, Message = "Insurer saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving POS insurer {InsurerId}", request.InsurerId);
            return StatusCode(500, "An error occurred while saving the POS insurer");
        }
    }

    /// <summary>Deactivate a POS insurer.</summary>
    [HttpPost("api/pharmacypos/insurers/{insurerId}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateInsurer(string insurerId)
    {
        try
        {
            await GetInsurerGrain(insurerId).DeactivateAsync();

            PosInsurerIndexEntry indexEntry = new()
            {
                InsurerId = insurerId,
                InsurerName = string.Empty,
                Bin = string.Empty,
                Pcn = string.Empty,
                IsActive = false,
            };
            await InsurerIndex.UpsertAsync(indexEntry);

            _logger.LogInformation("Deactivated POS insurer {InsurerId}", insurerId);

            return Ok(new { InsurerId = insurerId, Message = "Insurer deactivated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating POS insurer {InsurerId}", insurerId);
            return StatusCode(500, "An error occurred while deactivating the POS insurer");
        }
    }
}

// ── Request / Response DTOs ─────────────────────────────────────────────────

public record SubmitPosClaimRequest
{
    public string? PrescriptionId { get; init; }
    public NcpdpTransactionType TransactionType { get; init; }
    public string Bin { get; init; } = string.Empty;
    public string Pcn { get; init; } = string.Empty;
    public string NcpdpVersion { get; init; } = string.Empty;
    public string? GroupNumber { get; init; }
    public string? CardholderId { get; init; }
    public string? RelationshipCode { get; init; }
    public string? InsurerId { get; init; }
    public string? InsurerName { get; init; }
    public string? Ndc { get; init; }
    public string? DrugName { get; init; }
    public decimal? QuantityDispensed { get; init; }
    public int? DaysSupply { get; init; }
    public DateTime? DateOfService { get; init; }
    public decimal? IngredientCostSubmitted { get; init; }
    public decimal? DispensingFeeSubmitted { get; init; }
    public decimal? UsualAndCustomary { get; init; }
    public string? PharmacyNcpdpId { get; init; }
    public string? PharmacistName { get; init; }
    public string? PrescriberNpi { get; init; }
    public string? PrescriberName { get; init; }
    public string? OriginalClaimId { get; init; }
}

public record AdjudicatePosClaimRequest
{
    public PosClaimStatus Status { get; init; }
    public decimal? InsurancePaidAmount { get; init; }
    public decimal? PatientResponsibility { get; init; }
    public decimal? CopayAmount { get; init; }
    public decimal? CoinsuranceAmount { get; init; }
    public decimal? DeductibleAmount { get; init; }
    public string? AuthorizationNumber { get; init; }
    public List<PosRejection>? Rejections { get; init; }
    public List<DurMessage>? DurMessages { get; init; }
}

public record SavePosInsurerRequest
{
    public string InsurerId { get; init; } = string.Empty;
    public string InsurerName { get; init; } = string.Empty;
    public string Bin { get; init; } = string.Empty;
    public string Pcn { get; init; } = string.Empty;
    public string NcpdpVersion { get; init; } = "D0";
    public string? PharmacyNcpdpId { get; init; }
    public string? ServiceProviderIdQualifier { get; init; }
    public string? PlanName { get; init; }
    public string? HelpDeskPhone { get; init; }
    public bool IsActive { get; init; } = true;
}

public class PosResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
