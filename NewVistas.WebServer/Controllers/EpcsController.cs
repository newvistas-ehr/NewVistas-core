// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Electronic Prescribing of Controlled Substances (EPCS) API — 21 CFR Part 1311.
///
/// Covers DEA-compliant e-prescriptions with digital signature, 2FA verification,
/// provider credential management, and audit trail.
/// MUMPS routines: ORWDXEPCS.m, PSODGEPCS.m.
/// </summary>
[Authorize]
[ApiController]
[Produces("application/json")]
public class EpcsController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<EpcsController> _logger;

    public EpcsController(IGrainFactory grainFactory, ILogger<EpcsController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IEpcsProviderIndexGrain ProviderIndex =>
        _grainFactory.GetGrain<IEpcsProviderIndexGrain>("EPCS-PROVIDER-IDX");

    private IEpcsProviderCredentialGrain GetProviderGrain(string credentialId) =>
        _grainFactory.GetGrain<IEpcsProviderCredentialGrain>($"EPCS-PROVIDER:{credentialId}");

    // ── E-Prescriptions (patient-scoped) ────────────────────────────────────

    /// <summary>List all EPCS prescriptions for a patient, optionally filtered by status.</summary>
    [HttpGet("api/patient/{patientId}/epcs/prescriptions")]
    [ProducesResponseType(typeof(List<EpcsPrescriptionIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EpcsPrescriptionIndexEntry>>> GetPrescriptions(
        string patientId,
        [FromQuery] EpcsTransmissionStatus? status = null)
    {
        try
        {
            List<EpcsPrescriptionIndexEntry> results = status.HasValue
                ? await GetWorkflow(patientId).GetEpcsPrescriptionsByStatusAsync(status.Value)
                : await GetWorkflow(patientId).GetEpcsPrescriptionsAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EPCS prescriptions for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while retrieving EPCS prescriptions");
        }
    }

    /// <summary>Get the full state of a single EPCS e-prescription.</summary>
    [HttpGet("api/patient/{patientId}/epcs/prescriptions/{epcsId}")]
    [ProducesResponseType(typeof(EpcsPrescriptionState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EpcsPrescriptionState>> GetPrescription(string patientId, string epcsId)
    {
        try
        {
            EpcsPrescriptionState state = await GetWorkflow(patientId).GetEpcsPrescriptionAsync(epcsId);
            if (string.IsNullOrEmpty(state.PatientId))
                return NotFound(new { Message = $"EPCS prescription '{epcsId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EPCS prescription {EpcsId}", epcsId);
            return StatusCode(500, "An error occurred while retrieving the EPCS prescription");
        }
    }

    /// <summary>Create an EPCS e-prescription for a controlled substance.</summary>
    [HttpPost("api/patient/{patientId}/epcs/prescriptions")]
    [ProducesResponseType(typeof(EpcsResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePrescription(
        string patientId,
        [FromBody] CreateEpcsPrescriptionRequest request)
    {
        try
        {
            string epcsId = await GetWorkflow(patientId).CreateEpcsPrescriptionAsync(
                request.PrescriptionId,
                request.TransactionType,
                request.DrugName, request.Ndc, request.DeaSchedule,
                request.Quantity, request.DaysSupply, request.RefillsAuthorized,
                request.Sig, request.DiagnosisCode,
                request.PrescriberNpi, request.PrescriberDea, request.PrescriberName,
                request.PrescriberCredentialId,
                request.DestinationPharmacy);

            _logger.LogInformation(
                "Created EPCS prescription {EpcsId} ({TransactionType}) for patient {PatientId}",
                epcsId, request.TransactionType, patientId);

            return Created(
                $"api/patient/{patientId}/epcs/prescriptions/{epcsId}",
                new EpcsResponse { Id = epcsId, Message = "EPCS prescription created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EPCS prescription for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred while creating the EPCS prescription");
        }
    }

    /// <summary>Sign an EPCS e-prescription with 2FA verification — 21 CFR Part 1311.120.</summary>
    [HttpPost("api/patient/{patientId}/epcs/prescriptions/{epcsId}/sign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SignPrescription(
        string patientId, string epcsId,
        [FromBody] SignEpcsRequest request)
    {
        try
        {
            EpcsSignatureRecord signature = new()
            {
                PrescriptionHash = request.PrescriptionHash,
                CertificateThumbprint = request.CertificateThumbprint,
                SigningTime = request.SigningTime,
                TwoFactorMethod = request.TwoFactorMethod,
                TwoFactorVerificationTime = request.TwoFactorVerificationTime,
                IsValid = request.IsValid,
            };

            await GetWorkflow(patientId).SignEpcsPrescriptionAsync(epcsId, signature);

            _logger.LogInformation(
                "Signed EPCS prescription {EpcsId} with 2FA ({TwoFactorMethod}) for patient {PatientId}",
                epcsId, request.TwoFactorMethod, patientId);

            return Ok(new { EpcsId = epcsId, Message = "Prescription signed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing EPCS prescription {EpcsId}", epcsId);
            return StatusCode(500, "An error occurred while signing the EPCS prescription");
        }
    }

    /// <summary>Transmit an EPCS e-prescription to Surescripts/pharmacy network.</summary>
    [HttpPost("api/patient/{patientId}/epcs/prescriptions/{epcsId}/transmit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TransmitPrescription(
        string patientId, string epcsId,
        [FromBody] TransmitEpcsRequest request)
    {
        try
        {
            await GetWorkflow(patientId).TransmitEpcsPrescriptionAsync(epcsId, request.TransmissionMessageId);

            _logger.LogInformation(
                "Transmitted EPCS prescription {EpcsId} for patient {PatientId}",
                epcsId, patientId);

            return Ok(new { EpcsId = epcsId, Message = "Prescription transmitted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transmitting EPCS prescription {EpcsId}", epcsId);
            return StatusCode(500, "An error occurred while transmitting the EPCS prescription");
        }
    }

    /// <summary>Acknowledge receipt of an EPCS e-prescription by the pharmacy.</summary>
    [HttpPost("api/patient/{patientId}/epcs/prescriptions/{epcsId}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcknowledgePrescription(string patientId, string epcsId)
    {
        try
        {
            await GetWorkflow(patientId).AcknowledgeEpcsPrescriptionAsync(epcsId);

            _logger.LogInformation(
                "Acknowledged EPCS prescription {EpcsId} for patient {PatientId}",
                epcsId, patientId);

            return Ok(new { EpcsId = epcsId, Message = "Prescription acknowledged successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging EPCS prescription {EpcsId}", epcsId);
            return StatusCode(500, "An error occurred while acknowledging the EPCS prescription");
        }
    }

    /// <summary>Cancel an EPCS e-prescription.</summary>
    [HttpPost("api/patient/{patientId}/epcs/prescriptions/{epcsId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelPrescription(
        string patientId, string epcsId,
        [FromBody] CancelEpcsRequest request)
    {
        try
        {
            await GetWorkflow(patientId).CancelEpcsPrescriptionAsync(epcsId, request.UserId, request.Reason);

            _logger.LogInformation(
                "Cancelled EPCS prescription {EpcsId} for patient {PatientId}",
                epcsId, patientId);

            return Ok(new { EpcsId = epcsId, Message = "Prescription cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling EPCS prescription {EpcsId}", epcsId);
            return StatusCode(500, "An error occurred while cancelling the EPCS prescription");
        }
    }

    // ── Provider Credentials (system-level) ─────────────────────────────────

    /// <summary>List all EPCS provider credentials.</summary>
    [HttpGet("api/epcs/providers")]
    [ProducesResponseType(typeof(List<EpcsProviderIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EpcsProviderIndexEntry>>> GetProviders()
    {
        try
        {
            List<EpcsProviderIndexEntry> results = await ProviderIndex.GetAllAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EPCS provider credentials");
            return StatusCode(500, "An error occurred while retrieving EPCS provider credentials");
        }
    }

    /// <summary>List active EPCS provider credentials only.</summary>
    [HttpGet("api/epcs/providers/active")]
    [ProducesResponseType(typeof(List<EpcsProviderIndexEntry>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EpcsProviderIndexEntry>>> GetActiveProviders()
    {
        try
        {
            List<EpcsProviderIndexEntry> results = await ProviderIndex.GetActiveAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active EPCS provider credentials");
            return StatusCode(500, "An error occurred while retrieving active EPCS provider credentials");
        }
    }

    /// <summary>Get a single EPCS provider credential by ID.</summary>
    [HttpGet("api/epcs/providers/{credentialId}")]
    [ProducesResponseType(typeof(EpcsProviderCredentialState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EpcsProviderCredentialState>> GetProvider(string credentialId)
    {
        try
        {
            EpcsProviderCredentialState state = await GetProviderGrain(credentialId).GetAsync();
            if (string.IsNullOrEmpty(state.CredentialId))
                return NotFound(new { Message = $"Provider credential '{credentialId}' not found" });
            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EPCS provider credential {CredentialId}", credentialId);
            return StatusCode(500, "An error occurred while retrieving the EPCS provider credential");
        }
    }

    /// <summary>Create or update an EPCS provider credential.</summary>
    [HttpPost("api/epcs/providers")]
    [ProducesResponseType(typeof(EpcsResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> SaveProvider([FromBody] SaveEpcsProviderRequest request)
    {
        try
        {
            IEpcsProviderCredentialGrain grain = GetProviderGrain(request.CredentialId);
            await grain.SaveAsync(
                request.ProviderId, request.ProviderName,
                request.Npi, request.DeaNumber,
                request.IdentityProofingLevel,
                request.IdentityProofingDate,
                request.ConfiguredTwoFactorMethods,
                request.CertificateThumbprint, request.CertificateExpiration);

            EpcsProviderIndexEntry indexEntry = new()
            {
                CredentialId = request.CredentialId,
                ProviderId = request.ProviderId,
                ProviderName = request.ProviderName,
                DeaNumber = request.DeaNumber,
                CredentialStatus = EpcsCredentialStatus.Pending,
                IdentityProofingLevel = request.IdentityProofingLevel,
            };
            await ProviderIndex.UpsertAsync(indexEntry);

            _logger.LogInformation(
                "Saved EPCS provider credential {CredentialId} ({ProviderName})",
                request.CredentialId, request.ProviderName);

            return Created(
                $"api/epcs/providers/{request.CredentialId}",
                new EpcsResponse { Id = request.CredentialId, Message = "Provider credential saved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving EPCS provider credential {CredentialId}", request.CredentialId);
            return StatusCode(500, "An error occurred while saving the EPCS provider credential");
        }
    }

    /// <summary>Activate an EPCS provider credential.</summary>
    [HttpPost("api/epcs/providers/{credentialId}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateProvider(string credentialId)
    {
        try
        {
            await GetProviderGrain(credentialId).ActivateAsync();

            EpcsProviderCredentialState state = await GetProviderGrain(credentialId).GetAsync();
            EpcsProviderIndexEntry indexEntry = new()
            {
                CredentialId = credentialId,
                ProviderId = state.ProviderId,
                ProviderName = state.ProviderName,
                DeaNumber = state.DeaNumber,
                CredentialStatus = EpcsCredentialStatus.Active,
                IdentityProofingLevel = state.IdentityProofingLevel,
            };
            await ProviderIndex.UpsertAsync(indexEntry);

            _logger.LogInformation("Activated EPCS provider credential {CredentialId}", credentialId);

            return Ok(new { CredentialId = credentialId, Message = "Provider credential activated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating EPCS provider credential {CredentialId}", credentialId);
            return StatusCode(500, "An error occurred while activating the EPCS provider credential");
        }
    }

    /// <summary>Suspend an EPCS provider credential.</summary>
    [HttpPost("api/epcs/providers/{credentialId}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SuspendProvider(string credentialId)
    {
        try
        {
            await GetProviderGrain(credentialId).SuspendAsync();

            EpcsProviderCredentialState state = await GetProviderGrain(credentialId).GetAsync();
            EpcsProviderIndexEntry indexEntry = new()
            {
                CredentialId = credentialId,
                ProviderId = state.ProviderId,
                ProviderName = state.ProviderName,
                DeaNumber = state.DeaNumber,
                CredentialStatus = EpcsCredentialStatus.Suspended,
                IdentityProofingLevel = state.IdentityProofingLevel,
            };
            await ProviderIndex.UpsertAsync(indexEntry);

            _logger.LogInformation("Suspended EPCS provider credential {CredentialId}", credentialId);

            return Ok(new { CredentialId = credentialId, Message = "Provider credential suspended" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending EPCS provider credential {CredentialId}", credentialId);
            return StatusCode(500, "An error occurred while suspending the EPCS provider credential");
        }
    }

    /// <summary>Revoke an EPCS provider credential.</summary>
    [HttpPost("api/epcs/providers/{credentialId}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeProvider(string credentialId)
    {
        try
        {
            await GetProviderGrain(credentialId).RevokeAsync();

            EpcsProviderCredentialState state = await GetProviderGrain(credentialId).GetAsync();
            EpcsProviderIndexEntry indexEntry = new()
            {
                CredentialId = credentialId,
                ProviderId = state.ProviderId,
                ProviderName = state.ProviderName,
                DeaNumber = state.DeaNumber,
                CredentialStatus = EpcsCredentialStatus.Revoked,
                IdentityProofingLevel = state.IdentityProofingLevel,
            };
            await ProviderIndex.UpsertAsync(indexEntry);

            _logger.LogInformation("Revoked EPCS provider credential {CredentialId}", credentialId);

            return Ok(new { CredentialId = credentialId, Message = "Provider credential revoked" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking EPCS provider credential {CredentialId}", credentialId);
            return StatusCode(500, "An error occurred while revoking the EPCS provider credential");
        }
    }
}

// ── Request / Response DTOs ─────────────────────────────────────────────────

public record CreateEpcsPrescriptionRequest
{
    public string? PrescriptionId { get; init; }
    public EpcsScriptTransactionType TransactionType { get; init; }
    public string DrugName { get; init; } = string.Empty;
    public string? Ndc { get; init; }
    public string DeaSchedule { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public int DaysSupply { get; init; }
    public int RefillsAuthorized { get; init; }
    public string? Sig { get; init; }
    public string? DiagnosisCode { get; init; }
    public string? PrescriberNpi { get; init; }
    public string? PrescriberDea { get; init; }
    public string? PrescriberName { get; init; }
    public string? PrescriberCredentialId { get; init; }
    public EpcsPharmacyDestination? DestinationPharmacy { get; init; }
}

public record SignEpcsRequest
{
    public string PrescriptionHash { get; init; } = string.Empty;
    public string CertificateThumbprint { get; init; } = string.Empty;
    public DateTime SigningTime { get; init; }
    public EpcsTwoFactorMethod TwoFactorMethod { get; init; }
    public DateTime TwoFactorVerificationTime { get; init; }
    public bool IsValid { get; init; }
}

public record TransmitEpcsRequest
{
    public string? TransmissionMessageId { get; init; }
}

public record CancelEpcsRequest
{
    public string UserId { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public record SaveEpcsProviderRequest
{
    public string CredentialId { get; init; } = string.Empty;
    public string ProviderId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string? Npi { get; init; }
    public string? DeaNumber { get; init; }
    public IdentityProofingLevel IdentityProofingLevel { get; init; }
    public DateTime? IdentityProofingDate { get; init; }
    public List<EpcsTwoFactorMethod>? ConfiguredTwoFactorMethods { get; init; }
    public string? CertificateThumbprint { get; init; }
    public DateTime? CertificateExpiration { get; init; }
}

public class EpcsResponse
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
