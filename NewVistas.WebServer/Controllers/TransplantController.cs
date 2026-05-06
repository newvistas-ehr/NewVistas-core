// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransplantController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<TransplantController> _logger;

    public TransplantController(IGrainFactory grainFactory, ILogger<TransplantController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ITransplantWaitlistIndexGrain WaitlistIndex =>
        _grainFactory.GetGrain<ITransplantWaitlistIndexGrain>("TX-WAITLIST-IDX");

    private ITransplantDonorIndexGrain DonorIndex =>
        _grainFactory.GetGrain<ITransplantDonorIndexGrain>("TX-DONOR-IDX");

    // ── Waitlist Queries ──────────────────────────────────────────────────────

    [HttpGet("waitlist")]
    public async Task<IActionResult> GetAllWaitlist()
    {
        try
        {
            List<TransplantWaitlistEntry> result = await WaitlistIndex.GetAllPatientsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transplant waitlist");
            return StatusCode(500, "An error occurred retrieving the waitlist.");
        }
    }

    [HttpGet("waitlist/active")]
    public async Task<IActionResult> GetActiveWaitlist()
    {
        try
        {
            List<TransplantWaitlistEntry> result = await WaitlistIndex.GetActiveWaitlistAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active transplant waitlist");
            return StatusCode(500, "An error occurred retrieving the active waitlist.");
        }
    }

    [HttpGet("waitlist/organ/{organ}")]
    public async Task<IActionResult> GetWaitlistByOrgan(TransplantOrganType organ)
    {
        try
        {
            List<TransplantWaitlistEntry> result = await WaitlistIndex.GetPatientsByOrganAsync(organ);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving waitlist by organ type {Organ}", organ);
            return StatusCode(500, "An error occurred retrieving the waitlist.");
        }
    }

    [HttpGet("patients/{patientId}")]
    public async Task<IActionResult> GetPatient(string patientId)
    {
        try
        {
            ITransplantPatientGrain grain = _grainFactory.GetGrain<ITransplantPatientGrain>(
                $"TX-PATIENT:{Uri.UnescapeDataString(patientId)}");
            TransplantPatientState result = await grain.GetPatientAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transplant patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred retrieving the patient record.");
        }
    }

    // ── Waitlist Lifecycle ────────────────────────────────────────────────────

    [HttpPost("patients/{patientId}/register")]
    public async Task<IActionResult> RegisterPatient(string patientId, [FromBody] CreateTransplantPatientRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            string grainKey = $"TX-PATIENT:{decodedId}";
            ITransplantPatientGrain grain = _grainFactory.GetGrain<ITransplantPatientGrain>(grainKey);
            await grain.RegisterPatientAsync(
                decodedId, req.PatientName, req.DateOfBirth,
                req.OrganType, req.Priority, req.BloodType,
                req.HlaTyping, req.PanelReactiveAntibodyPct,
                req.PrimaryDiagnosis, req.DiagnosisCode,
                req.WeightKg, req.HeightCm, req.MeldScore,
                req.LocationId, req.LocationName,
                req.ReferringProviderId, req.ReferringProviderName, req.Notes);

            TransplantPatientState state = await grain.GetPatientAsync();
            int age = req.DateOfBirth.HasValue
                ? (int)((DateTime.UtcNow - req.DateOfBirth.Value).TotalDays / 365.25)
                : 0;

            await WaitlistIndex.UpsertPatientAsync(new TransplantWaitlistEntry
            {
                PatientId = decodedId,
                PatientName = req.PatientName,
                OrganType = req.OrganType,
                Status = TransplantStatus.PendingEvaluation,
                Priority = req.Priority,
                ListedDate = state.ListedDate ?? DateTime.UtcNow,
                BloodType = req.BloodType,
                AgeYears = age,
                PrimaryDiagnosis = req.PrimaryDiagnosis,
                UnosId = req.UnosId,
                LocationId = req.LocationId,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Created($"/api/transplant/patients/{Uri.EscapeDataString(patientId)}", new { patientId = decodedId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering transplant patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred registering the patient.");
        }
    }

    [HttpPost("patients/{patientId}/status")]
    public async Task<IActionResult> UpdateStatus(string patientId, [FromBody] TransplantUpdateStatusRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            ITransplantPatientGrain grain = _grainFactory.GetGrain<ITransplantPatientGrain>($"TX-PATIENT:{decodedId}");
            await grain.UpdateStatusAsync(req.Status, req.Reason);

            TransplantPatientState state = await grain.GetPatientAsync();
            await WaitlistIndex.UpsertPatientAsync(new TransplantWaitlistEntry
            {
                PatientId = decodedId,
                PatientName = state.PatientName,
                OrganType = state.OrganType,
                Status = state.Status,
                Priority = state.Priority,
                ListedDate = state.ListedDate ?? DateTime.UtcNow,
                BloodType = state.BloodType,
                PrimaryDiagnosis = state.PrimaryDiagnosis,
                UnosId = state.UnosId,
                LocationId = state.LocationId,
                LastModifiedDate = DateTime.UtcNow,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for transplant patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating the patient status.");
        }
    }

    [HttpPost("patients/{patientId}/meld")]
    public async Task<IActionResult> UpdateMeld(string patientId, [FromBody] TransplantUpdateMeldRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(patientId);
            ITransplantPatientGrain grain = _grainFactory.GetGrain<ITransplantPatientGrain>($"TX-PATIENT:{decodedId}");
            await grain.UpdateMeldScoreAsync(req.MeldScore);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating MELD score for {PatientId}", patientId);
            return StatusCode(500, "An error occurred updating the MELD score.");
        }
    }

    // ── Donor Queries ─────────────────────────────────────────────────────────

    [HttpGet("donors")]
    public async Task<IActionResult> GetAllDonors()
    {
        try
        {
            List<TransplantDonorSummaryEntry> result = await DonorIndex.GetAllDonorsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving donor list");
            return StatusCode(500, "An error occurred retrieving donors.");
        }
    }

    [HttpGet("donors/available")]
    public async Task<IActionResult> GetAvailableDonors()
    {
        try
        {
            List<TransplantDonorSummaryEntry> result = await DonorIndex.GetAvailableDonorsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available donors");
            return StatusCode(500, "An error occurred retrieving available donors.");
        }
    }

    [HttpGet("donors/organ/{organ}")]
    public async Task<IActionResult> GetDonorsByOrgan(TransplantOrganType organ)
    {
        try
        {
            List<TransplantDonorSummaryEntry> result = await DonorIndex.GetDonorsByOrganAsync(organ);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving donors by organ {Organ}", organ);
            return StatusCode(500, "An error occurred retrieving donors.");
        }
    }

    [HttpGet("donors/{donorId}")]
    public async Task<IActionResult> GetDonor(string donorId)
    {
        try
        {
            ITransplantDonorGrain grain = _grainFactory.GetGrain<ITransplantDonorGrain>(Uri.UnescapeDataString(donorId));
            TransplantDonorState result = await grain.GetDonorAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving donor {DonorId}", donorId);
            return StatusCode(500, "An error occurred retrieving the donor record.");
        }
    }

    // ── Donor Lifecycle ───────────────────────────────────────────────────────

    [HttpPost("donors")]
    public async Task<IActionResult> CreateDonor([FromBody] CreateTransplantDonorRequest req)
    {
        try
        {
            string donorId = $"TX-DONOR:{Guid.NewGuid()}";
            ITransplantDonorGrain grain = _grainFactory.GetGrain<ITransplantDonorGrain>(donorId);
            await grain.CreateDonorAsync(
                req.DonorType, req.OrganType,
                req.DonorName, req.DateOfBirth, req.BloodType,
                req.WeightKg, req.HeightCm, req.CauseOfDeath,
                req.CrossClampDateTime, req.RecoveryDateTime, req.ExpirationDateTime,
                req.HlaTyping, req.ColdIschemiaTimeHours,
                req.LocationId, req.LocationName,
                req.RecoveredById, req.RecoveredByName, req.Notes);

            int ageYears = req.DateOfBirth.HasValue
                ? (int)((DateTime.UtcNow - req.DateOfBirth.Value).TotalDays / 365.25)
                : 0;

            await DonorIndex.UpsertDonorAsync(new TransplantDonorSummaryEntry
            {
                DonorId = donorId,
                OrganType = req.OrganType,
                DonorType = req.DonorType,
                BloodType = req.BloodType,
                Status = DonorStatus.Available,
                DonorAgeYears = ageYears,
                RecoveryDateTime = req.RecoveryDateTime,
                ExpirationDateTime = req.ExpirationDateTime,
                LocationId = req.LocationId,
            });

            return Created($"/api/transplant/donors/{Uri.EscapeDataString(donorId)}", new { donorId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating donor organ record");
            return StatusCode(500, "An error occurred creating the donor record.");
        }
    }

    [HttpPost("donors/{donorId}/allocate")]
    public async Task<IActionResult> AllocateDonor(string donorId, [FromBody] TransplantAllocateRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(donorId);
            ITransplantDonorGrain grain = _grainFactory.GetGrain<ITransplantDonorGrain>(decodedId);
            await grain.AllocateToPatientAsync(req.PatientId, req.PatientName, req.AllocationDateTime);

            TransplantDonorState state = await grain.GetDonorAsync();
            await DonorIndex.UpsertDonorAsync(new TransplantDonorSummaryEntry
            {
                DonorId = decodedId,
                OrganType = state.OrganType,
                DonorType = state.DonorType,
                BloodType = state.BloodType,
                Status = DonorStatus.Allocated,
                DonorAgeYears = state.DateOfBirth.HasValue
                    ? (int)((DateTime.UtcNow - state.DateOfBirth.Value).TotalDays / 365.25) : 0,
                RecoveryDateTime = state.RecoveryDateTime,
                ExpirationDateTime = state.ExpirationDateTime,
                LocationId = state.LocationId,
                MatchedPatientId = req.PatientId,
                MatchedPatientName = req.PatientName,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error allocating donor {DonorId}", donorId);
            return StatusCode(500, "An error occurred allocating the donor organ.");
        }
    }

    [HttpPost("donors/{donorId}/discard")]
    public async Task<IActionResult> DiscardDonor(string donorId, [FromBody] TransplantDiscardRequest req)
    {
        try
        {
            string decodedId = Uri.UnescapeDataString(donorId);
            ITransplantDonorGrain grain = _grainFactory.GetGrain<ITransplantDonorGrain>(decodedId);
            await grain.DiscardOrganAsync(req.Reason);

            TransplantDonorState state = await grain.GetDonorAsync();
            await DonorIndex.UpsertDonorAsync(new TransplantDonorSummaryEntry
            {
                DonorId = decodedId,
                OrganType = state.OrganType,
                DonorType = state.DonorType,
                BloodType = state.BloodType,
                Status = DonorStatus.Discarded,
                DonorAgeYears = state.DateOfBirth.HasValue
                    ? (int)((DateTime.UtcNow - state.DateOfBirth.Value).TotalDays / 365.25) : 0,
                RecoveryDateTime = state.RecoveryDateTime,
                ExpirationDateTime = state.ExpirationDateTime,
                LocationId = state.LocationId,
            });

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding donor {DonorId}", donorId);
            return StatusCode(500, "An error occurred discarding the organ.");
        }
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

public record CreateTransplantPatientRequest(
    string PatientName,
    DateTime? DateOfBirth,
    TransplantOrganType OrganType,
    TransplantPriority Priority,
    BloodType BloodType,
    string? HlaTyping,
    decimal? PanelReactiveAntibodyPct,
    string PrimaryDiagnosis,
    string? DiagnosisCode,
    decimal? WeightKg,
    decimal? HeightCm,
    decimal? MeldScore,
    string LocationId,
    string LocationName,
    string? ReferringProviderId,
    string? ReferringProviderName,
    string? UnosId,
    string? Notes);

public record TransplantUpdateStatusRequest(TransplantStatus Status, string? Reason);
public record TransplantUpdateMeldRequest(decimal MeldScore);
public record TransplantAllocateRequest(string PatientId, string PatientName, DateTime AllocationDateTime);
public record TransplantDiscardRequest(string Reason);

public record CreateTransplantDonorRequest(
    DonorType DonorType,
    TransplantOrganType OrganType,
    string DonorName,
    DateTime? DateOfBirth,
    BloodType BloodType,
    decimal? WeightKg,
    decimal? HeightCm,
    string? CauseOfDeath,
    DateTime? CrossClampDateTime,
    DateTime RecoveryDateTime,
    DateTime? ExpirationDateTime,
    string? HlaTyping,
    decimal? ColdIschemiaTimeHours,
    string LocationId,
    string LocationName,
    string RecoveredById,
    string RecoveredByName,
    string? Notes);
