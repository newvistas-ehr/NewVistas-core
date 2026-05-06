// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/ediclaims")]
[Produces("application/json")]
public class EdiController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EdiController> _logger;

    public EdiController(IGrainFactory grainFactory, ILogger<EdiController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain W(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Claims ──────────────────────────────────────────────────────────────────

    [HttpPost("claims")]
    public async Task<IActionResult> PrepareClaim([FromBody] PrepareClaimRequest req)
    {
        try
        {
            string claimId = $"EDI-CLAIM:{Guid.NewGuid()}";
            IEdiClaimGrain claim = _grainFactory.GetGrain<IEdiClaimGrain>(claimId);

            // Map DTO service lines to grain state records
            List<EdiServiceLine> serviceLines = req.ServiceLines.Select(sl => new EdiServiceLine
            {
                LineNumber       = sl.LineNumber,
                ProcedureCode    = sl.ProcedureCode,
                ProcedureModifier = sl.ProcedureModifier,
                DiagnosisPointer = sl.DiagnosisPointer,
                BilledAmount     = sl.BilledAmount,
                Units            = sl.Units,
            }).ToList();

            if (!Enum.TryParse(req.ClaimType, out EdiClaimType claimType))
                claimType = EdiClaimType.Professional837P;

            await claim.PrepareAsync(
                req.PatientId,
                req.BillingActionId,
                req.ARAccountId,
                req.InsurancePlanId,
                req.PolicyId,
                req.PayerId,
                req.PayerName,
                claimType,
                req.TotalBilledAmount,
                req.DiagnosisCodes,
                serviceLines,
                req.ServiceFromDate,
                req.ServiceToDate,
                req.Notes);

            // Update per-patient claim index
            IEdiClaimIndexGrain idx = _grainFactory.GetGrain<IEdiClaimIndexGrain>($"EDI-CLAIM-IDX:{req.PatientId}");
            await idx.AddOrUpdateAsync(new EdiClaimIndexEntry
            {
                ClaimId          = claimId,
                PatientId        = req.PatientId,
                PayerName        = req.PayerName,
                ClaimType        = req.ClaimType,
                Status           = EdiClaimStatus.Draft.ToString(),
                TotalBilledAmount = req.TotalBilledAmount,
                PaidAmount       = null,
                ServiceFromDate  = req.ServiceFromDate,
            });

            _logger.LogInformation("EDI claim {ClaimId} prepared for patient {PatientId}", claimId, req.PatientId);
            return Created($"/api/ediclaims/claims/{Uri.EscapeDataString(claimId)}", new { claimId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing EDI claim for patient {PatientId}", req.PatientId);
            return StatusCode(500, "Error preparing EDI claim.");
        }
    }

    [HttpGet("claims/{claimId}")]
    public async Task<IActionResult> GetClaim(string claimId)
    {
        try
        {
            IEdiClaimGrain claim = _grainFactory.GetGrain<IEdiClaimGrain>(claimId);
            EdiClaimState state = await claim.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDI claim {ClaimId}", claimId);
            return StatusCode(500, "Error retrieving EDI claim.");
        }
    }

    [HttpGet("patients/{patientId}/claims")]
    public async Task<IActionResult> GetPatientClaims(string patientId)
    {
        try
        {
            List<EdiClaimIndexEntry> claims = await W(patientId).GetEdiClaimsAsync();
            return Ok(claims);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDI claims for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving EDI claims.");
        }
    }

    // ─── Transmissions ───────────────────────────────────────────────────────────

    [HttpPost("transmissions")]
    public async Task<IActionResult> OpenTransmission([FromBody] OpenTransmissionRequest req)
    {
        try
        {
            string txId = $"EDI-TX:{Guid.NewGuid()}";
            string batchNumber = $"TX-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            IEdiTransmissionGrain tx = _grainFactory.GetGrain<IEdiTransmissionGrain>(txId);
            await tx.OpenAsync(batchNumber, req.PayerId, req.PayerName, req.Notes);

            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            await txIdx.AddOrUpdateAsync(new EdiTransmissionIndexEntry
            {
                TransmissionId   = txId,
                BatchNumber      = batchNumber,
                PayerName        = req.PayerName,
                Status           = EdiTransmissionStatus.Open.ToString(),
                TotalClaims      = 0,
                TotalBilledAmount = 0m,
                SentDate         = null,
            });

            _logger.LogInformation("EDI transmission {TxId} opened for payer {PayerName}", txId, req.PayerName);
            return Created($"/api/ediclaims/transmissions/{Uri.EscapeDataString(txId)}", new { txId, batchNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening EDI transmission for payer {PayerName}", req.PayerName);
            return StatusCode(500, "Error opening transmission.");
        }
    }

    [HttpGet("transmissions")]
    public async Task<IActionResult> GetAllTransmissions()
    {
        try
        {
            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            List<EdiTransmissionIndexEntry> entries = await txIdx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDI transmissions");
            return StatusCode(500, "Error retrieving transmissions.");
        }
    }

    [HttpGet("transmissions/open")]
    public async Task<IActionResult> GetOpenTransmissions()
    {
        try
        {
            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            List<EdiTransmissionIndexEntry> entries = await txIdx.GetOpenAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving open EDI transmissions");
            return StatusCode(500, "Error retrieving open transmissions.");
        }
    }

    [HttpGet("transmissions/{txId}")]
    public async Task<IActionResult> GetTransmission(string txId)
    {
        try
        {
            IEdiTransmissionGrain tx = _grainFactory.GetGrain<IEdiTransmissionGrain>(txId);
            EdiTransmissionState state = await tx.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EDI transmission {TxId}", txId);
            return StatusCode(500, "Error retrieving transmission.");
        }
    }

    [HttpPost("transmissions/{txId}/add-claim")]
    public async Task<IActionResult> AddClaimToTransmission(string txId, [FromBody] AddClaimToTransmissionRequest req)
    {
        try
        {
            IEdiTransmissionGrain tx = _grainFactory.GetGrain<IEdiTransmissionGrain>(txId);
            await tx.AddClaimAsync(req.ClaimId, req.PatientId, req.TotalBilledAmount, req.ClaimType);

            // Update the claim's status to InTransmission
            IEdiClaimGrain claim = _grainFactory.GetGrain<IEdiClaimGrain>(req.ClaimId);
            await claim.AddToTransmissionAsync(txId);

            // Update per-patient claim index entry
            IEdiClaimIndexGrain claimIdx = _grainFactory.GetGrain<IEdiClaimIndexGrain>($"EDI-CLAIM-IDX:{req.PatientId}");
            EdiClaimState claimState = await claim.GetAsync();
            await claimIdx.AddOrUpdateAsync(new EdiClaimIndexEntry
            {
                ClaimId           = req.ClaimId,
                PatientId         = req.PatientId,
                PayerName         = claimState.PayerName,
                ClaimType         = claimState.ClaimType.ToString(),
                Status            = EdiClaimStatus.InTransmission.ToString(),
                TotalBilledAmount = claimState.TotalBilledAmount,
                PaidAmount        = claimState.PaidAmount,
                ServiceFromDate   = claimState.ServiceFromDate,
            });

            // Update transmission index
            EdiTransmissionState txState = await tx.GetAsync();
            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            await txIdx.AddOrUpdateAsync(new EdiTransmissionIndexEntry
            {
                TransmissionId    = txId,
                BatchNumber       = txState.BatchNumber,
                PayerName         = txState.PayerName,
                Status            = txState.Status.ToString(),
                TotalClaims       = txState.TotalClaims,
                TotalBilledAmount = txState.TotalBilledAmount,
                SentDate          = txState.SentDate,
            });

            return Ok(new { txId, claimId = req.ClaimId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding claim {ClaimId} to transmission {TxId}", req.ClaimId, txId);
            return StatusCode(500, "Error adding claim to transmission.");
        }
    }

    [HttpPost("transmissions/{txId}/send")]
    public async Task<IActionResult> SendTransmission(string txId, [FromBody] SendTransmissionRequest req)
    {
        try
        {
            IEdiTransmissionGrain tx = _grainFactory.GetGrain<IEdiTransmissionGrain>(txId);
            EdiTransmissionState txState = await tx.GetAsync();

            await tx.SendAsync(req.SentDate);

            // Mark each claim in the batch as Transmitted
            foreach (string claimId in txState.ClaimIds)
            {
                IEdiClaimGrain claim = _grainFactory.GetGrain<IEdiClaimGrain>(claimId);
                await claim.MarkTransmittedAsync(req.SentDate);
            }

            // Update transmission index
            EdiTransmissionState updatedTx = await tx.GetAsync();
            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            await txIdx.AddOrUpdateAsync(new EdiTransmissionIndexEntry
            {
                TransmissionId    = txId,
                BatchNumber       = updatedTx.BatchNumber,
                PayerName         = updatedTx.PayerName,
                Status            = updatedTx.Status.ToString(),
                TotalClaims       = updatedTx.TotalClaims,
                TotalBilledAmount = updatedTx.TotalBilledAmount,
                SentDate          = updatedTx.SentDate,
            });

            _logger.LogInformation("EDI transmission {TxId} sent with {Count} claims", txId, txState.ClaimIds.Count);
            return Ok(new { txId, claimsSent = txState.ClaimIds.Count, sentDate = req.SentDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending EDI transmission {TxId}", txId);
            return StatusCode(500, "Error sending transmission.");
        }
    }

    [HttpPost("transmissions/{txId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeTransmission(string txId, [FromBody] AcknowledgeTransmissionRequest req)
    {
        try
        {
            IEdiTransmissionGrain tx = _grainFactory.GetGrain<IEdiTransmissionGrain>(txId);
            await tx.RecordAcknowledgmentAsync(req.AcknowledgmentCode, req.AcceptedCount, req.RejectedCount, req.AcknowledgedDate);

            EdiTransmissionState txState = await tx.GetAsync();
            IEdiTransmissionIndexGrain txIdx = _grainFactory.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            await txIdx.AddOrUpdateAsync(new EdiTransmissionIndexEntry
            {
                TransmissionId    = txId,
                BatchNumber       = txState.BatchNumber,
                PayerName         = txState.PayerName,
                Status            = txState.Status.ToString(),
                TotalClaims       = txState.TotalClaims,
                TotalBilledAmount = txState.TotalBilledAmount,
                SentDate          = txState.SentDate,
            });

            return Ok(new { txId, status = txState.Status.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging EDI transmission {TxId}", txId);
            return StatusCode(500, "Error acknowledging transmission.");
        }
    }

    // ─── ERAs ────────────────────────────────────────────────────────────────────

    [HttpPost("eras")]
    public async Task<IActionResult> RecordEra([FromBody] RecordEraRequest req)
    {
        try
        {
            string eraId = $"ERA:{Guid.NewGuid()}";
            IEraGrain era = _grainFactory.GetGrain<IEraGrain>(eraId);

            List<EraClaimPayment> claimPayments = req.ClaimPayments.Select(cp => new EraClaimPayment
            {
                ClaimId               = cp.ClaimId,
                PatientId             = cp.PatientId,
                ARAccountId           = cp.ARAccountId,
                PaidAmount            = cp.PaidAmount,
                AllowedAmount         = cp.AllowedAmount,
                AdjustmentAmount      = cp.AdjustmentAmount,
                DenialReasonCode      = cp.DenialReasonCode,
                DenialReasonDescription = cp.DenialReasonDescription,
            }).ToList();

            await era.RecordAsync(
                req.PayerId,
                req.PayerName,
                req.CheckNumber,
                req.PaymentMethod,
                req.PaymentDate,
                req.TotalPaymentAmount,
                req.TransactionSetId,
                claimPayments,
                req.Notes);

            IEraIndexGrain eraIdx = _grainFactory.GetGrain<IEraIndexGrain>("ERA-IDX");
            await eraIdx.AddOrUpdateAsync(new EraIndexEntry
            {
                EraId              = eraId,
                PayerName          = req.PayerName,
                PaymentDate        = req.PaymentDate,
                TotalPaymentAmount = req.TotalPaymentAmount,
                Status             = EraStatus.Received.ToString(),
                ClaimCount         = req.ClaimPayments.Count,
                CheckNumber        = req.CheckNumber,
            });

            _logger.LogInformation("ERA {EraId} recorded for payer {PayerName} with {Count} claims", eraId, req.PayerName, req.ClaimPayments.Count);
            return Created($"/api/ediclaims/eras/{Uri.EscapeDataString(eraId)}", new { eraId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording ERA for payer {PayerName}", req.PayerName);
            return StatusCode(500, "Error recording ERA.");
        }
    }

    [HttpGet("eras")]
    public async Task<IActionResult> GetAllEras()
    {
        try
        {
            IEraIndexGrain eraIdx = _grainFactory.GetGrain<IEraIndexGrain>("ERA-IDX");
            List<EraIndexEntry> entries = await eraIdx.GetAllAsync();
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ERAs");
            return StatusCode(500, "Error retrieving ERAs.");
        }
    }

    [HttpGet("eras/{eraId}")]
    public async Task<IActionResult> GetEra(string eraId)
    {
        try
        {
            IEraGrain era = _grainFactory.GetGrain<IEraGrain>(eraId);
            EraState state = await era.GetAsync();
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ERA {EraId}", eraId);
            return StatusCode(500, "Error retrieving ERA.");
        }
    }

    [HttpPost("eras/{eraId}/process")]
    public async Task<IActionResult> ProcessEra(string eraId)
    {
        try
        {
            IEraGrain era = _grainFactory.GetGrain<IEraGrain>(eraId);
            await era.ProcessAsync();

            EraState state = await era.GetAsync();

            // Update ERA index with new status
            IEraIndexGrain eraIdx = _grainFactory.GetGrain<IEraIndexGrain>("ERA-IDX");
            await eraIdx.AddOrUpdateAsync(new EraIndexEntry
            {
                EraId              = eraId,
                PayerName          = state.PayerName,
                PaymentDate        = state.PaymentDate,
                TotalPaymentAmount = state.TotalPaymentAmount,
                Status             = state.Status.ToString(),
                ClaimCount         = state.ClaimPayments.Count,
                CheckNumber        = state.CheckNumber,
            });

            _logger.LogInformation("ERA {EraId} processed — status: {Status}", eraId, state.Status);
            return Ok(new { eraId, status = state.Status.ToString(), errorMessage = state.ErrorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ERA {EraId}", eraId);
            return StatusCode(500, "Error processing ERA.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record PrepareClaimRequest(
    string PatientId,
    string? BillingActionId,
    string? ARAccountId,
    string? InsurancePlanId,
    string? PolicyId,
    string PayerId,
    string PayerName,
    string ClaimType,
    decimal TotalBilledAmount,
    List<string> DiagnosisCodes,
    List<EdiServiceLineDto> ServiceLines,
    DateTime ServiceFromDate,
    DateTime? ServiceToDate,
    string? Notes);

public record EdiServiceLineDto(
    int LineNumber,
    string ProcedureCode,
    string? ProcedureModifier,
    string? DiagnosisPointer,
    decimal BilledAmount,
    decimal Units);

public record OpenTransmissionRequest(
    string PayerId,
    string PayerName,
    string? Notes);

public record AddClaimToTransmissionRequest(
    string ClaimId,
    string PatientId,
    decimal TotalBilledAmount,
    string ClaimType);

public record SendTransmissionRequest(DateTime SentDate);

public record AcknowledgeTransmissionRequest(
    string AcknowledgmentCode,
    int AcceptedCount,
    int RejectedCount,
    DateTime AcknowledgedDate);

public record RecordEraRequest(
    string PayerId,
    string PayerName,
    string? CheckNumber,
    string? PaymentMethod,
    DateTime PaymentDate,
    decimal TotalPaymentAmount,
    string? TransactionSetId,
    List<EraClaimPaymentDto> ClaimPayments,
    string? Notes);

public record EraClaimPaymentDto(
    string ClaimId,
    string PatientId,
    string? ARAccountId,
    decimal PaidAmount,
    decimal? AllowedAmount,
    decimal? AdjustmentAmount,
    string? DenialReasonCode,
    string? DenialReasonDescription);
